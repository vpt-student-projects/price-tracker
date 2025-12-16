using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PriceTracker.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [JsonIgnore]
        public string Salt { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; } = DateTime.Now; 

        public DateTime? LastLogin { get; set; } 

        public bool IsActive { get; set; } = true;

        [JsonIgnore]
        public List<Product> Products { get; set; } = new();
    }
}