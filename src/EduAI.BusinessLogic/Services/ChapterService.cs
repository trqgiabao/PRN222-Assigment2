using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;

namespace EduAI.BusinessLogic.Services;

public class ChapterService : IChapterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubjectService _subjectService;

    public ChapterService(IUnitOfWork unitOfWork, ISubjectService subjectService)
    {
        _unitOfWork = unitOfWork;
        _subjectService = subjectService;
    }

    public async Task<IReadOnlyList<ChapterDto>> GetBySubjectAsync(int subjectId, string userId, string role)
    {
        if (!await CanAccessSubjectAsync(subjectId, userId, role))
            return Array.Empty<ChapterDto>();

        var chapters = await _unitOfWork.Chapters.GetBySubjectIdAsync(subjectId);
        var subject = await _unitOfWork.Subjects.GetByIdAsync(subjectId);
        var result = new List<ChapterDto>();

        foreach (var chapter in chapters)
        {
            var document = await _unitOfWork.Documents.GetByChapterIdAsync(chapter.Id);
            var chunks = document != null
                ? await _unitOfWork.Chunks.GetByDocumentIdAsync(document.Id)
                : Array.Empty<DocumentChunk>();

            result.Add(new ChapterDto
            {
                Id = chapter.Id,
                SubjectId = chapter.SubjectId,
                SubjectName = subject?.Name ?? string.Empty,
                Name = chapter.Name,
                OrderNumber = chapter.OrderNumber,
                HasDocument = document != null,
                DocumentId = document?.Id,
                ChunkCount = chunks.Count
            });
        }

        return result;
    }

    public async Task<ChapterDto?> GetByIdAsync(int id, string userId, string role)
    {
        var chapter = await _unitOfWork.Chapters.GetWithDocumentAsync(id);
        if (chapter == null) return null;

        if (!await CanAccessSubjectAsync(chapter.SubjectId, userId, role))
            return null;

        var chunks = chapter.Document != null
            ? await _unitOfWork.Chunks.GetByDocumentIdAsync(chapter.Document.Id)
            : Array.Empty<DocumentChunk>();

        return new ChapterDto
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            SubjectName = chapter.Subject.Name,
            Name = chapter.Name,
            OrderNumber = chapter.OrderNumber,
            HasDocument = chapter.Document != null,
            DocumentId = chapter.Document?.Id,
            ChunkCount = chunks.Count
        };
    }

    public async Task<ChapterDto> CreateAsync(CreateChapterDto dto, string userId, string role)
    {
        if (role != Roles.Admin && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, dto.SubjectId))
            throw new UnauthorizedAccessException("You are not assigned to this subject.");

        var chapter = new Chapter
        {
            SubjectId = dto.SubjectId,
            Name = dto.Name.Trim(),
            OrderNumber = dto.OrderNumber
        };

        await _unitOfWork.Chapters.AddAsync(chapter);
        await _unitOfWork.SaveChangesAsync();

        var subject = await _unitOfWork.Subjects.GetByIdAsync(dto.SubjectId);
        return new ChapterDto
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            SubjectName = subject?.Name ?? string.Empty,
            Name = chapter.Name,
            OrderNumber = chapter.OrderNumber,
            HasDocument = false
        };
    }

    public async Task<ChapterDto?> UpdateAsync(UpdateChapterDto dto, string userId, string role)
    {
        var chapter = await _unitOfWork.Chapters.GetByIdAsync(dto.Id);
        if (chapter == null) return null;

        if (role != Roles.Admin && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, chapter.SubjectId))
            return null;

        chapter.Name = dto.Name.Trim();
        chapter.OrderNumber = dto.OrderNumber;
        chapter.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Chapters.Update(chapter);
        await _unitOfWork.SaveChangesAsync();

        var subject = await _unitOfWork.Subjects.GetByIdAsync(chapter.SubjectId);
        var document = await _unitOfWork.Documents.GetByChapterIdAsync(chapter.Id);

        return new ChapterDto
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            SubjectName = subject?.Name ?? string.Empty,
            Name = chapter.Name,
            OrderNumber = chapter.OrderNumber,
            HasDocument = document != null
        };
    }

    public async Task<bool> DeleteAsync(int id, string userId, string role)
    {
        var chapter = await _unitOfWork.Chapters.GetByIdAsync(id);
        if (chapter == null) return false;

        if (role != Roles.Admin && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, chapter.SubjectId))
            return false;

        _unitOfWork.Chapters.Remove(chapter);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task<bool> CanAccessSubjectAsync(int subjectId, string userId, string role)
    {
        if (role == Roles.Admin || role == Roles.Student)
            return true;

        if (role == Roles.Teacher)
            return await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId);

        return false;
    }
}
