using Beckend.JWT;
using Beckend.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Beckend.Services.Auth
{
    public class TokenService : ITokenService
    {
        private readonly JwtSettings _jwt;

        public TokenService(IOptions<JwtSettings> options)
        {
            _jwt = options.Value;
            if (_jwt.AccessTokenExpiryMinutes <= 0 ||
    _jwt.RefreshTokenExpiryDays <= 0)
            {
                throw new InvalidOperationException(
                    "JWT token expiration settings must be positive.");
            }
        }
        public string HashRefreshToken(string refreshToken)
        {
            var bytes = Encoding.UTF8.GetBytes(refreshToken);
            var hash = SHA256.HashData(bytes);

            return Convert.ToHexString(hash);
        }

        public string GenerateAccessToken(ContactInfo info, User user)
        {
            if (string.IsNullOrWhiteSpace(_jwt.SecretKey) ||
    Encoding.UTF8.GetByteCount(_jwt.SecretKey) < 32)
            {
                throw new InvalidOperationException(
                    "JWT secret must contain at least 32 bytes.");
            }

            if (string.IsNullOrWhiteSpace(_jwt.Issuer) ||
                string.IsNullOrWhiteSpace(_jwt.Audience))
            {
                throw new InvalidOperationException(
                    "JWT issuer and audience must be configured.");
            }
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, info?.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, $"{user.Surname ?? ""} {user.Name ?? ""}".Trim()),
                new Claim(ClaimTypes.Surname, user.Surname ?? string.Empty),
                new Claim(ClaimTypes.GivenName, user.Name ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public (string token, DateTime expires) CreateRefreshTokenWithExpiry()
        {
            var token = GenerateRefreshToken();
            var expires = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays);

            return (token, expires);
        }
    }
}