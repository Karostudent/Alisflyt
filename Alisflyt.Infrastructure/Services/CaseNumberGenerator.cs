using System;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Abstractions;

namespace Alisflyt.Infrastructure.Services
{
    public class CaseNumberGenerator : ICaseNumberGenerator
    {
        public ValueTask<string> GenerateAsync(CancellationToken cancellationToken = default)
        {
            // Simple readable unique id: ALIS- + 8 hex chars from GUID
            var id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            return ValueTask.FromResult($"ALIS-{id}");
        }
    }
}
