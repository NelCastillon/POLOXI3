using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Concepts;

public sealed class ConceptScheme : KnowledgeRecord
{
    public ConceptScheme(
        Guid id,
        string schemeCode,
        string name,
        string? description,
        string authorityCode,
        string? versionLabel,
        string statusCode,
        Guid? tenantId,
        bool isSystemDefined,
        Guid createdByUserId,
        DateTime createdUtc)
        : base(id, tenantId, isSystemDefined, createdByUserId, createdUtc)
    {
        SchemeCode = KnowledgeGuard.Code(schemeCode, "SchemeCode", 100);
        Name = KnowledgeGuard.Required(name, "Name", 200);
        Description = description?.Trim();
        AuthorityCode = KnowledgeGuard.Code(authorityCode, "AuthorityCode", 100);
        VersionLabel = versionLabel?.Trim();
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
    }

    public string SchemeCode { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string AuthorityCode { get; private set; }
    public string? VersionLabel { get; private set; }
    public string StatusCode { get; private set; }

    public void Update(string name, string? description, string authorityCode, string? versionLabel, string statusCode, Guid actorUserId, DateTime modifiedUtc)
    {
        Name = KnowledgeGuard.Required(name, "Name", 200);
        Description = description?.Trim();
        AuthorityCode = KnowledgeGuard.Code(authorityCode, "AuthorityCode", 100);
        VersionLabel = versionLabel?.Trim();
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
        MarkModified(actorUserId, modifiedUtc);
    }
}
