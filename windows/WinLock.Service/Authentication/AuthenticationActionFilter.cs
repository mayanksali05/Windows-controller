using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service.Logging;

namespace WinLock.Service.Authentication;

/// <summary>
/// Global action filter that enforces <see cref="RequireAuthenticationAttribute"/>.
/// Endpoints marked <c>[AllowAnonymous]</c> (health, dev token) are skipped.
/// Failures short-circuit with a structured 401 envelope and are logged.
/// </summary>
public sealed class AuthenticationActionFilter : IAsyncActionFilter
{
    private readonly IAuthenticationService _authentication;
    private readonly ISecurityEventLogger _log;

    public AuthenticationActionFilter(IAuthenticationService authentication, ISecurityEventLogger log)
    {
        _authentication = authentication;
        _log = log;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var allowAnonymous = context.ActionDescriptor.EndpointMetadata
            .OfType<AllowAnonymousAttribute>().Any();
        var requireAuth = context.ActionDescriptor.EndpointMetadata
            .OfType<RequireAuthenticationAttribute>().Any();

        if (allowAnonymous || !requireAuth)
        {
            await next();
            return;
        }

        var path = context.HttpContext.Request.Path.ToString();
        _log.Log(SecurityEventType.AuthenticationStarted, "Authentication started", new { path });

        var result = await _authentication.AuthenticateAsync(context.HttpContext.Request, context.HttpContext.RequestAborted);
        if (!result.Success)
        {
            _log.Log(SecurityEventType.AuthenticationFailed, "Authentication failed",
                new { code = result.FailureCode, path });

            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult(
                ApiResponse.Failure(result.FailureCode ?? ErrorCodes.AuthFailed,
                    result.FailureMessage ?? "Authentication failed"));
            return;
        }

        _log.Log(SecurityEventType.AuthenticationSuccess, "Authentication succeeded", new { path });
        await next();
    }
}