namespace EduAI.Model.DTOs;

public class CreateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateUserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UserOperationResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CreateUserResultDto
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? TemporaryPassword { get; set; }
    public string? ErrorMessage { get; set; }
    public bool EmailSent { get; set; }
}
