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

    public async Task<string> GenerateUserIdAsync(int clinicId, UserSystemRole role, CancellationToken cancellationToken = default)
    {
        // Role character mapping
        var roleChar = role switch
        {
            UserSystemRole.Doctor => "D",
            UserSystemRole.Secretary => "S",
            UserSystemRole.Patient => "P",
            UserSystemRole.LaboratoryTechnologist => "L",
            _ => throw new ArgumentException($"Unsupported role: {role}")
        };

        // Format clinic code as 2 digits
        var clinicCode = clinicId.ToString("D2");

        // Get the next sequence number for this role in this clinic
        var nextSequence = await GetNextSequenceAsync(clinicId, roleChar, cancellationToken);
        var sequenceNumber = nextSequence.ToString("D6");

        // Combine: [ClinicCode][RoleChar][SequenceNumber]
        return $"{clinicCode}{roleChar}{sequenceNumber}";
    }

    private async Task<int> GetNextSequenceAsync(int clinicId, string roleChar, CancellationToken cancellationToken = default)
    {
        // Get the highest existing sequence number for this clinic and role
        var existingIds = await _context.Patients
            .Where(p => p.UserID.StartsWith(clinicId.ToString("D2") + roleChar))
            .Select(p => p.UserID)
            .ToListAsync(cancellationToken);

        if (!existingIds.Any())
            return 1;

        // Extract sequence numbers from existing IDs and get the max
        var maxSequence = existingIds
            .Select(id => int.TryParse(id.Substring(3), out var seq) ? seq : 0)
            .Max();

        return maxSequence + 1;
    }
}
