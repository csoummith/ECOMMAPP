// Core/Entities/Order.cs
using ECOMMAPP.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ECOMMAPP.Core.Entities
{
    public class Order
{
    public Order()
    {
        Items = new List<OrderItem>();
        OrderDate = DateTime.UtcNow;
        Status = OrderStatus.PendingFulfillment;
        LastUpdated = DateTime.UtcNow;
    }

    public int Id { get; set; }
    
    [Display(Name = "Order Date")]
    public DateTime OrderDate { get; set; }
    
    public OrderStatus Status { get; set; }
    
    // Don't require these navigation properties
    [Display(Name = "Order Items")]
    public List<OrderItem> Items { get; set; }
    
    [ConcurrencyCheck]
    public DateTime LastUpdated { get; set; }
}

}