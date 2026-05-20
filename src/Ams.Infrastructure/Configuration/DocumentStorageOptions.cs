namespace Ams.Infrastructure.Configuration;

public sealed class DocumentStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string AccountUri { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "documents";
}
