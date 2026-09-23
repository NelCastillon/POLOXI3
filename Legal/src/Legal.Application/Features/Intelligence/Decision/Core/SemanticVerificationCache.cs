using System.Collections.Concurrent;

namespace Legal.Application.Features.Intelligence.Decision.Core;

public sealed class SemanticVerificationCache : ISemanticVerificationCache
{
    private readonly ConcurrentDictionary<string, SemanticVerificationResult> _entries = new(StringComparer.Ordinal);

    public bool TryGet(string key, out SemanticVerificationResult result) => _entries.TryGetValue(key, out result!);

    public void Store(string key, SemanticVerificationResult result) => _entries[key] = result;
}