using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Interfaces;
using ECOMMAPP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECOMMAPP.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductRepository> _logger;

        public ProductRepository(AppDbContext context, ILogger<ProductRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            _logger.LogInformation("Retrieving all products");
            return await _context.Products.ToListAsync();
        }

        public async Task<Product> GetByIdAsync(int id)
        {
            _logger.LogInformation($"Retrieving product with ID: {id}");
            return await _context.Products.FindAsync(id);
        }

public async Task<Product> AddAsync(Product product)
{
    try
    {
        if (product == null)
        {
            throw new ArgumentNullException(nameof(product));
        }

        // Set Id to 0 to ensure it's treated as a new entity
        product.Id = 0;
        
        // ALWAYS set LastUpdated to current time
        product.LastUpdated = DateTime.UtcNow;
        
        _logger.LogInformation($"Adding product: {product.Name}, LastUpdated: {product.LastUpdated}");
        
        // Add the product to the context
        _context.Products.Add(product);
        
        // Save changes to the database
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Successfully added product with ID: {product.Id}");
        
        // Return the product with its generated ID
        return product;
    }
    catch (DbUpdateException ex)
    {
        // Log the exception details
        _logger.LogError(ex, $"Database error while adding product: {ex.Message}");
        _logger.LogError($"Inner exception: {ex.InnerException?.Message}");
        
        // Also log the product details that caused the error
        _logger.LogError($"Product being added: Name={product?.Name}, Price={product?.Price}, StockQuantity={product?.StockQuantity}, LastUpdated={product?.LastUpdated}");
        
        throw new InvalidOperationException($"Failed to add product to database: {ex.Message}", ex);
    }
}
     
       public async Task UpdateAsync(Product product)
{
    if (product == null)
    {
        _logger.LogError("Attempt to update null product");
        throw new ArgumentNullException(nameof(product));
    }

    _logger.LogInformation($"Updating product ID: {product.Id}, LastUpdated: {product.LastUpdated}");
    
    try
    {
        // Get the current entity from the database
        var existingProduct = await _context.Products.FindAsync(product.Id);
        if (existingProduct == null)
        {
            _logger.LogError($"Update failed - Product with ID {product.Id} not found");
            throw new KeyNotFoundException($"Product with ID {product.Id} not found.");
        }

        // Update the entity properties
        existingProduct.Name = product.Name;
        existingProduct.Price = product.Price;
        existingProduct.StockQuantity = product.StockQuantity;
        
        // LastUpdated will be automatically updated by the SaveChanges override

        // Save changes
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Product ID: {product.Id} updated successfully");
    }
    catch (DbUpdateConcurrencyException ex)
    {
        if (!await ProductExists(product.Id))
        {
            _logger.LogError($"Update failed - Product with ID {product.Id} not found");
            throw new KeyNotFoundException($"Product with ID {product.Id} not found.");
        }
        _logger.LogError(ex, $"Concurrency error while updating product: {ex.Message}");
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Error updating product: {ex.Message}");
        throw;
    }
}

        public async Task DeleteAsync(int id)
        {
            _logger.LogInformation($"Deleting product ID: {id}");
            
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    _logger.LogWarning($"Delete failed - Product with ID {id} not found");
                    throw new KeyNotFoundException($"Product with ID {id} not found.");
                }

                // Check if the product is referenced in any orders
                bool isReferencedInOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
                if (isReferencedInOrders)
                {
                    _logger.LogWarning($"Cannot delete product ID {id} - referenced in orders");
                    throw new InvalidOperationException("Cannot delete product because it is referenced in one or more orders.");
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Product ID: {id} deleted successfully");
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException) && !(ex is InvalidOperationException))
            {
                _logger.LogError(ex, $"Error deleting product: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CheckStockAsync(int productId, int quantity)
        {
            _logger.LogInformation($"Checking stock for product ID: {productId}, quantity: {quantity}");
            var product = await _context.Products.FindAsync(productId);
            bool hasStock = product != null && product.StockQuantity >= quantity;
            _logger.LogInformation($"Stock check result: {hasStock}");
            return hasStock;
        }

        public async Task UpdateStockAsync(int productId, int quantity)
{
    _logger.LogInformation($"Updating stock for product ID: {productId}, quantity change: {quantity}");
    
    // Use a separate execution strategy with retries for handling concurrency
    await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        // Important: Use a new transaction for each stock update to ensure isolation
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // For thread safety, always get the latest version of the product
                var product = await _context.Products
                    // In SQL Server you could use: .FromSqlRaw("SELECT * FROM Products WITH (UPDLOCK) WHERE Id = {0}", productId)
                    // For MySQL, we'll just use Find and rely on optimistic concurrency
                    .FindAsync(productId);

                if (product == null)
                {
                    _logger.LogError($"Stock update failed - Product with ID {productId} not found");
                    throw new KeyNotFoundException($"Product with ID {productId} not found.");
                }

                // Calculate new stock level
                int newStock = product.StockQuantity - quantity;
                
                if (newStock < 0)
                {
                    _logger.LogError($"Insufficient stock for product {productId}");
                    throw new InvalidOperationException($"Insufficient stock for product {productId}");
                }

                // Update the stock
                product.StockQuantity = newStock;
                
                // Save changes
                await _context.SaveChangesAsync();
                
                // Commit the transaction
                await transaction.CommitAsync();
                
                _logger.LogInformation($"Stock updated successfully for product ID: {productId}, new stock: {newStock}");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Handle concurrency conflict - the record was updated by another process
                _logger.LogWarning(ex, $"Concurrency conflict while updating stock for product {productId}");
                
                // Transaction will be rolled back automatically
                // Let the exception propagate so caller can retry if needed
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating stock: {ex.Message}");
                // Transaction will be rolled back
                throw;
            }
        }
    });
}

        private async Task<bool> ProductExists(int id)
        {
            return await _context.Products.AnyAsync(e => e.Id == id);
        }
    }
}