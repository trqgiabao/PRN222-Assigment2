using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EduAI.Model.Settings;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace EduAI.Web.Pages.Documents;

[Authorize(Policy = "TeacherOnly")]
public class CreateModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;
    private readonly IChapterService _chapterService;
    private readonly AppSettings _appSettings;

    public CreateModel(
        IDocumentService documentService,
        ISubjectService subjectService,
        IChapterService chapterService,
        IOptions<AppSettings> appSettings)
    {
        _documentService = documentService;
        _subjectService = subjectService;
        _chapterService = chapterService;
        _appSettings = appSettings.Value;
    }

    [BindProperty]
    public DocumentCreateViewModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    public SelectList SubjectOptions { get; set; } = null!;
    public SelectList ChapterOptions { get; set; } = null!;
    public bool NoSubjectsAvailable { get; set; }
    public bool CanUpload { get; set; }
    public int MaxUploadMb { get; set; }
    public bool IsTeacher { get; set; }

    public async Task<IActionResult> OnGetAsync(int? subjectId)
    {
        await LoadPageAsync(subjectId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        IsTeacher = User.IsInRole(Roles.Teacher);
        await LoadPageAsync(Input.SubjectId);

        if (NoSubjectsAvailable)
        {
            ModelState.AddModelError(string.Empty, "No subjects available for upload.");
            return Page();
        }

        if (UploadFile == null || UploadFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn file tài liệu.");
            return Page();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var chapterId = await ResolveChapterIdAsync(userId, role);
        if (chapterId <= 0)
            return Page();

        await using var stream = UploadFile.OpenReadStream();
        var result = await _documentService.UploadAsync(new UploadDocumentDto
        {
            SubjectId = Input.SubjectId,
            ChapterId = chapterId,
            UploadedByUserId = userId,
            UploaderRole = role,
            FileName = UploadFile.FileName,
            FileStream = stream,
            ContentType = UploadFile.ContentType,
            FileSizeBytes = UploadFile.Length
        }, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Upload failed.");
            return Page();
        }

        return RedirectToPage("Details", new { id = result.DocumentId, showChunks = true });
    }

    private async Task<int> ResolveChapterIdAsync(string userId, string role)
    {
        if (!string.IsNullOrWhiteSpace(Input.NewChapterName))
        {
            var chapters = await _chapterService.GetBySubjectAsync(Input.SubjectId, userId, role);
            var nextOrder = chapters.Count == 0 ? 1 : chapters.Max(c => c.OrderNumber) + 1;
            var created = await _chapterService.CreateAsync(new CreateChapterDto
            {
                SubjectId = Input.SubjectId,
                Name = Input.NewChapterName.Trim(),
                OrderNumber = nextOrder
            }, userId, role);

            return created.Id;
        }

        if (Input.ChapterId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Chọn chương có sẵn hoặc nhập tên chương mới.");
            return 0;
        }

        var selected = Input.AvailableChapters.FirstOrDefault(c => c.Id == Input.ChapterId);
        if (selected == null)
        {
            ModelState.AddModelError(string.Empty, "Chapter không hợp lệ.");
            return 0;
        }

        if (selected.HasDocument)
        {
            ModelState.AddModelError(string.Empty, "Chương này đã có tài liệu. Chọn chương khác hoặc tạo chương mới.");
            return 0;
        }

        return Input.ChapterId;
    }

    private async Task LoadPageAsync(int? subjectId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;
        IsTeacher = role == Roles.Teacher;

        var subjects = await _subjectService.GetAllAsync(userId, role);
        NoSubjectsAvailable = subjects.Count == 0;

        if (subjects.Count == 0)
        {
            SubjectOptions = new SelectList(Array.Empty<object>(), "Id", "Name");
            ChapterOptions = new SelectList(Array.Empty<object>(), "Id", "Name");
            CanUpload = false;
            return;
        }

        var activeSubjectId = subjectId ?? Input.SubjectId;
        if (activeSubjectId <= 0 || subjects.All(s => s.Id != activeSubjectId))
            activeSubjectId = subjects[0].Id;

        Input.SubjectId = activeSubjectId;
        var chapters = await _chapterService.GetBySubjectAsync(activeSubjectId, userId, role);
        var available = chapters.Where(c => !c.HasDocument).ToList();
        Input.AvailableChapters = available;

        if (Input.ChapterId <= 0 || available.All(c => c.Id != Input.ChapterId))
            Input.ChapterId = available.FirstOrDefault()?.Id ?? 0;

        CanUpload = true;
        MaxUploadMb = (int)(_appSettings.MaxUploadBytes / (1024 * 1024));

        SubjectOptions = new SelectList(
            subjects.Select(s => new { s.Id, Name = s.Name }),
            "Id", "Name", activeSubjectId);

        var chapterChoices = new List<SelectListItem>
        {
            new() { Value = "0", Text = "-- Không chọn --" }
        };
        chapterChoices.AddRange(available.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.Name
        }));
        ChapterOptions = new SelectList(chapterChoices, "Value", "Text", Input.ChapterId.ToString());
    }
}
