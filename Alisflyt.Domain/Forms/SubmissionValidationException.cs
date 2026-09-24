namespace Alisflyt.Domain.Forms;

public sealed class SubmissionValidationException(IReadOnlyList<string> errors)
    : ArgumentException(string.Join("\n", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
