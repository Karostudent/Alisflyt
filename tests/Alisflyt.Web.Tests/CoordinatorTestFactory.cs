using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Alisflyt.Web.Tests;

internal static class CoordinatorTestFactory
{
    public static CoordinatorController Create(FakeGrantCaseApplicationService service, string role = "Koordinator")
    {
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
        {
            Key = "koordinator-test",
            Role = role,
            Name = "Koordinator Test"
        });

        var controller = new CoordinatorController(service)
        {
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
