using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECOMMAPP.Core.Entities
{
    public class Product
    {
        public Product()
        {
            // Always initialize LastUpdated in the constructor
            LastUpdated = DateTime.UtcNow;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100, ErrorMessage = "Product name cannot exceed 100 characters")]
        [Display(Name = "Product Name")]
        public string Name { get; set; }
        
        [Required]
        [Range(0.01, 10000, ErrorMessage = "Price must be between $0.01 and $10,000")]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "Price ($)")]
        public decimal Price { get; set; }
        
        [Required]
        [Range(0, 1000, ErrorMessage = "Stock quantity must be between 0 and 1,000")]
        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }
        
        [Required]
        [ConcurrencyCheck]
        public DateTime LastUpdated { get; set; }
    }
}