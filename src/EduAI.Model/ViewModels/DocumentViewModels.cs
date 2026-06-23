using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class DocumentIndexViewModel
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public IReadOnlyList<DTOs.DocumentDto> Documents { get; set; } = Array.Empty<DTOs.DocumentDto>();
}

public class DocumentDetailsViewModel
{
    public DTOs.DocumentDetailsDto? Document { get; set; }
    public bool IsAdmin { get; set; }
    public bool ShowChunks { get; set; }
    public int? SelectedChunkId { get; set; }
}

public class DocumentCreateViewModel
{
    [Required]
    public int SubjectId { get; set; }

    public int ChapterId { get; set; }

    [Display(Name = "New chapter name")]
    public string? NewChapterName { get; set; }

    public IReadOnlyList<DTOs.ChapterDto> AvailableChapters { get; set; } = Array.Empty<DTOs.ChapterDto>();
}

public class DocumentEditViewModel
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;

    [Required, StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public int ChapterId { get; set; }
}
