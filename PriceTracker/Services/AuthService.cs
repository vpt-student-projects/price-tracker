using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PriceTracker.Data;
using PriceTracker.Models;
using PriceTracker.Models.DTOs;

namespace PriceTracker.Services
{
    public interface IAuthService
    {
        Task<UserResponseDto?> Register(RegisterDto registerDto);
        Task<LoginResult?> Login(LoginDto loginDto);
        UserResponseDto? GetUserFromToken(string token);
        string GenerateToken(int userId, string username, string email, string role);
    }

    public class LoginResult
    {
        public string Token { get; set; } = string.Empty;
        public UserResponseDto User { get; set; } = new();
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<UserResponseDto?> Register(RegisterDto registerDto)
        {
            try
            {
                _logger.LogInformation($"Начало регистрации пользователя: {registerDto.Username}");

                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == registerDto.Username || u.Email == registerDto.Email);

                if (existingUser != null)
                {
                    _logger.LogWarning($"Попытка регистрации существующего пользователя: {registerDto.Username}");
                    return null;
                }

                using var hmac = new HMACSHA512();
                var salt = hmac.Key;
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDto.Password));

                var user = new User
                {
                    Username = registerDto.Username,
                    Email = registerDto.Email.ToLower(),
                    PasswordHash = Convert.ToBase64String(hash),
                    Salt = Convert.ToBase64String(salt),
                    Role = "User",
                    CreatedAt = DateTime.Now, 
                    IsActive = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Успешная регистрация пользователя: {user.Username} (ID: {user.Id})");

                return new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt,
                    IsActive = user.IsActive
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при регистрации пользователя {registerDto.Username}");
                throw;
            }
        }

        public async Task<LoginResult?> Login(LoginDto loginDto)
        {
            try
            {
                _logger.LogInformation($"Попытка входа пользователя: {loginDto.Username}");

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == loginDto.Username && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning($"Пользователь не найден: {loginDto.Username}");
                    return null;
                }

                if (!VerifyPassword(loginDto.Password, user.PasswordHash, user.Salt))
                {
                    _logger.LogWarning($"Неверный пароль для пользователя: {loginDto.Username}");
                    return null;
                }

                user.LastLogin = DateTime.Now;
                await _context.SaveChangesAsync();

                var token = GenerateToken(user.Id, user.Username, user.Email, user.Role);

                _logger.LogInformation($"Успешный вход пользователя: {user.Username} (ID: {user.Id})");

                return new LoginResult
                {
                    Token = token,
                    User = new UserResponseDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        Role = user.Role,
                        CreatedAt = user.CreatedAt,
                        LastLogin = user.LastLogin,
                        IsActive = user.IsActive
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при входе пользователя {loginDto.Username}");
                throw;
            }
        }

        public string GenerateToken(int userId, string username, string email, string role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT ключ не настроен в конфигурации"));

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, username),
                new(JwtRegisteredClaimNames.Email, email),
                new(ClaimTypes.Role, role),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new("userId", userId.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public UserResponseDto? GetUserFromToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("JWT ключ не настроен в конфигурации"));

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                var userId = int.Parse(principal.FindFirst("userId")?.Value ?? "0");

                var user = _context.Users.Find(userId);
                if (user == null || !user.IsActive)
                    return null;

                return new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role,
                    CreatedAt = user.CreatedAt,
                    LastLogin = user.LastLogin,
                    IsActive = user.IsActive
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке JWT токена");
                return null;
            }
        }

        private bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(storedSalt);
                using var hmac = new HMACSHA512(saltBytes);
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                var computedHashString = Convert.ToBase64String(computedHash);

                return computedHashString == storedHash;
            }
            catch
            {
                return false;
            }
        }
    }
}