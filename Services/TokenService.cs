using Beckend.JWT;
using Beckend.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Beckend.Services
{ 
    public class TokenService
    {
        private readonly JwtSettings _jwt;

        public TokenService(IOptions<JwtSettings> jwtOptions)
        {
            _jwt = jwtOptions.Value;

            Console.WriteLine($"TokenService initialized. SecretKey length: {_jwt?.SecretKey?.Length ?? 0}");
        }

        public string GenerateAccessToken(ContactInfo info, User user)
        {
            if (_jwt == null)
                throw new InvalidOperationException("JwtSettings is null");

            if (string.IsNullOrEmpty(_jwt.SecretKey))
                throw new InvalidOperationException("JwtSettings.SecretKey is empty");

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

        public (string token, DateTime expires) CreateRefreshTokenWithExpiry(int refreshDays)
        {
            var token = GenerateRefreshToken();
            var expires = DateTime.UtcNow.AddDays(refreshDays);
            return (token, expires);
        }
    }
}