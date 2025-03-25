
using ECOMMAPP.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECOMMAPP.Core.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync();
        Task<Product> GetProductByIdAsync(int id);
        Task<Product> CreateProductAsync(Product product);
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(int id);
        Task<bool> CheckStockAvailabilityAsync(int productId, int quantity);
    }
}