using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class DocumentRepository : GenericRepository<Document>, IDocumentRepository
{
    public DocumentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Document>> GetBySubjectIdAsync(int subjectId) =>
        await DbSet.AsNoTracking()
            .Include(d => d.Chapter)
            .Include(d => d.UploadedBy)
            .Where(d => d.SubjectId == subjectId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

    public async Task<Document?> GetByChapterIdAsync(int chapterId) =>
        await DbSet.FirstOrDefaultAsync(d => d.ChapterId == chapterId);

    public async Task<Document?> GetWithDetailsAsync(int id) =>
        await DbSet.Include(d => d.Subject)
            .Include(d => d.Chapter)
            .Include(d => d.UploadedBy)
            .FirstOrDefaultAsync(d => d.Id == id);
}
