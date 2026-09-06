using System.ComponentModel.DataAnnotations;
using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class AuthRequestValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void RegisterRequest_AcceptsNormalBoundedValues()
    {
        var request = new RegisterRequest
        {
            FirstName = "Violetta",
            Email = "violetta@example.com",
            Password = "password123"
        };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(101, "user@example.com", 12)]
    [InlineData(8, "oversized", 12)]
    [InlineData(8, "user@example.com", 101)]
    [InlineData(1, "user@example.com", 12)]
    public void RegisterRequest_RejectsInvalidOrOversizedInputs(int nameLength, string emailKind, int passwordLength)
    {
        var email = emailKind == "oversized"
            ? $"{new string('a', 312)}@example.com"
            : emailKind;

        var request = new RegisterRequest
        {
            FirstName = new string('A', nameLength),
            Email = email,
            Password = new string('p', passwordLength)
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void LoginRequest_RejectsMalformedEmailAndOversizedPassword()
    {
        var malformedEmail = new LoginRequest
        {
            Email = "not-an-email",
            Password = "password123"
        };
        var oversizedPassword = new LoginRequest
        {
            Email = "user@example.com",
            Password = new string('p', 513)
        };

        Assert.NotEmpty(Validate(malformedEmail));
        Assert.NotEmpty(Validate(oversizedPassword));
    }

    [Fact]
    public void ChangePasswordRequest_RejectsOversizedCurrentPassword()
    {
        var request = new ChangePasswordRequest
        {
            CurrentPassword = new string('p', 513),
            NewPassword = "new-password-123"
        };

        Assert.NotEmpty(Validate(request));
    }
}
