using System.Reflection;
using fletflow.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BackEnd_Gps.Tests;

public class RouteAuthorizationTests
{
    [Fact]
    public void FleetControllersDeclareTheExpectedRoleBoundary()
    {
        AssertControllerRoles(typeof(VehiclesController), "Admin", "Manager");
        AssertControllerRoles(typeof(DriversController), "Admin", "Manager");
        AssertControllerRoles(typeof(UsersController), "Admin");
    }

    [Fact]
    public void RouteEndpointsUseSupportedFleetRoles()
    {
        var actions = typeof(RoutesController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => new
            {
                Method = method,
                Authorization = method.GetCustomAttribute<AuthorizeAttribute>()
            })
            .Where(item => item.Authorization?.Roles is not null)
            .ToArray();

        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
        {
            var roles = action.Authorization!.Roles!.Split(',', StringSplitOptions.TrimEntries);
            Assert.DoesNotContain("Dispatcher", roles);
            Assert.All(roles, role => Assert.Contains(role, new[] { "Admin", "Manager", "Logistics", "Driver" }));
        });

        var routeManagementActions = new[] { "Create", "Update", "ChangeStatus", "Delete" };
        foreach (var actionName in routeManagementActions)
        {
            var action = actions.Single(item => item.Method.Name == actionName);
            Assert.Contains("Logistics", action.Authorization!.Roles!.Split(',', StringSplitOptions.TrimEntries));
        }

        var changeStatus = actions.Single(item => item.Method.Name == "ChangeStatus");
        Assert.Contains("Driver", changeStatus.Authorization!.Roles!.Split(',', StringSplitOptions.TrimEntries));
    }

    private static void AssertControllerRoles(Type controller, params string[] expectedRoles)
    {
        var authorization = controller.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorization);
        Assert.Equal(expectedRoles.Order(), authorization!.Roles!.Split(',', StringSplitOptions.TrimEntries).Order());
    }
}
