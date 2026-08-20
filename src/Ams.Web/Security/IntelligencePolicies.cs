namespace Ams.Web.Security;

public static class IntelligencePolicies
{
    public const string Read="Intelligence.Read";
    public const string Search="Intelligence.Search";
    public const string Recommend="Intelligence.Recommend";
    public const string Review="Intelligence.Review";
    public const string Configure="Intelligence.Configure";
    public const string Evaluate="Intelligence.Evaluate";
    public const string AuditRead="Intelligence.Audit.Read";
    public const string Analyze="Intelligence.Analyze";
    public const string Reason="Intelligence.Reason";
    public const string FindingsRead="Intelligence.Findings.Read";
    public const string FindingsReview="Intelligence.Findings.Review";
    public const string RelationshipsRead="Intelligence.Relationships.Read";
    public const string GovernanceManage="Intelligence.Governance.Manage";
    public static readonly string[] All=[Read,Search,Recommend,Review,Configure,Evaluate,AuditRead,Analyze,Reason,FindingsRead,FindingsReview,RelationshipsRead,GovernanceManage];
}
