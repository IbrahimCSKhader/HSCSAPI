using HSCSAPI.Data;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.DTOs.Common;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Services.Common;
using HSCSAPI.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Auth;

public class AuthService : IAuthService
{
    private const string RegistrationVerificationMessage = "Registration successful. Verification code sent to your email.";
    private const string EmailNotVerifiedMessage = "Email is not verified. Please verify your registration code before logging in.";

    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly UserIdGeneratorService _userIdGenerator;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IServiceExceptionHandler _exceptionHandler;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        UserIdGeneratorService userIdGenerator,
        ITokenService tokenService,
        IEmailService emailService,
        IServiceExceptionHandler exceptionHandler,
        ILogger<AuthService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _userIdGenerator = userIdGenerator;
        _tokenService = tokenService;
        _emailService = emailService;
        _exceptionHandler = exceptionHandler;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            var user = await LoadUserByEmailAsync(request.Email, cancellationToken);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            {
                return new AuthResponse { Success = false, Message = "Invalid email or password." };
            }

            if (!user.EmailConfirmed)
            {
                return new AuthResponse { Success = false, Message = EmailNotVerifiedMessage };
            }

            var role = await GetPrimaryRoleAsync(user);
            return BuildAuthenticatedResponse(user, role, "Login successful.");
        }, ex => new AuthResponse { Success = false, Message = $"Login failed: {ex.Message}" }, nameof(LoginAsync));
    }

    public async Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            if (request.ClinicId == Guid.Empty)
            {
                return new AuthResponse { Success = false, Message = "ClinicId is required." };
            }

            if (!Enum.TryParse<Gender>(request.Gender, true, out var gender))
            {
                return new AuthResponse { Success = false, Message = "Invalid gender value." };
            }

            BloodType? bloodType = null;
            if (!string.IsNullOrWhiteSpace(request.BloodType))
            {
                if (!Enum.TryParse<BloodType>(request.BloodType, true, out var parsedBloodType))
                {
                    return new AuthResponse { Success = false, Message = "Invalid blood type value." };
                }

                bloodType = parsedBloodType;
            }

            var user = await CreateBaseUserAsync(
                request.Email,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                request.ClinicId,
                cancellationToken);

            if (user is null)
            {
                return new AuthResponse { Success = false, Message = "Email already registered." };
            }

            var patientUserId = await _userIdGenerator.GenerateUserIdAsync(request.ClinicId, UserSystemRole.Patient, cancellationToken);
            user.PatientProfile = new Patient
            {
                PatientId = user.Id,
                UserID = patientUserId,
                Gender = gender,
                BloodType = bloodType,
                User = user
            };

            return await SaveUserWithRoleAsync(user, request.Password, UserSystemRole.Patient, cancellationToken);
        }, ex => new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" }, nameof(RegisterPatientAsync));
    }

    public async Task<AuthResponse> RegisterDoctorAsync(RegisterDoctorRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            if (request.ClinicId == Guid.Empty)
            {
                return new AuthResponse { Success = false, Message = "ClinicId is required." };
            }

            if (await _context.Doctors.AnyAsync(d => d.ProfessionalLicenseNumber == request.ProfessionalLicenseNumber, cancellationToken))
            {
                return new AuthResponse { Success = false, Message = "Professional license number is already registered." };
            }

            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                request.ClinicId,
                UserSystemRole.Doctor,
                user =>
                {
                    user.DoctorProfile = new Doctor
                    {
                        DoctorId = user.Id,
                        ProfessionalLicenseNumber = request.ProfessionalLicenseNumber,
                        User = user
                    };
                },
                cancellationToken);
        }, ex => new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" }, nameof(RegisterDoctorAsync));
    }

    public async Task<AuthResponse> RegisterSecretaryAsync(RegisterSecretaryRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            var clinicId = request.ClinicId == Guid.Empty ? null : request.ClinicId;

            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                clinicId,
                UserSystemRole.Secretary,
                user =>
                {
                    user.SecretaryProfile = new Secretary
                    {
                        SecretaryId = user.Id,
                        User = user
                    };
                },
                cancellationToken);
        }, ex => new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" }, nameof(RegisterSecretaryAsync));
    }

    public async Task<AuthResponse> RegisterAuthorizedMemberAsync(RegisterAuthorizedMemberRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                null,
                UserSystemRole.AuthorizedMember,
                user =>
                {
                    user.AuthorizedMemberProfile = new AuthorizedMember
                    {
                        AuthorizedMemberId = user.Id,
                        User = user
                    };
                },
                cancellationToken);
        }, ex => new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" }, nameof(RegisterAuthorizedMemberAsync));
    }

    public async Task<AuthResponse> RegisterLaboratoryTechnologistAsync(RegisterLaboratoryTechnologistRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            if (request.ClinicId == Guid.Empty)
            {
                return new AuthResponse { Success = false, Message = "ClinicId is required." };
            }

            if (await _context.LaboratoryTechnologists.AnyAsync(l => l.ProfessionalLicenseNumber == request.ProfessionalLicenseNumber, cancellationToken))
            {
                return new AuthResponse { Success = false, Message = "Professional license number is already registered." };
            }

            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                request.ClinicId,
                UserSystemRole.LaboratoryTechnologist,
                user =>
                {
                    user.LaboratoryTechnologistProfile = new LaboratoryTechnologist
                    {
                        LaboratoryTechnologistId = user.Id,
                        ProfessionalLicenseNumber = request.ProfessionalLicenseNumber,
                        User = user
                    };
                },
                cancellationToken);
        }, ex => new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" }, nameof(RegisterLaboratoryTechnologistAsync));
    }

    public async Task<AuthResponse> RegisterRadiologyTechnologistAsync(RegisterRadiologyTechnologistRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            if (request.ClinicId == Guid.Empty)
            {
                return new AuthResponse { Success = false, Message = "ClinicId is required." };
            }

            if (await _context.RadiologyTechnologists.AnyAsync(r => r.ProfessionalLicenseNumber == request.ProfessionalLicenseNumber, cancellationToken))
            {
                return new AuthResponse { Success = false, Message = "Professional license number is already registered." };
            }

            return await RegisterProfileUserAsync(
                request.Email,
                request.Password,
                request.Name,
                request.PhoneNumber,
                request.Address,
                request.DateOfBirth,
                request.ClinicId,
                UserSystemRole.RadiologyTechnologist,
                user =>
                {
                    user.RadiologyTechnologistProfile = new RadiologyTechnologist
                    {
                        RadiologyTechnologistId = user.Id,
                        ProfessionalLicenseNumber = request.ProfessionalLicenseNumber,
                        User = user
                    };
                },
                cancellationToken);
        }, ex => new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" }, nameof(RegisterRadiologyTechnologistAsync));
    }

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            var user = await LoadUserByEmailAsync(request.Email, cancellationToken);
            if (user == null)
            {
                return new ApiResponse
                {
                    Success = true,
                    Message = "If the email exists, a password reset code has been sent."
                };
            }

            var verificationCode = await CreateVerificationCodeAsync(
                user.Id,
                VerificationPurpose.PasswordReset,
                cancellationToken);

            await TrySendPasswordResetEmailAsync(user, verificationCode.Code, cancellationToken);

            return new ApiResponse
            {
                Success = true,
                Message = "If the email exists, a password reset code has been sent."
            };
        }, ex => new ApiResponse { Success = false, Message = $"Password reset request failed: {ex.Message}" }, nameof(ForgotPasswordAsync));
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            var user = await LoadUserByEmailAsync(request.Email, cancellationToken);
            if (user == null)
            {
                return new ApiResponse { Success = false, Message = "Invalid email or code." };
            }

            var verificationCode = await _context.UserVerificationCodes
                .FirstOrDefaultAsync(
                    vc => vc.UserId == user.Id
                        && vc.Code == request.VerificationCode
                        && vc.Purpose == VerificationPurpose.PasswordReset
                        && !vc.IsUsed
                        && vc.ExpiresAt > DateTime.UtcNow,
                    cancellationToken);

            if (verificationCode == null)
            {
                return new ApiResponse { Success = false, Message = "Invalid or expired verification code." };
            }

            var passwordValidation = await ValidatePasswordAsync(user, request.NewPassword);
            if (passwordValidation.Count > 0)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = string.Join(" ", passwordValidation.Select(error => error.Description))
                };
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            var resetResult = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, request.NewPassword);
                user.SecurityStamp = Guid.NewGuid().ToString("N");

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ApiResponse
                    {
                        Success = false,
                        Message = string.Join(" ", updateResult.Errors.Select(error => error.Description))
                    };
                }

                verificationCode.IsUsed = true;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ApiResponse { Success = true, Message = "Password successfully reset." };
            });

            if (!resetResult.Success)
            {
                return resetResult;
            }

            await TrySendPasswordResetConfirmationEmailAsync(user, cancellationToken);

            return resetResult;
        }, ex => new ApiResponse { Success = false, Message = $"Password reset failed: {ex.Message}" }, nameof(ResetPasswordAsync));
    }

    public async Task<ApiResponse> VerifyRegistrationCodeAsync(
    VerifyRegistrationCodeRequest request,
    CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            var user = await LoadUserByEmailAsync(request.Email, cancellationToken);
            if (user == null)
            {
                return new ApiResponse { Success = false, Message = "Invalid email or verification code." };
            }

            if (user.EmailConfirmed)
            {
                return new ApiResponse { Success = true, Message = "Email is already verified." };
            }

            var verificationCode = await _context.UserVerificationCodes
                .FirstOrDefaultAsync(
                    vc => vc.UserId == user.Id
                        && vc.Code == request.VerificationCode
                        && vc.Purpose == VerificationPurpose.EmailVerification
                        && !vc.IsUsed
                        && vc.ExpiresAt > DateTime.UtcNow,
                    cancellationToken);

            if (verificationCode == null)
            {
                _logger.LogWarning("Invalid or expired verification code attempt for email: {Email}", request.Email);
                return new ApiResponse { Success = false, Message = "Invalid or expired verification code." };
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                user.EmailConfirmed = true;
                verificationCode.IsUsed = true;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ApiResponse
                    {
                        Success = false,
                        Message = "Failed to verify email. Please try again."
                    };
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Email verified successfully for user: {Email}", request.Email);

                return new ApiResponse
                {
                    Success = true,
                    Message = "Email verified successfully."
                };
            });
        }, _ => new ApiResponse
        {
            Success = false,
            Message = "An error occurred while verifying your email. Please try again later."
        }, nameof(VerifyRegistrationCodeAsync));
    }

    private async Task<AuthResponse> RegisterProfileUserAsync(
        string email,
        string password,
        string name,
        string? phoneNumber,
        string? address,
        DateOnly? dateOfBirth,
        Guid? clinicId,
        UserSystemRole role,
        Action<User> configureProfile,
        CancellationToken cancellationToken)
    {
        var user = await CreateBaseUserAsync(email, name, phoneNumber, address, dateOfBirth, clinicId, cancellationToken);
        if (user == null)
        {
            return new AuthResponse { Success = false, Message = "Email already registered." };
        }

        configureProfile(user);
        return await SaveUserWithRoleAsync(user, password, role, cancellationToken);
    }



    private async Task<User?> CreateBaseUserAsync(
     string email,
     string name,
     string? phoneNumber,
     string? address,
     DateOnly? dateOfBirth,
     Guid? clinicId,
     CancellationToken cancellationToken)
    {
        var trimmedEmail = email.Trim();

        // استخدام طريقة Identity الرسمية للتحقق
        if (await _userManager.FindByEmailAsync(trimmedEmail) != null)
        {
            return null;
        }

        if (clinicId.HasValue)
        {
            var clinicExists = await _context.Clinics
                .AsNoTracking()
                .AnyAsync(c => c.ClinicId == clinicId.Value, cancellationToken);

            if (!clinicExists)
            {
                throw new InvalidOperationException("Clinic not found.");
            }
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            RegisteredAt = DateTime.UtcNow,
            Email = trimmedEmail,
            UserName = trimmedEmail, // الـ Identity سيتكفل بعمل الـ Normalize تلقائياً عند الحفظ
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            DateOfBirth = dateOfBirth,
            ClinicId = clinicId
        };
    }
    private async Task<AuthResponse> SaveUserWithRoleAsync(
        User user,
        string password,
        UserSystemRole role,
        CancellationToken cancellationToken)
    {
        var roleName = role.ToString();
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            return new AuthResponse { Success = false, Message = $"Role not found: {roleName}" };
        }

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return new AuthResponse
            {
                Success = false,
                Message = string.Join(" ", createResult.Errors.Select(error => error.Description))
            };
        }

        var addToRoleResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!addToRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return new AuthResponse
            {
                Success = false,
                Message = string.Join(" ", addToRoleResult.Errors.Select(error => error.Description))
            };
        }

        var verificationCode = new UserVerificationCode
        {
            UserId = user.Id,
            Code = GenerateVerificationCode(),
            Purpose = VerificationPurpose.EmailVerification,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false
        };

        var activeCodes = await _context.UserVerificationCodes
            .Where(vc => vc.UserId == user.Id && vc.Purpose == VerificationPurpose.EmailVerification && !vc.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var activeCode in activeCodes)
        {
            activeCode.IsUsed = true;
        }

        _context.UserVerificationCodes.Add(verificationCode);
        await _context.SaveChangesAsync(cancellationToken);

        var persistedUser = await LoadUserByIdAsync(user.Id, cancellationToken) ?? user;
        await TrySendWelcomeEmailAsync(persistedUser, role, verificationCode.Code, cancellationToken);

        return new AuthResponse
        {
            Success = true,
            Message = RegistrationVerificationMessage,
            User = MapToUserDto(persistedUser, roleName)
        };
    }
    private async Task<UserVerificationCode> CreateVerificationCodeAsync(
        Guid userId,
        VerificationPurpose purpose,
        CancellationToken cancellationToken)
    {
        var activeCodes = await _context.UserVerificationCodes
            .Where(vc => vc.UserId == userId && vc.Purpose == purpose && !vc.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var activeCode in activeCodes)
        {
            activeCode.IsUsed = true;
        }

        var verificationCode = new UserVerificationCode
        {
            UserId = userId,
            Code = GenerateVerificationCode(),
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false
        };

        _context.UserVerificationCodes.Add(verificationCode);
        await _context.SaveChangesAsync(cancellationToken);

        return verificationCode;
    }

    private async Task<User?> LoadUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = _userManager.NormalizeEmail(email.Trim());

        return await _userManager.Users
            .Include(u => u.PatientProfile)
            .Include(u => u.Clinic)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    private async Task<User?> LoadUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _userManager.Users
            .Include(u => u.PatientProfile)
            .Include(u => u.Clinic)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    private async Task<string> GetPrimaryRoleAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.FirstOrDefault() ?? string.Empty;
    }

    private UserDto MapToUserDto(User user, string role)
    {
        return new UserDto
        {
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            UserID = user.PatientProfile?.UserID,
            ClinicId = user.ClinicId,
            ClinicName = user.Clinic?.Name,
            EmailConfirmed = user.EmailConfirmed,
            Role = role
        };
    }

    private AuthResponse BuildAuthenticatedResponse(User user, string role, string message)
    {
        return new AuthResponse
        {
            Success = true,
            Message = message,
            User = MapToUserDto(user, role),
            Token = _tokenService.GenerateToken(user.Id, user.Email ?? string.Empty, role)
        };
    }

    private async Task<IList<IdentityError>> ValidatePasswordAsync(User user, string password)
    {
        var errors = new List<IdentityError>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, password);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors);
            }
        }

        return errors;
    }

    private async Task TrySendWelcomeEmailAsync(
        User user,
        UserSystemRole role,
        string verificationCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = "Welcome to HSCS";
            var body = $@"
<h2>Welcome, {user.Name} how are you?</h2>
<p>Your account has been created successfully.</p>
<p>Role: {role}</p>
<p>Email: {user.Email}</p>
<p>Your verification code is:</p>
<h3>{verificationCode}</h3>
<p>This code is valid for 15 minutes.</p>
";

            await _emailService.SendEmailAsync(user.Email!, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
        }
    }

    private async Task TrySendPasswordResetEmailAsync(User user, string verificationCode, CancellationToken cancellationToken)
    {
        try
        {
            var subject = "Password reset code";
            var body = $@"
<h2>Password reset</h2>
<p>Your password reset code is:</p>
<h3>{verificationCode}</h3>
<p>This code is valid for 15 minutes.</p>
";

            await _emailService.SendEmailAsync(user.Email!, subject, body, cancellationToken);
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
            var subject = "Password changed successfully";
            var body = @"
<h2>Password changed</h2>
<p>Your password has been changed successfully.</p>
";

            await _emailService.SendEmailAsync(user.Email!, subject, body, cancellationToken);
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
    public async Task<ApiResponse> ResendVerificationCodeAsync(
        ResendVerificationCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _exceptionHandler.ExecuteAsync(async () =>
        {
            var user = await LoadUserByEmailAsync(request.Email, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Resend verification code attempt for non-existent email: {Email}", request.Email);
                return new ApiResponse
                {
                    Success = true,
                    Message = "If your email is registered and not verified, you will receive a new verification code."
                };
            }

            if (user.EmailConfirmed)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "This email is already verified. You can login directly."
                };
            }

            // تعطيل الأكواد القديمة
            var oldCodes = await _context.UserVerificationCodes
                .Where(vc => vc.UserId == user.Id
                    && vc.Purpose == VerificationPurpose.EmailVerification
                    && !vc.IsUsed)
                .ToListAsync(cancellationToken);

            foreach (var oldCode in oldCodes)
            {
                oldCode.IsUsed = true;
            }

            var newCode = GenerateVerificationCode();
            var verificationCode = new UserVerificationCode
            {
                UserVerificationCodeId = Guid.NewGuid(),
                UserId = user.Id,
                Code = newCode,
                Purpose = VerificationPurpose.EmailVerification,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
            };

            _context.UserVerificationCodes.Add(verificationCode);
            await _context.SaveChangesAsync(cancellationToken);

            var userRole = await GetPrimaryRoleAsync(user);

            if (Enum.TryParse<UserSystemRole>(userRole, true, out var roleEnum))
            {
                await TrySendWelcomeEmailAsync(user, roleEnum, newCode, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Could not parse role '{Role}' for user {Email}, using default Patient role", userRole, user.Email);
                await TrySendWelcomeEmailAsync(user, UserSystemRole.Patient, newCode, cancellationToken);
            }

            _logger.LogInformation("New verification code sent to email: {Email}", request.Email);

            return new ApiResponse
            {
                Success = true,
                Message = "A new verification code has been sent to your email. It will expire in 15 minutes."
            };
        }, _ => new ApiResponse
        {
            Success = false,
            Message = "An error occurred. Please try again later."
        }, nameof(ResendVerificationCodeAsync));
    }
}
