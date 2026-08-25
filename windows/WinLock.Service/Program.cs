using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting.WindowsServices;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Certificates;
using WinLock.Service.Configuration;
using WinLock.Service.Logging;
using WinLock.Service.Locking;
using WinLock.Service.Status;

namespace WinLock.Service;

public partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (args.Contains("--development", StringComparer.OrdinalIgnoreCase))
        {
            builder.Environment.EnvironmentName = Environments.Development;
        }

        var serverOptions = builder.Configuration
            .GetSection(ServerOptions.SectionName)
            .Get<ServerOptions>() ?? new ServerOptions();

        builder.Services.AddSingleton(serverOptions);
        builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));

        builder.Host.UseWindowsService(options => options.ServiceName = "WinLockService");

        builder.Services.AddSingleton<IDevelopmentModeDetector, DevelopmentModeDetector>();
        builder.Services.AddSingleton<ISecurityEventLogger>(_ =>
        {
            var configured = builder.Configuration["Logging:Directory"];
            var directory = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WinLock", "logs")
                : configured;
            return new FileSecurityEventLogger(directory);
        });

        builder.Services.AddSingleton<DevTokenService>();
        builder.Services.AddSingleton<IAuthenticationService, DevelopmentAuthenticationService>();
        builder.Services.AddSingleton<IWindowsLockService, WindowsLockService>();
        builder.Services.AddSingleton<LockCoordinator>();
        builder.Services.AddSingleton<ISystemStatusService, WindowsSystemStatusService>();

        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<AuthenticationActionFilter>();
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("default", limit =>
            {
                limit.PermitLimit = serverOptions.RateLimitPermitsPerWindow ?? 60;
                limit.Window = TimeSpan.FromSeconds(serverOptions.RateLimitWindowSeconds);
                limit.QueueLimit = 0;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                var body = JsonSerializer.Serialize(
                    ApiResponse.Failure(ErrorCodes.RateLimited, "Too many requests"));
                await context.HttpContext.Response.WriteAsync(body);
            };
        });

        builder.WebHost.ConfigureKestrel(webBuilder =>
        {
            webBuilder.Limits.MaxRequestBodySize = 16 * 1024;
            webBuilder.Limits.MaxRequestHeadersTotalSize = 8 * 1024;
            webBuilder.Limits.MaxRequestLineSize = 8 * 1024;

            var bindAddress = IPAddress.TryParse(serverOptions.BindAddress, out var parsed)
                ? parsed
                : IPAddress.Any;

            if (serverOptions.UseHttps)
            {
                var certificate = CertificateProvider.LoadDevelopmentCertificate(serverOptions);
                webBuilder.Listen(bindAddress, serverOptions.Port, listen => listen.UseHttps(certificate));
            }
            else
            {
                webBuilder.Listen(bindAddress, serverOptions.Port);
            }
        });

        var app = builder.Build();

        var mode = app.Services.GetRequiredService<IDevelopmentModeDetector>();
        if (!mode.IsDevelopment)
        {
            throw new InvalidOperationException(
                "Production environment detected but no production authentication provider is " +
                "configured yet (challenge-response arrives in Phase 4). " +
                "Refusing to start with development-only authentication outside Development.");
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRateLimiter();
        app.MapControllers();

        if (mode.IsDevelopment)
        {
            var tokens = app.Services.GetRequiredService<DevTokenService>();
            Console.WriteLine("[WinLock] Development mode active.");
            Console.WriteLine($"[WinLock] Listening on {(serverOptions.UseHttps ? "https" : "http")}://{serverOptions.BindAddress}:{serverOptions.Port}");
            Console.WriteLine($"[WinLock] Development bearer token: {tokens.Token}");
        }

        await app.RunAsync();
        return 0;
    }
}

public partial class Program;