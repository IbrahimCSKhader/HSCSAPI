using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Identity;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(x => x.RoleId);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasData(
            new Role { RoleId = (int)UserSystemRole.SuperAdmin, Name = nameof(UserSystemRole.SuperAdmin) },
            new Role { RoleId = (int)UserSystemRole.Patient, Name = nameof(UserSystemRole.Patient) },
            new Role { RoleId = (int)UserSystemRole.Doctor, Name = nameof(UserSystemRole.Doctor) },
            new Role { RoleId = (int)UserSystemRole.Secretary, Name = nameof(UserSystemRole.Secretary) },
            new Role { RoleId = (int)UserSystemRole.AuthorizedMember, Name = nameof(UserSystemRole.AuthorizedMember) },
            new Role { RoleId = (int)UserSystemRole.LaboratoryTechnologist, Name = nameof(UserSystemRole.LaboratoryTechnologist) },
            new Role { RoleId = (int)UserSystemRole.RadiologyTechnologist, Name = nameof(UserSystemRole.RadiologyTechnologist) }
        );
    }
}
