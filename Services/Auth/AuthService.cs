using BCrypt.Net;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly UserIdGeneratorService _userIdGenerator;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext context, UserIdGeneratorService userIdGenerator, ITokenService tokenService)
    {
        _context = context;
        _userIdGenerator = userIdGenerator;
        _tokenService = tokenService;
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

            var userDto = MapToUserDto(user);
            var token = _tokenService.GenerateToken(user.UserId, user.Email, userDto.Role);

            return new AuthResponse
            {
                Success = true,
                Message = "Login successful",
                User = userDto,
                Token = token
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
            var clinic = await _context.Clinics.FindAsync(new object[] { request.ClinicId }, cancellationToken: cancellationToken);
            if (clinic == null)
            {
                return new AuthResponse { Success = false, Message = "Clinic not found" };
            }

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
                        ClinicId = request.ClinicId,
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

        return BuildSuccessResponse(user, message);
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
}
