using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.RegularExpressions;

namespace PriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public TestController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("parse")]
        public async Task<IActionResult> TestParse([FromQuery] string url)
        {
            try
            {
                Console.WriteLine($"🔍 Тестируем парсинг: {url}");

                var results = new List<ParseResult>();

                results.Add(await ParseWithMethod1(url));

                results.Add(await ParseWithMethod2(url));

                results.Add(await ParseWithMethod3(url));

                var successfulResult = results.FirstOrDefault(r => r.Success);
                if (successfulResult != null)
                {
                    return Ok(new
                    {
                        url,
                        success = true,
                        price = successfulResult.Price,
                        method = successfulResult.Method,
                        rawContent = successfulResult.RawContent?.Substring(0, Math.Min(200, successfulResult.RawContent.Length)) + "..."
                    });
                }
                else
                {
                    return Ok(new
                    {
                        url,
                        success = false,
                        errors = results.Select(r => r.Error).ToArray(),
                        allMethodsFailed = true
                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    url,
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("raw")]
        public async Task<IActionResult> GetRawHtml([FromQuery] string url)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("scraper");

                client.DefaultRequestHeaders.Referrer = new Uri("https://www.google.com/");

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    return Ok(new
                    {
                        url,
                        statusCode = response.StatusCode,
                        html = html.Substring(0, Math.Min(1000, html.Length)) + "...",
                        length = html.Length
                    });
                }
                else
                {
                    return Ok(new
                    {
                        url,
                        statusCode = response.StatusCode,
                        headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                        error = $"HTTP Error: {(int)response.StatusCode} {response.StatusCode}"
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("advanced")]
        public async Task<IActionResult> AdvancedTest([FromQuery] string url)
        {
            try
            {
                var result = await ParseWithAdvancedMethods(url);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    url,
                    success = false,
                    error = ex.Message
                });
            }
        }

        private async Task<ParseResult> ParseWithMethod1(string url)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("scraper");
                var html = await client.GetStringAsync(url);
                return ParseHtmlContent(html, url, "Method1 - Basic");
            }
            catch (Exception ex)
            {
                return new ParseResult
                {
                    Success = false,
                    Error = $"Method1: {ex.Message}",
                    Method = "Method1 - Basic"
                };
            }
        }

        private async Task<ParseResult> ParseWithMethod2(string url)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("scraper");

                client.DefaultRequestHeaders.Referrer = new Uri("https://www.google.com/");
                client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Google Chrome\";v=\"120\", \"Chromium\";v=\"120\", \"Not?A_Brand\";v=\"99\"");
                client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");

                var html = await client.GetStringAsync(url);
                return ParseHtmlContent(html, url, "Method2 - With Referer");
            }
            catch (Exception ex)
            {
                return new ParseResult
                {
                    Success = false,
                    Error = $"Method2: {ex.Message}",
                    Method = "Method2 - With Referer"
                };
            }
        }

        private async Task<ParseResult> ParseWithMethod3(string url)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("scraper");

                await Task.Delay(new Random().Next(1000, 3000));

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Referrer = new Uri("https://yandex.ru/");
                request.Headers.Add("DNT", "1");
                request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\", \"Not?A_Brand\";v=\"99\"");
                request.Headers.Add("sec-ch-ua-mobile", "?0");
                request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return new ParseResult
                    {
                        Success = false,
                        Error = $"Method3: HTTP {response.StatusCode}",
                        Method = "Method3 - Delayed with Headers"
                    };
                }

                var html = await response.Content.ReadAsStringAsync();
                return ParseHtmlContent(html, url, "Method3 - Delayed with Headers");
            }
            catch (Exception ex)
            {
                return new ParseResult
                {
                    Success = false,
                    Error = $"Method3: {ex.Message}",
                    Method = "Method3 - Delayed with Headers"
                };
            }
        }

        private async Task<object> ParseWithAdvancedMethods(string url)
        {
            var client = new HttpClient(new HttpClientHandler
            {
                UseCookies = true,
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                UseProxy = false
            });

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            client.DefaultRequestHeaders.Referrer = new Uri("https://www.google.com/");

            try
            {
                var response = await client.GetAsync(url);
                var html = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var parseResult = ParseHtmlContent(html, url, "Advanced Method");
                    return new
                    {
                        url,
                        success = parseResult.Success,
                        price = parseResult.Price,
                        method = parseResult.Method,
                        statusCode = response.StatusCode,
                        contentSample = html.Substring(0, Math.Min(300, html.Length)) + "..."
                    };
                }
                else
                {
                    return new
                    {
                        url,
                        success = false,
                        statusCode = response.StatusCode,
                        headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                        error = $"HTTP Error: {(int)response.StatusCode} {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    url,
                    success = false,
                    error = ex.Message
                };
            }
        }

        private ParseResult ParseHtmlContent(string html, string url, string method)
        {
            try
            {
                Console.WriteLine($"🛠 Парсим HTML длиной {html.Length} символов...");

                if (url.Contains("dns-shop.ru"))
                {
                    return ParseDnsShop(html, method);
                }
                else if (url.Contains("onlinetrade.ru"))
                {
                    return ParseOnlineTrade(html, method);
                }
                else if (url.Contains("citilink.ru"))
                {
                    return ParseCitilink(html, method);
                }

                return new ParseResult
                {
                    Success = false,
                    Error = "Неподдерживаемый магазин",
                    Method = method,
                    RawContent = html
                };
            }
            catch (Exception ex)
            {
                return new ParseResult
                {
                    Success = false,
                    Error = $"Ошибка парсинга: {ex.Message}",
                    Method = method,
                    RawContent = html
                };
            }
        }

        private ParseResult ParseDnsShop(string html, string method)
        {
            Console.WriteLine("🛠 Парсим DNS-Shop...");

            var jsonLdMatch = Regex.Match(html, @"<script type=\""application/ld\+json\"">(.*?)</script>", RegexOptions.Singleline);
            if (jsonLdMatch.Success)
            {
                var json = jsonLdMatch.Groups[1].Value;
                var priceMatch = Regex.Match(json, @"""price""\s*:\s*""?(\d+(?:[.,]\d+)?)""?");
                if (priceMatch.Success)
                {
                    var price = ParsePrice(priceMatch.Groups[1].Value);
                    if (price.HasValue)
                    {
                        return new ParseResult
                        {
                            Success = true,
                            Price = price.Value,
                            Method = $"{method} - JSON-LD",
                            RawContent = html
                        };
                    }
                }
            }

            var dataPriceMatch = Regex.Match(html, @"data-product-price=\""([^""]+)\""");
            if (dataPriceMatch.Success)
            {
                var price = ParsePrice(dataPriceMatch.Groups[1].Value);
                if (price.HasValue)
                {
                    return new ParseResult
                    {
                        Success = true,
                        Price = price.Value,
                        Method = $"{method} - data-product-price",
                        RawContent = html
                    };
                }
            }

            var productDataMatch = Regex.Match(html, @"productCardData.*?price.*?(\d+(?:[.,]\d+)?)", RegexOptions.Singleline);
            if (productDataMatch.Success)
            {
                var price = ParsePrice(productDataMatch.Groups[1].Value);
                if (price.HasValue)
                {
                    return new ParseResult
                    {
                        Success = true,
                        Price = price.Value,
                        Method = $"{method} - productCardData",
                        RawContent = html
                    };
                }
            }

            return new ParseResult
            {
                Success = false,
                Error = "Цена не найдена в HTML",
                Method = method,
                RawContent = html
            };
        }

        private ParseResult ParseOnlineTrade(string html, string method)
        {
            Console.WriteLine("🛠 Парсим OnlineTrade...");

            var itempropMatch = Regex.Match(html, @"itemprop=\""price\""[^>]*content=\""([^""]+)\""");
            if (itempropMatch.Success)
            {
                var price = ParsePrice(itempropMatch.Groups[1].Value);
                if (price.HasValue)
                {
                    return new ParseResult
                    {
                        Success = true,
                        Price = price.Value,
                        Method = $"{method} - itemprop price",
                        RawContent = html
                    };
                }
            }

            var jsonLdMatch = Regex.Match(html, @"<script type=\""application/ld\+json\"">(.*?)</script>", RegexOptions.Singleline);
            if (jsonLdMatch.Success)
            {
                var json = jsonLdMatch.Groups[1].Value;
                var priceMatch = Regex.Match(json, @"""price""\s*:\s*""?(\d+(?:[.,]\d+)?)""?");
                if (priceMatch.Success)
                {
                    var price = ParsePrice(priceMatch.Groups[1].Value);
                    if (price.HasValue)
                    {
                        return new ParseResult
                        {
                            Success = true,
                            Price = price.Value,
                            Method = $"{method} - JSON-LD",
                            RawContent = html
                        };
                    }
                }
            }

            return new ParseResult
            {
                Success = false,
                Error = "Цена не найдена в HTML",
                Method = method,
                RawContent = html
            };
        }

        private ParseResult ParseCitilink(string html, string method)
        {
            Console.WriteLine("🛠 Парсим Citilink...");

            var metaPriceMatch = Regex.Match(html, @"data-meta-price=\""([^""]+)\""");
            if (metaPriceMatch.Success)
            {
                var price = ParsePrice(metaPriceMatch.Groups[1].Value);
                if (price.HasValue)
                {
                    return new ParseResult
                    {
                        Success = true,
                        Price = price.Value,
                        Method = $"{method} - data-meta-price",
                        RawContent = html
                    };
                }
            }

            return new ParseResult
            {
                Success = false,
                Error = "Цена не найдена в HTML",
                Method = method,
                RawContent = html
            };
        }

        private decimal? ParsePrice(string priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText))
                return null;

            try
            {
                var cleanText = priceText
                    .Replace(" ", "")
                    .Replace("&nbsp;", "")
                    .Replace("₽", "")
                    .Replace("руб.", "")
                    .Replace("руб", "")
                    .Replace("р.", "")
                    .Replace("р", "")
                    .Trim();

                cleanText = cleanText.Replace(",", ".");
                cleanText = Regex.Replace(cleanText, @"[^\d.]", "");

                if (decimal.TryParse(cleanText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price))
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

    public class ParseResult
    {
        public bool Success { get; set; }
        public decimal Price { get; set; }
        public string Method { get; set; }
        public string Error { get; set; }
        public string RawContent { get; set; }
    }
}