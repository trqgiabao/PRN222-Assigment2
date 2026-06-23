using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class AuditLogFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [StringLength(64)]
    public string? UserId { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(2000)]
    public string? Details { get; set; }
}
