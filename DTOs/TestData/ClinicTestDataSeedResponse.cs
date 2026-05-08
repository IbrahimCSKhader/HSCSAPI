namespace HSCSAPI.DTOs.TestData;

public class ClinicTestDataSeedResponse
{
    public bool Success { get; set; }
    public bool AlreadyExecuted { get; set; }
    public string Message { get; set; } = string.Empty;
    public string DefaultPassword { get; set; } = string.Empty;
    public int ClinicsCount { get; set; }
    public int StaffCount { get; set; }
    public List<SeededClinicResponse> Clinics { get; set; } = new();
}

public class SeededClinicResponse
{
    public Guid ClinicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public Guid? AdminSecretaryId { get; set; }
    public List<SeededStaffMemberResponse> Staff { get; set; } = new();
}

public class SeededStaffMemberResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsClinicAdmin { get; set; }
}
