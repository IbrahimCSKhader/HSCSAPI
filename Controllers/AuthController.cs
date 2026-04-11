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
}


