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
            var startDate = DateTime.Now.AddDays(-days);

            var history = await _db.PriceHistories
                .Where(h => h.ProductId == productId && h.RetrievedAt >= startDate)
                .OrderBy(h => h.RetrievedAt)
                .ToListAsync();

            if (history.Count == 0) return NotFound();

            var priceChanges = new List<decimal>();
            for (int i = 1; i < history.Count; i++)
            {
                priceChanges.Add(history[i].Price - history[i - 1].Price);
            }

            var stats = new
            {
                ProductId = productId,
                Period = $"{days} дней",
                CurrentPrice = history.Last().Price,
                MinPrice = history.Min(h => h.Price),
                MaxPrice = history.Max(h => h.Price),
                AveragePrice = Math.Round(history.Average(h => h.Price), 2),
                MedianPrice = CalculateMedianPrice(history),
                PriceChanges = history.Count - 1,
                FirstRecord = history.First().RetrievedAt,
                LastRecord = history.Last().RetrievedAt,
                PriceVolatility = priceChanges.Any() ? Math.Round(priceChanges.Average(), 2) : 0,
                PriceHistory = history.Select(h => new {
                    h.Price,
                    RetrievedAt = h.RetrievedAt.ToString("dd.MM.yyyy HH:mm"),
                    Date = h.RetrievedAt
                }),
                Recommendations = GeneratePriceRecommendations(history)
            };

            return Ok(stats);
        }

        private static decimal CalculateMedianPrice(List<PriceHistory> history)
        {
            var prices = history.Select(h => h.Price).OrderBy(p => p).ToList();
            int count = prices.Count;

            if (count == 0) return 0;

            if (count % 2 == 0)
            {
                return (prices[count / 2 - 1] + prices[count / 2]) / 2;
            }
            else
            {
                return prices[count / 2];
            }
        }

        private static object GeneratePriceRecommendations(List<PriceHistory> history)
        {
            if (history.Count < 2) return new { Message = "Недостаточно данных для анализа" };

            var currentPrice = history.Last().Price;
            var minPrice = history.Min(h => h.Price);
            var avgPrice = history.Average(h => h.Price);

            var recommendations = new List<string>();

            if (currentPrice <= minPrice * 1.05m) 
            {
                recommendations.Add("💰 Отличная цена для покупки - близко к историческому минимуму");
            }
            else if (currentPrice >= avgPrice * 1.15m) 
            {
                recommendations.Add("⚠️ Цена выше среднего - рассмотрите ожидание снижения");
            }

            if (currentPrice < avgPrice)
            {
                recommendations.Add("📉 Текущая цена ниже среднего - хорошее время для покупки");
            }

            return new
            {
                BuyRecommendation = currentPrice <= avgPrice ? "Рекомендуется к покупке" : "Рассмотрите ожидание",
                PriceLevel = currentPrice switch
                {
                    var p when p <= minPrice * 1.05m => "Очень низкая",
                    var p when p <= avgPrice * 0.95m => "Низкая",
                    var p when p <= avgPrice * 1.05m => "Средняя",
                    _ => "Высокая"
                },
                Details = recommendations
            };
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
        [HttpGet("product/{productId}/price-stats")]
        public async Task<IActionResult> GetProductPriceStats(int productId, [FromQuery] int days = 30)
        {
            try
            {
                var startDate = DateTime.Now.AddDays(-days);

                var history = await _db.PriceHistories
                    .Where(h => h.ProductId == productId && h.RetrievedAt >= startDate)
                    .OrderBy(h => h.RetrievedAt)
                    .ToListAsync();

                if (history.Count == 0)
                    return NotFound(new { message = "No price history found for the specified period" });

                var stats = new
                {
                    ProductId = productId,
                    PeriodDays = days,
                    CurrentPrice = history.Last().Price,
                    MinPrice = history.Min(h => h.Price),
                    MaxPrice = history.Max(h => h.Price),
                    AveragePrice = Math.Round(history.Average(h => h.Price), 2),
                    PriceChanges = history.Count - 1,
                    AnalysisPeriod = $"{history.First().RetrievedAt:dd.MM.yyyy} - {history.Last().RetrievedAt:dd.MM.yyyy}",
                    TotalRecords = history.Count
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}