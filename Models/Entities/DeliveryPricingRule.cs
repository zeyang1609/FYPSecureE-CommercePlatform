using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class DeliveryPricingRule
    {
        [Key]
        [MaxLength(50)]
        public string DeliveryRuleID { get; set; }

        [Required]
        [MaxLength(50)]
        public string CourierID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ZoneRegion { get; set; } // e.g. "West Malaysia", "East Malaysia"

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal BaseWeightKg { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal BasePrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal IncrementalWeightKg { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal IncrementalPrice { get; set; }

        [ForeignKey("CourierID")]
        public virtual Courier Courier { get; set; }
    }
}
