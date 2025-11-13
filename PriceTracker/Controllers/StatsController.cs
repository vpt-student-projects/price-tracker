using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Data;
using PriceTracker.Models;

namespace PriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StatsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductStats(int productId, [FromQuery] int days = 30)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);

            var history = await _db.PriceHistories
                .Where(h => h.ProductId == productId && h.RetrievedAt >= startDate)
                .OrderBy(h => h.RetrievedAt)
                .ToListAsync();

            if (history.Count == 0) return NotFound();

            var stats = new
            {
                ProductId = productId,
                Period = $"{days} days",
                CurrentPrice = history.Last().Price,
                MinPrice = history.Min(h => h.Price),
                MaxPrice = history.Max(h => h.Price),
                AveragePrice = history.Average(h => h.Price),
                PriceChanges = history.Count - 1,
                FirstRecord = history.First().RetrievedAt,
                LastRecord = history.Last().RetrievedAt,
                PriceHistory = history.Select(h => new { h.Price, h.RetrievedAt })
            };

            return Ok(stats);
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            var totalProducts = await _db.Products.CountAsync();
            var activeProducts = await _db.Products.CountAsync(p => p.IsActive);
            var totalPriceRecords = await _db.PriceHistories.CountAsync();
            var todayUpdates = await _db.PriceHistories
                .CountAsync(h => h.RetrievedAt.Date == DateTime.UtcNow.Date);

            var mostTracked = await _db.Products
                .OrderByDescending(p => p.History.Count)
                .Take(5)
                .Select(p => new { p.Id, p.Name, Records = p.History.Count })
                .ToListAsync();

            return Ok(new
            {
                totalProducts,
                activeProducts,
                totalPriceRecords,
                todayUpdates,
                mostTrackedProducts = mostTracked
            });
        }
    }
}