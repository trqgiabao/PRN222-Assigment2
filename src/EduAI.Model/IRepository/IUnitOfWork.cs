namespace EduAI.Model.IRepository;

public interface IUnitOfWork
{
    ISubjectRepository Subjects { get; }
    IChapterRepository Chapters { get; }
    IDocumentRepository Documents { get; }
    IChunkRepository Chunks { get; }
    IEmbeddingRepository Embeddings { get; }
    IChatSessionRepository ChatSessions { get; }
    IChatMessageRepository ChatMessages { get; }
    IAuditLogRepository AuditLogs { get; }
    Task<int> SaveChangesAsync();
}
