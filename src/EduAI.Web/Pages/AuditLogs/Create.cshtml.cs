using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.AuditLogs;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly IAuditLogService _auditLogService;

    public CreateModel(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [BindProperty]
    public AuditLogFormViewModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = string.IsNullOrWhiteSpace(Input.UserId) ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : Input.UserId.Trim(),
            Action = Input.Action.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(Input.IpAddress) ? IpAddressHelper.GetClientIp(HttpContext) : Input.IpAddress.Trim(),
            Details = Input.Details
        });

        return RedirectToPage("Index");
    }
}
