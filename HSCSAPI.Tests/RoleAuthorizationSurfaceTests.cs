using System.Reflection;
using HSCSAPI.Controllers;
using HSCSAPI.Hub;
using HSCSAPI.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace HSCSAPI.Tests;

public class RoleAuthorizationSurfaceTests
{
    private static readonly string[] AllRoles = Enum.GetNames<UserSystemRole>();
    private static readonly IReadOnlyDictionary<string, ExpectedAccess> ExpectedActions = BuildExpectedActions();

    [Fact]
    public void ControllerActionInventory_MatchesDocumentedRoleCapabilityMatrix()
    {
        var actualActions = GetControllerActions();

        Assert.Equal(174, actualActions.Count);
        Assert.Equal(
            ExpectedActions.Keys.OrderBy(x => x),
            actualActions.Keys.OrderBy(x => x));

        foreach (var (key, action) in actualActions)
        {
            var expected = ExpectedActions[key];
            var actual = GetActualAccess(action.ControllerType, action.Method);
            Assert.Equal(expected.IsPublic, actual.IsPublic);
            Assert.Equal(expected.RequiresAuthentication, actual.RequiresAuthentication);
            Assert.Equal(expected.Roles.OrderBy(role => role), actual.Roles.OrderBy(role => role));
        }
    }

    [Fact]
    public void EverySystemRole_IsAllowedOnlyOnItsDocumentedActions()
    {
        var actualActions = GetControllerActions();

        foreach (var role in AllRoles)
        {
            foreach (var (key, action) in actualActions)
            {
                var actual = GetActualAccess(action.ControllerType, action.Method);
                var expected = ExpectedActions[key];
                Assert.Equal(expected.Allows(role), actual.Allows(role));
            }
        }
    }

    [Fact]
    public void RoleAccessCounts_MatchTheCurrentBackendSurface()
    {
        var expectedCounts = new Dictionary<string, int>
        {
            [nameof(UserSystemRole.SuperAdmin)] = 89,
            [nameof(UserSystemRole.Patient)] = 70,
            [nameof(UserSystemRole.Doctor)] = 72,
            [nameof(UserSystemRole.Secretary)] = 106,
            [nameof(UserSystemRole.AuthorizedMember)] = 57,
            [nameof(UserSystemRole.LaboratoryTechnologist)] = 56,
            [nameof(UserSystemRole.RadiologyTechnologist)] = 49
        };

        foreach (var (role, expectedCount) in expectedCounts)
        {
            Assert.Equal(expectedCount, ExpectedActions.Values.Count(access => access.Allows(role)));
        }
    }

    [Fact]
    public void EveryAuthorizeRoleName_IsAValidSystemRole()
    {
        var validRoles = AllRoles.ToHashSet(StringComparer.Ordinal);
        var protectedTypes = GetControllerActions().Values
            .Select(x => x.ControllerType)
            .Append(typeof(ChatHub))
            .Append(typeof(NotificationHub))
            .Distinct();

        foreach (var type in protectedTypes)
        {
            var attributes = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)));

            foreach (var role in attributes
                         .SelectMany(attribute => SplitRoles(attribute.Roles)))
            {
                Assert.Contains(role, validRoles);
            }
        }
    }

    [Fact]
    public void ControllerRoutes_AreUniqueByHttpVerbAndResolvedTemplate()
    {
        var routes = new List<string>();

        foreach (var action in GetControllerActions().Values)
        {
            var controllerTemplate = action.ControllerType
                .GetCustomAttribute<RouteAttribute>(inherit: true)?.Template ?? string.Empty;
            controllerTemplate = controllerTemplate.Replace(
                "[controller]",
                action.ControllerType.Name.Replace("Controller", string.Empty),
                StringComparison.OrdinalIgnoreCase);

            foreach (var httpAttribute in action.Method.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
            {
                var template = string.Join(
                    '/',
                    new[] { controllerTemplate.Trim('/'), httpAttribute.Template?.Trim('/') }
                        .Where(part => !string.IsNullOrWhiteSpace(part)));

                routes.AddRange(httpAttribute.HttpMethods.Select(verb => $"{verb} /{template}"));
            }
        }

        Assert.Equal(174, routes.Count);
        Assert.DoesNotContain(routes.GroupBy(route => route), group => group.Count() > 1);
    }

    [Fact]
    public void SignalRHubs_RequireAuthentication_AndExposeTheExpectedCallableSurface()
    {
        Assert.NotNull(typeof(ChatHub).GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(typeof(NotificationHub).GetCustomAttribute<AuthorizeAttribute>());

        var chatMethods = typeof(ChatHub)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.DeclaringType == typeof(ChatHub) && method.Name != nameof(ChatHub.OnConnectedAsync))
            .Select(method => method.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            new[] { "JoinChat", "LeaveChat", "MarkAsRead", "SendTextMessage", "SetTyping" },
            chatMethods);
        Assert.DoesNotContain(
            typeof(NotificationHub).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly),
            method => method.Name != nameof(NotificationHub.OnConnectedAsync));
    }

    private static IReadOnlyDictionary<string, ControllerAction> GetControllerActions()
    {
        return typeof(AuthController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(method => new ControllerAction(type, method)))
            .ToDictionary(
                action => $"{action.ControllerType.Name.Replace("Controller", string.Empty)}.{action.Method.Name}",
                StringComparer.Ordinal);
    }

    private static ExpectedAccess GetActualAccess(Type controllerType, MethodInfo method)
    {
        var attributes = controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .ToArray();

        if (attributes.Length == 0)
        {
            return ExpectedAccess.Public;
        }

        var roles = attributes.SelectMany(attribute => SplitRoles(attribute.Roles)).Distinct().ToArray();
        return roles.Length == 0 ? ExpectedAccess.Authenticated : ExpectedAccess.ForRoles(roles);
    }

    private static IEnumerable<string> SplitRoles(string? roles)
    {
        return string.IsNullOrWhiteSpace(roles)
            ? []
            : roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyDictionary<string, ExpectedAccess> BuildExpectedActions()
    {
        var actions = new Dictionary<string, ExpectedAccess>(StringComparer.Ordinal);

        Add(actions, "Auth", ExpectedAccess.Public,
            "Login", "RegisterPatient", "RegisterDoctor", "RegisterAuthorizedMember",
            "RegisterLaboratoryTechnologist", "RegisterRadiologyTechnologist", "ForgotPassword",
            "ResetPassword", "VerifyRegistrationCode", "ResendVerificationCode");
        Add(actions, "Auth", ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin)), "RegisterSecretary");

        Add(actions, "Clinics", ExpectedAccess.Public, "GetAll");
        Add(actions, "Clinics", ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin)), "Create", "Update", "Deactivate", "Activate");
        Add(actions, "Clinics", ExpectedAccess.ForRoles(nameof(UserSystemRole.Secretary)), "UpdateMyClinic");
        Add(actions, "Clinics", ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin), nameof(UserSystemRole.Secretary)),
            "UpdateSecretaryAccount", "DeactivateSecretaryAccount", "ActivateSecretaryAccount");

        Add(actions, "Appointments", ExpectedAccess.ForRoles(
                nameof(UserSystemRole.SuperAdmin), nameof(UserSystemRole.Secretary), nameof(UserSystemRole.Doctor), nameof(UserSystemRole.Patient)),
            "GetAll", "GetById");
        Add(actions, "Appointments", ExpectedAccess.ForRoles(nameof(UserSystemRole.Doctor), nameof(UserSystemRole.Patient)), "GetMine");
        Add(actions, "Appointments", ExpectedAccess.ForRoles(
                nameof(UserSystemRole.SuperAdmin), nameof(UserSystemRole.Secretary), nameof(UserSystemRole.Patient)),
            "Create", "Update", "Deactivate", "Activate");

        Add(actions, "AuthorizedMembers", ExpectedAccess.ForRoles(nameof(UserSystemRole.AuthorizedMember)),
            "GetDashboard", "GetMyProfile", "GetMyPatients", "GetMyPatient", "GetMyAppointments",
            "GetPatientMedicalRecords", "GetPatientMedicalRecord", "DownloadPatientMedicalRecord",
            "GetMyInvites", "AcceptInvite", "RejectInvite");

        Add(actions, "Chats", ExpectedAccess.Authenticated,
            "OpenChat", "GetChats", "GetMessages", "SendMessage", "EditMessage", "UnsendMessage", "MarkAsRead", "GetFile");
        Add(actions, "Notifications", ExpectedAccess.Authenticated,
            "GetMyNotifications", "UpdateReadStatus", "MarkAllAsRead", "Delete");
        Add(actions, "Reminders", ExpectedAccess.Authenticated,
            "GetMyReminders", "CreateReminder", "GetReminder", "UpdateReminder", "DismissReminder",
            "GetMyPreferences", "UpdateMyPreferences");

        Add(actions, "Doctors", ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin), nameof(UserSystemRole.Secretary)),
            "GetAll", "GetByClinic", "Update", "Deactivate", "Activate");
        Add(actions, "Doctors", ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin), nameof(UserSystemRole.Secretary), nameof(UserSystemRole.Doctor)),
            "GetById");
        Add(actions, "Doctors", ExpectedAccess.ForRoles(nameof(UserSystemRole.Doctor)),
            "GetMyProfile", "GetMyDashboard", "GetMyAppointments", "GetMyAppointmentDetail",
            "GetMyMedicalRecords", "GetMyMedicalRecord", "DownloadMyMedicalRecord", "UpdateMyProfile", "ChangeMyPassword");
        Add(actions, "Doctors", ExpectedAccess.Authenticated, "GetAvailability");
        Add(actions, "DoctorLabRequests", ExpectedAccess.ForRoles(nameof(UserSystemRole.Doctor)),
            "GetMyLabRequests", "CreateMyLabRequest", "GetMyLabRequest", "DownloadMyLabResultFile");
        Add(actions, "DoctorImagingRequests", ExpectedAccess.ForRoles(nameof(UserSystemRole.Doctor)),
            "GetMyImagingRequests", "CreateMyImagingRequest", "GetMyImagingRequest", "DownloadMyImagingResultFile");
        Add(actions, "DoctorMedicalFileUploads", ExpectedAccess.ForRoles(nameof(UserSystemRole.Doctor)),
            "GetUploadCategories", "GetMyUploadHistory", "UploadMyMedicalFile",
            "GetMyUploadedMedicalFile", "DownloadMyUploadedMedicalFile");

        AddProfileCrud(actions, "Patients", nameof(UserSystemRole.Patient));
        Add(actions, "Patients", ExpectedAccess.ForRoles(nameof(UserSystemRole.Patient)), "ChangeMyPassword");
        AddProfileCrud(actions, "LaboratoryTechnologists", nameof(UserSystemRole.LaboratoryTechnologist));
        AddProfileCrud(actions, "RadiologyTechnologists", nameof(UserSystemRole.RadiologyTechnologist));

        Add(actions, "PatientProfile", ExpectedAccess.ForRoles(nameof(UserSystemRole.Patient)),
            "GetDashboard", "GetMedicalRecords", "GetMedicalRecord", "DownloadMedicalRecord",
            "CreateDownloadRequest", "GetDownloadRequests", "GetAuthorizedMembers",
            "GetAuthorizedMemberInvites", "CreateAuthorizedMemberInvite", "DeactivateAuthorizedMember", "ActivateAuthorizedMember",
            "DeactivateAuthorizedMemberInvite", "ActivateAuthorizedMemberInvite");

        Add(actions, "LaboratoryTests", ExpectedAccess.ForRoles(nameof(UserSystemRole.LaboratoryTechnologist)),
            "GetTemplates", "GetTemplate", "GetMyRequests", "CreateResult", "GetResult", "GeneratePdf", "DownloadPdf");

        Add(actions, "Secretaries", ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin)), "GetAll", "GetAvailable");
        Add(actions, "Secretaries", ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin), nameof(UserSystemRole.Secretary)),
            "GetByClinic", "AssignToClinic", "RemoveFromClinic");
        Add(actions, "Secretaries", ExpectedAccess.ForRoles(nameof(UserSystemRole.Secretary)),
            "GetDashboard", "GetMyClinicSecretaries", "GetMyClinicPatients", "UpdateMyClinicPatient", "DeactivateMyClinicPatient", "ActivateMyClinicPatient",
            "GetMyClinicDoctors", "UpdateMyClinicDoctor", "DeactivateMyClinicDoctor", "ActivateMyClinicDoctor",
            "GetMyClinicLaboratoryTechnologists", "UpdateMyClinicLaboratoryTechnologist", "DeactivateMyClinicLaboratoryTechnologist", "ActivateMyClinicLaboratoryTechnologist",
            "GetMyClinicRadiologyTechnologists", "UpdateMyClinicRadiologyTechnologist", "DeactivateMyClinicRadiologyTechnologist", "ActivateMyClinicRadiologyTechnologist",
            "GetDoctorAvailabilitySlots", "CreateDoctorAvailabilitySlot", "DeleteDoctorAvailabilitySlot", "GetReports", "GenerateReport");

        Add(actions, "Standards", ExpectedAccess.Public,
            "SearchLoinc", "GetLoincByCode", "SearchLabTests", "SearchIcd10", "GetIcd10ByCode",
            "SearchRadiologyPlaybook", "GetRadiologyPlaybookByRpid", "GetImagingTypes", "SearchAll",
            "FindRxCui", "FindDrugs", "FindApproximate", "GetRxNormProperties", "GetRxNormRelated", "GetRxNormVersion");

        return actions;
    }

    private static void AddProfileCrud(
        IDictionary<string, ExpectedAccess> actions,
        string controller,
        string selfRole)
    {
        Add(actions, controller, ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin), nameof(UserSystemRole.Secretary)),
            "GetAll", "GetByClinic", "Update", "Deactivate", "Activate");
        Add(actions, controller, ExpectedAccess.ForRoles(nameof(UserSystemRole.SuperAdmin), nameof(UserSystemRole.Secretary), selfRole),
            "GetById");
        Add(actions, controller, ExpectedAccess.ForRoles(selfRole), "GetMyProfile", "UpdateMyProfile");
    }

    private static void Add(
        IDictionary<string, ExpectedAccess> actions,
        string controller,
        ExpectedAccess access,
        params string[] methodNames)
    {
        foreach (var methodName in methodNames)
        {
            actions.Add($"{controller}.{methodName}", access);
        }
    }

    private sealed record ControllerAction(Type ControllerType, MethodInfo Method);

    private sealed record ExpectedAccess(bool IsPublic, bool RequiresAuthentication, IReadOnlySet<string> Roles)
    {
        public static ExpectedAccess Public { get; } = new(true, false, new HashSet<string>());
        public static ExpectedAccess Authenticated { get; } = new(false, true, new HashSet<string>());

        public static ExpectedAccess ForRoles(params string[] roles) =>
            new(false, true, roles.ToHashSet(StringComparer.Ordinal));

        public bool Allows(string role) => IsPublic || (RequiresAuthentication && (Roles.Count == 0 || Roles.Contains(role)));
    }
}
