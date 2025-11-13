using AngleSharp;
using AngleSharp.Html.Parser;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PriceTracker.Workers
{
    public static class PriceWorkerHelper
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly HtmlParser htmlParser = new HtmlParser();

        static PriceWorkerHelper()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public static async Task<decimal?> ParsePriceOnce(string url)
        {
            try
            {
                if (url.Contains("dns-shop.ru"))
                    return await ParseDnsShop(url);
                else if (url.Contains("citilink.ru"))
                    return await ParseCitilink(url);
                else if (url.Contains("onlinetrade.ru"))
                    return await ParseOnlineTrade(url);
                else
                    return await ParseGeneric(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка парсинга {url}: {ex.Message}");
                return null;
            }
        }

        private static async Task<decimal?> ParseDnsShop(string url)
        {
            var html = await httpClient.GetStringAsync(url);
            var document = await htmlParser.ParseDocumentAsync(html);

            var productElement = document.QuerySelector("[data-product-price]");
            if (productElement != null)
            {
                var priceText = productElement.GetAttribute("data-product-price");
                if (!string.IsNullOrEmpty(priceText))
                {
                    var price = ParsePriceText(priceText);
                    if (price.HasValue) return price;
                }
            }

            var priceElement = document.QuerySelector(".product-buy__price");
            if (priceElement != null)
            {
                var price = ParsePriceText(priceElement.TextContent);
                if (price.HasValue) return price;
            }

            var scriptContent = document.QuerySelector("script[type=\"application/ld+json\"]")?.TextContent;
            if (!string.IsNullOrEmpty(scriptContent))
            {
                var match = Regex.Match(scriptContent, @"""price""\s*:\s*""?([\d\s,]+)""?");
                if (match.Success)
                {
                    var price = ParsePriceText(match.Groups[1].Value);
                    if (price.HasValue) return price;
                }
            }

            return null;
        }

        private static async Task<decimal?> ParseCitilink(string url)
        {
            var html = await httpClient.GetStringAsync(url);
            var document = await htmlParser.ParseDocumentAsync(html);

            var priceElement = document.QuerySelector("[data-meta-price]");
            if (priceElement != null)
            {
                var priceText = priceElement.GetAttribute("data-meta-price");
                if (!string.IsNullOrEmpty(priceText))
                {
                    var price = ParsePriceText(priceText);
                    if (price.HasValue) return price;
                }
            }

            var scriptContent = document.QuerySelector("script[type=\"application/ld+json\"]")?.TextContent;
            if (!string.IsNullOrEmpty(scriptContent))
            {
                var match = Regex.Match(scriptContent, @"""price""\s*:\s*""?([\d\s,]+)""?");
                if (match.Success)
                {
                    var price = ParsePriceText(match.Groups[1].Value);
                    if (price.HasValue) return price;
                }
            }

            priceElement = document.QuerySelector(".ProductHeader__price-default");
            if (priceElement != null)
            {
                var price = ParsePriceText(priceElement.TextContent);
                if (price.HasValue) return price;
            }

            return null;
        }

        private static async Task<decimal?> ParseOnlineTrade(string url)
        {
            var html = await httpClient.GetStringAsync(url);
            var document = await htmlParser.ParseDocumentAsync(html);

            var priceElement = document.QuerySelector("[itemprop=\"price\"]");
            if (priceElement != null)
            {
                var priceText = priceElement.GetAttribute("content") ?? priceElement.TextContent;
                var price = ParsePriceText(priceText);
                if (price.HasValue) return price;
            }

            priceElement = document.QuerySelector(".price.item_current_price");
            if (priceElement == null)
                priceElement = document.QuerySelector(".price");

            if (priceElement != null)
            {
                var price = ParsePriceText(priceElement.TextContent);
                if (price.HasValue) return price;
            }

            var scriptContent = document.QuerySelector("script:contains(\"price\")")?.TextContent;
            if (!string.IsNullOrEmpty(scriptContent))
            {
                var match = Regex.Match(scriptContent, @"price['\""]?\s*:\s*['\""]?([\d\s,]+)['\""]?");
                if (match.Success)
                {
                    var price = ParsePriceText(match.Groups[1].Value);
                    if (price.HasValue) return price;
                }
            }

            return null;
        }

        

        private static async Task<decimal?> ParseGeneric(string url)
        {
            var html = await httpClient.GetStringAsync(url);
            var document = await htmlParser.ParseDocumentAsync(html);

            var priceSelectors = new[]
            {
                ".price", ".Price", ".product-price", ".product_price",
                ".cost", ".value", "[itemprop=\"price\"]",
                ".current-price", ".final-price", ".sale-price"
            };

            foreach (var selector in priceSelectors)
            {
                var priceElement = document.QuerySelector(selector);
                if (priceElement != null)
                {
                    var priceText = priceElement.GetAttribute("content") ?? priceElement.TextContent;
                    var price = ParsePriceText(priceText);
                    if (price.HasValue) return price;
                }
            }

            var scriptContent = document.QuerySelector("script[type=\"application/ld+json\"]")?.TextContent;
            if (!string.IsNullOrEmpty(scriptContent))
            {
                var match = Regex.Match(scriptContent, @"""price""\s*:\s*""?([\d\s,]+)""?");
                if (match.Success)
                {
                    var price = ParsePriceText(match.Groups[1].Value);
                    if (price.HasValue) return price;
                }
            }

            return null;
        }

        private static decimal? ParsePriceText(string? priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText))
                return null;

            try
            {
                var cleanText = priceText
                    .Replace(" ", "")
                    .Replace("₽", "")
                    .Replace("руб.", "")
                    .Replace("руб", "")
                    .Replace("р.", "")
                    .Replace("р", "")
                    .Trim();

                cleanText = cleanText.Replace(",", ".");

                cleanText = Regex.Replace(cleanText, @"[^\d.]", "");

                if (string.IsNullOrWhiteSpace(cleanText) || !cleanText.Any(char.IsDigit))
                    return null;

                if (decimal.TryParse(cleanText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                {
                    Console.WriteLine($"✅ Успешно распарсена цена: {price} из '{priceText}'");
                    return price;
                }

                Console.WriteLine($"❌ Не удалось преобразовать: '{priceText}' -> '{cleanText}'");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при парсинге '{priceText}': {ex.Message}");
                return null;
            }
        }
    }
}