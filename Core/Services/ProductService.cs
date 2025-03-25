using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECOMMAPP.Core.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await _productRepository.GetAllAsync();
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            return await _productRepository.GetByIdAsync(id);
        }

       public async Task<Product> CreateProductAsync(Product product)
{
    if (product == null)
    {
        throw new ArgumentNullException(nameof(product));
    }

    // Basic validation
    if (string.IsNullOrWhiteSpace(product.Name))
    {
        throw new ArgumentException("Product name cannot be empty");
    }

    if (product.Price < 0)
    {
        throw new ArgumentException("Product price cannot be negative");
    }

    if (product.StockQuantity < 0)
    {
        throw new ArgumentException("Product stock quantity cannot be negative");
    }

    // Add the product
    return await _productRepository.AddAsync(product);
}

        public async Task UpdateProductAsync(Product product)
        {
            await _productRepository.UpdateAsync(product);
        }

        public async Task DeleteProductAsync(int id)
        {
            await _productRepository.DeleteAsync(id);
        }

        public async Task<bool> CheckStockAvailabilityAsync(int productId, int quantity)
        {
            return await _productRepository.CheckStockAsync(productId, quantity);
        }
    }
}