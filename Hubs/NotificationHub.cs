using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace DentalClinic.Hubs;

/// <summary>
/// Единый хаб для realtime-уведомлений вместо поллинга каждые 60 секунд.
/// Пациент попадает в группу "patient-{id}", админ — в группу "admins".
/// Врач (если авторизован) — в группу "doctor-{id}", на будущее.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);

        if (!string.IsNullOrEmpty(userId))
        {
            if (string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-{userId}");
            else if (string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"doctor-{userId}");
        }

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

        await base.OnConnectedAsync();
    }
}