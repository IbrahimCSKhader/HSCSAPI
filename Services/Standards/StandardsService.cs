using System.Collections.Concurrent;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Standards;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace HSCSAPI.Services.Standards;

public class StandardsService : IStandardsService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private static readonly string[] PreferredLabCodes =
    [
        "58410-2",
        "24331-1",
        "4548-4",
        "3016-3",
        "24356-8"
    ];

    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<LoincCodeResponse>>> LoincCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<Icd10CodeResponse>>> Icd10Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<RadiologyPlaybookResponse>>> RadiologyCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public StandardsService(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
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

        var dbResult = await TrySearchLoincTableAsync(trimmedQuery, normalizedPage, normalizedPageSize, activeOnly, cancellationToken);
        if (dbResult is not null)
        {
            return dbResult;
        }

        var records = GetLoincFileRecords();
        var filtered = records
            .Where(record => !activeOnly || IsActive(record.Status))
            .Where(record => MatchesLoinc(record, trimmedQuery));

        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            filtered = filtered
                .OrderBy(record => PreferredLabRank(record.Code))
                .ThenBy(record => record.Display);
        }
        else
        {
            filtered = filtered
                .OrderBy(record => ExactOrPrefixRank(record, trimmedQuery))
                .ThenBy(record => record.Display);
        }

        return Page(filtered, normalizedPage, normalizedPageSize);
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

        var dbResult = await TryGetLoincFromTableAsync(normalizedCode, cancellationToken);
        if (dbResult is not null)
        {
            return dbResult;
        }

        return GetLoincFileRecords()
            .FirstOrDefault(record => record.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
    }

    public Task<StandardPagedResponse<Icd10CodeResponse>> SearchIcd10Async(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var trimmedQuery = NormalizeQuery(query);
        var records = GetIcd10FileRecords()
            .Where(record => Matches(record.Code, trimmedQuery)
                || Matches(record.Description, trimmedQuery)
                || Matches(record.Display, trimmedQuery))
            .OrderBy(record => ExactOrPrefixRank(record.Code, record.Display, trimmedQuery))
            .ThenBy(record => record.Code);

        return Task.FromResult(Page(records, normalizedPage, normalizedPageSize));
    }

    public Task<Icd10CodeResponse?> GetIcd10ByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return Task.FromResult<Icd10CodeResponse?>(null);
        }

        var record = GetIcd10FileRecords()
            .FirstOrDefault(item => item.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(record);
    }

    public Task<StandardPagedResponse<RadiologyPlaybookResponse>> SearchRadiologyPlaybookAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var trimmedQuery = NormalizeQuery(query);
        var records = GetRadiologyFileRecords()
            .Where(record => Matches(record.Rpid, trimmedQuery)
                || Matches(record.LetterCode, trimmedQuery)
                || Matches(record.ShortName, trimmedQuery)
                || Matches(record.LongName, trimmedQuery)
                || Matches(record.Modality, trimmedQuery)
                || Matches(record.BodyRegion, trimmedQuery))
            .OrderBy(record => ExactOrPrefixRank(record.Rpid, record.Display, trimmedQuery))
            .ThenBy(record => record.Display);

        return Task.FromResult(Page(records, normalizedPage, normalizedPageSize));
    }

    public Task<RadiologyPlaybookResponse?> GetRadiologyPlaybookByRpidAsync(
        string rpid,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRpid = NormalizeCode(rpid);
        if (string.IsNullOrWhiteSpace(normalizedRpid))
        {
            return Task.FromResult<RadiologyPlaybookResponse?>(null);
        }

        var record = GetRadiologyFileRecords()
            .FirstOrDefault(item => item.Rpid.Equals(normalizedRpid, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(record);
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

    private async Task<StandardPagedResponse<LoincCodeResponse>?> TrySearchLoincTableAsync(
        string? query,
        int page,
        int pageSize,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await _dbContext.LoincCodes.AsNoTracking().AnyAsync(cancellationToken))
            {
                return null;
            }

            var recordsQuery = _dbContext.LoincCodes.AsNoTracking();
            if (activeOnly)
            {
                recordsQuery = recordsQuery.Where(record => record.Status == "ACTIVE");
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                recordsQuery = recordsQuery.Where(record =>
                    record.Code.Contains(query)
                    || (record.LongCommonName != null && record.LongCommonName.Contains(query))
                    || (record.ShortName != null && record.ShortName.Contains(query))
                    || (record.Component != null && record.Component.Contains(query)));
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
                ClassType = record.ClassType,
                LongCommonName = record.LongCommonName,
                ShortName = record.ShortName,
                Status = record.Status
            });

            var totalCount = await projected.CountAsync(cancellationToken);
            var items = await projected
                .OrderBy(record => record.Display)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new StandardPagedResponse<LoincCodeResponse>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = CalculateTotalPages(totalCount, pageSize),
                Items = items
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<LoincCodeResponse?> TryGetLoincFromTableAsync(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.LoincCodes
                .AsNoTracking()
                .Where(record => record.Code == code)
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
                    ClassType = record.ClassType,
                    LongCommonName = record.LongCommonName,
                    ShortName = record.ShortName,
                    Status = record.Status
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private IReadOnlyList<LoincCodeResponse> GetLoincFileRecords()
    {
        var path = ResolveFilePath("LoincTableCore.csv");
        return LoincCache.GetOrAdd(path, filePath => new Lazy<IReadOnlyList<LoincCodeResponse>>(() => LoadLoinc(filePath))).Value;
    }

    private IReadOnlyList<Icd10CodeResponse> GetIcd10FileRecords()
    {
        var path = ResolveFilePath("ICD 10.csv");
        return Icd10Cache.GetOrAdd(path, filePath => new Lazy<IReadOnlyList<Icd10CodeResponse>>(() => LoadIcd10(filePath))).Value;
    }

    private IReadOnlyList<RadiologyPlaybookResponse> GetRadiologyFileRecords()
    {
        var path = ResolveFilePath("core-playbook-dev.csv");
        return RadiologyCache.GetOrAdd(path, filePath => new Lazy<IReadOnlyList<RadiologyPlaybookResponse>>(() => LoadRadiology(filePath))).Value;
    }

    private string ResolveFilePath(string fileName)
    {
        return Path.Combine(_environment.ContentRootPath, "Files", fileName);
    }

    private static IReadOnlyList<LoincCodeResponse> LoadLoinc(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var parser = CreateParser(path);
        var headers = parser.ReadFields() ?? [];
        var headerIndex = BuildHeaderIndex(headers);
        var records = new List<LoincCodeResponse>();

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length == 0)
            {
                continue;
            }

            var code = GetField(fields, headerIndex, "LOINC_NUM");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var longCommonName = GetField(fields, headerIndex, "LONG_COMMON_NAME");
            var shortName = GetField(fields, headerIndex, "SHORTNAME");
            var component = GetField(fields, headerIndex, "COMPONENT");

            records.Add(new LoincCodeResponse
            {
                Code = code,
                Display = FirstNonBlank(longCommonName, shortName, component, code),
                Component = component,
                Property = GetField(fields, headerIndex, "PROPERTY"),
                TimeAspect = GetField(fields, headerIndex, "TIME_ASPCT"),
                System = GetField(fields, headerIndex, "SYSTEM"),
                ScaleType = GetField(fields, headerIndex, "SCALE_TYP"),
                MethodType = GetField(fields, headerIndex, "METHOD_TYP"),
                Class = GetField(fields, headerIndex, "CLASS"),
                ClassType = GetField(fields, headerIndex, "CLASSTYPE"),
                LongCommonName = longCommonName,
                ShortName = shortName,
                Status = GetField(fields, headerIndex, "STATUS")
            });
        }

        return records;
    }

    private static IReadOnlyList<Icd10CodeResponse> LoadIcd10(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var parser = CreateParser(path);
        var records = new List<Icd10CodeResponse>();

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length < 2)
            {
                continue;
            }

            var code = fields[0].Trim();
            var description = fields[1].Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            records.Add(new Icd10CodeResponse
            {
                Code = code,
                Description = description,
                Display = $"{code} - {description}"
            });
        }

        return records;
    }

    private static IReadOnlyList<RadiologyPlaybookResponse> LoadRadiology(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var parser = CreateParser(path);
        var headers = parser.ReadFields() ?? [];
        var headerIndex = BuildHeaderIndex(headers);
        var records = new List<RadiologyPlaybookResponse>();

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length == 0)
            {
                continue;
            }

            var rpid = GetField(fields, headerIndex, "RPID");
            if (string.IsNullOrWhiteSpace(rpid))
            {
                continue;
            }

            var longName = GetField(fields, headerIndex, "LONG_NAME");
            var shortName = GetField(fields, headerIndex, "SHORT_NAME");
            var letterCode = GetField(fields, headerIndex, "LETTER_CODE");

            records.Add(new RadiologyPlaybookResponse
            {
                Rpid = rpid,
                Display = FirstNonBlank(longName, shortName, letterCode, rpid),
                LetterCode = letterCode,
                ShortName = shortName,
                LongName = longName,
                Modality = GetField(fields, headerIndex, "MODALITY"),
                PlaybookType = GetField(fields, headerIndex, "PLAYBOOK_TYPE"),
                BodyRegion = GetField(fields, headerIndex, "BODY_REGION"),
                Laterality = GetField(fields, headerIndex, "LATERALITY"),
                ReasonForExam = GetField(fields, headerIndex, "REASON_FOR_EXAM")
            });
        }

        return records;
    }

    private static TextFieldParser CreateParser(string path)
    {
        var parser = new TextFieldParser(path)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");
        return parser;
    }

    private static Dictionary<string, int> BuildHeaderIndex(string[] headers)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim();
            if (!string.IsNullOrWhiteSpace(header) && !index.ContainsKey(header))
            {
                index[header] = i;
            }
        }

        return index;
    }

    private static string? GetField(string[] fields, IReadOnlyDictionary<string, int> headerIndex, string headerName)
    {
        return headerIndex.TryGetValue(headerName, out var index) && index < fields.Length
            ? Clean(fields[index])
            : null;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static bool MatchesLoinc(LoincCodeResponse record, string? query)
    {
        return Matches(record.Code, query)
            || Matches(record.Display, query)
            || Matches(record.Component, query)
            || Matches(record.ShortName, query)
            || Matches(record.LongCommonName, query)
            || Matches(record.Class, query);
    }

    private static bool Matches(string? value, string? query)
    {
        return string.IsNullOrWhiteSpace(query)
            || (!string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static int ExactOrPrefixRank(LoincCodeResponse record, string? query)
    {
        return ExactOrPrefixRank(record.Code, record.Display, query);
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

    private static int PreferredLabRank(string code)
    {
        var index = Array.FindIndex(PreferredLabCodes, preferred => preferred.Equals(code, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : PreferredLabCodes.Length;
    }

    private static bool IsActive(string? status)
    {
        return status?.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) != false;
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
