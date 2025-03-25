
using ECOMMAPP.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ECOMMAPP.Infrastructure.Data
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = serviceProvider.GetRequiredService<AppDbContext>())
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                // Look for existing products
                if (context.Products.Any())
                {
                    logger.LogInformation("Database already contains products, skipping seeding.");
                    return;   // DB has been seeded
                }

                logger.LogInformation("Adding seed products to database...");

                try
                {
                    context.Products.AddRange(
                        new Product
                        {
                            Name = "Laptop",
                            Price = 1200.00M,
                            StockQuantity = 10,
                            LastUpdated = DateTime.UtcNow
                        },
                        new Product
                        {
                            Name = "Smartphone",
                            Price = 800.00M,
                            StockQuantity = 15,
                            LastUpdated = DateTime.UtcNow
                        },
                        new Product
                        {
                            Name = "Tablet",
                            Price = 400.00M,
                            StockQuantity = 20,
                            LastUpdated = DateTime.UtcNow
                        },
                        new Product
                        {
                            Name = "Headphones",
                            Price = 150.00M,
                            StockQuantity = 30,
                            LastUpdated = DateTime.UtcNow
                        }
                    );

                    context.SaveChanges();
                    logger.LogInformation("Seed products added successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the database.");
                }
            }
        }
    }
}