using Microsoft.AspNetCore.Http;
using WinLock.Protocol;
using WinLock.Service.Authentication;
using Xunit;

namespace Windows.Tests;

public sealed class DevelopmentAuthenticationServiceTests
{
    [Fact]
    public async Task ValidToken_Succeeds()
    {
        var tokens = new DevTokenService();
        var auth = new DevelopmentAuthenticationService(tokens);
        var request = NewRequest();
        request.Headers.Authorization = $"Bearer {tokens.Token}";

        var result = await auth.AuthenticateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WrongToken_Fails()
    {
        var tokens = new DevTokenService();
        var auth = new DevelopmentAuthenticationService(tokens);
        var request = NewRequest();
        request.Headers.Authorization = "Bearer wrong-token";

        var result = await auth.AuthenticateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.AuthFailed, result.FailureCode);
    }

    [Fact]
    public async Task MissingHeader_Fails()
    {
        var auth = new DevelopmentAuthenticationService(new DevTokenService());

        var result = await auth.AuthenticateAsync(NewRequest(), CancellationToken.None);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task NonBearerScheme_Fails()
    {
        var auth = new DevelopmentAuthenticationService(new DevTokenService());
        var request = NewRequest();
        request.Headers.Authorization = "Basic abc";

        var result = await auth.AuthenticateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
    }

    private static HttpRequest NewRequest()
    {
        var context = new DefaultHttpContext();
        return context.Request;
    }
}