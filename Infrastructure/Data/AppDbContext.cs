using ECOMMAPP.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System;

namespace ECOMMAPP.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) 
            : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure relationships
            modelBuilder.Entity<OrderItem>()
            .HasOne(o => o.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(o => o.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(o => o.Product)
            .WithMany()
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure concurrency tokens
        modelBuilder.Entity<Product>()
            .Property(p => p.LastUpdated)
            .IsRequired()
            .IsConcurrencyToken();

        modelBuilder.Entity<Order>()
            .Property(o => o.LastUpdated)
            .IsRequired()
            .IsConcurrencyToken();
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Product product && (entry.State == EntityState.Added || entry.State == EntityState.Modified))
                {
                    product.LastUpdated = now;
                }
                else if (entry.Entity is Order order && (entry.State == EntityState.Added || entry.State == EntityState.Modified))
                {
                    order.LastUpdated = now;
                }
            }
        }
    }
}