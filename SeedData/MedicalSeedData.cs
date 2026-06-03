using HSCSAPI.Models.Enums;
using HSCSAPI.Models.MedicalFiles;

namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly SeedMedicalFile PrimaryMedicalFile = new(
        UploadedByDoctorKey: DoctorKey,
        FileType: MedicalFileType.Pdf,
        FilePath: "/seed/medical-files/consultation-summary.pdf",
        EncryptedChecksum: "seed-checksum-001",
        FileSizeInBytes: 4096,
        SeverityLevel: SeverityLevel.Low,
        UploadedDaysOffset: -1);

    public static readonly SeedFileDownloadRequest PrimaryFileDownloadRequest = new(
        PatientKey: PatientKey,
        ReviewedBySecretaryKey: StaffSecretaryKey,
        Reason: "Need a copy for follow-up.",
        PurposeDescription: "Sharing with another specialist for a second opinion.",
        Status: FileDownloadRequestStatus.Approved,
        SubmittedHoursOffset: -12,
        ReviewedHoursOffset: -6,
        RejectionReason: null);

    public static readonly SeedLabTestRequest PrimaryLabTestRequest = new(
        TestName: "Complete Blood Count",
        LaboratoryTechnologistKey: LaboratoryTechnologistKey);

    public static readonly SeedImagingTestRequest PrimaryImagingTestRequest = new(
        TestName: "Chest X-Ray",
        RadiologyTechnologistKey: RadiologyTechnologistKey);
}
