using HSCSAPI.DTOs.Auth;

namespace HSCSAPI.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterDoctorAsync(RegisterDoctorRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterSecretaryAsync(RegisterSecretaryRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterAuthorizedMemberAsync(RegisterAuthorizedMemberRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterLaboratoryTechnologistAsync(RegisterLaboratoryTechnologistRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterRadiologyTechnologistAsync(RegisterRadiologyTechnologistRequest request, CancellationToken cancellationToken = default);
}
