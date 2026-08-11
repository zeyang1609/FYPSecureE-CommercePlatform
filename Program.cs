using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using FYP.Data;
using FYP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHttpClient<PythonAiClient>();
builder.Services.AddScoped<IOtpService, OtpService>();
// Register TOTP Authenticator Service
builder.Services.AddScoped<FYP.Services.TotpService>();
builder.Services.AddScoped<IShippingService, ShippingService>();

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
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseMiddleware<FYP.Middleware.IpFilteringMiddleware>();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<FYP.Hubs.ChatHub>("/chatHub");
app.MapHub<FYP.Hubs.OrderHub>("/orderHub"); // Keep our OrderHub!
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Automatically update seeded accounts to have a real password for login and bypass OTP
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var seededUsers = db.Users.Where(u => u.Email == "demo_seller@secureplatform.com" || u.Email == "demo_admin@secureplatform.com").ToList();
    if (seededUsers.Any())
    {
        string validHash = FYP.Security.Argon2idHasher.HashPassword("password123");
        foreach (var user in seededUsers)
        {
            user.PasswordHash = validHash;
            user.MFA_Enabled = false; // Bypass OTP for seed account
        }
        db.SaveChanges();
        Console.WriteLine($"[INFO] Updated {seededUsers.Count} seeded accounts to use password123 and disabled MFA.");
    }
}

app.Run();
