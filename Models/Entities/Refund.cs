using FYP.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Refund
    {
        [Key]
        [MaxLength(50)]
        public string RefundID { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderID { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal RefundAmount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        [ForeignKey("OrderID")]
        public virtual Order Order { get; set; }
    }
}