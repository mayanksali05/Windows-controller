using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting.WindowsServices;
using WinLock.Cryptography;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Bluetooth;
using WinLock.Service.Certificates;
using WinLock.Service.Configuration;
using WinLock.Service.Discovery;
using WinLock.Service.Logging;
using WinLock.Service.Locking;
using WinLock.Service.Pairing;
using WinLock.Service.Security;
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

        var securityOptions = builder.Configuration
            .GetSection(SecurityOptions.SectionName)
            .Get<SecurityOptions>() ?? new SecurityOptions();
        builder.Services.AddSingleton(securityOptions);
        builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));

        builder.Host.UseWindowsService(options => options.ServiceName = "WinLockService");

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

        builder.Services.AddSingleton<ISigningService, Ed25519SigningService>();
        builder.Services.AddSingleton<ISecureStorage>(_ =>
        {
            var configured = builder.Configuration["Storage:Directory"];
            var directory = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WinLock", "storage")
                : configured;
            return new DpapiSecureStorage(directory);
        });
        builder.Services.AddSingleton<DeviceIdentityService>();
        builder.Services.AddSingleton<AuthorizedDeviceStore>();
        builder.Services.AddSingleton<DeviceAuthorizer>();
        builder.Services.AddSingleton<TlsPinProvider>();
        builder.Services.AddSingleton<IDiscoveryAdvertiser, MdnsResponder>();
        builder.Services.AddHostedService<DiscoveryBackgroundService>();

        builder.Services.AddSingleton(_ => new ChallengeStore(
            TimeSpan.FromSeconds(securityOptions.ChallengeLifetimeSeconds)));
        builder.Services.AddSingleton(_ => new SessionTokenService(
            TimeSpan.FromMinutes(securityOptions.SessionLifetimeMinutes)));
        builder.Services.AddSingleton<IAuthenticationService, ChallengeResponseAuthenticationService>();

        builder.Services.AddSingleton<IWindowsLockService, WindowsLockService>();
        builder.Services.AddSingleton<LockCoordinator>();
        builder.Services.AddSingleton<ISystemStatusService, WindowsSystemStatusService>();

        builder.Services.AddSingleton<IBluetoothProximityScanner>(_ => new WindowsBluetoothProximityScanner(
            TimeSpan.FromSeconds(securityOptions.ProximityAwayTimeoutSeconds),
            securityOptions.ProximityNearbyRssiThreshold));
        builder.Services.AddSingleton<ProximityMonitor>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ProximityMonitor>());

        builder.Services.AddSingleton(_ => new PairingSessionService(
            TimeSpan.FromSeconds(securityOptions.PairingTokenLifetimeSeconds)));

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

        var isDevelopment = app.Environment.IsDevelopment() ||
            string.Equals(serverOptions.Environment, "Development", StringComparison.OrdinalIgnoreCase);

        if (!isDevelopment && !serverOptions.UseHttps)
        {
            throw new InvalidOperationException(
                "Production mode requires HTTPS (Server:UseHttps=true). " +
                "Refusing to serve plaintext outside Development.");
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRateLimiter();
        app.MapControllers();

        if (isDevelopment)
        {
            Console.WriteLine("[WinLock] Development mode active.");
            Console.WriteLine($"[WinLock] Listening on {(serverOptions.UseHttps ? "https" : "http")}://{serverOptions.BindAddress}:{serverOptions.Port}");
            Console.WriteLine("[WinLock] Pairing sessions are created from the tray application (or POST /api/v1/pair/session once authenticated).");
        }

        await app.RunAsync();
        return 0;
    }
}

public partial class Program;