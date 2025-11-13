using Microsoft.EntityFrameworkCore;
using PriceTracker.Models;
namespace PriceTracker.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<PriceHistory> PriceHistories { get; set; }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .HasMany(p => p.History)
                .WithOne(h => h.Product)
                .HasForeignKey(h => h.ProductId);
        }
    }
}