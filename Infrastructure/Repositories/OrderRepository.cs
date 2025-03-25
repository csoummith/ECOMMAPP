using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Enums;
using ECOMMAPP.Core.Interfaces;
using ECOMMAPP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ECOMMAPP.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderRepository> _logger;  // Define the logger

        public OrderRepository(AppDbContext context, ILogger<OrderRepository> logger)  // Add logger parameter to constructor
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<Order>> GetAllAsync()
        {
            return await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .ToListAsync();
        }

        public async Task<Order> GetByIdAsync(int id)
        {
            return await _context.Orders
        .Include(o => o.Items)
        .ThenInclude(i => i.Product)
        .FirstOrDefaultAsync(o => o.Id == id);
        }

       public async Task<Order> AddAsync(Order order)
{
    try
            {
                _logger.LogInformation($"Adding new order with {order.Items?.Count ?? 0} items");
                
                // Ensure order items reference back to their parent order
                if (order.Items != null)
                {
                    foreach (var item in order.Items)
                    {
                        item.OrderId = order.Id;
                    }
                }
                
                // Add the order to the context
                _context.Orders.Add(order);
                
                // Save changes to the database
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Successfully added order with ID: {order.Id}");
                
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding order to database");
                throw;
            }

}

        public async Task UpdateAsync(Order order)
        {
            _context.Entry(order).State = EntityState.Modified;
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await OrderExists(order.Id))
                {
                    throw new KeyNotFoundException($"Order with ID {order.Id} not found.");
                }
                throw;
            }
        }

        public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
        {
            return await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.Status == status)
                .ToListAsync();
        }

        private async Task<bool> OrderExists(int id)
        {
            return await _context.Orders.AnyAsync(e => e.Id == id);
        }
    }
}
