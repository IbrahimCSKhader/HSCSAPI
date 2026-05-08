using HSCSAPI.Models.Enums;

namespace HSCSAPI.Models.Identity;

public static class IdentityRoleIds
{
    public static readonly Guid Patient = Guid.Parse("6d3a8a70-b6d1-4f01-8f10-2f87e65f1001");
    public static readonly Guid Doctor = Guid.Parse("6d3a8a70-b6d1-4f01-8f10-2f87e65f1002");
    public static readonly Guid Secretary = Guid.Parse("6d3a8a70-b6d1-4f01-8f10-2f87e65f1003");
    public static readonly Guid AuthorizedMember = Guid.Parse("6d3a8a70-b6d1-4f01-8f10-2f87e65f1004");
    public static readonly Guid LaboratoryTechnologist = Guid.Parse("6d3a8a70-b6d1-4f01-8f10-2f87e65f1005");
    public static readonly Guid RadiologyTechnologist = Guid.Parse("6d3a8a70-b6d1-4f01-8f10-2f87e65f1006");
    public static readonly Guid SuperAdmin = Guid.Parse("6d3a8a70-b6d1-4f01-8f10-2f87e65f1007");

    public static Guid Get(UserSystemRole role)
    {
        return role switch
        {
            UserSystemRole.Patient => Patient,
            UserSystemRole.Doctor => Doctor,
            UserSystemRole.Secretary => Secretary,
            UserSystemRole.AuthorizedMember => AuthorizedMember,
            UserSystemRole.LaboratoryTechnologist => LaboratoryTechnologist,
            UserSystemRole.RadiologyTechnologist => RadiologyTechnologist,
            UserSystemRole.SuperAdmin => SuperAdmin,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported role.")
        };
    }
}
