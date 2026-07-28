using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class FraudAlert
    {
        [Key]
        public string AlertID { get; set; } = string.Empty;

        [Required]
        public string OrderID { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,4)")]
        public decimal RiskScore { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string SHAP_Data { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        [ForeignKey("OrderID")]
        public virtual Order? Order { get; set; }
    }
}