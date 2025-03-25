using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Exceptions;
using ECOMMAPP.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        // Filter out any invalid items
        if (order.Items != null)
        {
            // Keep only items with valid ProductId and Quantity
            order.Items = order.Items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
        }
        
        if (order.Items == null || !order.Items.Any())
        {
            ModelState.AddModelError("", "Order must contain at least one valid product");
        }
        
        if (ModelState.IsValid)
        {
            var createdOrder = await _orderService.PlaceOrderAsync(order);
            TempData["SuccessMessage"] = $"Order #{createdOrder.Id} created successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
    catch (Exception ex)
    {
        ModelState.AddModelError("", $"Error creating order: {ex.Message}");
    }
    
    // If we get here, something failed, redisplay form
    var products = await _productService.GetAllProductsAsync();
    ViewBag.Products = new SelectList(products, "Id", "Name");
    
    // Ensure the order has at least one item for the form
    if (order.Items == null || !order.Items.Any())
    {
        order.Items = new List<OrderItem> { new OrderItem() };
    }
    
    return View(order);
}
public async Task<IActionResult> Create()
{
    try
    {
        // Get products for dropdown
        var products = await _productService.GetAllProductsAsync();
        ViewBag.Products = new SelectList(products, "Id", "Name");
        
        // Initialize a new order with one empty item
        var order = new Order
        {
            Items = new List<OrderItem> { new OrderItem() }
        };
        
        return View(order);
    }
    catch (Exception ex)
    {
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
                return View(order);
            }
            catch (OrderNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving order details for ID {id}");
                TempData["ErrorMessage"] = $"Error retrieving order details: {ex.Message}";
                return RedirectToAction(nameof(Index));
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
    }
}