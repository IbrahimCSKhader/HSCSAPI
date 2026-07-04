namespace HSCSAPI.DTOs.Patient;

public class UpdateMyPatientProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? BloodType { get; set; }
}

public class ChangePatientPasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ChangePatientPasswordResponse : HSCSAPI.DTOs.Common.ApiResponse
{
}
