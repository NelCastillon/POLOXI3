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

    public Task<LegalDocumentReadResult> OpenReadAsync(Guid tenantId, string storageReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageReference))
            throw new ArgumentException("A storage reference is required.", nameof(storageReference));
        if (!Uri.TryCreate(storageReference, UriKind.Absolute, out var uri) || !uri.IsFile)
            throw new InvalidOperationException("The stored document reference is not a local file reference.");

        var path = Path.GetFullPath(uri.LocalPath);
        // Contain reads to the configured storage root so a stored reference can never escape it.
        var root = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The stored document reference is outside the configured storage root.");
        if (!File.Exists(path))
            throw new FileNotFoundException("The stored document original was not found.", path);

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(new LegalDocumentReadResult(stream, stream.Length));
    }

    private static string ResolveRoot(string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException("DocumentIntelligence:StorageRoot is required.");
        return Path.GetFullPath(configuredRoot, AppContext.BaseDirectory);
    }
}
