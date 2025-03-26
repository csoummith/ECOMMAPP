using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Enums;
using ECOMMAPP.Core.Exceptions;
using ECOMMAPP.Core.Interfaces;
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

        public OrderService(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            INotificationService notificationService,
            ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _notificationService = notificationService;
            _logger = logger;
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

    // Step 1: Validate inventory for each item
    foreach (var item in order.Items)
    {
        _logger.LogInformation($"Validating inventory for product ID: {item.ProductId}, quantity: {item.Quantity}");
        
        // Retrieve the product to get current information
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

        // Set current price on the order item
        item.UnitPrice = product.Price;
        
        // Clear navigation properties to avoid validation issues
        item.Order = null;
        item.Product = null;
        
        _logger.LogInformation($"Set unit price to {item.UnitPrice} for product ID: {item.ProductId}");
    }

    // Step 2: Reserve inventory (reduce stock) for each item
    foreach (var item in order.Items)
    {
        _logger.LogInformation($"Updating stock for product ID: {item.ProductId}, quantity: {item.Quantity}");
        await _productRepository.UpdateStockAsync(item.ProductId, item.Quantity);
        _logger.LogInformation($"Reserved {item.Quantity} units of product ID {item.ProductId}");
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