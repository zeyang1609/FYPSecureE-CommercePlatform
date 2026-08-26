using System.Threading.RateLimiting;
using System.Net;
using Microsoft.AspNetCore.Http;
using FYP.Data;
using FYP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using QuestPDF.Infrastructure;

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Enforce TLS 1.2 and TLS 1.3 for Data in Transit
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ConfigureHttpsDefaults(listenOptions =>
    {
        listenOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
    });
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// 0. Add Cookie Authentication (Industry Standard Security)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SecurePlatform.AuthToken"; // Custom cookie name to obfuscate stack
        options.Cookie.HttpOnly = true; // Prevents JavaScript XSS access
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always; // Enforces HTTPS transmission
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax; // Lax allows cross-site top-level redirects like Stripe
        
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // Idle timeout
        options.SlidingExpiration = true; // Renew cookie on activity

        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
    });

// 1. Add In-Memory Cache (Required by OtpService to store temporary 6-digit verification codes)
builder.Services.AddMemoryCache();

var connectionString = builder.Configuration.GetConnectionString("SecureECommerceConnection");
var serverVersion = ServerVersion.AutoDetect(connectionString);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, serverVersion)
           .LogTo(Console.WriteLine, LogLevel.Information)
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors());

// 2. Register Custom Application Services & Microservices
builder.Services.AddHttpClient<PythonAiClient>(client => 
{
    var aiServiceUrl = builder.Configuration["AiServiceUrl"] ?? "http://localhost:5000/";
    client.BaseAddress = new Uri(aiServiceUrl);
});
builder.Services.AddScoped<IOtpService, OtpService>();
// Register TOTP Authenticator Service
builder.Services.AddScoped<FYP.Services.TotpService>();
builder.Services.AddScoped<IShippingService, ShippingService>();
builder.Services.AddScoped<IPdfReceiptService, PdfReceiptService>();
builder.Services.AddScoped<IPaymentSecurityService, PaymentSecurityService>();
builder.Services.AddSingleton<ICheckoutLockService, CheckoutLockService>();
builder.Services.AddSingleton<IPaymentEncryptionService, PaymentEncryptionService>();

// 3. Add Session Support (Required for Cart functionality)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Rate limiting setup
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress;
        // Exempt localhost / loopback from rate limiting during development
        if (ip != null && (IPAddress.IsLoopback(ip) || ip.ToString() == "127.0.0.1" || ip.ToString() == "::1"))
        {
            return RateLimitPartition.GetNoLimiter("localhost");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.OnRejected = async (context, token) =>
    {
        var ip = context.HttpContext.Connection.RemoteIpAddress;
        if (ip != null && !IPAddress.IsLoopback(ip) && ip.ToString() != "127.0.0.1" && ip.ToString() != "::1")
        {
            var ipStr = ip.ToString();
            // Auto-blacklist the IP using a scoped DbContext
            using (var scope = context.HttpContext.RequestServices.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Only add if it doesn't already exist
                if (!await dbContext.IpFilters.AnyAsync(f => f.IpAddress == ipStr))
                {
                    dbContext.IpFilters.Add(new FYP.Models.Entities.IpFilter
                    {
                        IpAddress = ipStr,
                        FilterAction = "Block",
                        Reason = "Auto-blacklisted due to rate limit violation",
                        AddedAt = DateTime.UtcNow,
                        AddedByAdminID = "SYSTEM"
                    });

                    // Log the security event
                    dbContext.AuditLogs.Add(new FYP.Models.Entities.AuditLog
                    {
                        LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = "SYSTEM",
                        Action = "System Auto-Blacklist (Rate Limit Exceeded)",
                        IP_Address = ipStr,
                        Timestamp = DateTime.UtcNow
                    });

                    await dbContext.SaveChangesAsync();

                    // Invalidate the blacklist cache so the middleware picks it up instantly
                    var cache = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                    cache.Remove("IpFilters_Blacklist");
                }
            }
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Too many requests. Your IP has been temporarily flagged for security review.", token);
    };
});

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseMiddleware<FYP.Middleware.IpFilteringMiddleware>();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<FYP.Hubs.ChatHub>("/chatHub");
app.MapHub<FYP.Hubs.OrderHub>("/orderHub"); 
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Automatically update seeded accounts to have a real password for login and bypass OTP
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    var seededUsers = db.Users.Where(u => u.Email == "demo_seller@secureplatform.com" || u.Email == "demo_admin@secureplatform.com").ToList();
    if (seededUsers.Any())
    {
        string validHash = FYP.Security.Argon2idHasher.HashPassword("password123");
        foreach (var user in seededUsers)
        {
            user.PasswordHash = validHash;
            user.MFA_Enabled = false;
        }
        db.SaveChanges();
        Console.WriteLine($"[INFO] Updated {seededUsers.Count} seeded accounts to use password123 and disabled MFA.");
    }
}

app.Run();
