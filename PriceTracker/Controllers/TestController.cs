using Microsoft.AspNetCore.Mvc;
using PriceTracker.Workers;

namespace PriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        [HttpGet("parse")]
        public async Task<IActionResult> TestParse([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return BadRequest("URL is required");
            }

            try
            {
                var price = await PriceWorkerHelper.ParsePriceOnce(url);
                return Ok(new
                {
                    Url = url,
                    Price = price,
                    Success = price.HasValue
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("test-sites")]
        public async Task<IActionResult> TestAllSites()
        {
            var testUrls = new[]
            {
                "https://books.toscrape.com/catalogue/a-light-in-the-attic_1000/index.html",
                "https://books.toscrape.com/catalogue/tipping-the-velvet_999/index.html",
            };

            var results = new List<object>();

            foreach (var url in testUrls)
            {
                var price = await PriceWorkerHelper.ParsePriceOnce(url);
                results.Add(new
                {
                    Url = url,
                    Price = price,
                    Success = price.HasValue
                });

                await Task.Delay(1000);
            }

            return Ok(results);
        }
    }
}