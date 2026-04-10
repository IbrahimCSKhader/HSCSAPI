using HSCSAPI.DTOs.Auth;

namespace HSCSAPI.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request, CancellationToken cancellationToken = default);
}
