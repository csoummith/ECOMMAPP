using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Interfaces;
using ECOMMAPP.Core.Services;
using ECOMMAPP.Core.Enums;
using ECOMMAPP.Core.Exceptions;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace ECOMMAPP.Tests.Services
{
    public class OrderServiceTests
    {
        private readonly Mock<IOrderRepository> _mockOrderRepo;
        private readonly Mock<IProductRepository> _mockProductRepo;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<OrderService>> _mockLogger;
        private readonly OrderService _orderService;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor; 

        public OrderServiceTests()
        {
            // Setup mocks
            _mockOrderRepo = new Mock<IOrderRepository>();
            _mockProductRepo = new Mock<IProductRepository>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<OrderService>>();
             _mockHttpContextAccessor = new Mock<IHttpContextAccessor>(); 
            
            // Create service with mocked dependencies
            _orderService = new OrderService(
                _mockOrderRepo.Object,
                _mockProductRepo.Object,
                _mockNotificationService.Object,
                _mockLogger.Object,_mockHttpContextAccessor.Object);
        }

        [Fact]
        public async Task PlaceOrder_ValidOrder_CreatesOrderAndReducesInventory()
        {
            // Arrange
            var order = new Order
            {
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2 },
                    new OrderItem { ProductId = 2, Quantity = 1 }
                }
            };

            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Product 1", Price = 10.0m, StockQuantity = 5 },
                new Product { Id = 2, Name = "Product 2", Price = 20.0m, StockQuantity = 3 }
            };

            // Setup product repository to return our test products
            foreach (var product in products)
            {
                _mockProductRepo.Setup(repo => repo.GetByIdAsync(product.Id))
                    .ReturnsAsync(product);
            }

            // Setup order repository to save and return an order
            _mockOrderRepo.Setup(repo => repo.AddAsync(It.IsAny<Order>()))
                .ReturnsAsync((Order o) => 
                {
                    o.Id = 1;
                    o.Status = OrderStatus.PendingFulfillment;
                    return o;
                });

            // Act
            var result = await _orderService.PlaceOrderAsync(order);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(1);
            result.Status.Should().Be(OrderStatus.PendingFulfillment);
            result.Items.Should().HaveCount(2);
            
            // Verify product repository was called to get products
            foreach (var product in products)
            {
                _mockProductRepo.Verify(repo => repo.GetByIdAsync(product.Id), Times.Once);
            }
            
            // Verify stock was updated
            _mockProductRepo.Verify(repo => repo.UpdateStockAsync(1, 2), Times.Once);
            _mockProductRepo.Verify(repo => repo.UpdateStockAsync(2, 1), Times.Once);
            
            // Verify order was created
            _mockOrderRepo.Verify(repo => repo.AddAsync(It.IsAny<Order>()), Times.Once);
        }

        [Fact]
        public async Task PlaceOrder_InsufficientInventory_ThrowsException()
        {
            // Arrange
            var order = new Order
            {
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 10 } // More than available
                }
            };

            var product = new Product 
            { 
                Id = 1, 
                Name = "Product 1", 
                Price = 10.0m, 
                StockQuantity = 5 // Less than requested
            };

            _mockProductRepo.Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync(product);

            // Act & Assert
            await Assert.ThrowsAsync<InsufficientStockException>(() => 
                _orderService.PlaceOrderAsync(order));
            
            // Verify product repository was called
            _mockProductRepo.Verify(repo => repo.GetByIdAsync(1), Times.Once);
            
            // Verify no updates were performed
            _mockProductRepo.Verify(repo => repo.UpdateStockAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _mockOrderRepo.Verify(repo => repo.AddAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrder_ProductNotFound_ThrowsException()
        {
            // Arrange
            var order = new Order
            {
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 999, Quantity = 1 }
                }
            };

            _mockProductRepo.Setup(repo => repo.GetByIdAsync(999))
                .ReturnsAsync((Product)null);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _orderService.PlaceOrderAsync(order));
            
            // Verify product repository was called
            _mockProductRepo.Verify(repo => repo.GetByIdAsync(999), Times.Once);
            
            // Verify no updates were performed
            _mockProductRepo.Verify(repo => repo.UpdateStockAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _mockOrderRepo.Verify(repo => repo.AddAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task CancelOrder_PendingOrder_CancelsOrderAndRestoresInventory()
        {
            // Arrange
            var orderId = 1;
            var order = new Order
            {
                Id = orderId,
                Status = OrderStatus.PendingFulfillment,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2 },
                    new OrderItem { ProductId = 2, Quantity = 1 }
                }
            };

            // Setup mocks
            _mockOrderRepo.Setup(repo => repo.GetByIdAsync(orderId))
                .ReturnsAsync(order);
            
           _mockOrderRepo.Setup(repo => repo.UpdateAsync(It.IsAny<Order>()))
    .Returns(Task.CompletedTask);

            // Act
            await _orderService.CancelOrderAsync(orderId);

            // Assert
            // Verify order was retrieved
            _mockOrderRepo.Verify(repo => repo.GetByIdAsync(orderId), Times.Once);
            
            // Verify inventory was restored (using negative quantity)
            _mockProductRepo.Verify(repo => repo.UpdateStockAsync(1, -2), Times.Once);
            _mockProductRepo.Verify(repo => repo.UpdateStockAsync(2, -1), Times.Once);
            
            // Verify order was updated
            _mockOrderRepo.Verify(repo => repo.UpdateAsync(It.Is<Order>(o => 
                o.Id == orderId && o.Status == OrderStatus.Canceled)), Times.Once);
        }

        [Fact]
        public async Task CancelOrder_AlreadyFulfilled_ThrowsException()
        {
            // Arrange
            var orderId = 1;
            var order = new Order
            {
                Id = orderId,
                Status = OrderStatus.Fulfilled,
                Items = new List<OrderItem>()
            };

            _mockOrderRepo.Setup(repo => repo.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _orderService.CancelOrderAsync(orderId));
            
            // Verify order was retrieved
            _mockOrderRepo.Verify(repo => repo.GetByIdAsync(orderId), Times.Once);
            
            // Verify no updates were performed
            _mockProductRepo.Verify(repo => repo.UpdateStockAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _mockOrderRepo.Verify(repo => repo.UpdateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task FulfillOrder_PendingOrder_MarksOrderAsFulfilledAndSendsNotification()
        {
            // Arrange
            var orderId = 1;
            var order = new Order
            {
                Id = orderId,
                Status = OrderStatus.PendingFulfillment,
                Items = new List<OrderItem>()
            };

            _mockOrderRepo.Setup(repo => repo.GetByIdAsync(orderId))
                .ReturnsAsync(order);
                
         _mockOrderRepo.Setup(repo => repo.UpdateAsync(It.IsAny<Order>()))
    .Returns(Task.CompletedTask);
                
            _mockNotificationService.Setup(svc => svc.SendOrderFulfillmentNotificationAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);

            // Act
            await _orderService.FulfillOrderAsync(orderId);

            // Assert
            // Verify order was retrieved
            _mockOrderRepo.Verify(repo => repo.GetByIdAsync(orderId), Times.Once);
            
            // Verify order was updated
            _mockOrderRepo.Verify(repo => repo.UpdateAsync(It.Is<Order>(o => 
                o.Id == orderId && o.Status == OrderStatus.Fulfilled)), Times.Once);
                
            // Verify notification was sent
            _mockNotificationService.Verify(svc => 
                svc.SendOrderFulfillmentNotificationAsync(It.IsAny<Order>()), Times.Once);
        }

        [Fact]
        public async Task FulfillOrder_NotPendingFulfillment_ThrowsException()
        {
            // Arrange
            var orderId = 1;
            var order = new Order
            {
                Id = orderId,
                Status = OrderStatus.Fulfilled,
                Items = new List<OrderItem>()
            };

            _mockOrderRepo.Setup(repo => repo.GetByIdAsync(orderId))
                .ReturnsAsync(order);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _orderService.FulfillOrderAsync(orderId));
            
            // Verify order was retrieved
            _mockOrderRepo.Verify(repo => repo.GetByIdAsync(orderId), Times.Once);
            
            // Verify no updates were performed
            _mockOrderRepo.Verify(repo => repo.UpdateAsync(It.IsAny<Order>()), Times.Never);
            _mockNotificationService.Verify(svc => 
                svc.SendOrderFulfillmentNotificationAsync(It.IsAny<Order>()), Times.Never);
        }
    }
}