using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FYP.Models.Entities
{
    public class Courier
    {
        [Key]
        [MaxLength(50)]
        public string CourierID { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(255)]
        public string TrackingUrlTemplate { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<DeliveryPricingRule> PricingRules { get; set; }
    }
}
