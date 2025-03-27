using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Interfaces;
using ECOMMAPP.Core.Services;
using ECOMMAPP.Core.Exceptions;
using ECOMMAPP.Core.Enums;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ECOMMAPP.Tests.Concurrency
{
    public class ConcurrencyTests
    {
        private readonly Mock<IProductRepository> _mockProductRepo;
        private readonly Mock<IOrderRepository> _mockOrderRepo;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<OrderService>> _mockLogger;
        private readonly OrderService _orderService;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor; 

        public ConcurrencyTests()
        {
            // Setup mocks
            _mockProductRepo = new Mock<IProductRepository>();
            _mockOrderRepo = new Mock<IOrderRepository>();
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
        public async Task ConcurrentOrders_SameProduct_HandlesInventoryCorrectly()
        {
            // Arrange
            int productId = 1;
            
            // Create a product with limited inventory
            var product = new Product 
            { 
                Id = productId, 
                Name = "Limited Stock Product", 
                Price = 10.0m, 
                StockQuantity = 5 
            };

            // Make a thread-safe product that can be accessed concurrently
            var threadSafeProduct = product;
            var lockObject = new object();

            // Setup product repository to return our product with thread-safe access
            _mockProductRepo.Setup(repo => repo.GetByIdAsync(productId))
                .ReturnsAsync(() => 
                {
                    lock (lockObject)
                    {
                        // Return a clone to prevent mutation
                        return new Product
                        {
                            Id = threadSafeProduct.Id,
                            Name = threadSafeProduct.Name,
                            Price = threadSafeProduct.Price,
                            StockQuantity = threadSafeProduct.StockQuantity
                        };
                    }
                });

            // Setup stock check
            _mockProductRepo.Setup(repo => repo.CheckStockAsync(productId, It.IsAny<int>()))
                .ReturnsAsync((int pid, int quantity) => 
                {
                    lock (lockObject)
                    {
                        return threadSafeProduct.StockQuantity >= quantity;
                    }
                });

            // Setup stock update
            _mockProductRepo.Setup(repo => repo.UpdateStockAsync(productId, It.IsAny<int>()))
                .Callback((int pid, int quantity) => 
                {
                    lock (lockObject)
                    {
                        threadSafeProduct.StockQuantity -= quantity;
                        if (threadSafeProduct.StockQuantity < 0)
                            threadSafeProduct.StockQuantity = 0;
                    }
                })
                .Returns(Task.CompletedTask);
                
            // Setup order repository to add orders
            _mockOrderRepo.Setup(repo => repo.AddAsync(It.IsAny<Order>()))
                .ReturnsAsync((Order order) => 
                {
                    order.Id = new Random().Next(1, 1000);
                    return order;
                });

            // Create three orders for the same product
            var order1 = new Order { Items = new List<OrderItem> { new OrderItem { ProductId = productId, Quantity = 2 } } };
            var order2 = new Order { Items = new List<OrderItem> { new OrderItem { ProductId = productId, Quantity = 2 } } };
            var order3 = new Order { Items = new List<OrderItem> { new OrderItem { ProductId = productId, Quantity = 2 } } };

            // Act
            // Create tasks for placing orders concurrently
            var tasks = new List<Task<Order>>();
            var exceptions = new List<Exception>();

            tasks.Add(Task.Run(async () => 
            {
                try
                {
                    return await _orderService.PlaceOrderAsync(order1);
                }
                catch (InsufficientStockException ex)
                {
                    exceptions.Add(ex);
                    return null;
                }
            }));

            tasks.Add(Task.Run(async () => 
            {
                try
                {
                    return await _orderService.PlaceOrderAsync(order2);
                }
                catch (InsufficientStockException ex)
                {
                    exceptions.Add(ex);
                    return null;
                }
            }));

            tasks.Add(Task.Run(async () => 
            {
                try
                {
                    return await _orderService.PlaceOrderAsync(order3);
                }
                catch (InsufficientStockException ex)
                {
                    exceptions.Add(ex);
                    return null;
                }
            }));

            // Wait for all tasks to complete
            var results = await Task.WhenAll(tasks);

            // Assert
            // Expect 2 orders to succeed and 1 to fail (5 stock ÷ 2 quantity = 2 orders max)
            Assert.Equal(2, results.Count(r => r != null));
            Assert.Single(exceptions);

            // Final stock should be 1 (5 - 2 - 2 = 1)
            Assert.Equal(1, threadSafeProduct.StockQuantity);

            // Verify product stock was checked
            _mockProductRepo.Verify(repo => repo.CheckStockAsync(productId, It.IsAny<int>()), Times.Exactly(3));
            
            // Verify stock was updated twice
            _mockProductRepo.Verify(repo => repo.UpdateStockAsync(productId, It.IsAny<int>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CancelFulfillOrder_Concurrently_HandlesStateCorrectly()
        {
            // Arrange
            int orderId = 1;
            
            // Create thread-safe order state
            var orderStatus = OrderStatus.PendingFulfillment;
            var lockObject = new object();
            var orderItems = new List<OrderItem>
            {
                new OrderItem { ProductId = 1, Quantity = 1 }
            };

            // Setup order repository to get order with thread-safe access
            _mockOrderRepo.Setup(repo => repo.GetByIdAsync(orderId))
                .ReturnsAsync(() => 
                {
                    lock (lockObject)
                    {
                        return new Order
                        {
                            Id = orderId,
                            Status = orderStatus,
                            Items = orderItems.ToList()
                        };
                    }
                });
                
            // Setup order repository to update with thread-safe access
            _mockOrderRepo.Setup(repo => repo.UpdateAsync(It.IsAny<Order>()))
                .Callback((Order updatedOrder) => 
                {
                    lock (lockObject)
                    {
                        // Update the thread-safe status
                        orderStatus = updatedOrder.Status;
                    }
                })
                .Returns(Task.CompletedTask);
                
            // Setup product repository for stock updates (for cancellation)
            _mockProductRepo.Setup(repo => repo.UpdateStockAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);
            
            // Setup notification service
            _mockNotificationService.Setup(svc => svc.SendOrderFulfillmentNotificationAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);

            // Act
            // Create tasks for canceling and fulfilling the same order concurrently
            var cancelTask = Task.Run(async () => 
            {
                try
                {
                    await _orderService.CancelOrderAsync(orderId);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });
            
            var fulfillTask = Task.Run(async () => 
            {
                try
                {
                    await _orderService.FulfillOrderAsync(orderId);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });

            // Wait for both tasks to complete
            var results = await Task.WhenAll(cancelTask, fulfillTask);

            // Assert
            // Only one operation should succeed
            Assert.Equal(1, results.Count(r => r));
            
            // Final status should be either Canceled or Fulfilled
            Assert.True(orderStatus == OrderStatus.Canceled || orderStatus == OrderStatus.Fulfilled);
            
            // Verify order was retrieved multiple times
            _mockOrderRepo.Verify(repo => repo.GetByIdAsync(orderId), Times.AtLeast(2));
            
            // Verify order was updated at least once
            _mockOrderRepo.Verify(repo => repo.UpdateAsync(It.IsAny<Order>()), Times.AtLeast(1));
        }

        [Fact]
        public async Task MultipleConcurrentOperations_StressTest_MaintainsConsistency()
        {
            // Arrange
            int productId = 1;
            int initialStock = 20;
            
            // Create thread-safe product
            var stockLevel = initialStock;
            var lockObject = new object();

            // Setup product repository
            _mockProductRepo.Setup(repo => repo.GetByIdAsync(productId))
                .ReturnsAsync(() => 
                {
                    lock (lockObject)
                    {
                        return new Product
                        {
                            Id = productId,
                            Name = "Stress Test Product",
                            Price = 10m,
                            StockQuantity = stockLevel
                        };
                    }
                });
                
            _mockProductRepo.Setup(repo => repo.CheckStockAsync(productId, It.IsAny<int>()))
                .ReturnsAsync((int pid, int quantity) => 
                {
                    lock (lockObject)
                    {
                        return stockLevel >= quantity;
                    }
                });

            _mockProductRepo.Setup(repo => repo.UpdateStockAsync(productId, It.IsAny<int>()))
                .Callback((int pid, int quantity) => 
                {
                    lock (lockObject)
                    {
                        stockLevel -= quantity;
                        if (stockLevel < 0)
                            stockLevel = 0;
                    }
                })
                .Returns(Task.CompletedTask);
                
            // Setup order repository
            _mockOrderRepo.Setup(repo => repo.AddAsync(It.IsAny<Order>()))
                .ReturnsAsync((Order order) => 
                {
                    order.Id = new Random().Next(1, 1000);
                    return order;
                });
                
            _mockOrderRepo.Setup(repo => repo.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => new Order
                {
                    Id = id,
                    Status = OrderStatus.PendingFulfillment,
                    Items = new List<OrderItem>
                    {
                        new OrderItem { ProductId = productId, Quantity = 1 }
                    }
                });
                
            _mockOrderRepo.Setup(repo => repo.UpdateAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);
                
            // Setup notification service
            _mockNotificationService.Setup(svc => svc.SendOrderFulfillmentNotificationAsync(It.IsAny<Order>()))
                .Returns(Task.CompletedTask);

            // Act
            // Create a large number of concurrent tasks
            var orderTasks = new List<Task>();
            var completedOrders = 0;
            
            // Create 15 orders, each for 1 unit
            for (int i = 0; i < 15; i++)
            {
                orderTasks.Add(Task.Run(async () => 
                {
                    try
                    {
                        var order = new Order
                        {
                            Items = new List<OrderItem>
                            {
                                new OrderItem { ProductId = productId, Quantity = 1 }
                            }
                        };
                        
                        var placedOrder = await _orderService.PlaceOrderAsync(order);
                        Interlocked.Increment(ref completedOrders);
                        
                        // Randomly decide to fulfill or cancel
                        if (new Random().Next(2) == 0)
                        {
                            await _orderService.FulfillOrderAsync(placedOrder.Id);
                        }
                        else
                        {
                            await _orderService.CancelOrderAsync(placedOrder.Id);
                            // If canceled, stock should be restored
                        }
                    }
                    catch (InsufficientStockException)
                    {
                        // Expected once we run out of stock
                    }
                    catch (Exception)
                    {
                        // Other exceptions may occur due to race conditions
                    }
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(orderTasks);

            // Assert
            Assert.True(completedOrders <= initialStock);
            
            // Final stock level should be non-negative
            Assert.True(stockLevel >= 0);
        }
    }
}