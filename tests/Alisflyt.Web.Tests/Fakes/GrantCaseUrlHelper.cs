using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace Alisflyt.Web.Tests.Fakes;

internal sealed class GrantCaseUrlHelper : IUrlHelper
{
    public ActionContext ActionContext { get; } = new();
    public string? Action(UrlActionContext context) => $"/GrantCases/{context.Action}/{new RouteValueDictionary(context.Values)["id"]}";
    public string? Content(string? contentPath) => contentPath;
    public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true;
    public string? Link(string? routeName, object? values) => throw new NotSupportedException();
    public string? RouteUrl(UrlRouteContext context) => throw new NotSupportedException();
}
