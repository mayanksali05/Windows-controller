using System.Text;
using WinLock.Protocol;
using Xunit;

namespace Protocol.Tests;

public sealed class ProtocolStringsTests
{
    [Fact]
    public void PairingSigningInput_UsesUnitSeparatorAndRecordSeparator()
    {
        var input = ProtocolStrings.PairingSigningInput("DEVICE", "NONCE");

        Assert.Equal("DEVICE\u001fNONCE\u001e", Encoding.UTF8.GetString(input));
    }

    [Fact]
    public void PairingSigningInput_IsDeterministic()
    {
        var a = ProtocolStrings.PairingSigningInput("X", "Y");
        var b = ProtocolStrings.PairingSigningInput("X", "Y");

        Assert.Equal(a, b);
    }

    [Fact]
    public void AuthenticationSigningInput_MatchesDocumentedShape()
    {
        var input = ProtocolStrings.AuthenticationSigningInput(
            "DEVICE", "CHALLENGE", "2026-08-25T00:00:00Z", ProtocolStrings.ChallengeVerifyEndpoint);

        Assert.Equal(
            "DEVICE\u001fCHALLENGE\u001f2026-08-25T00:00:00Z\u001f/api/v1/auth/verify\u001e",
            Encoding.UTF8.GetString(input));
    }
}