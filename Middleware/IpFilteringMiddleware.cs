using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System;

namespace FYP.Middleware
{
    public class IpFilteringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public IpFilteringMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress;

            if (ipAddress != null)
            {
                var blacklist = _configuration.GetSection("IpSecuritySettings:Blacklist").Get<string[]>() ?? Array.Empty<string>();
                var whitelist = _configuration.GetSection("IpSecuritySettings:Whitelist").Get<string[]>() ?? Array.Empty<string>();

                // Handle IPv4 mapped to IPv6 (e.g. ::ffff:127.0.0.1)
                string ipString = ipAddress.IsIPv4MappedToIPv6 ? ipAddress.MapToIPv4().ToString() : ipAddress.ToString();

                if (whitelist.Contains(ipString))
                {
                    // Allow explicitly whitelisted IP
                    await _next(context);
                    return;
                }

                if (blacklist.Contains(ipString))
                {
                    // Block blacklisted IP
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await context.Response.WriteAsync("Forbidden: Your IP address has been blacklisted.");
                    return;
                }
            }

            await _next(context);
        }
    }
}
