using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduAI.Web.Pages.Documents;

[Authorize(Policy = "TeacherOnly")]
public class EditModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly IChapterService _chapterService;

    public EditModel(IDocumentService documentService, IChapterService chapterService)
    {
        _documentService = documentService;
        _chapterService = chapterService;
    }

    [BindProperty]
    public DocumentEditViewModel Input { get; set; } = new();

    public SelectList ChapterOptions { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadFormAsync(id))
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || !await LoadFormAsync(Input.Id))
            return Page();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var result = await _documentService.UpdateAsync(new UpdateDocumentDto
        {
            Id = Input.Id,
            FileName = Input.FileName,
            ChapterId = Input.ChapterId
        }, userId, role, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Update failed.");
            return Page();
        }

        return RedirectToPage("Details", new { id = Input.Id });
    }

    private async Task<bool> LoadFormAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var doc = await _documentService.GetByIdAsync(id, userId, role);
        if (doc == null) return false;

        Input = new DocumentEditViewModel
        {
            Id = doc.Id,
            SubjectId = doc.SubjectId,
            SubjectName = doc.SubjectName,
            FileName = doc.FileName,
            ChapterId = doc.ChapterId
        };

        var chapters = await _chapterService.GetBySubjectAsync(doc.SubjectId, userId, role);
        ChapterOptions = new SelectList(chapters, "Id", "Name", Input.ChapterId);
        return true;
    }
}
