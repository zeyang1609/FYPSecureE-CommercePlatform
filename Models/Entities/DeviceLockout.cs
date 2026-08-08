using System;
using System.ComponentModel.DataAnnotations;

namespace FYP.Models.Entities
{
    public class DeviceLockout
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string DeviceIdentifier { get; set; } // Will store "Email:IP"

        public int FailedAttempts { get; set; }

        public DateTime? LockoutEnd { get; set; }
    }
}
