using EduAI.Model.Constants;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Study;

[Authorize(Policy = "StudentOnly")]
public class MaterialsModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;

    public MaterialsModel(IDocumentService documentService, ISubjectService subjectService)
    {
        _documentService = documentService;
        _subjectService = subjectService;
    }

    public DocumentIndexViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int subjectId)
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var subject = await _subjectService.GetByIdAsync(subjectId, studentId, Roles.Student);
        if (subject == null)
            return NotFound();

        ViewModel.SubjectId = subjectId;
        ViewModel.SubjectName = subject.Name;
        ViewModel.Documents = await _documentService.GetBySubjectAsync(subjectId, studentId, Roles.Student);
        return Page();
    }
}
