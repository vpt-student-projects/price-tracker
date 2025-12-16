using System;
using System.Text.Json.Serialization;

namespace PriceTracker.Models
{
    public class PriceHistory
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Price { get; set; }
        public DateTime RetrievedAt { get; set; } = DateTime.Now; 

        [JsonIgnore]
        public Product Product { get; set; }
    }
}