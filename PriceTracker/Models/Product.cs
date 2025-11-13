using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PriceTracker.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Url { get; set; } = string.Empty;

        public decimal? LastPrice { get; set; }

        public bool IsActive { get; set; } = true;

        [JsonIgnore]
        public List<PriceHistory> History { get; set; } = new();
    }
}