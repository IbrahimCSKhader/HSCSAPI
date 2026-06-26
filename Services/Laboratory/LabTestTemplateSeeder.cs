using HSCSAPI.Data;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.SeedData;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Laboratory;

public class LabTestTemplateSeeder
{
    private readonly AppDbContext _dbContext;

    public LabTestTemplateSeeder(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingCodes = await _dbContext.LabTestTemplates
            .AsNoTracking()
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in LabTestTemplateSeedData.Templates.Where(x => !existing.Contains(x.Code)))
        {
            var template = new LabTestTemplate
            {
                LabTestTemplateId = Guid.NewGuid(),
                Code = definition.Code,
                Name = definition.Name,
                LoincCode = definition.LoincCode,
                SpecimenType = definition.SpecimenType,
                PreparationInstructions = definition.PreparationInstructions,
                SourceUrl = definition.SourceUrl,
                Version = definition.Version,
                IsActive = true
            };

            template.Fields = definition.Fields
                .Select((field, index) => new LabTestFieldDefinition
                {
                    LabTestFieldDefinitionId = Guid.NewGuid(),
                    LabTestTemplateId = template.LabTestTemplateId,
                    Code = field.Code,
                    Label = field.Label,
                    LoincCode = field.LoincCode,
                    ValueType = field.ValueType,
                    Unit = field.Unit,
                    IsRequired = field.IsRequired,
                    DecimalPlaces = field.DecimalPlaces,
                    ReferenceRange = field.ReferenceRange,
                    AllowedValuesJson = field.AllowedValuesJson,
                    DisplayOrder = index + 1
                })
                .ToList();

            _dbContext.LabTestTemplates.Add(template);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
