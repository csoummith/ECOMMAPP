using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECOMMAPP.Core.Interfaces
{
    public interface IOrderService
    {
        Task<IEnumerable<Order>> GetAllOrdersAsync();
        Task<Order> GetOrderByIdAsync(int id);
        Task<Order> PlaceOrderAsync(Order order);
        Task CancelOrderAsync(int orderId);
        Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status);
        Task FulfillOrderAsync(int orderId);
    }
}
