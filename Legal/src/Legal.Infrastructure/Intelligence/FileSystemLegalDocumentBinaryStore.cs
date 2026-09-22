using Legal.Application.Abstractions.Intelligence;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

public sealed class FileSystemLegalDocumentBinaryStore(IOptions<DocumentIntelligenceOptions> options) : ILegalDocumentBinaryStore
{
    private readonly string _root = ResolveRoot(options.Value.StorageRoot);

    public async Task<string> StoreImmutableAsync(Guid tenantId, Guid documentId, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName));
        var directory = Path.Combine(_root, tenantId.ToString("N"), documentId.ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"original{extension}");
        await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await content.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
        return new Uri(path).AbsoluteUri;
    }

    private static string ResolveRoot(string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException("DocumentIntelligence:StorageRoot is required.");
        return Path.GetFullPath(configuredRoot, AppContext.BaseDirectory);
    }
}
