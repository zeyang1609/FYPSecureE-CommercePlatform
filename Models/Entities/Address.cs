using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace FYP.Models.Entities
{
    public class Address
    {
        [Key]
        public int AddressID { get; set; }

        [Required]
        [MaxLength(50)]
        public string UserID { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; }

        [Required]
        [MaxLength(500)]
        public string PhoneNumber { get; set; }

        [Required]
        [MaxLength(500)]
        public string StateArea { get; set; }

        [Required]
        [MaxLength(500)]
        public string PostalCode { get; set; }

        [MaxLength(500)]
        public string? UnitNumber { get; set; }

        [Required]
        [MaxLength(500)]
        public string HouseBuildingStreet { get; set; }

        [Required]
        [MaxLength(20)]
        public string Label { get; set; } // "Home" or "Work"

        public bool IsDefault { get; set; }

        [Column(TypeName = "decimal(10, 7)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(10, 7)")]
        public decimal? Longitude { get; set; }

        public virtual User User { get; set; }
    }
}
