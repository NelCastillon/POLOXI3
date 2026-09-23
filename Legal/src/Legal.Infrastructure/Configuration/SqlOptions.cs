namespace Legal.Infrastructure.Configuration;

public sealed class SqlOptions
{
    public const string SectionName = "Sql";

    public string ConnectionString { get; set; } = string.Empty;
}
