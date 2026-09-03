using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Intelligence;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

// POLOXI ABV Action Layer persistence: Domain-Pack configuration load (intents, urgency policies,
// owner mappings, action catalog) and ABV resolution observability (POLOXI.Abv* tables, migration
// 0173). Domain Packs are database-backed configuration; tenant rows override global (TenantId NULL).
public sealed partial class IntelligenceRepository:IIntelligenceAbvRepository
{
    public async Task<AbvDomainPack> GetDomainPackAsync(Guid tenantId,string? packCode,CancellationToken cancellationToken=default)
    {
        var byCode=!string.IsNullOrWhiteSpace(packCode);
        var packSql=byCode?"""
SELECT TOP(1) AbvDomainPackId,PackCode,Name
FROM POLOXI.AbvDomainPack
WHERE IsActive=1 AND IsDeleted=0 AND PackCode=@PackCode AND (TenantId=@TenantId OR TenantId IS NULL)
ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END,SortOrder;
""":"""
SELECT TOP(1) AbvDomainPackId,PackCode,Name
FROM POLOXI.AbvDomainPack
WHERE IsActive=1 AND IsDeleted=0 AND IsDefault=1 AND (TenantId=@TenantId OR TenantId IS NULL)
ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END,SortOrder;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var pack=await connection.QuerySingleOrDefaultAsync<AbvDomainPackRow>(new CommandDefinition(packSql,new{TenantId=tenantId,PackCode=packCode},cancellationToken:cancellationToken))
            ??throw new InvalidOperationException(byCode
                ?$"No active POLOXI ABV Domain Pack '{packCode}' is configured."
                :"No default POLOXI ABV Domain Pack (IsDefault=1) is configured; migration 0173 seed row may be missing.");

        var intents=(await connection.QueryAsync<AbvIntentRow>(new CommandDefinition("""
SELECT IntentCode,Name,Description FROM POLOXI.AbvIntentTaxonomy
WHERE IsActive=1 AND IsDeleted=0 AND AbvDomainPackId=@PackId ORDER BY SortOrder;
""",new{PackId=pack.AbvDomainPackId},cancellationToken:cancellationToken)))
            .Select(x=>new AbvIntentDefinition(x.IntentCode,x.Name,x.Description)).ToArray();

        var urgency=(await connection.QueryAsync<AbvUrgencyRow>(new CommandDefinition("""
SELECT PolicyCode,IntentCode,ImpactTierCode,PriorityCode,SlaHours FROM POLOXI.AbvUrgencyPolicy
WHERE IsActive=1 AND IsDeleted=0 AND AbvDomainPackId=@PackId ORDER BY SortOrder;
""",new{PackId=pack.AbvDomainPackId},cancellationToken:cancellationToken)))
            .Select(x=>new AbvUrgencyPolicyRule(x.PolicyCode,x.IntentCode,ParseImpact(x.ImpactTierCode),ParsePriority(x.PriorityCode),x.SlaHours)).ToArray();

        var owners=(await connection.QueryAsync<AbvOwnerRow>(new CommandDefinition("""
SELECT IntentCode,ImpactTierCode,OwnerRole FROM POLOXI.AbvOwnerMapping
WHERE IsActive=1 AND IsDeleted=0 AND AbvDomainPackId=@PackId ORDER BY SortOrder;
""",new{PackId=pack.AbvDomainPackId},cancellationToken:cancellationToken)))
            .Select(x=>new AbvOwnerMappingRule(x.IntentCode,x.ImpactTierCode is null?null:ParseImpact(x.ImpactTierCode),x.OwnerRole)).ToArray();

        var actions=(await connection.QueryAsync<AbvActionRow>(new CommandDefinition("""
SELECT IntentCode,ActionCode,Name,NextStep,PlaybookCode,ExecutionAllowed,HumanApprovalRequired FROM POLOXI.AbvActionCatalog
WHERE IsActive=1 AND IsDeleted=0 AND AbvDomainPackId=@PackId ORDER BY SortOrder;
""",new{PackId=pack.AbvDomainPackId},cancellationToken:cancellationToken)))
            .Select(x=>new AbvActionDefinition(x.IntentCode,x.ActionCode,x.Name,x.NextStep,x.PlaybookCode,x.ExecutionAllowed,x.HumanApprovalRequired)).ToArray();

        return new(pack.AbvDomainPackId,pack.PackCode,pack.Name,intents,urgency,owners,actions);
    }

    public async Task<Guid> RecordResolutionAsync(Guid tenantId,Guid userId,Guid? ambiguityRunId,Guid abvDomainPackId,string? proposedIntentCode,AbvResolutionOutcome outcome,long durationMilliseconds,CancellationToken cancellationToken=default)
    {
        const string sql="""
INSERT POLOXI.AbvResolution
(AbvResolutionId,TenantId,AmbiguityRunId,AbvDomainPackId,StatusCode,ProposedIntentCode,AcceptedIntentCode,IntentSourceCode,
 ImpactTierCode,MetricAtRisk,EstimatedExposure,ImpactSourceCode,PriorityCode,SlaHours,UrgencyPolicyCode,UrgencySourceCode,
 OwnerRole,OwnerSourceCode,ActionCode,NextStep,ActionabilityStatusCode,ExecutionAllowed,HumanApprovalRequired,FailureMessage,DurationMilliseconds,CreatedByUserId)
VALUES
(@AbvResolutionId,@TenantId,@AmbiguityRunId,@AbvDomainPackId,@StatusCode,@ProposedIntentCode,@AcceptedIntentCode,@IntentSourceCode,
 @ImpactTierCode,@MetricAtRisk,@EstimatedExposure,@ImpactSourceCode,@PriorityCode,@SlaHours,@UrgencyPolicyCode,@UrgencySourceCode,
 @OwnerRole,@OwnerSourceCode,@ActionCode,@NextStep,@ActionabilityStatusCode,@ExecutionAllowed,@HumanApprovalRequired,@FailureMessage,@DurationMilliseconds,@UserId);
""";
        var id=Guid.NewGuid();
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new
        {
            AbvResolutionId=id,
            TenantId=tenantId,
            AmbiguityRunId=ambiguityRunId,
            AbvDomainPackId=abvDomainPackId,
            StatusCode=ToStatusCode(outcome.Status),
            ProposedIntentCode=Truncate(proposedIntentCode,50),
            AcceptedIntentCode=Truncate(outcome.Intent?.IntentCode,50),
            IntentSourceCode=outcome.Intent is null?null:ToSourceCode(outcome.Intent.Source),
            ImpactTierCode=outcome.Impact is null?null:outcome.Impact.Tier.ToString().ToUpperInvariant(),
            MetricAtRisk=Truncate(outcome.Impact?.MetricAtRisk,200),
            EstimatedExposure=outcome.Impact?.EstimatedExposure,
            ImpactSourceCode=outcome.Impact is null?null:ToSourceCode(outcome.Impact.Source),
            PriorityCode=outcome.Urgency is null?null:outcome.Urgency.Priority.ToString().ToUpperInvariant(),
            SlaHours=outcome.Urgency?.SlaHours,
            UrgencyPolicyCode=Truncate(outcome.Urgency?.PolicyCode,50),
            UrgencySourceCode=outcome.Urgency is null?null:ToSourceCode(outcome.Urgency.Source),
            OwnerRole=Truncate(outcome.ExecutionPath?.OwnerRole,200),
            OwnerSourceCode=outcome.ExecutionPath is null?null:ToSourceCode(outcome.ExecutionPath.Source),
            ActionCode=Truncate(outcome.ExecutionPath?.ActionCode,100),
            NextStep=Truncate(outcome.ExecutionPath?.NextStep,1000),
            ActionabilityStatusCode=ToActionabilityCode(outcome.Actionability.Status),
            outcome.Actionability.ExecutionAllowed,
            outcome.Actionability.HumanApprovalRequired,
            FailureMessage=Truncate(outcome.FailureMessage,2000),
            DurationMilliseconds=durationMilliseconds,
            UserId=userId
        },cancellationToken:cancellationToken));
        return id;
    }

    private static AbvImpactTier ParseImpact(string code)=>code.ToUpperInvariant() switch
    {
        "CRITICAL"=>AbvImpactTier.Critical,"HIGH"=>AbvImpactTier.High,"MEDIUM"=>AbvImpactTier.Medium,_=>AbvImpactTier.Low
    };
    private static AbvPriority ParsePriority(string code)=>code.ToUpperInvariant() switch
    {
        "CRITICAL"=>AbvPriority.Critical,"HIGH"=>AbvPriority.High,"MEDIUM"=>AbvPriority.Medium,_=>AbvPriority.Low
    };
    private static string ToSourceCode(AbvSource source)=>source switch
    {
        AbvSource.Evidence=>"EVIDENCE",AbvSource.BusinessPolicy=>"BUSINESS_POLICY",AbvSource.DomainConfiguration=>"DOMAIN_CONFIG",_=>"DERIVED"
    };
    private static string ToStatusCode(AbvResolutionStatus status)=>status switch
    {
        AbvResolutionStatus.Resolved=>"RESOLVED",AbvResolutionStatus.NotConverged=>"NOT_CONVERGED",AbvResolutionStatus.IntentRejected=>"INTENT_REJECTED",_=>"FAILED"
    };
    private static string ToActionabilityCode(AbvActionabilityStatus status)=>status switch
    {
        AbvActionabilityStatus.ReadyForReview=>"READY_FOR_REVIEW",AbvActionabilityStatus.BlockedNotConverged=>"BLOCKED_NOT_CONVERGED",AbvActionabilityStatus.BlockedNoIntent=>"BLOCKED_NO_INTENT",_=>"BLOCKED_FAILED"
    };

    private sealed record AbvDomainPackRow(Guid AbvDomainPackId,string PackCode,string Name);
    private sealed record AbvIntentRow(string IntentCode,string Name,string? Description);
    private sealed record AbvUrgencyRow(string PolicyCode,string? IntentCode,string ImpactTierCode,string PriorityCode,int? SlaHours);
    private sealed record AbvOwnerRow(string? IntentCode,string? ImpactTierCode,string OwnerRole);
    private sealed record AbvActionRow(string IntentCode,string ActionCode,string Name,string? NextStep,string? PlaybookCode,bool ExecutionAllowed,bool HumanApprovalRequired);
}
