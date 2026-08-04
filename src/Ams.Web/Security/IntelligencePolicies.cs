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
    public static readonly string[] All=[Read,Search,Recommend,Review,Configure,Evaluate,AuditRead];
}
