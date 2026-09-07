using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Password1!")]
    [InlineData("Violetta2026#")]
    [InlineData("Strong-Pass9")]
    public void IsValid_AcceptsPasswordsMeetingEveryRequirement(string password)
    {
        Assert.True(PasswordPolicy.IsValid(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("       ")]
    [InlineData("Aa1!")]
    [InlineData("password1!")]
    [InlineData("PASSWORD1!")]
    [InlineData("Password!!")]
    [InlineData("Password11")]
    public void IsValid_RejectsPasswordsMissingAnyRequirement(string? password)
    {
        Assert.False(PasswordPolicy.IsValid(password));
    }
}
