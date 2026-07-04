using System.Reflection;
using HSCSAPI.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace HSCSAPI.Tests;

public class RequestedEndpointBatch2Tests
{
    [Fact]
    public void SecretaryAvailabilityListEndpoint_IsGet() => AssertRoute(nameof(SecretariesController.GetDoctorAvailabilitySlots), "GET", "my-clinic/doctors/{doctorId:guid}/availability-slots");

    [Fact]
    public void SecretaryAvailabilityCreateEndpoint_IsPost() => AssertRoute(nameof(SecretariesController.CreateDoctorAvailabilitySlot), "POST", "my-clinic/doctors/{doctorId:guid}/availability-slots");

    [Fact]
    public void SecretaryAvailabilityDeleteEndpoint_IsDelete() => AssertRoute(nameof(SecretariesController.DeleteDoctorAvailabilitySlot), "DELETE", "my-clinic/doctors/{doctorId:guid}/availability-slots/{slotId:guid}");

    [Fact]
    public void SecretaryReportsListEndpoint_IsGet() => AssertRoute(nameof(SecretariesController.GetReports), "GET", "my-clinic/reports");

    [Fact]
    public void SecretaryReportGenerationEndpoint_IsPost() => AssertRoute(nameof(SecretariesController.GenerateReport), "POST", "my-clinic/reports");

    private static void AssertRoute(string methodName, string verb, string template)
    {
        var method = typeof(SecretariesController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        var route = Assert.Single(method!.GetCustomAttributes<HttpMethodAttribute>(true));
        Assert.Contains(verb, route.HttpMethods);
        Assert.Equal(template, route.Template);
    }
}
