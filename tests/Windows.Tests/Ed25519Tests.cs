using WinLock.Protocol;
using WinLock.Service.Security;
using Xunit;

namespace Windows.Tests;

public sealed class Ed25519Tests
{
    [Fact]
    public void PrivateKeySeed_Is32Bytes()
    {
        var seed = Ed25519.GeneratePrivateKeySeed();
        Assert.Equal(Ed25519.PublicKeySize, seed.Length);
    }

    [Fact]
    public void PublicKey_Is32Bytes_AndStable()
    {
        var seed = Ed25519.GeneratePrivateKeySeed();
        var first = Ed25519.DerivePublicKey(seed);
        var second = Ed25519.DerivePublicKey(seed);

        Assert.Equal(Ed25519.PublicKeySize, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Sign_Verify_RoundTrips()
    {
        var seed = Ed25519.GeneratePrivateKeySeed();
        var publicKey = Ed25519.DerivePublicKey(seed);
        var message = ProtocolStrings.PairingSigningInput("DEVICE", "NONCE");

        var signature = Ed25519.Sign(seed, message);

        Assert.Equal(Ed25519.SignatureSize, signature.Length);
        Assert.True(Ed25519.Verify(publicKey, message, signature));
    }

    [Fact]
    public void Verify_RejectsTamperedMessage()
    {
        var seed = Ed25519.GeneratePrivateKeySeed();
        var publicKey = Ed25519.DerivePublicKey(seed);
        var message = ProtocolStrings.PairingSigningInput("DEVICE", "NONCE");
        var signature = Ed25519.Sign(seed, message);

        var tampered = ProtocolStrings.PairingSigningInput("EVICE", "NONCE");

        Assert.False(Ed25519.Verify(publicKey, tampered, signature));
    }

    [Fact]
    public void Verify_RejectsWrongKey()
    {
        var seedA = Ed25519.GeneratePrivateKeySeed();
        var seedB = Ed25519.GeneratePrivateKeySeed();
        var message = ProtocolStrings.PairingSigningInput("DEVICE", "NONCE");
        var signature = Ed25519.Sign(seedA, message);

        Assert.False(Ed25519.Verify(Ed25519.DerivePublicKey(seedB), message, signature));
    }

    [Fact]
    public void Verify_RejectsMalformedKeyAndSignature()
    {
        Assert.False(Ed25519.Verify(new byte[16], new byte[4], new byte[64]));
        Assert.False(Ed25519.Verify(new byte[32], new byte[4], new byte[32]));
    }

    [Fact]
    public void Signatures_AreDeterministic()
    {
        var seed = Ed25519.GeneratePrivateKeySeed();
        var message = ProtocolStrings.PairingSigningInput("DEVICE", "NONCE");

        Assert.Equal(Ed25519.Sign(seed, message), Ed25519.Sign(seed, message));
    }
}