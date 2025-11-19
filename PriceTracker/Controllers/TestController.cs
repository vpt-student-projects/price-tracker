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
    }
}