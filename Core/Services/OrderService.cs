using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Enums;
using ECOMMAPP.Core.Exceptions;
using ECOMMAPP.Core.Interfaces;
using ECOMMAPP.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECOMMAPP.Core.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<OrderService> _logger;
         private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderService(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            INotificationService notificationService,
            ILogger<OrderService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _notificationService = notificationService;
            _logger = logger;
             _httpContextAccessor = httpContextAccessor;
        }
 private class ReservationInfo
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public DateTime Timestamp { get; set; }
        }
        public async Task<IEnumerable<Order>> GetAllOrdersAsync()
        {
            _logger.LogInformation("Retrieving all orders");
            return await _orderRepository.GetAllAsync();
        }

        public async Task<Order> GetOrderByIdAsync(int id)
        {
            _logger.LogInformation($"Retrieving order with ID: {id}");
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                _logger.LogWarning($"Order with ID {id} not found");
                throw new OrderNotFoundException(id);
            }
            return order;
        }
public async Task<Order> PlaceOrderAsync(Order order)
{
      _logger.LogInformation("Placing a new order");

            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            if (order.Items == null || !order.Items.Any())
            {
                _logger.LogWarning("Order contains no items");
                throw new ArgumentException("Order must contain at least one item");
            }

            // Get reservations from session - only if HttpContext is available
            Dictionary<string, ReservationInfo> reservations = null;
            
            if (_httpContextAccessor.HttpContext != null)
            {
                reservations = _httpContextAccessor.HttpContext.Session?.GetObject<Dictionary<string, ReservationInfo>>("TempReservations");
                _logger.LogInformation($"Found {reservations?.Count ?? 0} reservations in session");
            }

            // Step 1: Validate inventory for each item
            foreach (var item in order.Items)
            {
                _logger.LogInformation($"Validating inventory for product ID: {item.ProductId}, quantity: {item.Quantity}");
                
                // Check if the item has a valid reservation
                bool hasValidReservation = false;
                
                if (reservations != null && !string.IsNullOrEmpty(item.ReservationId) && 
                    reservations.TryGetValue(item.ReservationId, out var reservation))
                {
                    _logger.LogInformation($"Found reservation {item.ReservationId} for product {item.ProductId}");
                    hasValidReservation = true;
                }
                
                if (!hasValidReservation)
                {
                    // No valid reservation, perform regular inventory check
                    var product = await _productRepository.GetByIdAsync(item.ProductId);
                    if (product == null)
                    {
                        _logger.LogWarning($"Product with ID {item.ProductId} not found");
                        throw new KeyNotFoundException($"Product with ID {item.ProductId} not found");
                    }

                    // Check if enough stock is available
                    if (product.StockQuantity < item.Quantity)
                    {
                        _logger.LogWarning($"Insufficient stock for product {product.Name} (ID: {product.Id}). " +
                                        $"Requested: {item.Quantity}, Available: {product.StockQuantity}");
                        throw new InsufficientStockException(
                            product.Id,
                            item.Quantity,
                            product.StockQuantity);
                    }

                    // Set current price on the order item if not already set
                    if (item.UnitPrice <= 0)
                    {
                        item.UnitPrice = product.Price;
                    }
                    
                    // Since this item wasn't properly reserved, we need to update stock now
                    _logger.LogInformation($"Updating stock for product ID: {item.ProductId}, quantity: {item.Quantity}");
                    await _productRepository.UpdateStockAsync(item.ProductId, item.Quantity);
                    _logger.LogInformation($"Reserved {item.Quantity} units of product ID {item.ProductId}");
                }
                else
                {
                    // Reservation exists, just retrieve the product to get the current price if needed
                    if (item.UnitPrice <= 0)
                    {
                        var product = await _productRepository.GetByIdAsync(item.ProductId);
                        if (product != null)
                        {
                            item.UnitPrice = product.Price;
                        }
                    }
                    
                    _logger.LogInformation($"Using existing reservation for product ID: {item.ProductId}");
                }
                
                // Clear navigation properties to avoid validation issues
                item.Order = null;
                item.Product = null;
            }

            // Step 2: Remove any used reservations from the session if available
            if (reservations != null && _httpContextAccessor.HttpContext != null)
            {
                foreach (var item in order.Items)
                {
                    if (!string.IsNullOrEmpty(item.ReservationId) && reservations.ContainsKey(item.ReservationId))
                    {
                        _logger.LogInformation($"Removing reservation {item.ReservationId} from session");
                        reservations.Remove(item.ReservationId);
                    }
                }

                // Update session
                _httpContextAccessor.HttpContext.Session?.SetObject("TempReservations", reservations);
            }
            
            // Step 3: Save the order
            order.OrderDate = DateTime.UtcNow;
            order.Status = OrderStatus.PendingFulfillment;
            order.LastUpdated = DateTime.UtcNow;
            
            _logger.LogInformation("Calling repository to save order");
            var createdOrder = await _orderRepository.AddAsync(order);
            _logger.LogInformation($"Order created successfully with ID: {createdOrder.Id}");
            
            return createdOrder;

}

        public async Task CancelOrderAsync(int orderId)
        {
            _logger.LogInformation($"Cancelling order with ID: {orderId}");
            
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning($"Order with ID {orderId} not found");
                throw new OrderNotFoundException(orderId);
            }

            // Check if the order can be cancelled
            if (order.Status == OrderStatus.Fulfilled)
            {
                _logger.LogWarning($"Cannot cancel order {orderId} because it is already fulfilled");
                throw new InvalidOperationException("Cannot cancel an already fulfilled order");
            }

            // Restore inventory
            foreach (var item in order.Items)
            {
                // Use negative quantity to increase stock
                await _productRepository.UpdateStockAsync(item.ProductId, -item.Quantity);
                _logger.LogInformation($"Restored {item.Quantity} units of product ID {item.ProductId}");
            }

            // Update order status
            order.Status = OrderStatus.Canceled;
            await _orderRepository.UpdateAsync(order);
            _logger.LogInformation($"Order {orderId} has been cancelled");
        }

        public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status)
        {
            _logger.LogInformation($"Retrieving orders with status: {status}");
            return await _orderRepository.GetByStatusAsync(status);
        }

        public async Task FulfillOrderAsync(int orderId)
        {
            _logger.LogInformation($"Fulfilling order with ID: {orderId}");
            
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning($"Order with ID {orderId} not found");
                throw new OrderNotFoundException(orderId);
            }

            // Check if the order can be fulfilled
            if (order.Status != OrderStatus.PendingFulfillment)
            {
                _logger.LogWarning($"Cannot fulfill order {orderId} because it is not in PendingFulfillment status");
                throw new InvalidOperationException($"Cannot fulfill order with status {order.Status}");
            }

            // Update order status
            order.Status = OrderStatus.Fulfilled;
             order.LastUpdated = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);
            
            // Send notification
            await _notificationService.SendOrderFulfillmentNotificationAsync(order);
            
            _logger.LogInformation($"Order {orderId} has been fulfilled");
        }
        public async Task UpdateOrderAsync(Order order)
{
    if (order == null)
    {
        throw new ArgumentNullException(nameof(order));
    }
    
    await _orderRepository.UpdateAsync(order);
}
    }
}