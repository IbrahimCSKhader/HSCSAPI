using HSCSAPI.DTOs.Standards;

namespace HSCSAPI.Services.Standards;

public interface IStandardsService
{
    Task<StandardPagedResponse<LoincCodeResponse>> SearchLoincAsync(
        string? query,
        int page,
        int pageSize,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    Task<LoincCodeResponse?> GetLoincByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<StandardPagedResponse<Icd10CodeResponse>> SearchIcd10Async(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Icd10CodeResponse?> GetIcd10ByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<StandardPagedResponse<RadiologyPlaybookResponse>> SearchRadiologyPlaybookAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<RadiologyPlaybookResponse?> GetRadiologyPlaybookByRpidAsync(
        string rpid,
        CancellationToken cancellationToken = default);

    Task<StandardPagedResponse<StandardSearchItemResponse>> SearchAllAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
