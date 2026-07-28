using FYP.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Payment
    {
        [Key]
        [MaxLength(50)]
        public string PaymentID { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderID { get; set; }

        [Required]
        [MaxLength(255)]
        public string PaymentToken { get; set; }

        [Required]
        [MaxLength(36)]
        public string IdempotencyKey { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        [ForeignKey("OrderID")]
        public virtual Order Order { get; set; }
    }
}