using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EduAI.Web.Helpers;

namespace EduAI.Web.Pages.Subjects;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ISubjectService _subjectService;
    private readonly IDocumentService _documentService;
    private readonly IChunkService _chunkService;

    public DetailsModel(ISubjectService subjectService, IDocumentService documentService, IChunkService chunkService)
    {
        _subjectService = subjectService;
        _documentService = documentService;
        _chunkService = chunkService;
    }

    public SubjectDetailsViewModel ViewModel { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<DocumentDto> Documents { get; set; } = Array.Empty<DocumentDto>();
    public int? ExpandedDocumentId { get; set; }
    public IReadOnlyList<ChunkDto> ExpandedChunks { get; set; } = Array.Empty<ChunkDto>();
    public bool ShowEmbedding { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, int? showChunks)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin
            : User.IsInRole(Roles.Teacher) ? Roles.Teacher : Roles.Student;

        ViewModel.Subject = await _subjectService.GetByIdAsync(id, userId, role);
        if (ViewModel.Subject == null) return NotFound();

        Documents = await _documentService.GetBySubjectAsync(id, userId ?? string.Empty, role);
        ShowEmbedding = role == Roles.Admin;

        if (showChunks.HasValue && role != Roles.Student)
        {
            ExpandedDocumentId = showChunks;
            ExpandedChunks = await _chunkService.GetByDocumentAsync(showChunks.Value, userId ?? string.Empty, role);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        if (!User.IsInRole(Roles.Admin))
            return Forbid();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _subjectService.DeleteAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ViewModel.Subject = await _subjectService.GetByIdAsync(id, null, Roles.Admin);
            ErrorMessage = result.ErrorMessage ?? "Không thể ẩn môn học.";
            return Page();
        }

        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostRestoreAsync(int id)
    {
        if (!User.IsInRole(Roles.Admin))
            return Forbid();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _subjectService.RestoreAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ViewModel.Subject = await _subjectService.GetByIdAsync(id, null, Roles.Admin);
            ErrorMessage = result.ErrorMessage ?? "Không thể hiện lại môn học.";
            return Page();
        }

        return RedirectToPage("Details", new { id });
    }
}
