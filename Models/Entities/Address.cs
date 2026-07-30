using System.ComponentModel.DataAnnotations;

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
        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [Required]
        [MaxLength(100)]
        public string StateArea { get; set; }

        [Required]
        [MaxLength(20)]
        public string PostalCode { get; set; }

        [MaxLength(50)]
        public string? UnitNumber { get; set; }

        [Required]
        [MaxLength(255)]
        public string HouseBuildingStreet { get; set; }

        [Required]
        [MaxLength(20)]
        public string Label { get; set; } // "Home" or "Work"

        public bool IsDefault { get; set; }

        public virtual User User { get; set; }
    }
}
