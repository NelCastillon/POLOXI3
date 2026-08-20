namespace Ams.Knowledge.Domain.Common;

public sealed class KnowledgeDomainException : InvalidOperationException
{
    public KnowledgeDomainException(string message) : base(message)
    {
    }
}
