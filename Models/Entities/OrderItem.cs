using FYP.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class OrderItem
    {
        [Key]
        [MaxLength(50)]
        public string OrderItemID { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ProductID { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [ForeignKey("OrderID")]
        public virtual Order Order { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; }
    }
}
