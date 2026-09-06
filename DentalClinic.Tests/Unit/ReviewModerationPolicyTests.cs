using DentalClinic.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ReviewModerationPolicyTests
{
    [Fact]
    public void ModerateReviewRequest_RejectsOversizedReasonBeforeController()
    {
        var request = new ModerateReviewRequest
        {
            Status = "rejected",
            RejectionReason = new string('x', 501)
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ModerateReviewRequest.RejectionReason)));
    }
}
