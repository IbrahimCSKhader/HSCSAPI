using System.Security.Claims;
using HSCSAPI.DTOs.Secretary;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Secretaries;

public interface ISecretariesService
{
    Task<ActionResult<List<SecretaryResponse>>> GetAllAsync(Guid? clinicId, CancellationToken cancellationToken = default);
    Task<ActionResult<List<SecretaryResponse>>> GetAvailableAsync(CancellationToken cancellationToken = default);
    Task<ActionResult<List<SecretaryResponse>>> GetByClinicAsync(Guid clinicId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<List<SecretaryResponse>>> GetMyClinicSecretariesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryResponse>> AssignToClinicAsync(Guid secretaryId, AssignSecretaryToClinicRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryResponse>> RemoveFromClinicAsync(Guid secretaryId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
