using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short1A")]
    [InlineData("lowercaseonly1")]
    [InlineData("UPPERCASEONLY1")]
    [InlineData("NoDigitsHere")]
    public void WeakPasswords_AreRejected(string? password)
    {
        Assert.False(PasswordPolicy.IsValid(password));
    }

    [Theory]
    [InlineData("StrongPass1")]
    [InlineData("DentalClinic2026")]
    public void StrongPasswords_AreAccepted(string password)
    {
        Assert.True(PasswordPolicy.IsValid(password));
    }
}
