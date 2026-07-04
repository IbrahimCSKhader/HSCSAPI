using System.Reflection;
using HSCSAPI.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace HSCSAPI.Tests;

public class RequestedEndpointBatch1Tests
{
    [Fact]
    public void DoctorAvailabilityEndpoint_IsGetWithDateRoute() =>
        AssertRoute<DoctorsController>(nameof(DoctorsController.GetAvailability), "GET", "{doctorId:guid}/availability");

    [Fact]
    public void PatientPasswordEndpoint_IsPut() =>
        AssertRoute<PatientsController>(nameof(PatientsController.ChangeMyPassword), "PUT", "me/password");

    [Fact]
    public void ChatEditEndpoint_IsPut() =>
        AssertRoute<ChatsController>(nameof(ChatsController.EditMessage), "PUT", "{chatId:guid}/messages/{messageId:guid}");

    [Fact]
    public void ChatUnsendEndpoint_IsDelete() =>
        AssertRoute<ChatsController>(nameof(ChatsController.UnsendMessage), "DELETE", "{chatId:guid}/messages/{messageId:guid}");

    [Fact]
    public void NotificationDeleteEndpoint_IsDelete() =>
        AssertRoute<NotificationsController>(nameof(NotificationsController.Delete), "DELETE", "{notificationId:guid}");

    private static void AssertRoute<TController>(string methodName, string verb, string template)
    {
        var method = typeof(TController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        var route = Assert.Single(Assert.IsAssignableFrom<IEnumerable<HttpMethodAttribute>>(
            method!.GetCustomAttributes<HttpMethodAttribute>(inherit: true)));
        Assert.Contains(verb, route.HttpMethods);
        Assert.Equal(template, route.Template);
    }
}
