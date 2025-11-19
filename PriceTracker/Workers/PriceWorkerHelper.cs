using AngleSharp;
using AngleSharp.Html.Parser;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace PriceTracker.Workers
{
    public static class PriceWorkerHelper
    {
        private static readonly HttpClient httpClient;

        static PriceWorkerHelper()
        {
            var handler = new HttpClientHandler()
            {
                UseCookies = true,
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseProxy = false
            };

            httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
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
                else if (url.Contains("citilink.ru"))
                {
                    return await ParseCitilink(url);
                }
                else if (url.Contains("e-katalog.ru") || url.Contains("ekatalog.ru") || url.Contains("e-catalog-tech.ru"))
                {
                    return await ParseEkatalog(url);
                }
                else if (url.Contains("mvideo.ru"))
                {
                    return await ParseMVideo(url);
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

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка парсинга BooksToScrape: {ex.Message}");
                return null;
            }
        }

        private static async Task<decimal?> ParseCitilink(string url)
        {
            try
            {
                Console.WriteLine("🔌 Парсим Citilink");
                var html = await httpClient.GetStringAsync(url);

                var metaPriceMatch = Regex.Match(html, @"data-meta-price=\""([^""]+)\""");
                if (metaPriceMatch.Success)
                {
                    var price = ParsePriceText(metaPriceMatch.Groups[1].Value);
                    if (price.HasValue)
                    {
                        Console.WriteLine($"✅ Нашли цену в Citilink: {price}");
                        return price;
                    }
                }

                var jsonLdMatch = Regex.Match(html, @"<script type=\""application/ld\+json\"">(.*?)</script>", RegexOptions.Singleline);
                if (jsonLdMatch.Success)
                {
                    var json = jsonLdMatch.Groups[1].Value;
                    var priceMatch = Regex.Match(json, @"""price""\s*:\s*""?([\d\s,]+)""?");
                    if (priceMatch.Success)
                    {
                        var price = ParsePriceText(priceMatch.Groups[1].Value);
                        if (price.HasValue)
                        {
                            Console.WriteLine($"✅ Нашли цену в Citilink (JSON-LD): {price}");
                            return price;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка парсинга Citilink: {ex.Message}");
                return null;
            }
        }

        private static async Task<decimal?> ParseEkatalog(string url)
        {
            try
            {
                Console.WriteLine("📊 Парсим E-katalog");
                var html = await httpClient.GetStringAsync(url);

                Console.WriteLine($"📄 Получили HTML длиной: {html.Length} символов");

                var fromPriceMatch = Regex.Match(html, @"от[^\d]*([\d\s]+)[^\d]*₽");
                if (fromPriceMatch.Success)
                {
                    var priceText = fromPriceMatch.Groups[1].Value;
                    var price = ParsePriceText(priceText);
                    if (price.HasValue)
                    {
                        Console.WriteLine($"✅ Нашли основную цену E-katalog 'от ... ₽': {price}");
                        return price;
                    }
                }

                var sellerPriceMatch = Regex.Match(html, @"<div[^>]*class=[^>]*price[^>]*>([^<]+)");
                if (sellerPriceMatch.Success)
                {
                    var priceText = sellerPriceMatch.Groups[1].Value;
                    var price = ParsePriceText(priceText);
                    if (price.HasValue && price > 1000 && price < 50000)
                    {
                        Console.WriteLine($"✅ Нашли цену E-katalog в списке продавцов: {price}");
                        return price;
                    }
                }

                var classPatterns = new[]
                {
                    @"class=\""price[^>]*>([^<]+)",
                    @"class=\""desc-price[^>]*>([^<]+)",
                    @"itemprop=\""price\""[^>]*content=\""([^""]+)\"""
                };

                foreach (var pattern in classPatterns)
                {
                    var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var priceText = match.Groups[1].Value;
                        var price = ParsePriceText(priceText);
                        if (price.HasValue && price > 1000 && price < 50000)
                        {
                            Console.WriteLine($"✅ Нашли цену E-katalog по классу: {price}");
                            return price;
                        }
                    }
                }

                var allPriceMatches = Regex.Matches(html, @"([\d\s]+)[^\d]*₽");
                var prices = new List<decimal>();

                foreach (Match match in allPriceMatches)
                {
                    var price = ParsePriceText(match.Groups[1].Value);
                    if (price.HasValue && price > 1000 && price < 50000)
                    {
                        prices.Add(price.Value);
                    }
                }

                if (prices.Any())
                {
                    var minPrice = prices.Min();
                    Console.WriteLine($"✅ Нашли минимальную цену E-katalog: {minPrice}");
                    return minPrice;
                }

                Console.WriteLine("❌ Не удалось найти цену на E-katalog");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка парсинга E-katalog: {ex.Message}");
                return null;
            }
        }

        private static async Task<decimal?> ParseMVideo(string url)
        {
            try
            {
                Console.WriteLine("📺 Парсим М.Видео с помощью Selenium");

                using var seleniumParser = new AdvancedPriceParser();
                var price = await seleniumParser.ParsePrice(url);

                if (price.HasValue)
                {
                    Console.WriteLine($"✅ Нашли цену в М.Видео через Selenium: {price}");
                    return price;
                }

                Console.WriteLine("🔄 Selenium не нашел цену, пробуем HttpClient");
                return await ParseMVideoWithHttpClient(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка Selenium парсинга М.Видео: {ex.Message}");
                return await ParseMVideoWithHttpClient(url);
            }
        }

        public static async Task<decimal?> ParseMVideoWithHttpClient(string url)
        {
            try
            {
                Console.WriteLine("🔄 Пробуем HttpClient для М.Видео");
                var html = await httpClient.GetStringAsync(url);

                var patterns = new[]
                {
                    @"productCardPrice.:.([\d\s]+)",
                    @"""price""\s*:\s*""?([\d\s,]+)""?",
                    @"data-product-price=\""([^""]+)\""",
                    @"itemprop=\""price\""[^>]*content=\""([^""]+)\""",
                    @"class=\""price__value[^>]*>([^<]+)",
                    @"class=\""product-buy__price[^>]*>([^<]+)",
                    @"currentPrice.:.'([^']+)'",
                    @"price.:.([\d\s]+)"
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var priceText = match.Groups[1].Value;
                        var price = ParsePriceText(priceText);
                        if (price.HasValue && price > 100 && price < 1000000)
                        {
                            Console.WriteLine($"✅ Нашли цену М.Видео по шаблону: {price}");
                            return price;
                        }
                    }
                }

                var allPrices = new List<decimal>();
                var priceMatches = Regex.Matches(html, @"([1-9]\d{0,2}[ \d]*\d)[^\d]*₽");

                foreach (Match match in priceMatches)
                {
                    var price = ParsePriceText(match.Groups[1].Value);
                    if (price.HasValue && price > 100 && price < 1000000)
                    {
                        allPrices.Add(price.Value);
                    }
                }

                if (allPrices.Any())
                {
                    var medianPrice = allPrices.OrderBy(p => p).Skip(allPrices.Count / 2).First();
                    Console.WriteLine($"✅ Нашли медианную цену М.Видео: {medianPrice}");
                    return medianPrice;
                }

                Console.WriteLine("❌ Не удалось найти цену на М.Видео");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка HttpClient парсинга М.Видео: {ex.Message}");
                return null;
            }
        }
        private static async Task<decimal?> ParseUniversal(string url)
        {
            try
            {
                Console.WriteLine("🌐 Используем универсальный парсер");
                var html = await httpClient.GetStringAsync(url);
                return ParseUniversalPrice(html);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка универсального парсера: {ex.Message}");
                return null;
            }
        }

        public static decimal? ParseUniversalPrice(string html)
        {
            try
            {
                var pricePatterns = new[]
                {
                    @"([\d\s]+)[^\d]{0,5}₽", 
                    @"([\d\s]+)[^\d]{0,5}руб", 
                    @"цена[^\d]*([\d\s]+)",    
                    @"стоимость[^\d]*([\d\s]+)" 
                };

                var possiblePrices = new List<decimal>();

                foreach (var pattern in pricePatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        var price = ParsePriceText(match.Groups[1].Value);
                        if (price.HasValue && price > 100 && price < 1000000)
                        {
                            possiblePrices.Add(price.Value);
                        }
                    }
                }

                if (possiblePrices.Any())
                {
                    var minPrice = possiblePrices.Min();
                    Console.WriteLine($"✅ Нашли цену через универсальный метод: {minPrice}");
                    return minPrice;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка универсального парсинга: {ex.Message}");
                return null;
            }
        }

        public static decimal? ParsePriceText(string? priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText))
            {
                Console.WriteLine("❌ Пустой текст цены");
                return null;
            }

            try
            {
                Console.WriteLine($"🔧 Обрабатываем текст цены: '{priceText}'");

                var cleanText = priceText.Trim()
                    .Replace(" ", "")
                    .Replace("&nbsp;", "")
                    .Replace("₽", "")
                    .Replace("₴", "")
                    .Replace("$", "")
                    .Replace("€", "")
                    .Replace("руб.", "")
                    .Replace("руб", "")
                    .Replace("р.", "")
                    .Replace("р", "")
                    .Replace("грн", "")
                    .Replace(",", ".");

                cleanText = Regex.Replace(cleanText, @"[^\d.]", "");

                var dotCount = cleanText.Count(c => c == '.');
                if (dotCount > 1)
                {
                    var lastDotIndex = cleanText.LastIndexOf('.');
                    cleanText = cleanText.Replace(".", "");
                    cleanText = cleanText.Insert(lastDotIndex - (dotCount - 1), ".");
                }

                if (string.IsNullOrWhiteSpace(cleanText) || !cleanText.Any(char.IsDigit))
                {
                    Console.WriteLine("❌ После очистки не осталось цифр");
                    return null;
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