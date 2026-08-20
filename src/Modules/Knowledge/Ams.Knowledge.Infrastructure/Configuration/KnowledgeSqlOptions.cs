namespace Ams.Knowledge.Infrastructure.Configuration;

public sealed class KnowledgeSqlOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ImportRootPath { get; set; } = string.Empty;
}
