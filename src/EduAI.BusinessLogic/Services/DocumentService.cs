using EduAI.BusinessLogic.Helpers;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.Enums;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Settings;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services;

public class DocumentService : IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".txt"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubjectService _subjectService;
    private readonly IAuditLogService _auditLogService;
    private readonly IGeminiAiService _geminiAiService;
    private readonly INotificationService _notificationService;
    private readonly ISubjectNotificationService _subjectNotificationService;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly AppSettings _appSettings;

    public DocumentService(
        IUnitOfWork unitOfWork,
        ISubjectService subjectService,
        IAuditLogService auditLogService,
        IGeminiAiService geminiAiService,
        INotificationService notificationService,
        ISubjectNotificationService subjectNotificationService,
        IDocumentIndexingQueue indexingQueue,
        IOptions<AppSettings> appSettings)
    {
        _unitOfWork = unitOfWork;
        _subjectService = subjectService;
        _auditLogService = auditLogService;
        _geminiAiService = geminiAiService;
        _notificationService = notificationService;
        _subjectNotificationService = subjectNotificationService;
        _indexingQueue = indexingQueue;
        _appSettings = appSettings.Value;
    }

    public async Task<IReadOnlyList<DocumentDto>> GetBySubjectAsync(int subjectId, string userId, string role)
    {
        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId))
            return Array.Empty<DocumentDto>();

        if (role == Roles.Student)
        {
            if (!await _subjectService.HasMaterialsAsync(subjectId))
                return Array.Empty<DocumentDto>();

            var studentDocs = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
            return studentDocs.Select(MapToDto).ToList();
        }

        var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
        var result = new List<DocumentDto>();
        foreach (var document in documents)
        {
            var dto = MapToDto(document);
            await EnrichWithChunkStatsAsync(dto, document.Id, role);
            result.Add(dto);
        }

        return result;
    }

    public async Task<DocumentDto?> GetByIdAsync(int id, string userId, string role)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(id);
        if (document == null) return null;

        if (role == Roles.Teacher && document.Subject.TeacherId != userId)
            return null;

        if (role == Roles.Student)
            return null;

        var dto = MapToDto(document);
        await EnrichWithChunkStatsAsync(dto, document.Id, role);
        return dto;
    }

    public async Task<DocumentDetailsDto?> GetDetailsByIdAsync(int id, string userId, string role)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(id);
        if (document == null)
            return null;

        if (role == Roles.Teacher && document.Subject.TeacherId != userId)
            return null;

        if (role == Roles.Student)
            return null;

        var chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(id);
        var embeddingChunkIds = role == Roles.Admin
            ? (await _unitOfWork.Embeddings.GetBySubjectIdAsync(document.SubjectId))
                .Where(e => e.DocumentId == id)
                .Select(e => e.ChunkId)
                .ToHashSet()
            : [];

        var chunkDtos = chunks.Select(c => new ChunkDto
        {
            Id = c.Id,
            SubjectId = c.SubjectId,
            SubjectName = document.Subject?.Name ?? string.Empty,
            ChapterId = c.ChapterId,
            ChapterName = document.Chapter?.Name ?? string.Empty,
            DocumentId = c.DocumentId,
            DocumentName = document.FileName,
            ChunkIndex = c.ChunkIndex,
            Content = c.Content,
            HasEmbedding = role == Roles.Admin && embeddingChunkIds.Contains(c.Id)
        }).ToList();

        var details = new DocumentDetailsDto
        {
            Id = document.Id,
            SubjectId = document.SubjectId,
            SubjectName = document.Subject?.Name ?? string.Empty,
            ChapterId = document.ChapterId,
            ChapterName = document.Chapter?.Name ?? string.Empty,
            FileName = document.FileName,
            FileType = document.FileType.ToString(),
            UploadedByName = document.UploadedBy?.FullName ?? string.Empty,
            CreatedAt = document.CreatedAt,
            FileSizeBytes = document.FileSizeBytes,
            ChunkCount = chunkDtos.Count,
            IndexStatus = ResolveIndexStatus(chunkDtos.Count, role, embeddingChunkIds.Count),
            ProcessedAt = chunkDtos.Count > 0
                ? chunks.Max(c => c.UpdatedAt ?? c.CreatedAt)
                : null,
            Chunks = chunkDtos
        };

        return details;
    }

    public async Task<UploadDocumentResultDto> UploadAsync(UploadDocumentDto dto, string? ipAddress)
    {
        var extension = Path.GetExtension(dto.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return new UploadDocumentResultDto
            {
                Success = false,
                ErrorMessage = "Only PDF, DOCX, PPT, and PPTX files are supported."
            };
        }

        var chapter = await _unitOfWork.Chapters.GetByIdAsync(dto.ChapterId);
        if (chapter == null)
        {
            return new UploadDocumentResultDto { Success = false, ErrorMessage = "Chapter does not exist." };
        }

        if (chapter.SubjectId != dto.SubjectId)
        {
            return new UploadDocumentResultDto { Success = false, ErrorMessage = "Chapter does not belong to the selected subject." };
        }

        if (dto.UploaderRole != Roles.Admin &&
            !await _subjectService.IsTeacherAssignedToSubjectAsync(dto.UploadedByUserId, dto.SubjectId))
        {
            return new UploadDocumentResultDto
            {
                Success = false,
                ErrorMessage = "You are not assigned to upload materials for this subject."
            };
        }

        var maxBytes = _appSettings.MaxUploadBytes;
        if (dto.FileSizeBytes <= 0 || dto.FileSizeBytes > maxBytes)
        {
            return new UploadDocumentResultDto
            {
                Success = false,
                ErrorMessage = $"File size must be between 1 byte and {maxBytes / (1024 * 1024)} MB."
            };
        }

        var existing = await _unitOfWork.Documents.GetByChapterIdAsync(dto.ChapterId);
        if (existing != null)
        {
            return new UploadDocumentResultDto
            {
                Success = false,
                ErrorMessage = "This chapter already has an uploaded document. Only one file per chapter is allowed."
            };
        }

        var uploadPath = string.IsNullOrWhiteSpace(_appSettings.UploadPath) ? "uploads" : _appSettings.UploadPath;
        var subjectFolder = Path.Combine(uploadPath, dto.SubjectId.ToString(), dto.ChapterId.ToString());
        Directory.CreateDirectory(subjectFolder);

        var safeFileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(subjectFolder, safeFileName);

        await using (var fileStream = File.Create(fullPath))
        {
            dto.FileStream.Position = 0;
            await dto.FileStream.CopyToAsync(fileStream);
        }

        var document = new Document
        {
            SubjectId = dto.SubjectId,
            ChapterId = dto.ChapterId,
            FileName = dto.FileName,
            FilePath = fullPath,
            FileType = MapFileType(extension),
            UploadedByUserId = dto.UploadedByUserId,
            FileSizeBytes = dto.FileSizeBytes
        };

        await _unitOfWork.Documents.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();

        await _indexingQueue.EnqueueAsync(document.Id);

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = dto.UploadedByUserId,
            Action = AuditActions.UploadDocument,
            IpAddress = ipAddress,
            Details = $"Uploaded document '{dto.FileName}' to chapter {dto.ChapterId}"
        });

        var subject = await _subjectService.GetByIdAsync(dto.SubjectId, null, Roles.Admin);
        if (subject != null)
        {
            await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
            {
                Action = "Updated",
                SubjectId = subject.Id,
                Subject = subject
            });
        }

        return new UploadDocumentResultDto
        {
            Success = true,
            DocumentId = document.Id,
            ChunksCreated = 0
        };
    }

    public async Task<DocumentOperationResultDto> UpdateAsync(
        UpdateDocumentDto dto, string userId, string role, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(dto.FileName))
            return DocFail("File name is required.");

        var document = await _unitOfWork.Documents.GetWithDetailsAsync(dto.Id);
        if (document == null)
            return DocFail("Document not found.");

        if (role == Roles.Teacher && document.Subject.TeacherId != userId)
            return DocFail("You are not assigned to this subject.");

        if (role != Roles.Admin && role != Roles.Teacher)
            return DocFail("Access denied.");

        if (dto.ChapterId != document.ChapterId)
        {
            var chapter = await _unitOfWork.Chapters.GetByIdAsync(dto.ChapterId);
            if (chapter == null || chapter.SubjectId != document.SubjectId)
                return DocFail("Chapter does not belong to this subject.");

            var existingDoc = await _unitOfWork.Documents.GetByChapterIdAsync(dto.ChapterId);
            if (existingDoc != null && existingDoc.Id != document.Id)
                return DocFail("Target chapter already has a document.");

            document.ChapterId = dto.ChapterId;
            var chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(document.Id);
            foreach (var chunk in chunks)
            {
                chunk.ChapterId = dto.ChapterId;
                _unitOfWork.Chunks.Update(chunk);
            }

            var embeddings = await _unitOfWork.Embeddings.GetBySubjectIdAsync(document.SubjectId);
            foreach (var embedding in embeddings.Where(e => e.DocumentId == document.Id))
            {
                embedding.ChapterId = dto.ChapterId;
                _unitOfWork.Embeddings.Update(embedding);
            }
        }

        document.FileName = dto.FileName.Trim();
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.UpdateDocument,
            IpAddress = ipAddress,
            Details = $"Updated document '{document.FileName}' (Id: {document.Id})"
        });

        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "Document",
            Action = "Updated",
            EntityId = document.Id,
            Message = $"Document '{document.FileName}' updated"
        });

        var updated = await GetByIdAsync(document.Id, userId, role);
        return new DocumentOperationResultDto { Success = true, Document = updated };
    }

    public async Task<bool> DeleteAsync(int id, string userId, string role, string? ipAddress)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(id);
        if (document == null) return false;

        if (role == Roles.Teacher && document.Subject.TeacherId != userId)
            return false;

        if (role != Roles.Admin && role != Roles.Teacher)
            return false;

        var subjectId = document.SubjectId;

        if (File.Exists(document.FilePath))
            File.Delete(document.FilePath);

        _unitOfWork.Documents.Remove(document);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.DeleteDocument,
            IpAddress = ipAddress,
            Details = $"Deleted document '{document.FileName}' (Id: {document.Id})"
        });

        var subject = await _subjectService.GetByIdAsync(subjectId, null, Roles.Admin);
        if (subject != null)
        {
            await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
            {
                Action = subject.HasMaterials ? "Updated" : "MaterialsRemoved",
                SubjectId = subject.Id,
                Subject = subject
            });
        }

        return true;
    }

    public async Task<DocumentDownloadResultDto> GetDownloadFileAsync(int documentId, string userId, string role, string? ipAddress)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(documentId);
        if (document == null)
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "Document not found."
            };
        }

        if (role == Roles.Teacher && document.Subject.TeacherId != userId)
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "You are not assigned to this subject."
            };
        }

        if (role == Roles.Student && !await _subjectService.HasMaterialsAsync(document.SubjectId))
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "Materials are not available for this subject."
            };
        }

        if (role != Roles.Admin && role != Roles.Teacher && role != Roles.Student)
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "Access denied."
            };
        }

        if (!File.Exists(document.FilePath))
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "File is missing on the server."
            };
        }

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.DownloadDocument,
            IpAddress = ipAddress,
            Details = $"Downloaded document '{document.FileName}' (Id: {document.Id})"
        });

        return new DocumentDownloadResultDto
        {
            Success = true,
            FilePath = document.FilePath,
            FileName = document.FileName,
            ContentType = ResolveContentType(document.FileName)
        };
    }

    // Indexing is executed in background (see IDocumentIndexingService).

    private static DocumentFileType MapFileType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => DocumentFileType.Pdf,
        ".docx" => DocumentFileType.Docx,
        ".ppt" => DocumentFileType.Ppt,
        ".pptx" => DocumentFileType.Pptx,
        _ => DocumentFileType.Pdf
    };

    private static DocumentDto MapToDto(Document document) => new()
    {
        Id = document.Id,
        SubjectId = document.SubjectId,
        SubjectName = document.Subject?.Name ?? string.Empty,
        ChapterId = document.ChapterId,
        ChapterName = document.Chapter?.Name ?? string.Empty,
        FileName = document.FileName,
        FileType = document.FileType.ToString(),
        UploadedByName = document.UploadedBy?.FullName ?? string.Empty,
        CreatedAt = document.CreatedAt,
        FileSizeBytes = document.FileSizeBytes
    };

    private async Task EnrichWithChunkStatsAsync(DocumentDto dto, int documentId, string role)
    {
        var chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(documentId);
        dto.ChunkCount = chunks.Count;

        if (chunks.Count == 0)
        {
            dto.IndexStatus = "Pending";
            return;
        }

        if (role == Roles.Admin)
        {
            var embeddings = await _unitOfWork.Embeddings.GetBySubjectIdAsync(dto.SubjectId);
            var embeddedCount = embeddings.Count(e => e.DocumentId == documentId);
            dto.IndexStatus = ResolveIndexStatus(chunks.Count, role, embeddedCount);
        }
        else
        {
            dto.IndexStatus = "Indexed";
        }
    }

    private static string ResolveContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };

    private static string ResolveIndexStatus(int chunkCount, string role, int embeddedCount)
    {
        if (chunkCount == 0)
            return "Pending";

        if (role == Roles.Admin && embeddedCount < chunkCount)
            return "Processing";

        return "Indexed";
    }

    private static DocumentOperationResultDto DocFail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
