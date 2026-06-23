using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Appointment;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Services.Appointments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class AppointmentFilteringTests
{
    [Fact]
    public async Task GetAllAsync_FiltersAppointmentsByDateRange()
    {
        using var context = new AppointmentFilteringTestContext();
        var clinic = context.AddClinic();
        var doctor = context.AddDoctor(clinic.ClinicId);
        var patient = context.AddPatient(clinic.ClinicId, "pat-001");
        var fromDate = new DateOnly(2026, 6, 22);
        var toDate = new DateOnly(2026, 6, 24);

        context.AddAppointment(doctor.DoctorId, patient.PatientId, fromDate.AddDays(-1), new TimeOnly(9, 0));
        var firstExpected = context.AddAppointment(doctor.DoctorId, patient.PatientId, fromDate, new TimeOnly(10, 0));
        var secondExpected = context.AddAppointment(doctor.DoctorId, patient.PatientId, toDate, new TimeOnly(11, 0));
        context.AddAppointment(doctor.DoctorId, patient.PatientId, toDate.AddDays(1), new TimeOnly(12, 0));
        await context.DbContext.SaveChangesAsync();

        var response = await context.Service.GetAllAsync(
            clinicId: null,
            fromDate,
            toDate,
            AppointmentFilteringTestContext.Principal(Guid.NewGuid(), UserSystemRole.SuperAdmin));

        var appointments = OkValue(response);
        Assert.Equal([firstExpected.AppointmentId, secondExpected.AppointmentId], appointments.Select(x => x.AppointmentId));
        Assert.All(appointments, appointment =>
            Assert.InRange(appointment.AppointmentDate, fromDate, toDate));
    }

    [Fact]
    public async Task GetMineAsync_FiltersCurrentDoctorsAppointmentsByDateRange()
    {
        using var context = new AppointmentFilteringTestContext();
        var clinic = context.AddClinic();
        var doctor = context.AddDoctor(clinic.ClinicId);
        var otherDoctor = context.AddDoctor(clinic.ClinicId, email: "other.doctor@test.local");
        var patient = context.AddPatient(clinic.ClinicId, "pat-002");
        var fromDate = new DateOnly(2026, 6, 22);
        var toDate = new DateOnly(2026, 6, 26);

        context.AddAppointment(doctor.DoctorId, patient.PatientId, fromDate.AddDays(-1), new TimeOnly(9, 0));
        var firstExpected = context.AddAppointment(doctor.DoctorId, patient.PatientId, fromDate, new TimeOnly(10, 0));
        var secondExpected = context.AddAppointment(doctor.DoctorId, patient.PatientId, toDate, new TimeOnly(15, 0));
        context.AddAppointment(doctor.DoctorId, patient.PatientId, toDate.AddDays(1), new TimeOnly(16, 0));
        context.AddAppointment(otherDoctor.DoctorId, patient.PatientId, fromDate.AddDays(1), new TimeOnly(11, 0));
        await context.DbContext.SaveChangesAsync();

        var response = await context.Service.GetMineAsync(
            fromDate,
            toDate,
            AppointmentFilteringTestContext.Principal(doctor.DoctorId, UserSystemRole.Doctor));

        var appointments = OkValue(response);
        Assert.Equal([firstExpected.AppointmentId, secondExpected.AppointmentId], appointments.Select(x => x.AppointmentId));
        Assert.All(appointments, appointment =>
        {
            Assert.Equal(doctor.DoctorId, appointment.DoctorId);
            Assert.InRange(appointment.AppointmentDate, fromDate, toDate);
        });
    }

    [Fact]
    public async Task GetMineAsync_RejectsInvalidDateRange()
    {
        using var context = new AppointmentFilteringTestContext();

        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.GetMineAsync(
            new DateOnly(2026, 6, 26),
            new DateOnly(2026, 6, 22),
            AppointmentFilteringTestContext.Principal(Guid.NewGuid(), UserSystemRole.Doctor)));
    }

    private static List<AppointmentResponse> OkValue(ActionResult<List<AppointmentResponse>> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<List<AppointmentResponse>>(ok.Value);
    }
}

internal sealed class AppointmentFilteringTestContext : IDisposable
{
    public AppointmentFilteringTestContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        Service = new AppointmentsService(DbContext);
    }

    public AppDbContext DbContext { get; }
    public AppointmentsService Service { get; }

    public Clinic AddClinic()
    {
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = "Central Clinic",
            CreatedBySuperAdminUserId = Guid.NewGuid()
        };

        DbContext.Clinics.Add(clinic);
        DbContext.SaveChanges();
        return clinic;
    }

    public Doctor AddDoctor(Guid clinicId, string email = "doctor@test.local")
    {
        var user = AddUser(email, "Dr. Samer Khalil", clinicId);
        var doctor = new Doctor
        {
            DoctorId = user.Id,
            ProfessionalLicenseNumber = $"DOC-{Guid.NewGuid():N}",
            User = user
        };

        DbContext.Doctors.Add(doctor);
        DbContext.SaveChanges();
        return doctor;
    }

    public Patient AddPatient(Guid clinicId, string patientUserId)
    {
        var user = AddUser($"{patientUserId}@test.local", "Sarah Al-Hassan", clinicId);
        var patient = new Patient
        {
            PatientId = user.Id,
            UserID = patientUserId,
            Gender = Gender.Female,
            BloodType = BloodType.OPositive,
            User = user
        };

        DbContext.Patients.Add(patient);
        DbContext.SaveChanges();
        return patient;
    }

    public Appointment AddAppointment(
        Guid doctorId,
        Guid patientId,
        DateOnly appointmentDate,
        TimeOnly appointmentTime)
    {
        var slot = new AvailabilitySlot
        {
            AvailabilitySlotId = Guid.NewGuid(),
            DoctorId = doctorId,
            DayOfWeek = appointmentDate.DayOfWeek,
            StartTime = appointmentTime,
            EndTime = appointmentTime.AddMinutes(45),
            IsAvailable = false
        };
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            AvailabilitySlotId = slot.AvailabilitySlotId,
            AppointmentDate = appointmentDate,
            AppointmentTime = appointmentTime,
            Notes = "Follow-up visit"
        };

        DbContext.AvailabilitySlots.Add(slot);
        DbContext.Appointments.Add(appointment);
        return appointment;
    }

    public static ClaimsPrincipal Principal(Guid userId, UserSystemRole role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            ],
            "Test"));
    }

    public void Dispose()
    {
        DbContext.Dispose();
    }

    private User AddUser(string email, string name, Guid clinicId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow,
            ClinicId = clinicId
        };

        DbContext.Users.Add(user);
        return user;
    }
}
