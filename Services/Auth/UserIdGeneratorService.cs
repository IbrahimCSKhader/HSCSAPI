using HSCSAPI.Data;
using HSCSAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Auth;

public class UserIdGeneratorService
{
    private readonly AppDbContext _context;

    public UserIdGeneratorService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUserIdAsync(Guid clinicId, UserSystemRole role, CancellationToken cancellationToken = default)
    {
        var roleChar = role switch
        {
            UserSystemRole.Doctor => "D",
            UserSystemRole.Secretary => "S",
            UserSystemRole.Patient => "P",
            UserSystemRole.LaboratoryTechnologist => "L",
            _ => throw new ArgumentException($"Unsupported role: {role}")
        };

        var clinicCode = clinicId.ToString("N")[..4].ToUpperInvariant();
        var nextSequence = await GetNextSequenceAsync(clinicCode, roleChar, cancellationToken);
        var sequenceNumber = nextSequence.ToString("D6");

        return $"{clinicCode}{roleChar}{sequenceNumber}";
    }

    private async Task<int> GetNextSequenceAsync(string clinicCode, string roleChar, CancellationToken cancellationToken = default)
    {
        var existingIds = await _context.Patients
            .Where(p => p.UserID.StartsWith(clinicCode + roleChar))
            .Select(p => p.UserID)
            .ToListAsync(cancellationToken);

        if (!existingIds.Any())
        {
            return 1;
        }

        var maxSequence = existingIds
            .Select(id => int.TryParse(id.Substring(clinicCode.Length + roleChar.Length), out var seq) ? seq : 0)
            .Max();

        return maxSequence + 1;
    }
}
