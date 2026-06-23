namespace EduAI.Model.DTOs;

public class DocumentDto
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int ChapterId { get; set; }
    public string ChapterName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long FileSizeBytes { get; set; }
    public int ChunkCount { get; set; }
    public string IndexStatus { get; set; } = "Pending";
}

public class DocumentDetailsDto : DocumentDto
{
    public DateTime? ProcessedAt { get; set; }
    public IReadOnlyList<ChunkDto> Chunks { get; set; } = Array.Empty<ChunkDto>();
}

public class UploadDocumentDto
{
    public int SubjectId { get; set; }
    public int ChapterId { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public string UploaderRole { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public Stream FileStream { get; set; } = Stream.Null;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public class UploadDocumentResultDto
{
    public bool Success { get; set; }
    public int? DocumentId { get; set; }
    public string? ErrorMessage { get; set; }
    public int ChunksCreated { get; set; }
}

public class UpdateDocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int ChapterId { get; set; }
}

public class DocumentOperationResultDto
{
    public bool Success { get; set; }
    public DocumentDto? Document { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DocumentDownloadResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
}
