using BCrypt.Net;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.DTOs.Common;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly UserIdGeneratorService _userIdGenerator;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        UserIdGeneratorService userIdGenerator,
        ITokenService tokenService,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userIdGenerator = userIdGenerator;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Include(u => u.PatientProfile)
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                return new AuthResponse { Success = false, Message = "Invalid email or password" };
            }

            var verificationCode = new UserVerificationCode
            {
                UserId = user.UserId,
                Code = GenerateVerificationCode(),
                Purpose = VerificationPurpose.Login,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            };

            _context.Add(verificationCode);
            await _context.SaveChangesAsync(cancellationToken);

            await TrySendLoginVerificationEmailAsync(user, verificationCode.Code, cancellationToken);

            return new AuthResponse
            {
                Success = true,
                Message = "Verification code sent to your email.",
                User = MapToUserDto(user)
            };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Login failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RegisterPatientCoreAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> RegisterDoctorAsync(RegisterDoctorRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                UserSystemRole.Doctor,
                user =>
                {
                    user.DoctorProfile = new Doctor
                    {
                        ProfessionalLicenseNumber = request.ProfessionalLicenseNumber,
                        User = user
                    };
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> RegisterSecretaryAsync(RegisterSecretaryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                UserSystemRole.Secretary,
                user =>
                {
                    user.SecretaryProfile = new Secretary
                    {
                        ClinicId = null,
                        User = user
                    };
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> RegisterAuthorizedMemberAsync(RegisterAuthorizedMemberRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                UserSystemRole.AuthorizedMember,
                user =>
                {
                    user.AuthorizedMemberProfile = new AuthorizedMember
                    {
                        User = user
                    };
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> RegisterLaboratoryTechnologistAsync(RegisterLaboratoryTechnologistRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                UserSystemRole.LaboratoryTechnologist,
                user =>
                {
                    user.LaboratoryTechnologistProfile = new LaboratoryTechnologist
                    {
                        ProfessionalLicenseNumber = request.ProfessionalLicenseNumber,
                        User = user
                    };
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> RegisterRadiologyTechnologistAsync(RegisterRadiologyTechnologistRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                UserSystemRole.RadiologyTechnologist,
                user =>
                {
                    user.RadiologyTechnologistProfile = new RadiologyTechnologist
                    {
                        ProfessionalLicenseNumber = request.ProfessionalLicenseNumber,
                        User = user
                    };
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
    }

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
            if (user == null)
            {
                return new ApiResponse
                {
                    Success = true,
                    Message = "If the email exists, a password reset code has been sent."
                };
            }

            var verificationCode = new UserVerificationCode
            {
                UserId = user.UserId,
                Code = GenerateVerificationCode(),
                Purpose = VerificationPurpose.PasswordReset,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            };

            _context.Add(verificationCode);
            await _context.SaveChangesAsync(cancellationToken);

            await TrySendPasswordResetEmailAsync(user, verificationCode.Code, cancellationToken);

            return new ApiResponse
            {
                Success = true,
                Message = "If the email exists, a password reset code has been sent."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse { Success = false, Message = $"Password reset request failed: {ex.Message}" };
        }
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
            if (user == null)
            {
                return new ApiResponse { Success = false, Message = "Invalid email or code." };
            }

            var verificationCode = await _context.Set<UserVerificationCode>()
                .FirstOrDefaultAsync(vc => vc.UserId == user.UserId
                    && vc.Code == request.VerificationCode
                    && vc.Purpose == VerificationPurpose.PasswordReset
                    && !vc.IsUsed
                    && vc.ExpiresAt > DateTime.UtcNow,
                    cancellationToken);

            if (verificationCode == null)
            {
                return new ApiResponse { Success = false, Message = "Invalid or expired verification code." };
            }

            user.PasswordHash = HashPassword(request.NewPassword);
            verificationCode.IsUsed = true;
            await _context.SaveChangesAsync(cancellationToken);

            await TrySendPasswordResetConfirmationEmailAsync(user, cancellationToken);

            return new ApiResponse { Success = true, Message = "Password successfully reset." };
        }
        catch (Exception ex)
        {
            return new ApiResponse { Success = false, Message = $"Password reset failed: {ex.Message}" };
        }
    }

    public async Task<ApiResponse> VerifyCodeAsync(VerifyCodeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
            if (user == null)
            {
                return new ApiResponse { Success = false, Message = "Invalid email or verification code." };
            }

            var verificationCode = await _context.Set<UserVerificationCode>()
                .FirstOrDefaultAsync(vc => vc.UserId == user.UserId
                    && vc.Code == request.VerificationCode
                    && vc.Purpose == request.Purpose
                    && !vc.IsUsed
                    && vc.ExpiresAt > DateTime.UtcNow,
                    cancellationToken);

            if (verificationCode == null)
            {
                return new ApiResponse { Success = false, Message = "Invalid or expired verification code." };
            }

            return new ApiResponse { Success = true, Message = "Verification code is valid." };
        }
        catch (Exception ex)
        {
            return new ApiResponse { Success = false, Message = $"Code verification failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> VerifyLoginCodeAsync(VerifyCodeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Include(u => u.PatientProfile)
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

            if (user == null)
            {
                return new AuthResponse { Success = false, Message = "Invalid email or verification code." };
            }

            var verificationCode = await _context.Set<UserVerificationCode>()
                .FirstOrDefaultAsync(vc => vc.UserId == user.UserId
                    && vc.Code == request.VerificationCode
                    && vc.Purpose == VerificationPurpose.Login
                    && !vc.IsUsed
                    && vc.ExpiresAt > DateTime.UtcNow,
                    cancellationToken);

            if (verificationCode == null)
            {
                return new AuthResponse { Success = false, Message = "Invalid or expired verification code." };
            }

            verificationCode.IsUsed = true;
            await _context.SaveChangesAsync(cancellationToken);

            return BuildSuccessResponse(user, "Login successful");
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Login verification failed: {ex.Message}" };
        }
    }

    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    private async Task<AuthResponse> RegisterPatientCoreAsync(RegisterPatientRequest request, CancellationToken cancellationToken)
    {
        var clinic = await _context.Clinics.FindAsync(new object[] { request.ClinicId }, cancellationToken: cancellationToken);
        if (clinic == null)
        {
            return new AuthResponse { Success = false, Message = "Clinic not found" };
        }

        var user = await CreateBaseUserAsync(request.Email, request.Password, request.Name, request.PhoneNumber, request.Address, request.DateOfBirth, cancellationToken);
        var userID = await _userIdGenerator.GenerateUserIdAsync(request.ClinicId, UserSystemRole.Patient, cancellationToken);
        var genderEnum = Enum.Parse<Gender>(request.Gender);

        user.PatientProfile = new Patient
        {
            UserID = userID,
            Gender = genderEnum,
            BloodType = !string.IsNullOrEmpty(request.BloodType) ? Enum.Parse<BloodType>(request.BloodType) : null,
            User = user
        };

        return await SaveUserWithRoleAsync(user, UserSystemRole.Patient, cancellationToken, "Registration successful");
    }

    private async Task<AuthResponse> RegisterProfileUserAsync(
        string email,
        string password,
        string name,
        string? phoneNumber,
        string? address,
        DateOnly? dateOfBirth,
        UserSystemRole role,
        Action<User> configureProfile,
        CancellationToken cancellationToken)
    {
        var user = await CreateBaseUserAsync(email, password, name, phoneNumber, address, dateOfBirth, cancellationToken);
        configureProfile(user);
        return await SaveUserWithRoleAsync(user, role, cancellationToken, "Registration successful");
    }

    private async Task<User> CreateBaseUserAsync(
        string email,
        string password,
        string name,
        string? phoneNumber,
        string? address,
        DateOnly? dateOfBirth,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email already registered");
        }

        return new User
        {
            Name = name,
            Email = normalizedEmail,
            PhoneNumber = phoneNumber,
            Address = address,
            DateOfBirth = dateOfBirth,
            PasswordHash = HashPassword(password)
        };
    }

    private async Task<AuthResponse> SaveUserWithRoleAsync(User user, UserSystemRole role, CancellationToken cancellationToken, string message)
    {
        var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role.ToString(), cancellationToken);
        if (roleEntity == null)
        {
            return new AuthResponse { Success = false, Message = $"Role not found: {role}" };
        }

        user.UserRoles.Add(new UserRole { User = user, Role = roleEntity });

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        await TrySendWelcomeEmailAsync(user, role, cancellationToken);

        return BuildSuccessResponse(user, message);
    }

    private async Task TrySendWelcomeEmailAsync(User user, UserSystemRole role, CancellationToken cancellationToken)
    {
        try
        {
            var subject = "Welcome to HSCSAPI";
            var body = $@"
<h2>Welcome, {user.Name}</h2>
<p>Your account has been created successfully.</p>
<p>Role: {role}</p>
<p>Email: {user.Email}</p>
";

            await _emailService.SendEmailAsync(user.Email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
        }
    }

    private AuthResponse BuildSuccessResponse(User user, string message)
    {
        var userDto = MapToUserDto(user);
        var token = _tokenService.GenerateToken(user.UserId, user.Email, userDto.Role);

        return new AuthResponse
        {
            Success = true,
            Message = message,
            User = userDto,
            Token = token
        };
    }

    private UserDto MapToUserDto(User user)
    {
        var primaryRole = user.UserRoles
            .Select(ur => ur.Role.Name)
            .FirstOrDefault();

        return new UserDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            UserID = user.PatientProfile?.UserID,
            Role = primaryRole ?? string.Empty
        };
    }

    private async Task TrySendLoginVerificationEmailAsync(User user, string verificationCode, CancellationToken cancellationToken)
    {
        try
        {
            var subject = "رمز التحقق لتسجيل الدخول";
            var body = $@"
<h2>رمز التحقق لتسجيل الدخول</h2>
<p>رمز التحقق الخاص بك هو:</p>
<h3>{verificationCode}</h3>
<p>هذا الكود صالح لمدة 15 دقيقة فقط.</p>
";
            await _emailService.SendEmailAsync(user.Email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send login verification code to {Email}", user.Email);
        }
    }

    private async Task TrySendPasswordResetEmailAsync(User user, string verificationCode, CancellationToken cancellationToken)
    {
        try
        {
            var subject = "رمز إعادة تعيين كلمة المرور";
            var body = $@"
<h2>أمر إعادة تعيين كلمة المرور</h2>
<p>رمز التحقق الخاص بك هو:</p>
<h3>{verificationCode}</h3>
<p>هذا الكود صالح لمدة 15 دقيقة فقط.</p>
";
            await _emailService.SendEmailAsync(user.Email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password reset code to {Email}", user.Email);
        }
    }

    private async Task TrySendPasswordResetConfirmationEmailAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var subject = "تم تغيير كلمة المرور بنجاح";
            var body = $@"
<h2>تغيير كلمة المرور</h2>
<p>تم تغيير كلمة المرور لحسابك بنجاح.</p>
<p>إن لم تكن هذه العملية منك، الرجاء التواصل مع الدعم أو تغيير كلمة المرور مرة أخرى.</p>
";
            await _emailService.SendEmailAsync(user.Email, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password reset confirmation to {Email}", user.Email);
        }
    }

    private static string GenerateVerificationCode()
    {
        var buffer = new byte[4];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        var code = BitConverter.ToUInt32(buffer, 0) % 1000000;
        return code.ToString("D6");
    }
}
