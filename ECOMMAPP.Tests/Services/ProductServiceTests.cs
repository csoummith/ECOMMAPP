using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ECOMMAPP.Core.Entities;
using ECOMMAPP.Core.Interfaces;
using ECOMMAPP.Core.Services;

namespace ECOMMAPP.Tests.Services
{
    public class ProductServiceTests
    {
        private readonly Mock<IProductRepository> _mockProductRepo;
        private readonly Mock<ILogger<ProductService>> _mockLogger;
        private readonly ProductService _productService;

        public ProductServiceTests()
        {
            // Setup
            _mockProductRepo = new Mock<IProductRepository>();
            _mockLogger = new Mock<ILogger<ProductService>>();
           _productService = new ProductService(_mockProductRepo.Object);
        }

        [Fact]
        public async Task GetProductById_ExistingProduct_ReturnsProduct()
        {
            // Arrange
            int productId = 1;
            var product = new Product 
            { 
                Id = productId, 
                Name = "Test Product", 
                Price = 9.99m, 
                StockQuantity = 10 
            };
            
            _mockProductRepo.Setup(repo => repo.GetByIdAsync(productId))
                .ReturnsAsync(product);

            // Act
            var result = await _productService.GetProductByIdAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(productId, result.Id);
            Assert.Equal("Test Product", result.Name);
            Assert.Equal(9.99m, result.Price);
            Assert.Equal(10, result.StockQuantity);
            
            _mockProductRepo.Verify(repo => repo.GetByIdAsync(productId), Times.Once);
        }

        [Fact]
        public async Task GetProductById_NonExistingProduct_ReturnsNull()
        {
            // Arrange
            int nonExistingId = 999;
            
            _mockProductRepo.Setup(repo => repo.GetByIdAsync(nonExistingId))
                .ReturnsAsync((Product)null);

            // Act
            var result = await _productService.GetProductByIdAsync(nonExistingId);

            // Assert
            Assert.Null(result);
            _mockProductRepo.Verify(repo => repo.GetByIdAsync(nonExistingId), Times.Once);
        }

        [Fact]
        public async Task GetAllProducts_ReturnsAllProducts()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Product 1", Price = 9.99m, StockQuantity = 10 },
                new Product { Id = 2, Name = "Product 2", Price = 19.99m, StockQuantity = 5 }
            };
            
            _mockProductRepo.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(products);

            // Act
            var result = await _productService.GetAllProductsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Contains(result, p => p.Name == "Product 1");
            Assert.Contains(result, p => p.Name == "Product 2");
            
            _mockProductRepo.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateProduct_ValidProduct_ReturnsCreatedProduct()
        {
            // Arrange
            var newProduct = new Product
            {
                Name = "New Product",
                Price = 14.99m,
                StockQuantity = 15
            };

            _mockProductRepo.Setup(repo => repo.AddAsync(It.IsAny<Product>()))
                .ReturnsAsync((Product product) => 
                {
                    product.Id = 1;
                    return product;
                });

            // Act
            var result = await _productService.CreateProductAsync(newProduct);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("New Product", result.Name);
            Assert.Equal(14.99m, result.Price);
            Assert.Equal(15, result.StockQuantity);
            
            _mockProductRepo.Verify(repo => repo.AddAsync(It.IsAny<Product>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProduct_ExistingProduct_UpdatesProduct()
        {
            // Arrange
            int productId = 1;
            var product = new Product
            {
                Id = productId,
                Name = "Updated Product",
                Price = 19.99m,
                StockQuantity = 20
            };
            
            _mockProductRepo.Setup(repo => repo.GetByIdAsync(productId))
                .ReturnsAsync(new Product { Id = productId });
                
            _mockProductRepo.Setup(repo => repo.UpdateAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            // Act
            await _productService.UpdateProductAsync(product);

            // Assert
            _mockProductRepo.Verify(repo => repo.UpdateAsync(It.IsAny<Product>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProduct_NonExistingProduct_ThrowsKeyNotFoundException()
        {
            // Arrange
            int nonExistingId = 999;
            var product = new Product
            {
                Id = nonExistingId,
                Name = "Non-existing Product",
                Price = 9.99m,
                StockQuantity = 10
            };
            
            _mockProductRepo.Setup(repo => repo.GetByIdAsync(nonExistingId))
                .ReturnsAsync((Product)null);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _productService.UpdateProductAsync(product));
                
            _mockProductRepo.Verify(repo => repo.GetByIdAsync(nonExistingId), Times.Once);
            _mockProductRepo.Verify(repo => repo.UpdateAsync(It.IsAny<Product>()), Times.Never);
        }

        [Fact]
        public async Task DeleteProduct_ExistingProduct_DeletesProduct()
        {
            // Arrange
            int productId = 1;
            
            _mockProductRepo.Setup(repo => repo.GetByIdAsync(productId))
                .ReturnsAsync(new Product { Id = productId });
                
            _mockProductRepo.Setup(repo => repo.DeleteAsync(productId))
                .Returns(Task.CompletedTask);

            // Act
            await _productService.DeleteProductAsync(productId);

            // Assert
            _mockProductRepo.Verify(repo => repo.DeleteAsync(productId), Times.Once);
        }

        [Fact]
        public async Task DeleteProduct_NonExistingProduct_ThrowsKeyNotFoundException()
        {
            // Arrange
            int nonExistingId = 999;
            
            _mockProductRepo.Setup(repo => repo.GetByIdAsync(nonExistingId))
                .ReturnsAsync((Product)null);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => 
                _productService.DeleteProductAsync(nonExistingId));
                
            _mockProductRepo.Verify(repo => repo.GetByIdAsync(nonExistingId), Times.Once);
            _mockProductRepo.Verify(repo => repo.DeleteAsync(nonExistingId), Times.Never);
        }

        [Fact]
        public async Task CheckStockAvailability_SufficientStock_ReturnsTrue()
        {
            // Arrange
            int productId = 1;
            int quantity = 5;
            
            _mockProductRepo.Setup(repo => repo.CheckStockAsync(productId, quantity))
                .ReturnsAsync(true);

            // Act
            var result = await _productService.CheckStockAvailabilityAsync(productId, quantity);

            // Assert
            Assert.True(result);
            _mockProductRepo.Verify(repo => repo.CheckStockAsync(productId, quantity), Times.Once);
        }

        [Fact]
        public async Task CheckStockAvailability_InsufficientStock_ReturnsFalse()
        {
            // Arrange
            int productId = 1;
            int quantity = 100;
            
            _mockProductRepo.Setup(repo => repo.CheckStockAsync(productId, quantity))
                .ReturnsAsync(false);

            // Act
            var result = await _productService.CheckStockAvailabilityAsync(productId, quantity);

            // Assert
            Assert.False(result);
            _mockProductRepo.Verify(repo => repo.CheckStockAsync(productId, quantity), Times.Once);
        }
    }
}