using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.AuditLogs;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly IAuditLogService _auditLogService;

    public EditModel(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [BindProperty]
    public AuditLogFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var log = await _auditLogService.GetByIdAsync(id);
        if (log == null) return NotFound();

        Input = new AuditLogFormViewModel
        {
            Id = log.Id,
            Action = log.Action,
            UserId = log.UserId,
            IpAddress = log.IpAddress,
            Details = log.Details
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var updated = await _auditLogService.UpdateAsync(new UpdateAuditLogDto
        {
            Id = Input.Id,
            Action = Input.Action,
            IpAddress = Input.IpAddress,
            Details = Input.Details
        });

        if (updated == null)
        {
            ModelState.AddModelError(string.Empty, "Audit log not found.");
            return Page();
        }

        return RedirectToPage("Details", new { id = Input.Id });
    }
}
