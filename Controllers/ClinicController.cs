using DentalClinic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/clinic")]
public sealed class ClinicController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ClinicController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet("profile")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public ActionResult<PublicClinicProfile> GetPublicProfile()
        => Ok(PublicClinicProfile.FromConfiguration(_configuration));
}
