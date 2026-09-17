using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alisflyt.Application.Abstractions
{
    public interface ICaseNumberGenerator
    {
        ValueTask<string> GenerateAsync(CancellationToken cancellationToken = default);
    }
}
