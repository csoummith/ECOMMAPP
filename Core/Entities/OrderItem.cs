// Core/Entities/OrderItem.cs
using System.ComponentModel.DataAnnotations;

namespace ECOMMAPP.Core.Entities
{
   public class OrderItem
{
    public int Id { get; set; }
    
    public int OrderId { get; set; }
    
    [Required(ErrorMessage = "Please select a product")]
    [Display(Name = "Product")]
    public int ProductId { get; set; }
    
    [Required]
    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
    [Display(Name = "Quantity")]
    public int Quantity { get; set; } = 1;
    
    [Display(Name = "Unit Price")]
    public decimal UnitPrice { get; set; }
    
    // Make navigation properties nullable to avoid validation errors
    public virtual Order? Order { get; set; }
    public virtual Product? Product { get; set; }
}
}