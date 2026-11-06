
using Beckend.Enums;
using Beckend.JWT;
using Beckend.Models;
using Beckend.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<UserSession> _userSessions;
        private readonly TokenService _tokenService;

        public AuthController(
            IMongoDatabase database,
            TokenService tokenService)
        {
            _users = database.GetCollection<User>("Users");
            _userSessions =
                database.GetCollection<UserSession>("UserSessions");
            _tokenService = tokenService;
        }

        private static bool IsUserBanned(User user)
        {
            if (!user.IsBanned)
                return false;

            return !user.BannedUntil.HasValue ||
                   user.BannedUntil.Value > DateTime.UtcNow;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    message = "All fields are required"
                });
            }

            var email = request.Email.Trim().ToLowerInvariant();

            var existingUser = await _users
                .Find(u => u.ContactInfo.Email == email)
                .FirstOrDefaultAsync();

            if (existingUser != null)
            {
                return BadRequest(new
                {
                    message = "User already exists"
                });
            }

            var nameParts = request.FullName.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            var user = new User
            {
                Surname = nameParts.Length > 0 ? nameParts[0] : "",
                Name = nameParts.Length > 1 ? nameParts[1] : "",
                Role = UserRole.User,
                ContactInfo = new ContactInfo
                {
                    Email = email,
                    Password = BCrypt.Net.BCrypt.HashPassword(
                        request.Password)
                }
            };

            await _users.InsertOneAsync(user);

            return Ok(new
            {
                message = "User registered successfully",
                userId = user.Id
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password"
                });
            }

            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _users
                .Find(u => u.ContactInfo.Email == email)
                .FirstOrDefaultAsync();

            if (user == null ||
                string.IsNullOrEmpty(user.ContactInfo?.Password) ||
                !BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.ContactInfo.Password))
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password"
                });
            }

            if (IsUserBanned(user))
            {
                return StatusCode(403, new
                {
                    message = "Your account is banned"
                });
            }

            var accessToken =
                _tokenService.GenerateAccessToken(
                    user.ContactInfo, user);

            var (refreshToken, expires) =
                _tokenService.CreateRefreshTokenWithExpiry();

            var session = new UserSession
            {
                UserId = user.Id,
                RefreshTokenHash =
                    _tokenService.HashRefreshToken(refreshToken),
                RefreshTokenExpiry = expires,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _userSessions.InsertOneAsync(session);

            return Ok(new
            {
                accessToken,
                refreshToken,
                refreshTokenExpiry = expires,
                user = new
                {
                    user.Id,
                    user.Surname,
                    user.Name,
                    Role = user.Role.ToString(),
                    user.ContactInfo.Email
                }
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new
                {
                    message = "Refresh token is required"
                });
            }

            var oldHash =
                _tokenService.HashRefreshToken(request.RefreshToken);

            var now = DateTime.UtcNow;
            var session = await _userSessions
                .Find(s =>
                    s.RefreshTokenHash == oldHash &&
                    s.IsActive &&
                    s.RefreshTokenExpiry > now)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid or expired refresh token"
                });
            }

            var user = await _users
                .Find(u => u.Id == session.UserId)
                .FirstOrDefaultAsync();

            if (user == null || IsUserBanned(user))
            {
                return Unauthorized(new
                {
                    message = "Session is no longer valid"
                });
            }

            var (newRefreshToken, expires) =
                _tokenService.CreateRefreshTokenWithExpiry();

            var newHash =
                _tokenService.HashRefreshToken(newRefreshToken);
            var filter = Builders<UserSession>.Filter.And(
                Builders<UserSession>.Filter.Eq(
                    s => s.Id, session.Id),
                Builders<UserSession>.Filter.Eq(
                    s => s.RefreshTokenHash, oldHash),
                Builders<UserSession>.Filter.Eq(
                    s => s.IsActive, true),
                Builders<UserSession>.Filter.Gt(
                    s => s.RefreshTokenExpiry, DateTime.UtcNow)
            );

            var update = Builders<UserSession>.Update
                .Set(s => s.RefreshTokenHash, newHash)
                .Set(s => s.RefreshTokenExpiry, expires);

            var updatedSession =
                await _userSessions.FindOneAndUpdateAsync(
                    filter,
                    update);

            if (updatedSession == null)
            {
                return Unauthorized(new
                {
                    message = "Refresh token already used"
                });
            }

            var newAccessToken =
                _tokenService.GenerateAccessToken(
                    user.ContactInfo, user);

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken,
                refreshTokenExpiry = expires
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(
            [FromBody] RefreshRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                var hash = _tokenService.HashRefreshToken(
                    request.RefreshToken);

                await _userSessions.UpdateOneAsync(
                    s => s.RefreshTokenHash == hash &&
                         s.IsActive,
                    Builders<UserSession>.Update
                        .Set(s => s.IsActive, false)
                );
            }

            return Ok(new
            {
                message = "Logged out successfully"
            });
        }
    }
}
