namespace HSCSAPI.Services.Common;

public interface IServiceExceptionHandler
{
    Task<TResponse> ExecuteAsync<TResponse>(
        Func<Task<TResponse>> operation,
        Func<Exception, TResponse> failureResponseFactory,
        string operationName);
}
