using Microsoft.EntityFrameworkCore;
using PriceTracker.Data;

namespace PriceTracker.Workers
{
    public class PriceWorker : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<PriceWorker> _logger;

        public PriceWorker(IServiceProvider services, ILogger<PriceWorker> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var products = await db.Products.Where(p => p.IsActive).ToListAsync(stoppingToken);

                foreach (var product in products)
                {
                    try
                    {
                        var price = await PriceWorkerHelper.ParsePriceOnce(product.Url);
                        if (price.HasValue)
                        {
                            product.LastPrice = price.Value;
                            db.PriceHistories.Add(new PriceTracker.Models.PriceHistory
                            {
                                ProductId = product.Id,
                                Price = price.Value,
                                RetrievedAt = DateTime.UtcNow
                            });
                            await db.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation("Updated {ProductName}: {Price}", product.Name, price.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка парсинга {Url}", product.Url);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}