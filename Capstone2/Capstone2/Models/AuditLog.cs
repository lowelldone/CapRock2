using System;
using System.ComponentModel.DataAnnotations;

namespace Capstone2.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string? Username { get; set; }
        public string? Role { get; set; }

        public string? Action { get; set; }
        public string? Details { get; set; }

        public string? OrderNumber { get; set; }
        public int? WaiterId { get; set; }
    }
}


