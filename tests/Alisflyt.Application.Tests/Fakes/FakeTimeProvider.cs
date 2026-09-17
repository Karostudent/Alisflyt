using System;

namespace Alisflyt.Application.Tests.Fakes
{
    internal class FakeTimeProvider : System.TimeProvider
    {
        private DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset now) => _now = now;

        public void Advance(TimeSpan ts) => _now = _now.Add(ts);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
