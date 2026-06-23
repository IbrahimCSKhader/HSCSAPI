namespace HSCSAPI.Models.Standards;

public class LoincCode
{
    public string Code { get; set; } = string.Empty;
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
    public string? VersionFirstReleased { get; set; }
    public string? VersionLastChanged { get; set; }

}
