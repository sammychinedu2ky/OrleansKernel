using System.Security.Claims;

namespace API.Util;

public static class RetrieveUserId
{
    public static Guid? GetUserId(ClaimsPrincipal claim)
    {
        var userId =   claim.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous User";
        if (Guid.TryParse(userId, out var parsedUserId))
        {
            return parsedUserId;
        }
        return null;
    }
}