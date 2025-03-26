using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Exceptions;
using ECOMMAPP.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECOMMAPP.Models;
using ECOMMAPP.Extensions;


namespace ECOMMAPP.Controllers
{
    public class OrdersController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService, 
            IProductService productService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _productService = productService;
            _logger = logger;
        }

        // GET: Orders
        public async Task<IActionResult> Index()
        {
            try
            {
                var orders = await _orderService.GetAllOrdersAsync();
                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders");
                TempData["ErrorMessage"] = "Error retrieving orders.";
                return View(new List<Order>());
            }
        }

        // GET: Orders/Create
       [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(Order order)
{
    try
    {
        _logger.LogInformation("Processing order submission with {ItemCount} items", order.Items?.Count ?? 0);
        
        // Validate order has items
        if (order.Items == null || !order.Items.Any())
        {
            ModelState.AddModelError("", "Order must contain at least one item");
            
            // Get products for dropdown again to redisplay form
            var products = await _productService.GetAllProductsAsync();
            ViewBag.Products = products.Select(p => new
            {
                Value = p.Id.ToString(),
                Text = p.Name,
                Price = p.Price
            }).ToList();
            
            return View(order);
        }
        
        // Filter out any invalid items
        order.Items = order.Items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
        
        if (!order.Items.Any())
        {
            ModelState.AddModelError("", "Order must contain at least one valid item");
            
            // Get products for dropdown again to redisplay form
            var products = await _productService.GetAllProductsAsync();
            ViewBag.Products = products.Select(p => new
            {
                Value = p.Id.ToString(),
                Text = p.Name,
                Price = p.Price
            }).ToList();
            
            return View(order);
        }
        
        if (ModelState.IsValid)
        {
            // Place the order through the service
            try
            {
                var createdOrder = await _orderService.PlaceOrderAsync(order);
                _logger.LogInformation("Order created successfully with ID: {OrderId}", createdOrder.Id);
                
                TempData["SuccessMessage"] = $"Order #{createdOrder.Id} created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (InsufficientStockException ex)
            {
                _logger.LogWarning(ex, "Insufficient stock for product {ProductId}", ex.ProductId);
                ModelState.AddModelError("", $"Insufficient stock for product ID {ex.ProductId}. Requested: {ex.RequestedQuantity}, Available: {ex.AvailableQuantity}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                ModelState.AddModelError("", $"Error creating order: {ex.Message}");
            }
        }
        else
        {
            // Log validation errors
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    _logger.LogWarning("Validation error for {Property}: {Error}", state.Key, error.ErrorMessage);
                }
            }
        }
        
        // If we get here, something failed, redisplay form
        var productList = await _productService.GetAllProductsAsync();
        ViewBag.Products = productList.Select(p => new
        {
            Value = p.Id.ToString(),
            Text = p.Name,
            Price = p.Price
        }).ToList();
        
        return View(order);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unhandled error in order creation");
        TempData["ErrorMessage"] = "An unexpected error occurred while creating your order.";
        return RedirectToAction(nameof(Index));
    }

}
public async Task<IActionResult> Create()
{
    try
    {
        _logger.LogInformation("Displaying order creation form");
        // Get products for dropdown
        var products = await _productService.GetAllProductsAsync();
        // Create SelectListItem objects with additional data for price
        var productItems = products.Select(p => new
        {
            Value = p.Id.ToString(),
            Text = $"{p.Name} ({p.StockQuantity} in stock)",
            Price = p.Price
        }).ToList();
        
        ViewBag.Products = productItems;
        
        // Initialize a new order with empty items collection
        var order = new Order
        {
            Items = new List<OrderItem>()
        };
        
        return View(order);
    }
    catch (Exception ex)
    {
         _logger.LogError(ex, "Error preparing order creation form");
        TempData["ErrorMessage"] = $"Error preparing order form: {ex.Message}";
        return RedirectToAction(nameof(Index));
    }
}

        // GET: Orders/Details/5
       public async Task<IActionResult> Details(int id)
{
    try
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        
        if (order == null)
        {
            return NotFound();
        }

        // Make sure product details are loaded
        if (order.Items != null)
        {
            foreach (var item in order.Items)
            {
                if (item.Product == null && item.ProductId > 0)
                {
                    item.Product = await _productService.GetProductByIdAsync(item.ProductId);
                }
            }
        }

        return View(order);
    }
    catch (OrderNotFoundException)
    {
        return NotFound();
    }
    catch (Exception ex)
    {
        TempData["ErrorMessage"] = $"Error retrieving order details: {ex.Message}";
        return RedirectToAction(nameof(Index));
    }
}
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Fulfill(int id)
{
    try
    {
        // For debugging
        Console.WriteLine($"Fulfill action called for order ID: {id}");
        
        // Get the order
        var order = await _orderService.GetOrderByIdAsync(id);
        
        // Update status directly
        order.Status = ECOMMAPP.Core.Enums.OrderStatus.Fulfilled;
        
        // Save changes
        await _orderService.UpdateOrderAsync(order);
        
        TempData["SuccessMessage"] = "Order has been fulfilled successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }
    catch (OrderNotFoundException)
    {
        return NotFound();
    }
    catch (InvalidOperationException ex)
    {
        TempData["ErrorMessage"] = ex.Message;
        return RedirectToAction(nameof(Details), new { id });
    }
    catch (Exception ex)
    {
        TempData["ErrorMessage"] = $"Error fulfilling order: {ex.Message}";
        return RedirectToAction(nameof(Details), new { id });
    }
}

        // POST: Orders/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _orderService.CancelOrderAsync(id);
                TempData["SuccessMessage"] = "Order cancelled successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling order ID {id}");
                TempData["ErrorMessage"] = $"Error cancelling order: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
[HttpPost]
public async Task<IActionResult> ValidateStock(int productId, int quantity)
{
    try
    {
        // Get product
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
        {
            return Json(new { success = false, message = "Product not found" });
        }

        // Check if requested quantity is available
        if (product.StockQuantity < quantity)
        {
            return Json(new { 
                success = false, 
                message = $"Insufficient stock. Only {product.StockQuantity} units available." 
            });
        }

        // Return success with product price
        return Json(new { 
            success = true, 
            unitPrice = product.Price
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error validating stock for product {ProductId}", productId);
        return Json(new { success = false, message = "Error validating stock: " + ex.Message });
    }
}
[HttpPost]
public async Task<IActionResult> ReserveStock(int productId, int quantity)
{
    try
    {
        // Get product
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
        {
            return Json(new { success = false, message = "Product not found" });
        }

        // Check if requested quantity is available
        if (product.StockQuantity < quantity)
        {
            return Json(new { 
                success = false, 
                message = $"Insufficient stock. Only {product.StockQuantity} units available." 
            });
        }

        // Reduce the stock quantity
        product.StockQuantity -= quantity;
        await _productService.UpdateProductAsync(product);

        // Generate a reservation ID
        string reservationId = Guid.NewGuid().ToString();

        // Store reservation in session
        var reservations = HttpContext.Session.GetObject<Dictionary<string, ReservationInfo>>("TempReservations") 
            ?? new Dictionary<string, ReservationInfo>();
            
        reservations[reservationId] = new ReservationInfo 
        { 
            ProductId = productId, 
            Quantity = quantity,
            Timestamp = DateTime.UtcNow
        };
        
        HttpContext.Session.SetObject("TempReservations", reservations);

        return Json(new { 
            success = true, 
            reservationId = reservationId,
            unitPrice = product.Price,
            newStockQuantity = product.StockQuantity
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error reserving stock for product {ProductId}", productId);
        return Json(new { success = false, message = "Error reserving stock: " + ex.Message });
    }
}


[HttpPost]
public async Task<IActionResult> ReleaseStock(int productId, int quantity, string reservationId)
{
    try
    {
        // Get product
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
        {
            return Json(new { success = false, message = "Product not found" });
        }

        // Increase the stock quantity
        product.StockQuantity += quantity;
        await _productService.UpdateProductAsync(product);

        // Remove the reservation from session
        var reservations = HttpContext.Session.GetObject<Dictionary<string, ReservationInfo>>("TempReservations");
        if (reservations != null && reservations.ContainsKey(reservationId))
        {
            reservations.Remove(reservationId);
            HttpContext.Session.SetObject("TempReservations", reservations);
        }

        return Json(new { 
            success = true,
            newStockQuantity = product.StockQuantity
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error releasing stock for product {ProductId}", productId);
        return Json(new { success = false, message = "Error releasing stock: " + ex.Message });
    }
}


    }
}