using System.Reflection;
using HSCSAPI.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace HSCSAPI.Tests;

public class RequestedEndpointBatch4Tests
{
    [Fact]
    public void ImagingWorkQueueEndpoint_IsGet() => AssertRoute<ImagingTestsController>(nameof(ImagingTestsController.GetMyRequests), "GET", "my-requests");
    [Fact]
    public void ImagingDetailEndpoint_IsGet() => AssertRoute<ImagingTestsController>(nameof(ImagingTestsController.GetMyRequest), "GET", "my-requests/{imagingTestRequestId:guid}");
    [Fact]
    public void ImagingResultUploadEndpoint_IsPost() => AssertRoute<ImagingTestsController>(nameof(ImagingTestsController.UploadResult), "POST", "my-requests/{imagingTestRequestId:guid}/results");
    [Fact]
    public void ImagingResultFileEndpoint_IsGet() => AssertRoute<ImagingTestsController>(nameof(ImagingTestsController.DownloadResultFile), "GET", "my-requests/{imagingTestRequestId:guid}/result-file");
    [Fact]
    public void LaboratoryResultFileUploadEndpoint_IsPost() => AssertRoute<LaboratoryTestsController>(nameof(LaboratoryTestsController.UploadResultFile), "POST", "my-requests/{labTestRequestId:guid}/result-file");
    [Fact]
    public void LaboratoryResultFileDownloadEndpoint_IsGet() => AssertRoute<LaboratoryTestsController>(nameof(LaboratoryTestsController.DownloadResultFile), "GET", "my-requests/{labTestRequestId:guid}/result-file");
    [Fact]
    public void AuthorizedMemberPasswordEndpoint_IsPut() => AssertRoute<AuthorizedMembersController>(nameof(AuthorizedMembersController.ChangeMyPassword), "PUT", "me/password");

    private static void AssertRoute<T>(string methodName, string verb, string template)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        var route = Assert.Single(method!.GetCustomAttributes<HttpMethodAttribute>(true));
        Assert.Contains(verb, route.HttpMethods);
        Assert.Equal(template, route.Template);
    }
}
