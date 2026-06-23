using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;
using Microsoft.AspNetCore.Identity;

namespace EduAI.BusinessLogic.Services;

public class SubjectService : ISubjectService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubjectNotificationService _subjectNotificationService;

    public SubjectService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        UserManager<ApplicationUser> userManager,
        ISubjectNotificationService subjectNotificationService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _userManager = userManager;
        _subjectNotificationService = subjectNotificationService;
    }

    public async Task<IReadOnlyList<SubjectDto>> GetAllAsync(string? userId, string role)
    {
        IReadOnlyList<Subject> subjects = role switch
        {
            Roles.Admin => await _unitOfWork.Subjects.GetAllWithTeacherAsync(includeInactive: true),
            Roles.Teacher => string.IsNullOrEmpty(userId)
                ? Array.Empty<Subject>()
                : await _unitOfWork.Subjects.GetByTeacherIdAsync(userId),
            Roles.Student => await GetSubjectsWithMaterialsAsync(),
            _ => Array.Empty<Subject>()
        };

        var result = new List<SubjectDto>();
        foreach (var subject in subjects)
        {
            var dto = MapToDto(subject);
            await EnrichMaterialStatsAsync(dto, subject.Id);
            result.Add(dto);
        }

        return result;
    }

    public async Task<SubjectDto?> GetByIdAsync(int id, string? userId, string role)
    {
        var subject = await _unitOfWork.Subjects.GetWithTeacherAsync(id);
        if (subject == null) return null;

        if (!subject.IsActive && role != Roles.Admin)
            return null;

        if (role == Roles.Teacher && subject.TeacherId != userId)
            return null;

        var dto = MapToDto(subject);
        await EnrichMaterialStatsAsync(dto, subject.Id);

        if (role == Roles.Student && !dto.HasMaterials)
            return null;

        return dto;
    }

    public async Task<bool> HasMaterialsAsync(int subjectId)
    {
        var chunks = await _unitOfWork.Chunks.GetBySubjectIdAsync(subjectId);
        return chunks.Count > 0;
    }

    public async Task<SubjectOperationResultDto> CreateAsync(CreateSubjectDto dto, string adminId, string? ipAddress)
    {
        var name = dto.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Subject name is required."
            };
        }

        if (await _unitOfWork.Subjects.ExistsByNameAsync(name))
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Subject '{name}' already exists."
            };
        }

        var subject = new Subject
        {
            Name = name,
            Description = dto.Description?.Trim()
        };

        await _unitOfWork.Subjects.AddAsync(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.CreateSubject,
            IpAddress = ipAddress,
            Details = $"Created subject: {subject.Name}"
        });

        var subjectDto = MapToDto(subject);
        await EnrichMaterialStatsAsync(subjectDto, subject.Id);

        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = "Created",
            SubjectId = subject.Id,
            Subject = subjectDto
        });

        return new SubjectOperationResultDto
        {
            Success = true,
            Subject = subjectDto
        };
    }

    public async Task<SubjectOperationResultDto> UpdateAsync(UpdateSubjectDto dto, string adminId, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(dto.Id);
        if (subject == null)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Subject not found."
            };
        }

        var name = dto.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Subject name is required."
            };
        }

        if (await _unitOfWork.Subjects.ExistsByNameAsync(name, dto.Id))
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Subject '{name}' already exists."
            };
        }

        subject.Name = name;
        subject.Description = dto.Description?.Trim();
        subject.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.UpdateSubject,
            IpAddress = ipAddress,
            Details = $"Updated subject: {subject.Name} (Id: {subject.Id})"
        });

        var updated = await GetByIdAsync(subject.Id, null, Roles.Admin);
        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = "Updated",
            SubjectId = subject.Id,
            Subject = updated
        });

        return new SubjectOperationResultDto
        {
            Success = true,
            Subject = updated
        };
    }

    public async Task<SubjectOperationResultDto> DeleteAsync(int id, string adminId, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(id);
        if (subject == null)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Subject not found."
            };
        }

        if (!subject.IsActive)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Subject is already hidden."
            };
        }

        subject.IsActive = false;
        subject.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.DeactivateSubject,
            IpAddress = ipAddress,
            Details = $"Hidden subject: {subject.Name} (Id: {id})"
        });

        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = "Deleted",
            SubjectId = id
        });

        return new SubjectOperationResultDto { Success = true };
    }

    public async Task<SubjectOperationResultDto> RestoreAsync(int id, string adminId, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(id);
        if (subject == null)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Subject not found."
            };
        }

        if (subject.IsActive)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Subject is already visible."
            };
        }

        subject.IsActive = true;
        subject.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.RestoreSubject,
            IpAddress = ipAddress,
            Details = $"Restored subject: {subject.Name} (Id: {id})"
        });

        var restored = await GetByIdAsync(subject.Id, null, Roles.Admin);
        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = "Created",
            SubjectId = subject.Id,
            Subject = restored
        });

        return new SubjectOperationResultDto
        {
            Success = true,
            Subject = restored
        };
    }

    public async Task<AssignTeacherResultDto> AssignTeacherAsync(AssignTeacherDto dto, string adminId, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(dto.SubjectId);
        if (subject == null)
        {
            return new AssignTeacherResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy môn học."
            };
        }

        if (!string.IsNullOrEmpty(subject.TeacherId))
        {
            if (string.IsNullOrWhiteSpace(dto.TeacherId))
            {
                return new AssignTeacherResultDto
                {
                    Success = false,
                    ErrorMessage = "Môn học đã có giáo viên. Phiên bản demo chưa hỗ trợ gỡ giáo viên."
                };
            }

            if (!string.Equals(subject.TeacherId, dto.TeacherId, StringComparison.Ordinal))
            {
                return new AssignTeacherResultDto
                {
                    Success = false,
                    ErrorMessage = "Môn học đã có giáo viên. Phiên bản demo chưa hỗ trợ đổi giáo viên khác."
                };
            }

            return new AssignTeacherResultDto { Success = true };
        }

        if (string.IsNullOrWhiteSpace(dto.TeacherId))
        {
            return new AssignTeacherResultDto { Success = true };
        }

        var teacher = await _userManager.FindByIdAsync(dto.TeacherId);
        if (teacher == null)
        {
            return new AssignTeacherResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy tài khoản giáo viên."
            };
        }

        if (!teacher.IsActive)
        {
            return new AssignTeacherResultDto
            {
                Success = false,
                ErrorMessage = "Không thể gán tài khoản giáo viên đã bị khóa."
            };
        }

        var roles = await _userManager.GetRolesAsync(teacher);
        if (!roles.Contains(Roles.Teacher))
        {
            return new AssignTeacherResultDto
            {
                Success = false,
                ErrorMessage = "Người dùng được chọn không phải giáo viên."
            };
        }

        var previousTeacher = subject.TeacherId;
        subject.TeacherId = dto.TeacherId;
        subject.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.AssignTeacher,
            IpAddress = ipAddress,
            Details = $"Assigned teacher {teacher.FullName} ({dto.TeacherId}) to subject {subject.Name}"
        });

        var assigned = await GetByIdAsync(subject.Id, null, Roles.Admin);
        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = "TeacherAssigned",
            SubjectId = subject.Id,
            Subject = assigned,
            PreviousTeacherId = previousTeacher
        });

        return new AssignTeacherResultDto { Success = true };
    }

    public async Task<bool> IsTeacherAssignedToSubjectAsync(string teacherId, int subjectId)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(subjectId);
        return subject is { IsActive: true, TeacherId: var tid } && tid == teacherId;
    }

    private async Task<IReadOnlyList<Subject>> GetSubjectsWithMaterialsAsync()
    {
        var subjects = await _unitOfWork.Subjects.GetAllWithTeacherAsync();
        var eligible = new List<Subject>();
        foreach (var subject in subjects)
        {
            if (await HasMaterialsAsync(subject.Id))
                eligible.Add(subject);
        }

        return eligible;
    }

    private async Task EnrichMaterialStatsAsync(SubjectDto dto, int subjectId)
    {
        var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
        var chunks = await _unitOfWork.Chunks.GetBySubjectIdAsync(subjectId);
        dto.DocumentCount = documents.Count;
        dto.ChunkCount = chunks.Count;
        dto.HasMaterials = chunks.Count > 0;
    }

    private static SubjectDto MapToDto(Subject subject) => new()
    {
        Id = subject.Id,
        Name = subject.Name,
        Description = subject.Description,
        TeacherId = subject.TeacherId,
        TeacherName = subject.Teacher?.FullName,
        CreatedAt = subject.CreatedAt,
        IsActive = subject.IsActive
    };
}
