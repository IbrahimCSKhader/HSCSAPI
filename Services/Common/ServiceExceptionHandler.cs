namespace HSCSAPI.Services.Common;

public class ServiceExceptionHandler : IServiceExceptionHandler
{
    private readonly ILogger<ServiceExceptionHandler> _logger;

    public ServiceExceptionHandler(ILogger<ServiceExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> ExecuteAsync<TResponse>(
        Func<Task<TResponse>> operation,
        Func<Exception, TResponse> failureResponseFactory,
        string operationName)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{OperationName} failed.", operationName);
            return failureResponseFactory(ex);
        }
    }
}
