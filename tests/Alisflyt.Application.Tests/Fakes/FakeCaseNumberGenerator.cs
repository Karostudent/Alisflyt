using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Abstractions;

namespace Alisflyt.Application.Tests.Fakes
{
    internal class FakeCaseNumberGenerator : ICaseNumberGenerator
    {
        private readonly string _value;

        public FakeCaseNumberGenerator(string value)
        {
            _value = value;
        }

        public ValueTask<string> GenerateAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_value);
        }
    }
}
