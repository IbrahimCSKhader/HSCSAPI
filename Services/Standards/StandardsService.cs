using HSCSAPI.Data;
using HSCSAPI.DTOs.Standards;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Standards;

public class StandardsService : IStandardsService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const string Icd10CodeSystem = "ICD-10";
    private const string RadiologyStandardSystem = "RadLexPlaybook";

    private static readonly string[] PreferredLabCodes =
    [
        "58410-2",
        "24331-1",
        "4548-4",
        "3016-3",
        "24356-8"
    ];

    private readonly AppDbContext _dbContext;

    public StandardsService(AppDbContext dbContext, IWebHostEnvironment? _ = null)
    {
        _dbContext = dbContext;
    }

    public async Task<StandardPagedResponse<LoincCodeResponse>> SearchLoincAsync(
        string? query,
        int page,
        int pageSize,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var trimmedQuery = NormalizeQuery(query);

        var recordsQuery = _dbContext.LoincCodes.AsNoTracking();
        if (activeOnly)
        {
            recordsQuery = recordsQuery.Where(record =>
                record.IsActive
                && (record.Status == null || record.Status == "ACTIVE"));
        }

        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            var loweredQuery = trimmedQuery.ToLower();
            recordsQuery = recordsQuery.Where(record =>
                record.Code.ToLower().Contains(loweredQuery)
                || (record.LongCommonName != null && record.LongCommonName.ToLower().Contains(loweredQuery))
                || (record.ShortName != null && record.ShortName.ToLower().Contains(loweredQuery))
                || (record.Component != null && record.Component.ToLower().Contains(loweredQuery))
                || (record.Class != null && record.Class.ToLower().Contains(loweredQuery)));
        }

        var projected = recordsQuery.Select(record => new LoincCodeResponse
        {
            Code = record.Code,
            Display = record.LongCommonName ?? record.ShortName ?? record.Component ?? record.Code,
            Component = record.Component,
            Property = record.Property,
            TimeAspect = record.TimeAspect,
            System = record.System,
            ScaleType = record.ScaleType,
            MethodType = record.MethodType,
            Class = record.Class,
            ClassType = record.ClassType == null ? null : record.ClassType.Value.ToString(),
            LongCommonName = record.LongCommonName,
            ShortName = record.ShortName,
            Status = record.Status
        });

        var ordered = OrderLoinc(projected, trimmedQuery);
        return await PageAsync(ordered, normalizedPage, normalizedPageSize, cancellationToken);
    }

    public async Task<LoincCodeResponse?> GetLoincByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return null;
        }

        return await _dbContext.LoincCodes
            .AsNoTracking()
            .Where(record => record.Code == normalizedCode)
            .Select(record => new LoincCodeResponse
            {
                Code = record.Code,
                Display = record.LongCommonName ?? record.ShortName ?? record.Component ?? record.Code,
                Component = record.Component,
                Property = record.Property,
                TimeAspect = record.TimeAspect,
                System = record.System,
                ScaleType = record.ScaleType,
                MethodType = record.MethodType,
                Class = record.Class,
                ClassType = record.ClassType == null ? null : record.ClassType.Value.ToString(),
                LongCommonName = record.LongCommonName,
                ShortName = record.ShortName,
                Status = record.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StandardPagedResponse<Icd10CodeResponse>> SearchIcd10Async(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var trimmedQuery = NormalizeQuery(query);

        var recordsQuery = _dbContext.DiagnosisCodes
            .AsNoTracking()
            .Where(record => record.IsActive && record.CodeSystem == Icd10CodeSystem);

        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            var loweredQuery = trimmedQuery.ToLower();
            recordsQuery = recordsQuery.Where(record =>
                record.Code.ToLower().Contains(loweredQuery)
                || (record.DisplayCode != null && record.DisplayCode.ToLower().Contains(loweredQuery))
                || record.Name.ToLower().Contains(loweredQuery)
                || (record.Description != null && record.Description.ToLower().Contains(loweredQuery)));
        }

        var projected = recordsQuery.Select(record => new Icd10CodeResponse
        {
            Code = record.DisplayCode ?? record.Code,
            Description = record.Description ?? record.Name,
            Display = (record.DisplayCode ?? record.Code) + " - " + record.Name
        });

        var ordered = OrderByExactOrPrefix(projected, trimmedQuery);
        return await PageAsync(ordered, normalizedPage, normalizedPageSize, cancellationToken);
    }

    public async Task<Icd10CodeResponse?> GetIcd10ByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return null;
        }

        return await _dbContext.DiagnosisCodes
            .AsNoTracking()
            .Where(record => record.IsActive
                && record.CodeSystem == Icd10CodeSystem
                && (record.Code == normalizedCode || record.DisplayCode == normalizedCode))
            .Select(record => new Icd10CodeResponse
            {
                Code = record.DisplayCode ?? record.Code,
                Description = record.Description ?? record.Name,
                Display = (record.DisplayCode ?? record.Code) + " - " + record.Name
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StandardPagedResponse<RadiologyPlaybookResponse>> SearchRadiologyPlaybookAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var trimmedQuery = NormalizeQuery(query);

        var recordsQuery = _dbContext.RadiologyExamCatalogs
            .AsNoTracking()
            .Where(record => record.IsActive && record.StandardSystem == RadiologyStandardSystem);

        if (!string.IsNullOrWhiteSpace(trimmedQuery))
        {
            var loweredQuery = trimmedQuery.ToLower();
            recordsQuery = recordsQuery.Where(record =>
                record.Rpid.ToLower().Contains(loweredQuery)
                || (record.LetterCode != null && record.LetterCode.ToLower().Contains(loweredQuery))
                || (record.ShortName != null && record.ShortName.ToLower().Contains(loweredQuery))
                || (record.LongName != null && record.LongName.ToLower().Contains(loweredQuery))
                || (record.Modality != null && record.Modality.ToLower().Contains(loweredQuery))
                || (record.BodyRegion != null && record.BodyRegion.ToLower().Contains(loweredQuery)));
        }

        var projected = recordsQuery.Select(record => new RadiologyPlaybookResponse
        {
            Rpid = record.Rpid,
            Display = record.LongName ?? record.ShortName ?? record.LetterCode ?? record.Rpid,
            LetterCode = record.LetterCode,
            ShortName = record.ShortName,
            LongName = record.LongName,
            Modality = record.Modality,
            PlaybookType = record.PlaybookType,
            BodyRegion = record.BodyRegion,
            Laterality = record.Laterality,
            ReasonForExam = record.ReasonForExam
        });

        var ordered = projected
            .OrderBy(record => !string.IsNullOrWhiteSpace(trimmedQuery) && record.Rpid == trimmedQuery
                ? 0
                : !string.IsNullOrWhiteSpace(trimmedQuery) && record.Display.StartsWith(trimmedQuery)
                    ? 1
                    : 2)
            .ThenBy(record => record.Display);

        return await PageAsync(ordered, normalizedPage, normalizedPageSize, cancellationToken);
    }

    public async Task<RadiologyPlaybookResponse?> GetRadiologyPlaybookByRpidAsync(
        string rpid,
        CancellationToken cancellationToken = default)
    {
        var normalizedRpid = NormalizeCode(rpid);
        if (string.IsNullOrWhiteSpace(normalizedRpid))
        {
            return null;
        }

        return await _dbContext.RadiologyExamCatalogs
            .AsNoTracking()
            .Where(record => record.IsActive
                && record.StandardSystem == RadiologyStandardSystem
                && record.Rpid == normalizedRpid)
            .Select(record => new RadiologyPlaybookResponse
            {
                Rpid = record.Rpid,
                Display = record.LongName ?? record.ShortName ?? record.LetterCode ?? record.Rpid,
                LetterCode = record.LetterCode,
                ShortName = record.ShortName,
                LongName = record.LongName,
                Modality = record.Modality,
                PlaybookType = record.PlaybookType,
                BodyRegion = record.BodyRegion,
                Laterality = record.Laterality,
                ReasonForExam = record.ReasonForExam
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ImagingTypeResponse>> GetImagingTypesAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var trimmedQuery = NormalizeQuery(query);

        var modalities = await _dbContext.RadiologyExamCatalogs
            .AsNoTracking()
            .Where(record => record.IsActive
                && record.StandardSystem == RadiologyStandardSystem
                && record.Modality != null
                && record.Modality != string.Empty)
            .Select(record => record.Modality!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return modalities
            .Select(ToImagingType)
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(item => Matches(item.Code, trimmedQuery)
                || Matches(item.Display, trimmedQuery)
                || Matches(item.Modality, trimmedQuery))
            .OrderBy(item => ExactOrPrefixRank(item.Code, item.Display, trimmedQuery))
            .ThenBy(item => item.Display)
            .ToList();
    }

    public async Task<StandardPagedResponse<StandardSearchItemResponse>> SearchAllAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var trimmedQuery = NormalizeQuery(query);

        var loinc = await SearchLoincAsync(trimmedQuery, 1, MaxPageSize, activeOnly: true, cancellationToken);
        var icd10 = await SearchIcd10Async(trimmedQuery, 1, MaxPageSize, cancellationToken);
        var radiology = await SearchRadiologyPlaybookAsync(trimmedQuery, 1, MaxPageSize, cancellationToken);

        var combined = loinc.Items
            .Select(item => new StandardSearchItemResponse
            {
                Source = "LOINC",
                Code = item.Code,
                Display = item.Display,
                Category = item.Class
            })
            .Concat(icd10.Items.Select(item => new StandardSearchItemResponse
            {
                Source = "ICD10",
                Code = item.Code,
                Display = item.Display,
                Category = "Diagnosis"
            }))
            .Concat(radiology.Items.Select(item => new StandardSearchItemResponse
            {
                Source = "RADLEX_PLAYBOOK",
                Code = item.Rpid,
                Display = item.Display,
                Category = item.Modality
            }))
            .OrderBy(item => item.Source)
            .ThenBy(item => ExactOrPrefixRank(item.Code, item.Display, trimmedQuery))
            .ThenBy(item => item.Display);

        return Page(combined, normalizedPage, normalizedPageSize);
    }

    private static IQueryable<LoincCodeResponse> OrderLoinc(
        IQueryable<LoincCodeResponse> source,
        string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return source
                .OrderBy(record => record.Code == PreferredLabCodes[0]
                    ? 0
                    : record.Code == PreferredLabCodes[1]
                        ? 1
                        : record.Code == PreferredLabCodes[2]
                            ? 2
                            : record.Code == PreferredLabCodes[3]
                                ? 3
                                : record.Code == PreferredLabCodes[4]
                                    ? 4
                                    : 5)
                .ThenBy(record => record.Display);
        }

        return source
            .OrderBy(record => record.Code == query
                ? 0
                : record.Code.StartsWith(query)
                    ? 1
                    : record.Display.StartsWith(query)
                        ? 2
                        : 3)
            .ThenBy(record => record.Display);
    }

    private static IQueryable<Icd10CodeResponse> OrderByExactOrPrefix(
        IQueryable<Icd10CodeResponse> source,
        string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return source.OrderBy(record => record.Code);
        }

        return source
            .OrderBy(record => record.Code == query
                ? 0
                : record.Code.StartsWith(query)
                    ? 1
                    : record.Display.StartsWith(query)
                        ? 2
                        : 3)
            .ThenBy(record => record.Code);
    }

    private static ImagingTypeResponse ToImagingType(string modality)
    {
        var normalized = modality.Trim().ToUpperInvariant();
        return normalized switch
        {
            "XR" => new ImagingTypeResponse { Code = "XRAY", Display = "XRAY - X-Ray", Modality = "XR" },
            "MR" => new ImagingTypeResponse { Code = "MRI", Display = "MRI - MRI", Modality = "MR" },
            "CT" => new ImagingTypeResponse { Code = "CT", Display = "CT - CT Scan", Modality = "CT" },
            "ULTRASOUND" => new ImagingTypeResponse { Code = "US", Display = "US - Ultrasound", Modality = "ULTRASOUND" },
            "MAMMOGRAPHY" => new ImagingTypeResponse { Code = "MAMMO", Display = "MAMMO - Mammography", Modality = "MAMMOGRAPHY" },
            _ => new ImagingTypeResponse
            {
                Code = normalized,
                Display = $"{normalized} - {ToFriendlyDisplay(normalized)}",
                Modality = normalized
            }
        };
    }

    private static string ToFriendlyDisplay(string value)
    {
        return string.Join(
            ' ',
            value
                .Replace('&', ' ')
                .Replace('-', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length == 0 ? word : word[0] + word[1..].ToLowerInvariant()));
    }

    private static bool Matches(string? value, string? query)
    {
        return string.IsNullOrWhiteSpace(query)
            || (!string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static int ExactOrPrefixRank(string code, string? display, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 2;
        }

        if (code.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(display)
            && display.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string? NormalizeQuery(string? query)
    {
        return string.IsNullOrWhiteSpace(query) ? null : query.Trim();
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim();
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        var normalizedPage = page <= 0 ? DefaultPage : page;
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return (normalizedPage, normalizedPageSize);
    }

    private static async Task<StandardPagedResponse<T>> PageAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new StandardPagedResponse<T>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = CalculateTotalPages(totalCount, pageSize),
            Items = items
        };
    }

    private static StandardPagedResponse<T> Page<T>(IEnumerable<T> source, int page, int pageSize)
    {
        var items = source.ToList();
        var totalCount = items.Count;

        return new StandardPagedResponse<T>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = CalculateTotalPages(totalCount, pageSize),
            Items = items
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        };
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
