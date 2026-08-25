namespace WinLock.Service.Authentication;

/// <summary>Reports whether the service is running in the Development environment.</summary>
public interface IDevelopmentModeDetector
{
    bool IsDevelopment { get; }
}

/// <summary>
/// Development mode is true when the ASP.NET environment is Development or when
/// <c>Server:Environment</c> equals "Development". Used to gate development-only
/// endpoints and to refuse startup outside Development while only the
/// development authentication provider exists.
/// </summary>
public sealed class DevelopmentModeDetector : IDevelopmentModeDetector
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public DevelopmentModeDetector(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public bool IsDevelopment =>
        _environment.IsDevelopment() ||
        string.Equals(_configuration["Server:Environment"], "Development", StringComparison.OrdinalIgnoreCase);
}