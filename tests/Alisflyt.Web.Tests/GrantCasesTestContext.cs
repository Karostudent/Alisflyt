using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Web.Models;
using Microsoft.AspNetCore.Http;

namespace Alisflyt.Web.Tests;

internal static class GrantCasesTestContext
{
    public static HttpContext CreateAlis(string hprNumber = "123456789")
    {
        var context = new DefaultHttpContext { Session = new TestSession() };
        DemoAuthentication.SignIn(context, new DemoUser
        {
            Key = hprNumber == "123456789" ? "alis-anna" : "alis-test",
            Role = "ALIS",
            Name = "Test ALIS",
            HprNumber = hprNumber
        });
        return context;
    }

    public static HttpContext CreateSupervisor()
    {
        var context = new DefaultHttpContext { Session = new TestSession() };
        DemoAuthentication.SignIn(context, new DemoUser
        {
            Key = "veileder-per",
            Role = "Veileder",
            Name = "Per Olsen",
            HprNumber = "987654321"
        });
        return context;
    }

    public static HttpContext CreateCoordinator()
    {
        var context = new DefaultHttpContext { Session = new TestSession() };
        DemoAuthentication.SignIn(context, new DemoUser
        {
            Key = "koordinator-marta",
            Role = "Koordinator",
            Name = "Marta Solberg",
            HprNumber = "000000000"
        });
        return context;
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
}
