using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.Property(x => x.Id)
            .HasColumnName("UserId");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.RegisteredAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.UserName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("EmailIndex");

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(x => x.Address)
            .HasMaxLength(300);

        builder.HasOne(x => x.Clinic)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.UserRoles)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Notifications)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PatientProfile)
            .WithOne(x => x.User)
            .HasForeignKey<Patient>(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DoctorProfile)
            .WithOne(x => x.User)
            .HasForeignKey<Doctor>(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SecretaryProfile)
            .WithOne(x => x.User)
            .HasForeignKey<Secretary>(x => x.SecretaryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AuthorizedMemberProfile)
            .WithOne(x => x.User)
            .HasForeignKey<AuthorizedMember>(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LaboratoryTechnologistProfile)
            .WithOne(x => x.User)
            .HasForeignKey<LaboratoryTechnologist>(x => x.LaboratoryTechnologistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RadiologyTechnologistProfile)
            .WithOne(x => x.User)
            .HasForeignKey<RadiologyTechnologist>(x => x.RadiologyTechnologistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
