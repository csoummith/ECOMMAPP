using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ECOMMAPP.Infrastructure.Services
{
    public class EmailNotificationService : INotificationService
    {
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(ILogger<EmailNotificationService> logger)
        {
            _logger = logger;
        }

        public Task SendOrderFulfillmentNotificationAsync(Order order)
        {
            // In a real application, this would send an actual email
            // For this example, we just log the notification
            _logger.LogInformation($"[EMAIL NOTIFICATION] Order {order.Id} has been fulfilled and is ready for shipping.");
            
            // Log order details
            _logger.LogInformation($"Order Details: {order.Items.Count} items, Order Date: {order.OrderDate}");
            
            return Task.CompletedTask;
        }
    }
}
