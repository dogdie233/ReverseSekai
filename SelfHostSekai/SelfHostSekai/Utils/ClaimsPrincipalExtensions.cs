using System.Security.Claims;

namespace SelfHostSekai.Utils;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal user)
    {
        public long GetUserIdRequired()
        {
            return long.TryParse(user.FindFirst("userId")?.Value, out var id) ? id : throw new InvalidOperationException("UserId claim is missing or invalid.");
        }

        public string? GetSessionToken()
        {
            return user.FindFirst("sessionToken")?.Value;
        }
    }
}