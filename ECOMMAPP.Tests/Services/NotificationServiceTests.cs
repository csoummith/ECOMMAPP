using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Interfaces;
using ECOMMAPP.Core.Enums;
using ECOMMAPP.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ECOMMAPP.Tests.Services
{
    public class NotificationServiceTests
    {
        private readonly Mock<ILogger<EmailNotificationService>> _mockLogger;
        private readonly EmailNotificationService _notificationService;

        public NotificationServiceTests()
        {
            _mockLogger = new Mock<ILogger<EmailNotificationService>>();
            _notificationService = new EmailNotificationService(_mockLogger.Object);
        }

        [Fact]
        public async Task SendOrderFulfillmentNotification_ValidOrder_LogsNotification()
        {
            // Arrange
            var order = new Order
            {
                Id = 123,
                Status = OrderStatus.Fulfilled,
                OrderDate = DateTime.UtcNow,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 2, UnitPrice = 10.0m },
                    new OrderItem { ProductId = 2, Quantity = 1, UnitPrice = 20.0m }
                }
            };

            // Act
            await _notificationService.SendOrderFulfillmentNotificationAsync(order);

            // Assert
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains($"Order {order.Id} has been fulfilled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendOrderFulfillmentNotification_NullOrder_ThrowsArgumentNullException()
        {
            // Arrange
            Order order = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _notificationService.SendOrderFulfillmentNotificationAsync(order));
            
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task SendOrderFulfillmentNotification_SimulateFailure_LogsErrorAndRethrows()
        {
            // Arrange
            var order = new Order
            {
                Id = 124,
                Status = OrderStatus.Fulfilled,
                OrderDate = DateTime.UtcNow
            };

            // Setup logger to simulate error
            _mockLogger
                .Setup(l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains($"Order {order.Id} has been fulfilled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Throws(new IOException("Simulated notification failure"));
                
            // Act & Assert
            await Assert.ThrowsAsync<IOException>(() => 
                _notificationService.SendOrderFulfillmentNotificationAsync(order));
            
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains($"Order {order.Id} has been fulfilled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendOrderFulfillmentNotification_WithRetry_EventuallySucceeds()
        {
            // Arrange
            var order = new Order
            {
                Id = 125,
                Status = OrderStatus.Fulfilled,
                OrderDate = DateTime.UtcNow
            };

            // Setup a counter to fail the first time, succeed after that
            var attemptCount = 0;
            
            _mockLogger
                .Setup(l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains($"Order {order.Id} has been fulfilled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Callback(() => 
                {
                    if (attemptCount++ == 0)
                    {
                        throw new IOException("Temporary failure");
                    }
                });

            // Create notification service with retry
            var notificationServiceWithRetry = new NotificationServiceWithRetry(_mockLogger.Object);

            // Act
            await notificationServiceWithRetry.SendOrderFulfillmentNotificationAsync(order);

            // Assert
            attemptCount.Should().BeGreaterThan(1);
        }
    }

    // Example implementation of a notification service with retry logic
    public class NotificationServiceWithRetry : INotificationService
    {
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly int _maxRetries = 3;

        public NotificationServiceWithRetry(ILogger<EmailNotificationService> logger)
        {
            _logger = logger;
        }

        public async Task SendOrderFulfillmentNotificationAsync(Order order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            Exception lastException = null;

            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    // If not first attempt, add delay
                    if (attempt > 0)
                    {
                        await Task.Delay(100 * attempt); // Exponential backoff
                    }

                    // Simulate sending notification
                    _logger.LogInformation($"[EMAIL NOTIFICATION] Order {order.Id} has been fulfilled and is ready for shipping.");
                    return; // Success
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning($"Attempt {attempt + 1} failed to send notification for order {order.Id}: {ex.Message}");
                }
            }

            // If we get here, all attempts failed
            _logger.LogError(lastException, $"Failed to send notification for order {order.Id} after {_maxRetries} attempts");
            throw lastException;
        }
    }
}