using System.Security.Claims;

namespace API.Util;

public static class RetrieveUserId
{
    public static string? GetUserId(ClaimsPrincipal claim)
    {
        var userId = claim.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous User";
        return userId;
    }
}