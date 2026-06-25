using System.ComponentModel.DataAnnotations;

namespace HSCSAPI.DTOs.Reminders;

public class ReminderListResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int AllCount { get; set; }
    public int UpcomingCount { get; set; }
    public int PastCount { get; set; }
    public List<ReminderResponse> Items { get; set; } = [];
}

public class ReminderResponse
{
    public Guid ReminderId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid? AuthorizedMemberId { get; set; }
    public string? AuthorizedMemberName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReminderAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DismissedAt { get; set; }
}

public class CreateReminderRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    public Guid? AuthorizedMemberId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Category { get; set; }

    [Required]
    public DateTime ReminderAt { get; set; }
}

public class UpdateReminderRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Category { get; set; }

    [Required]
    public DateTime ReminderAt { get; set; }

    public Guid? AuthorizedMemberId { get; set; }
}

public class ReminderPreferencesResponse
{
    public Guid UserId { get; set; }
    public bool AppointmentRemindersEnabled { get; set; }
    public bool LabResultRemindersEnabled { get; set; }
    public bool MessageRemindersEnabled { get; set; }
    public bool InAppNotificationsEnabled { get; set; }
    public bool EmailRemindersEnabled { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateReminderPreferencesRequest
{
    [Required]
    public bool? AppointmentRemindersEnabled { get; set; }

    [Required]
    public bool? LabResultRemindersEnabled { get; set; }

    [Required]
    public bool? MessageRemindersEnabled { get; set; }

    [Required]
    public bool? InAppNotificationsEnabled { get; set; }

    [Required]
    public bool? EmailRemindersEnabled { get; set; }
}
