namespace HSCSAPI.DTOs.Clinic;

public class TreatmentCatalogResponse
{
    public string TreatmentId { get; set; } = string.Empty;
    public string TreatmentName { get; set; } = string.Empty;
    public string DoctorSpecialty { get; set; } = string.Empty;
    public int ActiveDoctorsCount { get; set; }
}
