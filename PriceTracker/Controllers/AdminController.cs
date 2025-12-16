using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Data;
using PriceTracker.Models.DTOs;

namespace PriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AppDbContext db, ILogger<AdminController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet("system-stats")]
        public async Task<IActionResult> GetSystemStats()
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            try
            {
                var totalUsers = await _db.Users.CountAsync();
                var activeUsers = await _db.Users.CountAsync(u => u.IsActive);
                var totalProducts = await _db.Products.CountAsync();
                var activeProducts = await _db.Products.CountAsync(p => p.IsActive);
                var totalPriceRecords = await _db.PriceHistories.CountAsync();
                var today = DateTime.UtcNow.Date;
                var todayUpdates = await _db.PriceHistories
                    .CountAsync(h => h.RetrievedAt.Date == today);
                var adminCount = await _db.Users.CountAsync(u => u.Role == "Admin");
                var userCount = await _db.Users.CountAsync(u => u.Role == "User");

                var recentUsers = await _db.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(10)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.Role,
                        CreatedAt = u.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                        LastLogin = u.LastLogin.HasValue ? u.LastLogin.Value.ToString("dd.MM.yyyy HH:mm") : "Никогда",
                        u.IsActive,
                        ProductsCount = u.Products.Count
                    })
                    .ToListAsync();

                var recentProducts = await _db.Products
                    .Include(p => p.User)
                    .OrderByDescending(p => p.Id)
                    .Take(10)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Url,
                        LastPrice = p.LastPrice.HasValue ? $"{p.LastPrice.Value:N0} ₽" : "Нет данных",
                        p.IsActive,
                        HistoryCount = p.History.Count,
                        UserName = p.User != null ? p.User.Username : "Система",
                        UserEmail = p.User != null ? p.User.Email : null
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Overview = new
                    {
                        totalUsers,
                        activeUsers,
                        totalProducts,
                        activeProducts,
                        totalPriceRecords,
                        todayUpdates
                    },
                    UserStats = new[]
                    {
                new { Role = "Admin", Count = adminCount },
                new { Role = "User", Count = userCount }
            },
                    RecentUsers = recentUsers,
                    RecentProducts = recentProducts,
                    ServerTime = DateTime.Now.ToString("HH:mm:ss"),
                    Uptime = "24 часа"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка получения системной статистики");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] string? search = null)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            try
            {
                var query = _db.Users.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(u =>
                        u.Username.ToLower().Contains(search) ||
                        u.Email.ToLower().Contains(search) ||
                        u.Role.ToLower().Contains(search));
                }

                var users = await query
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.Role,
                        u.CreatedAt,
                        u.LastLogin,
                        u.IsActive,
                        ProductsCount = u.Products.Count,
                        TotalPriceChecks = u.Products.SelectMany(p => p.History).Count(),
                        LastActivity = u.Products.SelectMany(p => p.History)
                            .OrderByDescending(h => h.RetrievedAt)
                            .Select(h => (DateTime?)h.RetrievedAt)
                            .FirstOrDefault()
                    })
                    .OrderByDescending(u => u.CreatedAt)
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка получения пользователей");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetAllProducts([FromQuery] string? search = null,
                                               [FromQuery] bool? active = null,
                                               [FromQuery] int? userId = null)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            try
            {
                var query = _db.Products.Include(p => p.User).AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(p =>
                        p.Name.ToLower().Contains(search) ||
                        p.Url.ToLower().Contains(search));
                }

                if (active.HasValue)
                    query = query.Where(p => p.IsActive == active.Value);

                if (userId.HasValue)
                    query = query.Where(p => p.UserId == userId);

                var products = await query
                    .Include(p => p.History) 
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Url,
                        p.LastPrice,
                        p.IsActive,
                        HistoryCount = p.History.Count,
                        User = p.User != null ? new
                        {
                            p.User.Id,
                            p.User.Username,
                            p.User.Email
                        } : null,
                        LastUpdate = p.History
                            .OrderByDescending(h => h.RetrievedAt)
                            .Select(h => (DateTime?)h.RetrievedAt)
                            .FirstOrDefault(),
                        History = p.History.Select(h => new { h.Price }).ToList()
                    })
                    .OrderByDescending(p => p.Id)
                    .ToListAsync();
                var productsResult = products.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Url,
                    p.LastPrice,
                    p.IsActive,
                    p.HistoryCount,
                    p.User,
                    p.LastUpdate,
                    MinPrice = p.History.Any() ? p.History.Min(h => h.Price) : (decimal?)null,
                    MaxPrice = p.History.Any() ? p.History.Max(h => h.Price) : (decimal?)null,
                    PriceChanges = p.HistoryCount - 1
                }).ToList();

                return Ok(productsResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка получения товаров");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] AdminUserUpdateDto dto)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            try
            {
                var user = await _db.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "Пользователь не найден" });

                if (!string.IsNullOrEmpty(dto.Username) && dto.Username != user.Username)
                {
                    var existingUser = await _db.Users
                        .FirstOrDefaultAsync(u => u.Username == dto.Username && u.Id != id);
                    if (existingUser != null)
                        return BadRequest(new { message = "Имя пользователя уже занято" });
                    user.Username = dto.Username;
                }

                if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
                {
                    var existingUser = await _db.Users
                        .FirstOrDefaultAsync(u => u.Email == dto.Email && u.Id != id);
                    if (existingUser != null)
                        return BadRequest(new { message = "Email уже используется" });
                    user.Email = dto.Email;
                }

                if (!string.IsNullOrEmpty(dto.Role))
                    user.Role = dto.Role;

                if (dto.IsActive.HasValue)
                    user.IsActive = dto.IsActive.Value;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.Role,
                    user.IsActive,
                    message = "Данные пользователя обновлены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обновления пользователя");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            try
            {
                if (currentUser.Id == id)
                    return BadRequest(new { message = "Нельзя удалить самого себя" });

                var user = await _db.Users
                    .Include(u => u.Products)
                    .ThenInclude(p => p.History)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return NotFound(new { message = "Пользователь не найден" });
                foreach (var product in user.Products)
                {
                    _db.PriceHistories.RemoveRange(product.History);
                }
                _db.Products.RemoveRange(user.Products);
                _db.Users.Remove(user);

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Пользователь и все его данные удалены",
                    deletedProducts = user.Products.Count,
                    deletedPriceRecords = user.Products.Sum(p => p.History.Count)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка удаления пользователя");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("products/{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] AdminProductUpdateDto dto)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            try
            {
                var product = await _db.Products.FindAsync(id);
                if (product == null)
                    return NotFound(new { message = "Товар не найден" });

                if (!string.IsNullOrEmpty(dto.Name))
                    product.Name = dto.Name;

                if (!string.IsNullOrEmpty(dto.Url))
                    product.Url = dto.Url;

                if (dto.IsActive.HasValue)
                    product.IsActive = dto.IsActive.Value;

                if (dto.UserId.HasValue)
                {
                    var user = await _db.Users.FindAsync(dto.UserId.Value);
                    if (user == null)
                        return BadRequest(new { message = "Пользователь не найден" });
                    product.UserId = dto.UserId;
                }

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    product.Id,
                    product.Name,
                    product.Url,
                    product.IsActive,
                    product.UserId,
                    message = "Данные товара обновлены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обновления товара");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            try
            {
                var product = await _db.Products
                    .Include(p => p.History)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                    return NotFound(new { message = "Товар не найден" });

                var historyCount = product.History.Count;
                _db.PriceHistories.RemoveRange(product.History);
                _db.Products.Remove(product);

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Товар удален",
                    deletedPriceRecords = historyCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка удаления товара");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("activity-log")]
        public async Task<IActionResult> GetActivityLog([FromQuery] int limit = 100)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            if (currentUser == null || currentUser.Role != "Admin")
                return Forbid();

            try
            {
                var priceUpdates = await _db.PriceHistories
                    .Include(h => h.Product)
                    .ThenInclude(p => p.User)
                    .OrderByDescending(h => h.RetrievedAt)
                    .Take(limit)
                    .Select(h => new
                    {
                        Type = "PRICE_UPDATE",
                        Time = h.RetrievedAt,
                        Product = h.Product.Name,
                        Price = h.Price,
                        User = h.Product.User != null ? h.Product.User.Username : "Система",
                        Role = h.Product.User != null ? h.Product.User.Role : "Система"
                    })
                    .ToListAsync();

                var userLogins = await _db.Users
                    .Where(u => u.LastLogin != null)
                    .OrderByDescending(u => u.LastLogin)
                    .Take(limit)
                    .Select(u => new
                    {
                        Type = "USER_LOGIN",
                        Time = u.LastLogin.Value,
                        Product = "",
                        Price = 0m,
                        User = u.Username,
                        Role = u.Role
                    })
                    .ToListAsync();

                var activityLog = priceUpdates
                    .Cast<object>()
                    .Concat(userLogins.Cast<object>())
                    .OrderByDescending(a => ((dynamic)a).Time)
                    .Take(limit)
                    .ToList();

                return Ok(activityLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка получения лога активности");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class AdminUserUpdateDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AdminProductUpdateDto
    {
        public string? Name { get; set; }
        public string? Url { get; set; }
        public bool? IsActive { get; set; }
        public int? UserId { get; set; }
    }
}