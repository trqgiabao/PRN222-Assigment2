using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IDocumentRepository : IGenericRepository<Document>
{
    Task<IReadOnlyList<Document>> GetBySubjectIdAsync(int subjectId);
    Task<Document?> GetByChapterIdAsync(int chapterId);
    Task<Document?> GetWithDetailsAsync(int id);
}
