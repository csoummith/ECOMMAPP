// Program.cs
using ECOMMAPP.Core.Interfaces;
using ECOMMAPP.Core.Services;
using ECOMMAPP.Infrastructure.Data;
using ECOMMAPP.Infrastructure.Repositories;
using ECOMMAPP.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => {
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString));
    
    // Enable detailed errors and sensitive data logging in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

// Register repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Register services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<INotificationService, EmailNotificationService>();

// Register background service for order fulfillment - comment this out temporarily
// builder.Services.AddHostedService<OrderFulfillmentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
//builder.WebHost.UseUrls("http://localhost:5000");
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Initializing database...");
        var context = services.GetRequiredService<AppDbContext>();
        
        // Check if database exists
        bool dbExists = context.Database.CanConnect();
        logger.LogInformation($"Database connection test: {(dbExists ? "successful" : "failed")}");
        
        if (!dbExists)
        {
            // Create database
            logger.LogInformation("Creating database...");
            context.Database.EnsureDeleted(); // Ensure clean slate
            context.Database.EnsureCreated();
            logger.LogInformation("Database created successfully");
        }
        
        // Seed data if no products exist
        if (!context.Products.Any())
        {
            logger.LogInformation("Seeding database with initial data...");
            SeedData.Initialize(services);
            logger.LogInformation("Database seeded successfully");
        }
        else
        {
            logger.LogInformation("Database already contains data, skipping seed");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database");
        // We'll continue the application even if database init fails
    }
}

app.Run();