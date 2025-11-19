using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace PriceTracker.Workers
{
    public class AdvancedPriceParser : IDisposable
    {
        private IWebDriver _driver;
        private bool _disposed = false;

        public AdvancedPriceParser()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            _driver = new ChromeDriver(options);
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
        }

        public async Task<decimal?> ParsePrice(string url)
        {

            try
            {
                Console.WriteLine($"🌐 Selenium загружает страницу: {url}");
                _driver.Navigate().GoToUrl(url);

                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(20));
                wait.Until(driver => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));

                await Task.Delay(3000);
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, 500);");
                await Task.Delay(2000);
                var priceSelectors = new[]
                {
                    ".product-buy__price",          
                    "[data-product-price]",         
                    ".price__main-value",            
                    ".product-card-price__current",  
                    ".price__value",                
                    ".product-price__value",         
                    "[itemprop=price]",             
                    ".product-price-current",       
                    ".lg-visible .price"             
                };

                foreach (var selector in priceSelectors)
                {
                    try
                    {
                        var element = _driver.FindElement(By.CssSelector(selector));
                        if (element != null && !string.IsNullOrEmpty(element.Text))
                        {
                            var priceText = element.Text;
                            Console.WriteLine($"🔍 Нашли элемент с текстом: '{priceText}' по селектору: {selector}");

                            var price = PriceWorkerHelper.ParsePriceText(priceText);
                            if (price.HasValue)
                            {
                                Console.WriteLine($"✅ Selenium нашел цену по селектору {selector}: {price}");
                                return price;
                            }
                        }
                    }
                    catch (NoSuchElementException)
                    {
                        Console.WriteLine($"❌ Элемент не найден по селектору: {selector}");
                        continue;
                    }
                }

                Console.WriteLine("🔍 Ищем цену в JavaScript переменных...");
                var scriptResults = new[]
                {
                    "return window.productCardPrice",
                    "return window.currentPrice",
                    "return window.productData?.price",
                    "return window.__INITIAL_STATE__?.product?.price",
                    "return JSON.parse(document.querySelector('[type=application/ld+json]')?.textContent || '{}')?.offers?.price"
                };

                foreach (var script in scriptResults)
                {
                    try
                    {
                        var result = ((IJavaScriptExecutor)_driver).ExecuteScript(script);
                        if (result != null)
                        {
                            Console.WriteLine($"🔍 JS переменная: {script} = {result}");
                            var price = PriceWorkerHelper.ParsePriceText(result.ToString());
                            if (price.HasValue)
                            {
                                Console.WriteLine($"✅ Selenium нашел цену в JS: {price}");
                                return price;
                            }
                        }
                    }
                    catch (Exception jsEx)
                    {
                        Console.WriteLine($"⚠️ Ошибка выполнения JS {script}: {jsEx.Message}");
                        continue;
                    }
                }

                Console.WriteLine("🔍 Ищем цену по тексту на странице...");
                var pageText = _driver.PageSource;
                var priceFromText = PriceWorkerHelper.ParseUniversalPrice(pageText);
                if (priceFromText.HasValue)
                {
                    Console.WriteLine($"✅ Нашли цену в тексте страницы: {priceFromText}");
                    return priceFromText;
                }

                Console.WriteLine("❌ Selenium не смог найти цену на странице");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Selenium ошибка для {url}: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _driver?.Quit();
                _driver?.Dispose();
                _disposed = true;
            }
        }
    }
}