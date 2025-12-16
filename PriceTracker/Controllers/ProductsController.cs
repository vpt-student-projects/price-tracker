using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Data;
using PriceTracker.Models;
using PriceTracker.Models.DTOs;
using PriceTracker.Workers;

namespace PriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(AppDbContext db, ILogger<ProductsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];
            var query = _db.Products.Include(p => p.User).AsQueryable();

            if (currentUser == null)
            {
                query = query.Where(p => p.UserId == null);
            }
            else if (currentUser.Role != "Admin")
            {
               
                var adminIds = await _db.Users
                    .Where(u => u.Role == "Admin")
                    .Select(u => u.Id)
                    .ToListAsync();

                query = query.Where(p =>
                    p.UserId == currentUser.Id ||    
                    p.UserId == null ||              
                    adminIds.Contains(p.UserId.Value) 
                );
            }

            var list = await query
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Url,
                    p.LastPrice,
                    p.IsActive,
                    HistoryCount = p.History.Count,
                    UserId = p.UserId,
                    UserName = p.User != null ? p.User.Username : "Гость",
                    UserRole = p.User != null ? p.User.Role : null
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];

            var product = await _db.Products
                .Include(p => p.User)
                .Include(p => p.History)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            if (currentUser == null)
            {
                if (product.UserId != null)
                    return Forbid();
            }
            else if (currentUser.Role != "Admin")
            {
                if (product.UserId != currentUser.Id && product.UserId != null)
                {
                    var isOwnerAdmin = product.User != null && product.User.Role == "Admin";
                    if (!isOwnerAdmin)
                        return Forbid();
                }
            }

            var result = new
            {
                product.Id,
                product.Name,
                product.Url,
                product.LastPrice,
                product.IsActive,
                product.UserId,
                UserName = product.User != null ? product.User.Username : "Гость",
                UserRole = product.User != null ? product.User.Role : null,
                History = product.History.Select(h => new
                {
                    h.Id,
                    h.ProductId,
                    h.Price,
                    h.RetrievedAt
                }).ToList()
            };

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];

            if (product == null || string.IsNullOrWhiteSpace(product.Url) || string.IsNullOrWhiteSpace(product.Name))
                return BadRequest("Product must contain Name and Url.");

            product.Id = 0;
            product.History = product.History ?? new List<PriceHistory>();

            if (currentUser != null)
            {
                product.UserId = currentUser.Id;
            }

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Product update)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];

            if (update == null) return BadRequest();

            var product = await _db.Products
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            if (currentUser == null)
                return Unauthorized();

            if (currentUser.Role != "Admin")
            {
                if (product.UserId != currentUser.Id)
                    return Forbid(); 
            }

            product.Name = update.Name ?? product.Name;
            product.Url = update.Url ?? product.Url;
            product.IsActive = update.IsActive;

            await _db.SaveChangesAsync();
            var result = new
            {
                product.Id,
                product.Name,
                product.Url,
                product.LastPrice,
                product.IsActive,
                HistoryCount = product.History.Count,
                product.UserId
            };

            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];

            var product = await _db.Products
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            if (currentUser == null)
                return Unauthorized();

            if (currentUser.Role != "Admin")
            {
                if (product.UserId != currentUser.Id)
                    return Forbid(); 
            }

            var histories = _db.PriceHistories.Where(h => h.ProductId == id);
            _db.PriceHistories.RemoveRange(histories);
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id:int}/refresh")]
        public async Task<IActionResult> RefreshPrice(int id)
        {
            var currentUser = (UserResponseDto?)HttpContext.Items["User"];

            var product = await _db.Products
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            if (currentUser == null)
                return Unauthorized();

            if (currentUser.Role != "Admin")
            {
                if (product.UserId != currentUser.Id)
                    return Forbid();
            }

            try
            {
                var price = await Workers.PriceWorkerHelper.ParsePriceOnce(product.Url);
                if (price.HasValue)
                {
                    product.LastPrice = price.Value;
                    _db.PriceHistories.Add(new PriceHistory
                    {
                        ProductId = product.Id,
                        Price = price.Value,
                        RetrievedAt = DateTime.Now
                    });
                    await _db.SaveChangesAsync();

                    return Ok(new
                    {
                        product.Id,
                        price = price.Value,
                        message = "Price updated successfully"
                    });
                }
                else
                {
                    return Ok(new
                    {
                        product.Id,
                        message = "Price not found on the page"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing price for {Url}", product.Url);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}