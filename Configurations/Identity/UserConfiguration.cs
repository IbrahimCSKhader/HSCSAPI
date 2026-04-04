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

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(x => x.Address)
            .HasMaxLength(300);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

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
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DoctorProfile)
            .WithOne(x => x.User)
            .HasForeignKey<Doctor>(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SecretaryProfile)
            .WithOne(x => x.User)
            .HasForeignKey<Secretary>(x => x.SecretaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AuthorizedMemberProfile)
            .WithOne(x => x.User)
            .HasForeignKey<AuthorizedMember>(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LaboratoryTechnologistProfile)
            .WithOne(x => x.User)
            .HasForeignKey<LaboratoryTechnologist>(x => x.LaboratoryTechnologistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RadiologyTechnologistProfile)
            .WithOne(x => x.User)
            .HasForeignKey<RadiologyTechnologist>(x => x.RadiologyTechnologistId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
