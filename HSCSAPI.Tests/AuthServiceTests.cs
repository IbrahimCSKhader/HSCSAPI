using HSCSAPI.Data;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Auth;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task RegisterVerifyAndLoginDoctorFlow_WorksCorrectly()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clinicId = await dbContext.Clinics.Select(c => c.ClinicId).FirstAsync();

        var registerResult = await authService.RegisterDoctorAsync(new RegisterDoctorRequest
        {
            Name = "Doctor Test",
            Email = "doctor.flow@test.local",
            Password = "Doctor123",
            ClinicId = clinicId,
            ProfessionalLicenseNumber = "DOC-FLOW-001"
        });

        Assert.True(registerResult.Success);
        Assert.NotNull(registerResult.User);
        Assert.False(registerResult.User!.EmailConfirmed);
        Assert.Equal(clinicId, registerResult.User.ClinicId);
        Assert.Null(registerResult.Token);

        var loginBeforeVerification = await authService.LoginAsync(new LoginRequest
        {
            Email = "doctor.flow@test.local",
            Password = "Doctor123"
        });

        Assert.False(loginBeforeVerification.Success);
        Assert.Contains("not verified", loginBeforeVerification.Message, StringComparison.OrdinalIgnoreCase);

        var verificationCode = await dbContext.UserVerificationCodes
            .Where(vc => vc.Purpose == VerificationPurpose.EmailVerification)
            .OrderByDescending(vc => vc.ExpiresAt)
            .FirstAsync(vc => vc.User.Email == "doctor.flow@test.local");

        var verificationResult = await authService.VerifyRegistrationCodeAsync(new VerifyRegistrationCodeRequest
        {
            Email = "doctor.flow@test.local",
            VerificationCode = verificationCode.Code
        });

        Assert.True(verificationResult.Success);

        var loginAfterVerification = await authService.LoginAsync(new LoginRequest
        {
            Email = "doctor.flow@test.local",
            Password = "Doctor123"
        });

        Assert.True(loginAfterVerification.Success);
        Assert.NotNull(loginAfterVerification.Token);
        Assert.Equal(nameof(UserSystemRole.Doctor), loginAfterVerification.User!.Role);
        Assert.True(loginAfterVerification.User.EmailConfirmed);
        Assert.Equal(clinicId, loginAfterVerification.User.ClinicId);
    }

    [Fact]
    public async Task ForgotAndResetPassword_UpdatesLoginCredentials()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        const string patientEmail = "patient@seed.local";
        const string oldPassword = "SeedPassword123";
        const string newPassword = "PatientNew123";

        var forgotPasswordResult = await authService.ForgotPasswordAsync(new ForgotPasswordRequest
        {
            Email = patientEmail
        });

        Assert.True(forgotPasswordResult.Success);

        var verificationCode = await dbContext.UserVerificationCodes
            .Where(vc => vc.Purpose == VerificationPurpose.PasswordReset)
            .OrderByDescending(vc => vc.ExpiresAt)
            .FirstAsync(vc => vc.User.Email == patientEmail && !vc.IsUsed);

        var resetResult = await authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = patientEmail,
            VerificationCode = verificationCode.Code,
            NewPassword = newPassword
        });

        Assert.True(resetResult.Success);

        var oldPasswordLogin = await authService.LoginAsync(new LoginRequest
        {
            Email = patientEmail,
            Password = oldPassword
        });

        Assert.False(oldPasswordLogin.Success);

        var newPasswordLogin = await authService.LoginAsync(new LoginRequest
        {
            Email = patientEmail,
            Password = newPassword
        });

        Assert.True(newPasswordLogin.Success);
        Assert.NotNull(newPasswordLogin.Token);
    }
}
