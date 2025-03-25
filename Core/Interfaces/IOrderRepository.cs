using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECOMMAPP.Core.Interfaces
{
    public interface IOrderRepository
    {
        Task<IEnumerable<Order>> GetAllAsync();
        Task<Order> GetByIdAsync(int id);
        Task<Order> AddAsync(Order order);
        Task UpdateAsync(Order order);
        Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status);
    }
}