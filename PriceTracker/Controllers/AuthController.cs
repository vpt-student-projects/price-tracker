using Microsoft.AspNetCore.Mvc;
using PriceTracker.Models.DTOs;
using PriceTracker.Services;
using System.Linq;

namespace PriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                var userResponse = await _authService.Register(registerDto);
                if (userResponse == null)
                    return BadRequest(new { message = "Пользователь с таким именем или email уже существует" });

                var token = _authService.GenerateToken(userResponse.Id, userResponse.Username, userResponse.Email, userResponse.Role);

                return Ok(new
                {
                    Token = token,
                    User = userResponse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка регистрации");
                return StatusCode(500, new { message = "Ошибка сервера" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var loginResult = await _authService.Login(loginDto);
                if (loginResult == null)
                    return Unauthorized(new { message = "Неверное имя пользователя или пароль" });

                return Ok(new
                {
                    Token = loginResult.Token,
                    User = loginResult.User
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка входа");
                return StatusCode(500, new { message = "Ошибка сервера" });
            }
        }

        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var user = (UserResponseDto?)HttpContext.Items["User"];
            if (user == null)
                return Unauthorized();

            return Ok(user);
        }

        [HttpGet("debug")]
        public IActionResult DebugAuth()
        {
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            var user = HttpContext.Items["User"] as UserResponseDto;

            return Ok(new
            {
                HasAuthHeader = !string.IsNullOrEmpty(authHeader),
                AuthHeader = authHeader?.Substring(0, Math.Min(50, authHeader.Length)) + "...",
                HasUserInContext = user != null,
                User = user,
                AllHeaders = HttpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
            });
        }
    }
}