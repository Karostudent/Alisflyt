using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace Alisflyt.Web.Tests.Fakes
{
    // Minimal IUrlHelper implementation used by unit tests.
    internal class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new ActionContext();

        public string Action(UrlActionContext actionContext)
        {
            var action = actionContext?.Action ?? "Index";
            var controller = actionContext?.Controller ?? "Home";
            return $"/{controller}/{action}";
        }

        public string Action(string action, string controller, object values) => $"/{controller}/{action}";

        public string Action(string action, string controller, object values, string protocol, string host, string fragment) => $"/{controller}/{action}";

        public string? Content(string? contentPath) => contentPath ?? string.Empty;

        public bool IsLocalUrl(string? url) => !string.IsNullOrEmpty(url) && url.StartsWith("/");

        public string? Link(string? routeName, object? values) => "/" + (routeName ?? "route");

        public string RouteUrl(UrlRouteContext routeContext) => "/" + (routeContext?.RouteName ?? "route");

        public string RouteUrl(string routeName, object values) => "/" + (routeName ?? "route");
    }
}
