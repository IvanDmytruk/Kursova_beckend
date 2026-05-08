using Beckend.Enums;
using Beckend.JWT;
using Beckend.Models;
using Beckend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using BCrypt.Net;

namespace Beckend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<UserSession> _userSessions;
        private readonly TokenService _tokenService;
        private readonly JwtSettings _jwtSettings;

        public AuthController(IMongoClient client, TokenService tokenService, IOptions<JwtSettings> jwtSettings)
        {
            var database = client.GetDatabase("CreateadTournament");
            _users = database.GetCollection<User>("Users");
            _userSessions = database.GetCollection<UserSession>("UserSessions");
            _tokenService = tokenService;
            _jwtSettings = jwtSettings.Value;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Перевірка чи існує користувач
            var existingUser = await _users.Find(u => u.ContactInfo.Email == request.Email).FirstOrDefaultAsync();
            if (existingUser != null)
                return BadRequest(new { message = "User already exists" });

            // Хешування паролю
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var nameParts = request.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var user = new User
            {
                Surname = nameParts.Length > 0 ? nameParts[0] : "",
                Name = nameParts.Length > 1 ? nameParts[1] : "",
                Role = UserRole.User,
                ContactInfo = new ContactInfo
                {
                    Email = request.Email,
                    Password = passwordHash
                }
            };

            await _users.InsertOneAsync(user);

            return Ok(new { message = "User registered successfully", userId = user.Id });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Пошук користувача за email
            var user = await _users.Find(u => u.ContactInfo.Email == request.Email).FirstOrDefaultAsync();
            if (user == null)
                return Unauthorized(new { message = "Invalid email or password" });

            // Перевірка паролю
            if (string.IsNullOrEmpty(user.ContactInfo.Password))
                return Unauthorized(new { message = "Invalid email or password" });

            bool isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.ContactInfo.Password);
            if (!isValid)
                return Unauthorized(new { message = "Invalid email or password" });

            // Генерація токенів
            var accessToken = _tokenService.GenerateAccessToken(user.ContactInfo, user);
            var (refreshToken, expires) = _tokenService.CreateRefreshTokenWithExpiry(_jwtSettings.RefreshTokenExpiryDays);

            // Зберігаємо сесію
            var session = new UserSession
            {
                UserId = user.Id,
                RefreshToken = refreshToken,
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
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
                return BadRequest(new { message = "Refresh token is required" });

            // Пошук активної сесії за refresh token
            var session = await _userSessions
                .Find(s => s.RefreshToken == request.RefreshToken && s.IsActive && s.RefreshTokenExpiry > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (session == null)
                return Unauthorized(new { message = "Invalid or expired refresh token" });

            // Пошук користувача
            var user = await _users.Find(u => u.Id == session.UserId).FirstOrDefaultAsync();
            if (user == null)
                return Unauthorized(new { message = "User not found" });

            // Деактивуємо стару сесію (одноразове використання refresh token)
            session.IsActive = false;
            await _userSessions.ReplaceOneAsync(s => s.Id == session.Id, session);

            // Генерація нових токенів
            var newAccessToken = _tokenService.GenerateAccessToken(user.ContactInfo, user);
            var (newRefreshToken, expires) = _tokenService.CreateRefreshTokenWithExpiry(_jwtSettings.RefreshTokenExpiryDays);

            // Створюємо нову сесію
            var newSession = new UserSession
            {
                UserId = user.Id,
                RefreshToken = newRefreshToken,
                RefreshTokenExpiry = expires,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            await _userSessions.InsertOneAsync(newSession);

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken,
                refreshTokenExpiry = expires
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
                return BadRequest(new { message = "Refresh token is required" });

            // Деактивуємо сесію при виході
            var session = await _userSessions.Find(s => s.RefreshToken == request.RefreshToken).FirstOrDefaultAsync();
            if (session != null)
            {
                session.IsActive = false;
                await _userSessions.ReplaceOneAsync(s => s.Id == session.Id, session);
            }

            return Ok(new { message = "Logged out successfully" });
        }
    }
}