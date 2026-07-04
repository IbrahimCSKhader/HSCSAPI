using System.Reflection;
using HSCSAPI.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace HSCSAPI.Tests;

public class RequestedEndpointBatch3Tests
{
    [Fact]
    public void ReportDownloadEndpoint_IsGet() => AssertRoute<SecretariesController>(nameof(SecretariesController.DownloadReport), "GET", "my-clinic/reports/{reportId:guid}/files/{fileId:guid}");
    [Fact]
    public void LabRequestDetailEndpoint_IsGet() => AssertRoute<LaboratoryTestsController>(nameof(LaboratoryTestsController.GetMyRequest), "GET", "my-requests/{labTestRequestId:guid}");
    [Fact]
    public void LabPasswordEndpoint_IsPut() => AssertRoute<LaboratoryTechnologistsController>(nameof(LaboratoryTechnologistsController.ChangeMyPassword), "PUT", "me/password");
    [Fact]
    public void RadiologyPasswordEndpoint_IsPut() => AssertRoute<RadiologyTechnologistsController>(nameof(RadiologyTechnologistsController.ChangeMyPassword), "PUT", "me/password");
    [Fact]
    public void AuthorizedMemberUpdateEndpoint_IsPut() => AssertRoute<AuthorizedMembersController>(nameof(AuthorizedMembersController.UpdateMyProfile), "PUT", "me");

    private static void AssertRoute<T>(string methodName, string verb, string template)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        var route = Assert.Single(method!.GetCustomAttributes<HttpMethodAttribute>(true));
        Assert.Contains(verb, route.HttpMethods);
        Assert.Equal(template, route.Template);
    }
}
