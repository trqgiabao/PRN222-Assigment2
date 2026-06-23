using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IAuditLogService
{
    Task LogAsync(CreateAuditLogDto dto);
    Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int count = 100);
    Task<IReadOnlyList<AuditLogDto>> SearchAsync(AuditLogQueryDto query);
    Task<AuditLogDto?> GetByIdAsync(int id);
    Task<bool> DeleteAsync(int id);
    Task<AuditLogDto?> UpdateAsync(UpdateAuditLogDto dto);
}
