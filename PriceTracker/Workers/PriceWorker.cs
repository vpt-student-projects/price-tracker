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

                _logger.LogInformation($"🔄 Начинаем обновление цен для {products.Count} товаров");

                int successCount = 0;
                int failCount = 0;

                foreach (var product in products)
                {
                    try
                    {
                        _logger.LogInformation($"🔍 Парсим цену для: {product.Name}");
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

                            successCount++;
                            _logger.LogInformation($"✅ Обновлен {product.Name}: {price.Value}");
                        }
                        else
                        {
                            failCount++;
                            _logger.LogWarning($"❌ Не удалось получить цену для: {product.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, $"💥 Ошибка парсинга {product.Url}");
                    }
                }

                await db.SaveChangesAsync(stoppingToken);

                _logger.LogInformation($"📊 Итог обновления: Успешно {successCount}, Не удалось {failCount}");

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}