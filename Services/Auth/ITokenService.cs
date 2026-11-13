using Beckend.Models;

namespace Beckend.Services.Auth;

public interface ITokenService
{
    string HashRefreshToken(string refreshToken);

    string GenerateAccessToken(
        ContactInfo info,
        User user);

    string GenerateRefreshToken();

    (string token, DateTime expires)
        CreateRefreshTokenWithExpiry();
}
