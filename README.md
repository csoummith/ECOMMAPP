**E-commerce System**
**Project Overview**
This e-commerce system is a web application built with ASP.NET Core that follows clean architecture principles. It enables users to manage products, place orders, and process them asynchronously. The system handles inventory management with concurrency control to ensure accurate stock tracking even during simultaneous order processing.
**Key Features**

**Product Management:** Full CRUD operations for products
**Order Processing:**Place orders with automatic inventory verification
**Inventory Control:** Real-time stock management with concurrency handling
**Asynchronous Fulfillment:** Background service for order processing
**Dual Interface:** Both MVC views for human interaction and API endpoints for programmatic access

**Database Structure**
The application uses a MySQL database with the following structure:
**Database Schema**
-- Create database
CREATE DATABASE ECOM CHARACTER SET utf8mb4;
USE ECOM;

-- Products table
CREATE TABLE `Products` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) NOT NULL,
    `Price` decimal(18, 2) NOT NULL,
    `StockQuantity` int NOT NULL,
    `LastUpdated` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
);

-- Orders table
CREATE TABLE `Orders` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OrderDate` datetime(6) NOT NULL,
    `Status` int NOT NULL,
    `LastUpdated` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
);

-- OrderItems table
CREATE TABLE `OrderItems` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OrderId` int NOT NULL,
    `ProductId` int NOT NULL,
    `Quantity` int NOT NULL,
    `UnitPrice` decimal(18, 2) NOT NULL,
    'ReservationId' VARCHAR(50)
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrderItems_Orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_OrderItems_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
);

-- Create indexes for performance
CREATE INDEX `IX_OrderItems_OrderId` ON `OrderItems`(`OrderId`);
CREATE INDEX `IX_OrderItems_ProductId` ON `OrderItems`(`ProductId`);
CREATE INDEX `IX_Orders_Status` ON `Orders`(`Status`);

**Getting Started**
Prerequisites

.NET 6.0 SDK or later
MySQL Server
Visual Studio Code (or your preferred IDE)

**Setup Instructions**

**Clone the repositor**
git clone https://github.com/csoummith/ECOMMAPP.git
cd ECOMMAPP

**Configure the database connection**
Update the connection string in appsettings.json:
json"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=ECOM;User=yourusername;Password=yourpassword;"
}

**Build and run the application**
dotnet build
dotnet run


**Setting Up Debugging**

Create launch.json
Create a .vscode/launch.json file with the following content:
{
    
    "version": "0.2.0",
    "configurations": [

     {
      "name": "ECOMMAPP (HTTP only)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/bin/Debug/net7.0/ECOMMAPP.dll",
      "args": [],
      "cwd": "${workspaceFolder}",
      "stopAtEntry": false,
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "http://localhost:5000"
      }
    }

    ]
  }


Create tasks.json
Create a .vscode/tasks.json file with the following content:
{
    "version": "2.0.0",
    "tasks": [
      {
        "label": "build",
        "command": "dotnet",
        "type": "process",
        "args": [
          "build",
          "${workspaceFolder}/ECOMMAPP.csproj",
          "/property:GenerateFullPaths=true",
          "/consoleloggerparameters:NoSummary"
        ],
        "problemMatcher": "$msCompile"
      }
    ]
  }

The project follows a clean architecture pattern with the following structure:

Controllers/: MVC controllers for handling web requests
API/: API controllers for RESTful endpoints
Core/: Contains the business domain

Entities/: Domain models (Product, Order, OrderItem)
Interfaces/: Abstractions for repositories and services
Services/: Business logic implementation
Enums/: Enumeration types (e.g., OrderStatus)
Exceptions/: Custom exception types


Infrastructure/: Implementation details

Data/: Database context and configuration
Repositories/: Data access implementation
Services/: External service implementations


Views/: Razor views for the user interface
wwwroot/: Static assets (CSS, JavaScript, images)

To run the application from the termina:
dotnet build 
dotnet run 

To test the Application 
dotnet build 
dotnet test


**Concurrency and Asynchronous Processing**
This application implements robust concurrency control and asynchronous processing to handle multiple users and background operations.
Concurrency Control

Optimistic Concurrency: Uses LastUpdated DateTime fields with the [ConcurrencyCheck] attribute to detect and prevent conflicts when multiple users update the same records
Database Transactions: Ensures atomic operations during inventory updates to maintain data integrity
Thread-Safe Operations: The UpdateStockAsync method in ProductRepository uses database transactions to safely modify inventory quantities

Inventory Management

Real-Time Validation: Checks product availability before allowing order placement
Inventory Locking: When adding items to cart, inventory is temporarily reserved to prevent overselling
Inventory Restoration: When orders are canceled, the reserved inventory is automatically restored

Asynchronous Order Processing

Background Service: OrderFulfillmentService runs as a hosted service separate from the request pipeline
IHostedService Implementation: Inherits from BackgroundService to process orders asynchronously
Periodic Processing: Scans for pending orders at regular intervals
Notification System: Simulates sending notifications when orders are fulfilled

Implementation Details

Dependency Injection: Services are scoped appropriately for concurrent access
Task-based Processing: All operations use async/await patterns for non-blocking execution
Exception Handling: Robust exception handling prevents failed operations from affecting the entire system
Order Status Management: Clear state transitions from PendingFulfillment to Fulfilled or Canceled

This architecture ensures that the system can handle multiple concurrent users placing orders while maintaining data consistency and providing a responsive user experience.

