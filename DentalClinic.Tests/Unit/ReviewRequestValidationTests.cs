using System.ComponentModel.DataAnnotations;
using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ReviewRequestValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void CreateReviewRequest_AcceptsPersistableReview()
    {
        var request = new CreateReviewRequest
        {
            Rating = 5,
            Text = "Очень внимательное отношение и понятное объяснение лечения."
        };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(6, 20)]
    [InlineData(5, 9)]
    [InlineData(5, 1001)]
    public void CreateReviewRequest_RejectsInvalidRatingOrTextLength(int rating, int textLength)
    {
        var request = new CreateReviewRequest
        {
            Rating = rating,
            Text = new string('a', textLength)
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void ModerateReviewRequest_RejectsOversizedReasonAndStatus()
    {
        var oversizedReason = new ModerateReviewRequest
        {
            Status = "rejected",
            RejectionReason = new string('r', 501)
        };
        var oversizedStatus = new ModerateReviewRequest
        {
            Status = new string('s', 21),
            RejectionReason = "Причина"
        };

        Assert.NotEmpty(Validate(oversizedReason));
        Assert.NotEmpty(Validate(oversizedStatus));
    }
}
