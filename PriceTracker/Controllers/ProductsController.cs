using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Data;
using PriceTracker.Models;
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
            var list = await _db.Products
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

            return Ok(list);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _db.Products
                .Include(p => p.History)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            var result = new
            {
                product.Id,
                product.Name,
                product.Url,
                product.LastPrice,
                product.IsActive,
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
            if (product == null || string.IsNullOrWhiteSpace(product.Url) || string.IsNullOrWhiteSpace(product.Name))
                return BadRequest("Product must contain Name and Url.");

            product.Id = 0;
            product.History = product.History ?? new List<PriceHistory>();

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Product update)
        {
            if (update == null) return BadRequest();

            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();

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
                HistoryCount = product.History.Count
            };

            return Ok(result);
        }
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();

            var histories = _db.PriceHistories.Where(h => h.ProductId == id);
            _db.PriceHistories.RemoveRange(histories);
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();

            return NoContent();
        }
        [HttpPost("{id:int}/refresh")]
        public async Task<IActionResult> RefreshPrice(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();

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
                        RetrievedAt = DateTime.UtcNow
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
        [HttpPost("initialize")]
        public async Task<IActionResult> InitializeAllPrices()
        {
            var products = await _db.Products.Where(p => p.IsActive).ToListAsync();
            var updatedProducts = new List<object>();

            foreach (var product in products)
            {
                try
                {
                    var price = await PriceWorkerHelper.ParsePriceOnce(product.Url);
                    if (price.HasValue)
                    {
                        product.LastPrice = price.Value;
                        _db.PriceHistories.Add(new PriceHistory
                        {
                            ProductId = product.Id,
                            Price = price.Value,
                            RetrievedAt = DateTime.UtcNow
                        });
                        updatedProducts.Add(new { product.Id, product.Name, price = price.Value });
                    }
                }
                catch
                {
                    continue;
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { UpdatedCount = updatedProducts.Count, Products = updatedProducts });
        }
    }
}