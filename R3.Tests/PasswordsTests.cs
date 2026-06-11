using R3.Common;

namespace R3.Tests;

public class PasswordsTests
{
    [Fact]
    public void Verify_TrueForCorrectPassword()
    {
        var hash = Passwords.Hash("s3cret-password");
        Assert.True(Passwords.Verify("s3cret-password", hash));
    }

    [Fact]
    public void Verify_FalseForWrongPassword()
    {
        var hash = Passwords.Hash("s3cret-password");
        Assert.False(Passwords.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_IsSaltedSoTwoHashesDiffer()
    {
        Assert.NotEqual(Passwords.Hash("same"), Passwords.Hash("same"));
    }
}
