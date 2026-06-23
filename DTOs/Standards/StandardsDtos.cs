namespace HSCSAPI.DTOs.Standards;

public class StandardPagedResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = [];
}

public class StandardSearchItemResponse
{
    public string Source { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public class LoincCodeResponse
{
    public string Code { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string? Component { get; set; }
    public string? Property { get; set; }
    public string? TimeAspect { get; set; }
    public string? System { get; set; }
    public string? ScaleType { get; set; }
    public string? MethodType { get; set; }
    public string? Class { get; set; }
    public string? ClassType { get; set; }
    public string? LongCommonName { get; set; }
    public string? ShortName { get; set; }
    public string? Status { get; set; }
}

public class Icd10CodeResponse
{
    public string Code { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RadiologyPlaybookResponse
{
    public string Rpid { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string? LetterCode { get; set; }
    public string? ShortName { get; set; }
    public string? LongName { get; set; }
    public string? Modality { get; set; }
    public string? PlaybookType { get; set; }
    public string? BodyRegion { get; set; }
    public string? Laterality { get; set; }
    public string? ReasonForExam { get; set; }
}
