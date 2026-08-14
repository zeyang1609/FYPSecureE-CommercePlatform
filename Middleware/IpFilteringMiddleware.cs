using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace FYP.Middleware
{
    public class IpFilteringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;

        public IpFilteringMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress;

            if (ipAddress != null)
            {
                // Handle IPv4 mapped to IPv6 (e.g. ::ffff:127.0.0.1)
                string ipString = ipAddress.IsIPv4MappedToIPv6 ? ipAddress.MapToIPv4().ToString() : ipAddress.ToString();

                // Always allow localhost / loopback during development
                if (IPAddress.IsLoopback(ipAddress) || ipString == "127.0.0.1" || ipString == "::1" || ipString == "localhost")
                {
                    await _next(context);
                    return;
                }

                // Retrieve from cache if possible to avoid DB query on every single HTTP request
                if (!_cache.TryGetValue("IpFilters_Whitelist", out List<string> whitelist) ||
                    !_cache.TryGetValue("IpFilters_Blacklist", out List<string> blacklist))
                {
                    // Cache miss: resolve DbContext and fetch from DB
                    using (var scope = context.RequestServices.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var allFilters = await dbContext.IpFilters.ToListAsync();
                        
                        whitelist = allFilters.Where(f => f.FilterAction == "Allow").Select(f => f.IpAddress).ToList();
                        blacklist = allFilters.Where(f => f.FilterAction == "Block").Select(f => f.IpAddress).ToList();

                        // Cache the results for 2 minutes to balance performance and real-time updates
                        var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(2));
                        _cache.Set("IpFilters_Whitelist", whitelist, cacheEntryOptions);
                        _cache.Set("IpFilters_Blacklist", blacklist, cacheEntryOptions);
                    }
                }

                if (whitelist != null && whitelist.Contains(ipString))
                {
                    // Allow explicitly whitelisted IP
                    await _next(context);
                    return;
                }

                if (blacklist != null && blacklist.Contains(ipString))
                {
                    // Block blacklisted IP
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await context.Response.WriteAsync("Forbidden: Your IP address has been globally blacklisted by SecurePlatform security policies.");
                    return;
                }
            }

            await _next(context);
        }
    }
}
