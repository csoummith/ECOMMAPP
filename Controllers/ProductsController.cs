using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore; 

namespace ECOMMAPP.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IProductService productService, ILogger<ProductsController> logger)
        {
            _productService = productService;
            _logger = logger;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _productService.GetAllProductsAsync();
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                TempData["ErrorMessage"] = $"Error retrieving products: {ex.Message}";
                return View(Array.Empty<Product>());
            }
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    _logger.LogWarning($"Product with ID {id} not found");
                    return NotFound();
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving product details for ID {id}");
                TempData["ErrorMessage"] = $"Error retrieving product details: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Products/Create
        public IActionResult Create()
        {
            _logger.LogInformation("Displaying product creation form");
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            _logger.LogInformation($"Attempting to create product: {product?.Name}, Price: {product?.Price}, Stock: {product?.StockQuantity}");
            
            // Log all form data received
            foreach (var key in Request.Form.Keys)
            {
                _logger.LogInformation($"Form field: {key}, Value: {Request.Form[key]}");
            }
            
            try
            {
                if (ModelState.IsValid)
                {
                    _logger.LogInformation("Model state is valid, proceeding with product creation");
                    
                    if (product == null)
                    {
                        _logger.LogWarning("Product object is null");
                        ModelState.AddModelError("", "No product data was received");
                        return View();
                    }

                    var createdProduct = await _productService.CreateProductAsync(product);
                    
                    _logger.LogInformation($"Product created successfully with ID: {createdProduct.Id}");
                    TempData["SuccessMessage"] = "Product created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                
                // Log validation errors
                _logger.LogWarning("Model state is invalid");
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        _logger.LogWarning($"Validation error: {error.ErrorMessage}");
                    }
                }
                
                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                ModelState.AddModelError("", $"Error creating product: {ex.Message}");
                return View(product);
            }
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    _logger.LogWarning($"Product with ID {id} not found for editing");
                    return NotFound();
                }
                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving product for editing, ID: {id}");
                TempData["ErrorMessage"] = $"Error retrieving product for editing: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Products/Edit/5
        [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int id, Product product)
{
    if (id != product.Id)
    {
        _logger.LogWarning($"ID mismatch: URL ID {id} vs product ID {product.Id}");
        return NotFound();
    }

    if (ModelState.IsValid)
    {
        try
        {
            _logger.LogInformation("Model state is valid, proceeding with product update");
            
            await _productService.UpdateProductAsync(product);
            
            _logger.LogInformation($"Product updated successfully, ID: {product.Id}");
            TempData["SuccessMessage"] = "Product updated successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, $"Product with ID {id} not found during update");
            return NotFound();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Check if the product still exists
            var exists = await _productService.GetProductByIdAsync(id) != null;
            
            if (!exists)
            {
                _logger.LogError($"Product with ID {id} no longer exists");
                return NotFound();
            }
            else
            {
                // Product exists but was changed by another user
                _logger.LogError(ex, $"Concurrency error updating product ID: {id}");
                ModelState.AddModelError("", "This record has been modified by another user. Please reload and try again.");
                return View(product);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating product ID: {id}");
            ModelState.AddModelError("", $"Error updating product: {ex.Message}");
            return View(product);
        }
    }
    
    // If ModelState is invalid
    _logger.LogWarning("Model state is invalid for product update");
    foreach (var modelState in ModelState.Values)
    {
        foreach (var error in modelState.Errors)
        {
            _logger.LogWarning($"Validation error: {error.ErrorMessage}");
        }
    }
    
    return View(product);
}
        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    _logger.LogWarning($"Product with ID {id} not found for deletion");
                    return NotFound();
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving product for deletion, ID: {id}");
                TempData["ErrorMessage"] = $"Error retrieving product for deletion: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation($"Confirming deletion of product ID: {id}");
            
            try
            {
                await _productService.DeleteProductAsync(id);
                
                _logger.LogInformation($"Product ID: {id} deleted successfully");
                TempData["SuccessMessage"] = "Product deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, $"Product with ID {id} not found during deletion confirmation");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, $"Cannot delete product ID: {id} - referenced in orders");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error deleting product ID: {id}");
                TempData["ErrorMessage"] = $"Error deleting product: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}