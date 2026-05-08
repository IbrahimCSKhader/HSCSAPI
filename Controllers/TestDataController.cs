using HSCSAPI.DTOs.TestData;
using HSCSAPI.Services.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/test-data")]
public class TestDataController : ControllerBase
{
    private readonly OneTimeClinicTestSeedService _oneTimeClinicTestSeedService;

    public TestDataController(OneTimeClinicTestSeedService oneTimeClinicTestSeedService)
    {
        _oneTimeClinicTestSeedService = oneTimeClinicTestSeedService;
    }

    [HttpPost("seed-clinics-once")]
    [AllowAnonymous]
    public async Task<ActionResult<ClinicTestDataSeedResponse>> SeedClinicsOnce(CancellationToken cancellationToken)
    {
        var result = await _oneTimeClinicTestSeedService.SeedOnceAsync(cancellationToken);

        if (!result.Success)
        {
            return Conflict(result);
        }

        return Ok(result);
    }
}
