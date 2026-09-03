using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Intelligence;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

// Model-Adaptive Ambiguity subsystem persistence: capability profiles, versioned prompt strategies,
// and run/node/dependency/issue/invocation observability (POLOXI.* tables, migration 0170).
public sealed partial class IntelligenceRepository:IIntelligenceAmbiguityRepository
{
    public async Task<ModelCapabilityProfile> GetModelCapabilityProfileAsync(Guid tenantId,string? modelCode,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT TOP(1) ModelCodePattern,TierCode,SemanticReasoning,MultiAmbiguityRecall,RecursiveDecomposition,StructuralReliability,InstructionFollowing,CostScore,LatencyScore,RecommendedMaxDepth,RecommendedScaffoldingCode
FROM POLOXI.ModelCapabilityProfile
WHERE IsActive=1 AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) AND @ModelCode LIKE ModelCodePattern
ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END,SortOrder;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row=await connection.QuerySingleOrDefaultAsync<ModelCapabilityRow>(new CommandDefinition(sql,new{TenantId=tenantId,ModelCode=modelCode??string.Empty},cancellationToken:cancellationToken))
            ??throw new InvalidOperationException("No active POLOXI model capability profile matched; the global '%' fallback row is missing.");
        return ToProfile(row,modelCode);
    }

    public async Task<ModelCapabilityProfile?> GetEscalationProfileAsync(Guid tenantId,ModelTier aboveTier,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT TOP(1) ModelCodePattern,TierCode,SemanticReasoning,MultiAmbiguityRecall,RecursiveDecomposition,StructuralReliability,InstructionFollowing,CostScore,LatencyScore,RecommendedMaxDepth,RecommendedScaffoldingCode
FROM POLOXI.ModelCapabilityProfile
WHERE IsActive=1 AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL)
AND CASE TierCode WHEN N'SMALL' THEN 0 WHEN N'STANDARD' THEN 1 ELSE 2 END>@TierRank
ORDER BY CASE TierCode WHEN N'SMALL' THEN 0 WHEN N'STANDARD' THEN 1 ELSE 2 END,CostScore DESC,CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END,SortOrder;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row=await connection.QuerySingleOrDefaultAsync<ModelCapabilityRow>(new CommandDefinition(sql,new{TenantId=tenantId,TierRank=(int)aboveTier},cancellationToken:cancellationToken));
        return row is null?null:ToProfile(row,null);
    }

    public async Task<AmbiguityPromptTemplate> GetPromptTemplateAsync(Guid tenantId,string purposeCode,PromptScaffoldingLevel level,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT TOP(1) PurposeCode,ScaffoldingCode,VersionNumber,SystemPrompt,UserPromptTemplate
FROM POLOXI.PromptStrategy
WHERE IsActive=1 AND IsDeleted=0 AND PurposeCode=@PurposeCode AND ScaffoldingCode=@ScaffoldingCode AND (TenantId=@TenantId OR TenantId IS NULL)
ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END,VersionNumber DESC;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row=await connection.QuerySingleOrDefaultAsync<PromptStrategyRow>(new CommandDefinition(sql,new{TenantId=tenantId,PurposeCode=purposeCode,ScaffoldingCode=ToScaffoldingCode(level)},cancellationToken:cancellationToken));
        // Repair prompts are only seeded HEAVY; fall back to the heaviest available strategy for the purpose.
        row??=await connection.QuerySingleOrDefaultAsync<PromptStrategyRow>(new CommandDefinition("""
SELECT TOP(1) PurposeCode,ScaffoldingCode,VersionNumber,SystemPrompt,UserPromptTemplate
FROM POLOXI.PromptStrategy
WHERE IsActive=1 AND IsDeleted=0 AND PurposeCode=@PurposeCode AND (TenantId=@TenantId OR TenantId IS NULL)
ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END,CASE ScaffoldingCode WHEN N'HEAVY' THEN 0 WHEN N'MEDIUM' THEN 1 ELSE 2 END,VersionNumber DESC;
""",new{TenantId=tenantId,PurposeCode=purposeCode},cancellationToken:cancellationToken));
        if(row is null)throw new InvalidOperationException($"No active POLOXI prompt strategy is configured for purpose {purposeCode}.");
        return new(row.PurposeCode,FromScaffoldingCode(row.ScaffoldingCode),row.VersionNumber,row.SystemPrompt,row.UserPromptTemplate);
    }

    public async Task<Guid> StartRunAsync(Guid tenantId,Guid userId,string queryText,QueryComplexityProfile complexity,PromptScaffoldingLevel scaffolding,string? modelCode,CancellationToken cancellationToken=default)
    {
        const string sql="""
INSERT POLOXI.AmbiguityRun(AmbiguityRunId,TenantId,QueryText,ComplexityLevelCode,AmbiguityLikelihood,SemanticComplexity,ConstraintComplexity,InteractionComplexity,EvidenceComplexity,ConceptCount,SelectedModelCode,SelectedScaffoldingCode,StatusCode,CreatedByUserId)
VALUES(@AmbiguityRunId,@TenantId,@QueryText,@ComplexityLevelCode,@AmbiguityLikelihood,@SemanticComplexity,@ConstraintComplexity,@InteractionComplexity,@EvidenceComplexity,@ConceptCount,@SelectedModelCode,@SelectedScaffoldingCode,N'RUNNING',@UserId);
""";
        var runId=Guid.NewGuid();
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{AmbiguityRunId=runId,TenantId=tenantId,QueryText=Truncate(queryText,4000),ComplexityLevelCode=complexity.OverallLevel.ToString().ToUpperInvariant(),complexity.AmbiguityLikelihood,complexity.SemanticComplexity,complexity.ConstraintComplexity,complexity.InteractionComplexity,complexity.EvidenceComplexity,complexity.ConceptCount,SelectedModelCode=Truncate(modelCode,100),SelectedScaffoldingCode=ToScaffoldingCode(scaffolding),UserId=userId},cancellationToken:cancellationToken));
        return runId;
    }

    public async Task RecordInvocationAsync(Guid tenantId,Guid ambiguityRunId,AmbiguityModelInvocationRecord invocation,CancellationToken cancellationToken=default)
    {
        const string sql="""
INSERT POLOXI.AmbiguityModelInvocation(TenantId,AmbiguityRunId,TaskTypeCode,ModelCode,PromptPurposeCode,PromptScaffoldingCode,PromptVersionNumber,InputTokenCount,OutputTokenCount,DurationMilliseconds,IsSuccess,IsSchemaValid,RetryNumber,EscalatedFromModelCode,FailureMessage)
VALUES(@TenantId,@AmbiguityRunId,@TaskTypeCode,@ModelCode,@PromptPurposeCode,@PromptScaffoldingCode,@PromptVersionNumber,@InputTokenCount,@OutputTokenCount,@DurationMilliseconds,@IsSuccess,@IsSchemaValid,@RetryNumber,@EscalatedFromModelCode,@FailureMessage);
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,AmbiguityRunId=ambiguityRunId,TaskTypeCode=invocation.TaskType.ToString().ToUpperInvariant(),ModelCode=Truncate(invocation.ModelCode,100),PromptPurposeCode=invocation.PromptPurposeCode,PromptScaffoldingCode=ToScaffoldingCode(invocation.Scaffolding),PromptVersionNumber=invocation.PromptVersionNumber,invocation.InputTokenCount,invocation.OutputTokenCount,invocation.DurationMilliseconds,invocation.IsSuccess,invocation.IsSchemaValid,invocation.RetryNumber,EscalatedFromModelCode=Truncate(invocation.EscalatedFromModelCode,100),FailureMessage=Truncate(invocation.FailureMessage,1000)},cancellationToken:cancellationToken));
    }

    public async Task RecordValidationIssuesAsync(Guid tenantId,Guid ambiguityRunId,int attemptNumber,IReadOnlyCollection<HierarchyValidationIssue> issues,CancellationToken cancellationToken=default)
    {
        if(issues.Count==0)return;
        const string sql="""
INSERT POLOXI.AmbiguityValidationIssue(TenantId,AmbiguityRunId,AttemptNumber,IssueCode,SeverityCode,NodeKey,IssueMessage)
VALUES(@TenantId,@AmbiguityRunId,@AttemptNumber,@IssueCode,@SeverityCode,@NodeKey,@IssueMessage);
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,issues.Select(issue=>new{TenantId=tenantId,AmbiguityRunId=ambiguityRunId,AttemptNumber=attemptNumber,IssueCode=Truncate(issue.IssueCode,50),SeverityCode=issue.Severity,NodeKey=Truncate(issue.NodeId,100),IssueMessage=Truncate(issue.Message,1000)}).ToArray(),cancellationToken:cancellationToken));
    }

    public async Task CompleteRunAsync(Guid tenantId,Guid userId,Guid ambiguityRunId,string statusCode,int attemptCount,string? selectedModelCode,string? escalatedFromModelCode,bool coverageSuspicion,ValidatedHierarchy hierarchy,IReadOnlyCollection<BranchRuntimeState> states,string? compositeJson,long durationMilliseconds,CancellationToken cancellationToken=default)
    {
        const string updateSql="""
UPDATE POLOXI.AmbiguityRun SET StatusCode=@StatusCode,AttemptCount=@AttemptCount,SelectedModelCode=@SelectedModelCode,EscalatedFromModelCode=@EscalatedFromModelCode,AmbiguityCount=@AmbiguityCount,CoverageSuspicion=@CoverageSuspicion,CompositeJson=@CompositeJson,DurationMilliseconds=@DurationMilliseconds,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId
WHERE AmbiguityRunId=@AmbiguityRunId AND TenantId=@TenantId AND IsDeleted=0;
""";
        const string nodeSql="""
INSERT POLOXI.AmbiguityNode(TenantId,AmbiguityRunId,NodeKey,ParentNodeKey,Depth,DisplayName,SourceText,NodeTypeCode,AmbiguityTypeCode,MaterialityCode,DecisionRoleCode,OperationalDefinition,MetricOrObservation,EvidenceNeeded,EvidenceTypeCode,PreferenceDirectionCode,IsLeaf,ProposedConfidence,BranchStatusCode,Priority,EvidenceSupport,InformationGain,DecisionImpact,ResidualUncertainty,ResolutionReason,SemanticRoleCode,SortOrder,CreatedByUserId)
VALUES(@TenantId,@AmbiguityRunId,@NodeKey,@ParentNodeKey,@Depth,@DisplayName,@SourceText,@NodeTypeCode,@AmbiguityTypeCode,@MaterialityCode,@DecisionRoleCode,@OperationalDefinition,@MetricOrObservation,@EvidenceNeeded,@EvidenceTypeCode,@PreferenceDirectionCode,@IsLeaf,@ProposedConfidence,@BranchStatusCode,@Priority,@EvidenceSupport,@InformationGain,@DecisionImpact,@ResidualUncertainty,@ResolutionReason,@SemanticRoleCode,@SortOrder,@UserId);
""";
        const string dependencySql="""
INSERT POLOXI.AmbiguityNodeDependency(TenantId,AmbiguityRunId,SourceNodeKey,TargetNodeKey,DependencyTypeCode,Reason,Strength,CreatedByUserId)
VALUES(@TenantId,@AmbiguityRunId,@SourceNodeKey,@TargetNodeKey,@DependencyTypeCode,@Reason,@Strength,@UserId);
""";
        var stateById=states.ToDictionary(x=>x.NodeId,StringComparer.Ordinal);
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(updateSql,new{StatusCode=statusCode,AttemptCount=attemptCount,SelectedModelCode=Truncate(selectedModelCode,100),EscalatedFromModelCode=Truncate(escalatedFromModelCode,100),AmbiguityCount=hierarchy.Proposal.AmbiguityCount,CoverageSuspicion=coverageSuspicion,CompositeJson=compositeJson,DurationMilliseconds=durationMilliseconds,UserId=userId,AmbiguityRunId=ambiguityRunId,TenantId=tenantId},cancellationToken:cancellationToken));
        if(hierarchy.Nodes.Count>0)
            await connection.ExecuteAsync(new CommandDefinition(nodeSql,hierarchy.Nodes.Select((node,index)=>
            {
                var state=stateById.GetValueOrDefault(node.Id);
                return new{TenantId=tenantId,AmbiguityRunId=ambiguityRunId,NodeKey=Truncate(node.Id,100),ParentNodeKey=Truncate(node.ParentId,100),node.Depth,DisplayName=Truncate(node.Name,300),SourceText=Truncate(node.SourceText,1000),NodeTypeCode=ToCode(node.NodeType.ToString()),AmbiguityTypeCode=node.AmbiguityType is null?null:ToCode(node.AmbiguityType.ToString()!),MaterialityCode=node.Materiality.ToString().ToUpperInvariant(),DecisionRoleCode=ToCode(node.DecisionRole.ToString()),OperationalDefinition=Truncate(node.OperationalDefinition,1000),MetricOrObservation=Truncate(node.MetricOrObservation,500),EvidenceNeeded=Truncate(node.EvidenceNeeded,1000),EvidenceTypeCode=Truncate(node.EvidenceType,40),PreferenceDirectionCode=ToCode(node.PreferenceDirection.ToString()),node.IsLeaf,node.ProposedConfidence,BranchStatusCode=(state?.Status??BranchStatus.Proposed).ToString().ToUpperInvariant(),Priority=state?.Priority??0m,EvidenceSupport=state?.EvidenceSupport??0m,InformationGain=state?.InformationGain??0m,DecisionImpact=state?.DecisionImpact??0m,ResidualUncertainty=state?.ResidualUncertainty??1m,ResolutionReason=Truncate(state?.ResolutionReason,1000),SemanticRoleCode=ToCode((state?.SemanticRole??SemanticRole.CompetingInterpretation).ToString()),SortOrder=index,UserId=userId};
            }).ToArray(),cancellationToken:cancellationToken));
        if(hierarchy.Dependencies.Count>0)
            await connection.ExecuteAsync(new CommandDefinition(dependencySql,hierarchy.Dependencies.Select(dependency=>new{TenantId=tenantId,AmbiguityRunId=ambiguityRunId,SourceNodeKey=Truncate(dependency.SourceNodeId,100),TargetNodeKey=Truncate(dependency.TargetNodeId,100),DependencyTypeCode=ToCode(dependency.Type.ToString()),Reason=Truncate(dependency.Reason,1000),dependency.Strength,UserId=userId}).ToArray(),cancellationToken:cancellationToken));
    }

    private static ModelCapabilityProfile ToProfile(ModelCapabilityRow row,string? modelCode)
    {
        var materialized=modelCode??row.ModelCodePattern.Replace("%",string.Empty);
        return new(){ModelId=materialized,Tier=row.TierCode switch{"SMALL"=>ModelTier.Small,"PREMIUM"=>ModelTier.Premium,_=>ModelTier.Standard},SemanticReasoning=row.SemanticReasoning,MultiAmbiguityRecall=row.MultiAmbiguityRecall,RecursiveDecomposition=row.RecursiveDecomposition,StructuralReliability=row.StructuralReliability,InstructionFollowing=row.InstructionFollowing,CostScore=row.CostScore,LatencyScore=row.LatencyScore,RecommendedMaxDepth=row.RecommendedMaxDepth,RecommendedScaffolding=FromScaffoldingCode(row.RecommendedScaffoldingCode)};
    }

    private static string ToScaffoldingCode(PromptScaffoldingLevel level)=>level.ToString().ToUpperInvariant();
    private static PromptScaffoldingLevel FromScaffoldingCode(string code)=>code switch{"HEAVY"=>PromptScaffoldingLevel.Heavy,"LIGHT"=>PromptScaffoldingLevel.Light,_=>PromptScaffoldingLevel.Medium};
    // PascalCase enum name → UPPER_SNAKE_CASE code (e.g. SubDimension → SUB_DIMENSION).
    private static string ToCode(string name)=>string.Concat(name.Select((character,index)=>char.IsUpper(character)&&index>0?"_"+character:character.ToString())).ToUpperInvariant();
    private static string? Truncate(string? value,int length)=>value is null?null:value.Length<=length?value:value[..length];

    private sealed record ModelCapabilityRow(string ModelCodePattern,string TierCode,decimal SemanticReasoning,decimal MultiAmbiguityRecall,decimal RecursiveDecomposition,decimal StructuralReliability,decimal InstructionFollowing,decimal CostScore,decimal LatencyScore,int RecommendedMaxDepth,string RecommendedScaffoldingCode);
    private sealed record PromptStrategyRow(string PurposeCode,string ScaffoldingCode,int VersionNumber,string SystemPrompt,string UserPromptTemplate);
}
