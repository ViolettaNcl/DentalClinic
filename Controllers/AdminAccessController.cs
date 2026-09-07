using System.Security.Claims;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Controllers;

[ApiController]
[Route("api/admin-access")]
[Authorize(Roles = "Admin")]
public sealed class AdminAccessController : ControllerBase
{
    private const string PasswordRequirementsMessage =
        "Пароль должен содержать минимум 8 символов, включая заглавную и строчную буквы, цифру и специальный символ";

    private readonly AdminAccessService _access;

    public AdminAccessController(AdminAccessService access)
    {
        _access = access;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var actorId = GetCurrentAdminId();
        if (!await _access.IsSuperAdminAsync(actorId, cancellationToken))
            return Forbid();

        var admins = await _access.ListAsync(cancellationToken);
        return Ok(admins.Select(admin => new
        {
            admin.Id,
            admin.Email,
            admin.IsSuperAdmin,
            admin.CreatedAt,
            isCurrent = admin.Id == actorId
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAdminAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(request.Password))
            return BadRequest(new { message = PasswordRequirementsMessage });

        var result = await _access.CreateAsync(
            GetCurrentAdminId(),
            request.Email,
            request.Password,
            request.IsSuperAdmin,
            cancellationToken);

        if (!result.Succeeded)
            return MapError(result.Error);

        return Created($"/api/admin-access/{result.Admin!.Id}", result.Admin);
    }

    [HttpPut("{id:int}/super-admin")]
    public async Task<IActionResult> SetSuperAdmin(
        int id,
        [FromBody] SetSuperAdminRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _access.SetSuperAdminAsync(
            GetCurrentAdminId(),
            id,
            request.IsSuperAdmin,
            cancellationToken);

        return result.Succeeded ? Ok(result.Admin) : MapError(result.Error);
    }

    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetAdminPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(request.NewPassword))
            return BadRequest(new { message = PasswordRequirementsMessage });

        var result = await _access.ResetPasswordAsync(
            GetCurrentAdminId(),
            id,
            request.NewPassword,
            cancellationToken);

        return result.Succeeded ? Ok(new { message = "Пароль администратора обновлён" }) : MapError(result.Error);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _access.DeleteAsync(
            GetCurrentAdminId(),
            id,
            cancellationToken);

        return result.Succeeded ? NoContent() : MapError(result.Error);
    }

    private IActionResult MapError(AdminAccessError error)
        => error switch
        {
            AdminAccessError.Forbidden => Forbid(),
            AdminAccessError.NotFound => NotFound(new { message = "Администратор не найден" }),
            AdminAccessError.DuplicateEmail => Conflict(new { message = "Email уже используется администратором" }),
            AdminAccessError.PatientEmailConflict => Conflict(new { message = "Email уже используется пациентом" }),
            AdminAccessError.LastSuperAdmin => Conflict(new { message = "Нельзя удалить или понизить последнего суперадминистратора" }),
            AdminAccessError.CannotDeleteSelf => BadRequest(new { message = "Нельзя удалить собственную активную учётную запись" }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = "Не удалось изменить доступ администратора" })
        };

    private int GetCurrentAdminId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}