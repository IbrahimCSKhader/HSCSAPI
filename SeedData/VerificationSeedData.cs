using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;

namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly IReadOnlyList<SeedVerificationCode> VerificationCodes =
    [
        new SeedVerificationCode(
            UserKey: AdminSecretaryKey,
            Code: "111111",
            Purpose: VerificationPurpose.EmailVerification,
            ExpiresDaysOffset: -1,
            IsUsed: true),
        new SeedVerificationCode(
            UserKey: PatientKey,
            Code: "222222",
            Purpose: VerificationPurpose.PasswordReset,
            ExpiresDaysOffset: 7,
            IsUsed: false)
    ];
}
