using System.ComponentModel.DataAnnotations;
using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DoctorRequestValidationTests
{
    [Fact]
    public void CreateDoctorRequest_AcceptsCompleteKnowledgeProfile()
    {
        var request = new CreateDoctorRequest
        {
            FullName = "Dr. Анна Тестова",
            Specialization = "Имплантология, хирургия",
            ExperienceYears = 12,
            Bio = "Практикующий стоматолог-хирург."
        };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(81)]
    public void CreateDoctorRequest_RejectsImpossibleExperience(int years)
    {
        var request = new CreateDoctorRequest
        {
            FullName = "Dr. Test",
            ExperienceYears = years
        };

        Assert.Contains(Validate(request), result =>
            result.MemberNames.Contains(nameof(CreateDoctorRequest.ExperienceYears)));
    }

    [Fact]
    public void CreateDoctorRequest_RejectsOversizedKnowledgeFields()
    {
        var request = new CreateDoctorRequest
        {
            FullName = new string('N', 151),
            Specialization = new string('S', 301),
            Bio = new string('B', 501)
        };

        var results = Validate(request);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateDoctorRequest.FullName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateDoctorRequest.Specialization)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateDoctorRequest.Bio)));
    }

    [Fact]
    public void UpdateDoctorRequest_AllowsStatusOnlyPatch_ButRejectsEmptySuppliedName()
    {
        var statusOnly = new UpdateDoctorRequest { IsActive = false };
        Assert.Empty(Validate(statusOnly));

        var emptyName = new UpdateDoctorRequest { FullName = "" };
        Assert.Contains(Validate(emptyName), result =>
            result.MemberNames.Contains(nameof(UpdateDoctorRequest.FullName)));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
