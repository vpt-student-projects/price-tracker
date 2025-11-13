using AngleSharp;
using System.Globalization;
using System.Text.RegularExpressions;
namespace PriceTracker.Workers
{
    public static class PriceWorkerHelper
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly Random random = new Random();

        static PriceWorkerHelper()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public static async Task<decimal?> ParsePriceOnce(string url)
        {
            try
            {
                Console.WriteLine($"🔍 Начинаем парсинг: {url}");

                if (url.Contains("books.toscrape.com"))
                {
                    return await ParseBooksToScrape(url);
                }
                else if (url.Contains("dns-shop.ru"))
                {
                    return await ParseDnsShop(url);
                }
                else if (url.Contains("citilink.ru"))
                {
                    return await ParseCitilink(url);
                }
                else if (url.Contains("onlinetrade.ru"))
                {
                    return await ParseOnlineTrade(url);
                }
                else
                {
                    return await ParseUniversal(url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Критическая ошибка парсинга {url}: {ex.Message}");
                return null;
            }
        }
        private static async Task<decimal?> ParseBooksToScrape(string url)
        {
            try
            {
                Console.WriteLine("📚 Используем парсер для BooksToScrape");

                var config = Configuration.Default.WithDefaultLoader();
                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(url);
                var priceElement = document.QuerySelector(".price_color");
                if (priceElement != null)
                {
                    var priceText = priceElement.TextContent;
                    Console.WriteLine($"✅ Нашли цену на BooksToScrape: {priceText}");
                    return ParsePriceText(priceText);
                }
                priceElement = document.QuerySelector("p.price_color");
                if (priceElement != null)
                {
                    var priceText = priceElement.TextContent;
                    Console.WriteLine($"✅ Нашли цену (альтернативный селектор): {priceText}");
                    return ParsePriceText(priceText);
                }
                Console.WriteLine("❌ Не удалось найти цену на BooksToScrape");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка парсинга BooksToScrape: {ex.Message}");
                return null;
            }
        }
        private static async Task<decimal?> ParseDnsShop(string url)
        {
            Console.WriteLine("🛒 Парсер DNS-Shop (в разработке)");
            return await ParseWithHttpClient(url, "dns-shop.ru");
        }
        private static async Task<decimal?> ParseCitilink(string url)
        {
            Console.WriteLine("🔌 Парсер Citilink (в разработке)");
            return await ParseWithHttpClient(url, "citilink.ru");
        }
        private static async Task<decimal?> ParseOnlineTrade(string url)
        {
            Console.WriteLine("📦 Парсер OnlineTrade (в разработке)");
            return await ParseWithHttpClient(url, "onlinetrade.ru");
        }
        private static async Task<decimal?> ParseUniversal(string url)
        {
            Console.WriteLine("🌐 Используем универсальный парсер");
            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(url);
                var selectors = new[]
                {
                    ".price", ".price__value", ".product-price", ".current-price",
                    "[class*='price']", "[itemprop='price']", ".cost", ".value"
                };
                foreach (var selector in selectors)
                {
                    var element = document.QuerySelector(selector);
                    if (element != null)
                    {
                        var priceText = element.TextContent?.Trim();
                        if (!string.IsNullOrEmpty(priceText))
                        {
                            var price = ParsePriceText(priceText);
                            if (price.HasValue)
                            {
                                Console.WriteLine($"✅ Нашли цену через селектор '{selector}': {priceText}");
                                return price;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ AngleSharp не сработал: {ex.Message}");
            }
            return await ParseWithHttpClient(url, "universal");
        }
        private static async Task<decimal?> ParseWithHttpClient(string url, string siteType)
        {
            try
            {
                var html = await httpClient.GetStringAsync(url);
                var patterns = new[]
                {
                    @"[""']price[""']\s*[:=]\s*[""']?([\d\s,\.]+)[""']?",
                    @"<span[^>]*class=[""'][^""']*price[^""']*[""'][^>]*>([\d\s,\.]+)<\/span>",
                    @"<meta[^>]*itemprop=[""']price[""'][^>]*content=[""']([\d\s,\.]+)[""']",
                    @"data-price=[""']([\d\s,\.]+)[""']",
                    @"content=[""']([\d\s,\.]+)[""'][^>]*itemprop=[""']price[""']"
                };
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var priceText = match.Groups[1].Value;
                        var price = ParsePriceText(priceText);
                        if (price.HasValue)
                        {
                            Console.WriteLine($"✅ Нашли цену через regex: {priceText}");
                            return price;
                        }
                    }
                }
                Console.WriteLine($"❌ Не удалось найти цену для {siteType}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка HttpClient для {siteType}: {ex.Message}");
                return null;
            }
        }
        private static decimal? ParsePriceText(string? priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText))
            {
                Console.WriteLine("❌ Пустой текст цены");
                return null;
            }
            try
            {
                Console.WriteLine($"🔧 Обрабатываем текст цены: '{priceText}'");
                var cleanText = priceText.Trim();
                cleanText = Regex.Replace(cleanText, @"[^\d,\.\s]", "");
                cleanText = Regex.Replace(cleanText, @"\s+", "");
                Console.WriteLine($"🔧 После очистки: '{cleanText}'");
                cleanText = cleanText.Replace(",", ".");
                var dotCount = cleanText.Count(c => c == '.');
                if (dotCount > 1)
                {
                    var lastDotIndex = cleanText.LastIndexOf('.');
                    cleanText = cleanText.Replace(".", "");
                    cleanText = cleanText.Insert(lastDotIndex - (dotCount - 1), ".");
                }

                Console.WriteLine($"🔧 Финальный текст для парсинга: '{cleanText}'");

                if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                {
                    Console.WriteLine($"✅ Успешно распарсена цена: {price}");
                    return price;
                }

                Console.WriteLine($"❌ Не удалось преобразовать в число: '{cleanText}'");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка парсинга текста цены '{priceText}': {ex.Message}");
                return null;
            }
        }
    }
}