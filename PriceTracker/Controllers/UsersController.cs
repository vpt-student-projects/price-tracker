using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Data;
using PriceTracker.Models;
using PriceTracker.Models.DTOs;

namespace PriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<UsersController> _logger;

        public UsersController(AppDbContext db, ILogger<UsersController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            var users = await _db.Users
                .Select(u => new UserResponseDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    LastLogin = u.LastLogin,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || (currentUser.Role != "Admin" && currentUser.Id != id))
                return Forbid();

            var user = await _db.Users
                .Where(u => u.Id == id)
                .Select(u => new UserResponseDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    LastLogin = u.LastLogin,
                    IsActive = u.IsActive
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();

            return Ok(user);
        }

        [HttpPut("{id}/role")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleDto dto)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Role = dto.Role;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Роль обновлена" });
        }

        [HttpPut("{id}/active")]
        public async Task<IActionResult> UpdateActive(int id, [FromBody] UpdateActiveDto dto)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Статус обновлен" });
        }

        [HttpGet("{id}/products")]
        public async Task<IActionResult> GetUserProducts(int id)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || (currentUser.Role != "Admin" && currentUser.Id != id))
                return Forbid();

            var products = await _db.Products
                .Where(p => p.UserId == id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Url,
                    p.LastPrice,
                    p.IsActive,
                    HistoryCount = p.History.Count
                })
                .ToListAsync();

            return Ok(products);
        }
    }

    public class UpdateRoleDto
    {
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateActiveDto
    {
        public bool IsActive { get; set; }
    }
}