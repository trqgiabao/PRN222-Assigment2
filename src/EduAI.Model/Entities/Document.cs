using EduAI.Model.Enums;

namespace EduAI.Model.Entities;

public class Document : BaseEntity
{
    public int SubjectId { get; set; }
    public int ChapterId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DocumentFileType FileType { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public Subject Subject { get; set; } = null!;
    public Chapter Chapter { get; set; } = null!;
    public ApplicationUser UploadedBy { get; set; } = null!;
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
