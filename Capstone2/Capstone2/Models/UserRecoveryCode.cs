using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    [Table("UserRecoveryCodes")]
    public class UserRecoveryCode
    {
        [Key]
        public int RecoveryId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(256)]
        public string CodeHash { get; set; }

        [Required]
        [MaxLength(128)]
        public string Salt { get; set; }

        public bool IsUsed { get; set; }

        [Required]
        public DateTime CreatedUtc { get; set; }

        public DateTime? UsedUtc { get; set; }
    }
}


