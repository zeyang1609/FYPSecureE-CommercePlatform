using FYP.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class FraudAlert
    {
        [Key]
        [MaxLength(50)]
        public string AlertID { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderID { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal RiskScore { get; set; }

        public string SHAP_Data { get; set; }

        [ForeignKey("OrderID")]
        public virtual Order Order { get; set; }
    }
}