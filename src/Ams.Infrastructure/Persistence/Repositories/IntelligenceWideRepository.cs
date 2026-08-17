using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Intelligence;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

// Dapper persistence for the isolated Wide dynamic disambiguation pipeline.
public sealed class IntelligenceWideRepository(ISqlConnectionFactory connectionFactory):IIntelligenceWideRepository
{
    public async Task<WideConfiguration> GetWideConfigurationAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT
COALESCE(TRY_CONVERT(decimal(5,4),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.SearchWide.TargetConfidence' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.85) TargetConfidence,
COALESCE(TRY_CONVERT(decimal(5,4),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.SearchWide.MinimumBranchConfidence' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.35) MinimumBranchConfidence,
COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.SearchWide.MaximumBranchesPerLevel' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),5) MaximumBranchesPerLevel,
COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.SearchWide.AbsoluteDepthCeiling' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),25) AbsoluteDepthCeiling,
COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.SearchWide.MaximumTotalLlmCalls' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),30) MaximumTotalLlmCalls;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row=await connection.QuerySingleAsync<WideConfigurationRow>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));
        return new(Math.Clamp(row.TargetConfidence,0,1),Math.Clamp(row.MinimumBranchConfidence,0,1),Math.Clamp(row.MaximumBranchesPerLevel,1,12),Math.Clamp(row.AbsoluteDepthCeiling,1,100),Math.Clamp(row.MaximumTotalLlmCalls,1,200));
    }

    public async Task<Guid> StartWideExecutionAsync(WideExecutionStart start,CancellationToken cancellationToken=default)
    {
        const string sql="""
DECLARE @WideExecutionId UNIQUEIDENTIFIER=NEWID();
INSERT EPH.WideExecution(WideExecutionId,TenantId,UserId,QueryText,CorrelationId,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@WideExecutionId,@TenantId,@UserId,@QueryText,@CorrelationId,N'RUNNING',SYSUTCDATETIME(),@UserId,0);
SELECT @WideExecutionId;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,new{start.TenantId,start.UserId,start.QueryText,start.CorrelationId},cancellationToken:cancellationToken));
    }

    public async Task SaveWideBranchesAsync(IReadOnlyCollection<WideBranchRecord> branches,Guid userId,CancellationToken cancellationToken=default)
    {
        if(branches.Count==0)return;
        const string sql="""
INSERT EPH.WideBranch(WideBranchId,WideExecutionId,ParentWideBranchId,TenantId,LevelNumber,BranchCode,DisplayName,Interpretation,CapabilityCode,SearchText,GroundingStatusCode,EvidenceCount,Confidence,ContinueNarrowing,StopReason,IsEliminated,EliminationReason,SortOrder,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@WideBranchId,@WideExecutionId,@ParentWideBranchId,@TenantId,@LevelNumber,@BranchCode,@DisplayName,@Interpretation,@CapabilityCode,@SearchText,@GroundingStatusCode,@EvidenceCount,@Confidence,@ContinueNarrowing,@StopReason,@IsEliminated,@EliminationReason,@SortOrder,SYSUTCDATETIME(),@UserId,0);
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        foreach(var branch in branches)
            await connection.ExecuteAsync(new CommandDefinition(sql,new{branch.WideBranchId,branch.WideExecutionId,branch.ParentWideBranchId,branch.TenantId,branch.LevelNumber,branch.BranchCode,branch.DisplayName,branch.Interpretation,branch.CapabilityCode,branch.SearchText,branch.GroundingStatusCode,branch.EvidenceCount,branch.Confidence,branch.ContinueNarrowing,branch.StopReason,branch.IsEliminated,branch.EliminationReason,branch.SortOrder,UserId=userId},cancellationToken:cancellationToken));
    }

    public async Task UpdateWideBranchOutcomeAsync(Guid tenantId,Guid wideBranchId,string groundingStatusCode,int evidenceCount,bool isEliminated,string? eliminationReason,CancellationToken cancellationToken=default)
    {
        const string sql="""
UPDATE EPH.WideBranch SET GroundingStatusCode=@GroundingStatusCode,EvidenceCount=@EvidenceCount,IsEliminated=@IsEliminated,EliminationReason=@EliminationReason,ModifiedDateUtc=SYSUTCDATETIME()
WHERE WideBranchId=@WideBranchId AND TenantId=@TenantId AND IsDeleted=0;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,WideBranchId=wideBranchId,GroundingStatusCode=groundingStatusCode,EvidenceCount=evidenceCount,IsEliminated=isEliminated,EliminationReason=eliminationReason},cancellationToken:cancellationToken));
    }

    public async Task CompleteWideExecutionAsync(Guid tenantId,Guid userId,Guid wideExecutionId,string statusCode,string terminationReasonCode,int depthReached,int llmCallCount,decimal finalConfidence,string answerVerificationCode,string? finalAnswer,long durationMilliseconds,CancellationToken cancellationToken=default)
    {
        const string sql="""
UPDATE EPH.WideExecution SET StatusCode=@StatusCode,TerminationReasonCode=@TerminationReasonCode,DepthReached=@DepthReached,LlmCallCount=@LlmCallCount,FinalConfidence=@FinalConfidence,AnswerVerificationCode=@AnswerVerificationCode,FinalAnswer=@FinalAnswer,DurationMilliseconds=@DurationMilliseconds,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId
WHERE WideExecutionId=@WideExecutionId AND TenantId=@TenantId AND IsDeleted=0;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,UserId=userId,WideExecutionId=wideExecutionId,StatusCode=statusCode,TerminationReasonCode=terminationReasonCode,DepthReached=depthReached,LlmCallCount=llmCallCount,FinalConfidence=finalConfidence,AnswerVerificationCode=answerVerificationCode,FinalAnswer=finalAnswer,DurationMilliseconds=durationMilliseconds},cancellationToken:cancellationToken));
    }

    private sealed record WideConfigurationRow(decimal TargetConfidence,decimal MinimumBranchConfidence,int MaximumBranchesPerLevel,int AbsoluteDepthCeiling,int MaximumTotalLlmCalls);
}
