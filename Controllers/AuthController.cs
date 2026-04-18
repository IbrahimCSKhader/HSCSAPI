using HSCSAPI.DTOs.Auth;
using HSCSAPI.Services.Auth;
using Microsoft.AspNetCore.Mvc;
namespace HSCSAPI.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.LoginAsync(request, cancellationToken);
        
        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }

    [HttpPost("register-patient")]
    public async Task<ActionResult<AuthResponse>> RegisterPatient([FromBody] RegisterPatientRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RegisterPatientAsync(request, cancellationToken);
        
        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(RegisterPatient), result);
    }

    [HttpPost("register-doctor")]
    public async Task<ActionResult<AuthResponse>> RegisterDoctor([FromBody] RegisterDoctorRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RegisterDoctorAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(RegisterDoctor), result);
    }

    [HttpPost("register-secretary")]
    public async Task<ActionResult<AuthResponse>> RegisterSecretary([FromBody] RegisterSecretaryRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RegisterSecretaryAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(RegisterSecretary), result);
    }

    [HttpPost("register-authorized-member")]
    public async Task<ActionResult<AuthResponse>> RegisterAuthorizedMember([FromBody] RegisterAuthorizedMemberRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RegisterAuthorizedMemberAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(RegisterAuthorizedMember), result);
    }

    [HttpPost("register-laboratory-technologist")]
    public async Task<ActionResult<AuthResponse>> RegisterLaboratoryTechnologist([FromBody] RegisterLaboratoryTechnologistRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RegisterLaboratoryTechnologistAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(RegisterLaboratoryTechnologist), result);
    }

    [HttpPost("register-radiology-technologist")]
    public async Task<ActionResult<AuthResponse>> RegisterRadiologyTechnologist([FromBody] RegisterRadiologyTechnologistRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RegisterRadiologyTechnologistAsync(request, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(RegisterRadiologyTechnologist), result);
    }
}


