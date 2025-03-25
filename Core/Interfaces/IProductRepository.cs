using ECOMMAPP.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECOMMAPP.Core.Interfaces
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product> GetByIdAsync(int id);
        Task<Product> AddAsync(Product product);
        Task UpdateAsync(Product product);
        Task DeleteAsync(int id);
        Task<bool> CheckStockAsync(int productId, int quantity);
        Task UpdateStockAsync(int productId, int quantity);
    }
}