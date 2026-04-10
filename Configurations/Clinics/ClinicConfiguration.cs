using HSCSAPI.Models.Clinics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Clinics;

public class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.ToTable("Clinics");

        builder.HasKey(x => x.ClinicId);
        builder.Property(x => x.ClinicId).ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        builder.HasOne(x => x.CreatedBySuperAdminUser)
            .WithMany(x => x.CreatedClinics)
            .HasForeignKey(x => x.CreatedBySuperAdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AdminSecretary)
            .WithOne(x => x.ManagedClinic)
            .HasForeignKey<Clinic>(x => x.AdminSecretaryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
