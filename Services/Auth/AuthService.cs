using System.Security.Cryptography;
using System.Text;
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
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Include(u => u.PatientProfile)
                .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                return new AuthResponse { Success = false, Message = "Invalid email or password" };
            }

            var userDto = MapToUserDto(user);
            var token = _tokenService.GenerateToken(user.UserId, user.Email, userDto.Roles);

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
            // Check if email already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
            if (existingUser != null)
            {
                return new AuthResponse { Success = false, Message = "Email already registered" };
            }

            // Verify clinic exists
            var clinic = await _context.Clinics.FindAsync(new object[] { request.ClinicId }, cancellationToken: cancellationToken);
            if (clinic == null)
            {
                return new AuthResponse { Success = false, Message = "Clinic not found" };
            }

            // Create new user
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Address = request.Address,
                DateOfBirth = request.DateOfBirth,
                PasswordHash = HashPassword(request.Password)
            };

            // Generate Patient UserID
            var userID = await _userIdGenerator.GenerateUserIdAsync(request.ClinicId, UserSystemRole.Patient, cancellationToken);

            // Create patient profile
            var genderEnum = Enum.Parse<Gender>(request.Gender);
            var patient = new Patient
            {
                UserID = userID,
                Gender = genderEnum,
                BloodType = !string.IsNullOrEmpty(request.BloodType) ? Enum.Parse<BloodType>(request.BloodType) : null,
                User = user
            };

            user.PatientProfile = patient;

            // Assign Patient role
            var patientRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Patient", cancellationToken);
            if (patientRole != null)
            {
                var userRole = new UserRole { User = user, Role = patientRole };
                user.UserRoles.Add(userRole);
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            var userDto = MapToUserDto(user);
            var token = _tokenService.GenerateToken(user.UserId, user.Email, userDto.Roles);

            return new AuthResponse
            {
                Success = true,
                Message = "Registration successful",
                User = userDto,
                Token = token
            };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
    }

    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    private bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput.Equals(hash);
    }

    private UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            UserID = user.PatientProfile?.UserID,
            Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
        };
    }
}
