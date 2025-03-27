using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using ECOMMAPP.Core.Entities;
using ECOMMAPP.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using ECOMMAPP.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Moq;

namespace ECOMAPP.Tests.Repositories
{
    public class ProductRepositoryTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly ProductRepository _productRepository;
        private readonly Mock<ILogger<ProductRepository>> _mockLogger;

        public ProductRepositoryTests()
        {
            // Create in-memory database for testing
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
                
            _dbContext = new AppDbContext(options);
            _mockLogger = new Mock<ILogger<ProductRepository>>();
            _productRepository = new ProductRepository(_dbContext, _mockLogger.Object);
            
            // Seed test data
            SeedTestData();
        }

        private void SeedTestData()
        {
            _dbContext.Products.AddRange(
                new Product { Id = 1, Name = "Test Product 1", Price = 10.0m, StockQuantity = 5, LastUpdated = DateTime.UtcNow },
                new Product { Id = 2, Name = "Test Product 2", Price = 15.0m, StockQuantity = 10, LastUpdated = DateTime.UtcNow },
                new Product { Id = 3, Name = "Test Product 3", Price = 20.0m, StockQuantity = 15, LastUpdated = DateTime.UtcNow }
            );
            
            _dbContext.SaveChanges();
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllProducts()
        {
            // Act
            var result = await _productRepository.GetAllAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().Contain(p => p.Name == "Test Product 1");
            result.Should().Contain(p => p.Name == "Test Product 2");
            result.Should().Contain(p => p.Name == "Test Product 3");
        }

        [Fact]
        public async Task GetByIdAsync_ExistingProduct_ReturnsProduct()
        {
            // Arrange
            var existingProduct = await _dbContext.Products.FirstAsync();

            // Act
            var result = await _productRepository.GetByIdAsync(existingProduct.Id);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(existingProduct.Id);
            result.Name.Should().Be(existingProduct.Name);
            result.Price.Should().Be(existingProduct.Price);
            result.StockQuantity.Should().Be(existingProduct.StockQuantity);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistingProduct_ReturnsNull()
        {
            // Arrange
            var nonExistingId = 999;

            // Act
            var result = await _productRepository.GetByIdAsync(nonExistingId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task AddAsync_ValidProduct_AddsToDatabase()
        {
            // Arrange
            var newProduct = new Product
            {
                Name = "New Test Product",
                Price = 25.0m,
                StockQuantity = 20,
                LastUpdated = DateTime.UtcNow
            };

            // Act
            var result = await _productRepository.AddAsync(newProduct);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.Name.Should().Be(newProduct.Name);
            
            // Verify it's in the database
            var dbProduct = await _dbContext.Products.FindAsync(result.Id);
            dbProduct.Should().NotBeNull();
            dbProduct.Name.Should().Be("New Test Product");
            dbProduct.Price.Should().Be(25.0m);
            dbProduct.StockQuantity.Should().Be(20);
        }

        [Fact]
        public async Task UpdateAsync_ExistingProduct_UpdatesInDatabase()
        {
            // Arrange
            var existingProduct = await _dbContext.Products.FirstAsync();
            existingProduct.Name = "Updated Name";
            existingProduct.Price = 30.0m;
            existingProduct.StockQuantity = 25;

            // Act
            await _productRepository.UpdateAsync(existingProduct);

            // Assert
            // Verify it's updated in the database
            var dbProduct = await _dbContext.Products.FindAsync(existingProduct.Id);
            dbProduct.Should().NotBeNull();
            dbProduct.Name.Should().Be("Updated Name");
            dbProduct.Price.Should().Be(30.0m);
            dbProduct.StockQuantity.Should().Be(25);
        }

        [Fact]
        public async Task DeleteAsync_ExistingProduct_RemovesFromDatabase()
        {
            // Arrange
            var existingProduct = await _dbContext.Products.FirstAsync();

            // Act
            await _productRepository.DeleteAsync(existingProduct.Id);

            // Assert
            // Verify it's removed from the database
            var dbProduct = await _dbContext.Products.FindAsync(existingProduct.Id);
            dbProduct.Should().BeNull();
        }

        [Fact]
        public async Task CheckStockAsync_SufficientStock_ReturnsTrue()
        {
            // Arrange
            var product = await _dbContext.Products.FirstAsync(p => p.StockQuantity >= 10);
            int requestedQuantity = 5;

            // Act
            var result = await _productRepository.CheckStockAsync(product.Id, requestedQuantity);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task CheckStockAsync_InsufficientStock_ReturnsFalse()
        {
            // Arrange
            var product = await _dbContext.Products.FirstAsync();
            int requestedQuantity = product.StockQuantity + 1; // One more than available

            // Act
            var result = await _productRepository.CheckStockAsync(product.Id, requestedQuantity);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateStockAsync_DecreaseStock_UpdatesDatabase()
        {
            // Arrange
            var product = await _dbContext.Products.FirstAsync();
            int originalQuantity = product.StockQuantity;
            int decreaseAmount = 2;

            // Act
            await _productRepository.UpdateStockAsync(product.Id, decreaseAmount);

            // Assert
            var updatedProduct = await _dbContext.Products.FindAsync(product.Id);
            updatedProduct.StockQuantity.Should().Be(originalQuantity - decreaseAmount);
        }

        [Fact]
        public async Task UpdateStockAsync_IncreaseStock_UpdatesDatabase()
        {
            // Arrange
            var product = await _dbContext.Products.FirstAsync();
            int originalQuantity = product.StockQuantity;
            int increaseAmount = -2; // Negative means increase

            // Act
            await _productRepository.UpdateStockAsync(product.Id, increaseAmount);

            // Assert
            var updatedProduct = await _dbContext.Products.FindAsync(product.Id);
            updatedProduct.StockQuantity.Should().Be(originalQuantity - increaseAmount); // - (-2) = +2
        }
    }
}