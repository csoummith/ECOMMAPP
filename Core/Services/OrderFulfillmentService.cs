using ECOMMAPP.Core.Enums;
using ECOMMAPP.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECOMMAPP.Core.Services
{
    public class OrderFulfillmentService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderFulfillmentService> _logger;
        private readonly Random _random = new Random();

        public OrderFulfillmentService(
            IServiceProvider serviceProvider,
            ILogger<OrderFulfillmentService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Order Fulfillment Service running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing pending orders.");
                }

                // Wait before next processing cycle (10-20 seconds)
                int delay = _random.Next(10000, 20000);
                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task ProcessPendingOrdersAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                    
                    // Get pending orders
                    var pendingOrders = await orderService.GetOrdersByStatusAsync(OrderStatus.PendingFulfillment);
                    
                    foreach (var order in pendingOrders)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;
                        
                        try
                        {
                            _logger.LogInformation($"Processing order {order.Id} for fulfillment");
                            
                            // Simulate processing time (2-5 seconds)
                            int processingTime = _random.Next(1000, 5000);
                            await Task.Delay(processingTime, stoppingToken);
                            
                            // Fulfill the order
                            await orderService.FulfillOrderAsync(order.Id);
                            
                            _logger.LogInformation($"Order {order.Id} fulfilled successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error fulfilling order {order.Id}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving pending orders");
                }
            }
        }
    }
}