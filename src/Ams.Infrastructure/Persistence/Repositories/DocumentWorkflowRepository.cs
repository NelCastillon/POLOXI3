using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentWorkflow;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DocumentWorkflowRepository : IDocumentWorkflowRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DocumentWorkflowRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ══════════════════════════════════════════════════════════════════════
    // WORKFLOW TEMPLATES
    // ══════════════════════════════════════════════════════════════════════

    public async Task<DocumentWorkflowTemplateDto?> GetWorkflowTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentWorkflowTemplate WHERE WorkflowTemplateId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentWorkflowTemplateDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<DocumentWorkflowTemplateDto?> GetWorkflowTemplateByCodeAsync(Guid tenantId, string templateCode, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentWorkflowTemplate WHERE TenantId = @TenantId AND TemplateCode = @TemplateCode AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentWorkflowTemplateDto>(new CommandDefinition(sql, new { TenantId = tenantId, TemplateCode = templateCode }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DocumentWorkflowTemplateDto>> SearchWorkflowTemplatesAsync(Guid tenantId, string? workflowType, bool? isActive, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT * FROM DMS.DocumentWorkflowTemplate
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@WorkflowType IS NULL OR @WorkflowType = '' OR WorkflowType = @WorkflowType)
      AND (@IsActive IS NULL OR IsActive = @IsActive)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TemplateName LIKE '%' + @SearchTerm + '%' OR TemplateCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY SortOrder, TemplateName OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM DMS.DocumentWorkflowTemplate
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@WorkflowType IS NULL OR @WorkflowType = '' OR WorkflowType = @WorkflowType)
  AND (@IsActive IS NULL OR IsActive = @IsActive)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TemplateName LIKE '%' + @SearchTerm + '%' OR TemplateCode LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, WorkflowType = workflowType, IsActive = isActive, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DocumentWorkflowTemplateDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DocumentWorkflowTemplateDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<DocumentWorkflowTemplateDto>> GetActiveWorkflowTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentWorkflowTemplate WHERE TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0 ORDER BY SortOrder, TemplateName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentWorkflowTemplateDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateWorkflowTemplateAsync(CreateWorkflowTemplateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentWorkflowTemplate 
    (WorkflowTemplateId, TenantId, TemplateName, TemplateCode, Description, WorkflowType, IsSequential, RequiresAllApprovals, 
     AutoArchiveOnComplete, NotifyOnStart, NotifyOnComplete, TriggerOnUpload, TriggerOnCategory, TriggerOnDocType, SortOrder, 
     IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES 
    (@Id, @TenantId, @TemplateName, @TemplateCode, @Description, @WorkflowType, @IsSequential, @RequiresAllApprovals, 
     @AutoArchiveOnComplete, @NotifyOnStart, @NotifyOnComplete, @TriggerOnUpload, @TriggerOnCategory, @TriggerOnDocType, @SortOrder, 
     1, GETUTCDATE(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.TemplateName, request.TemplateCode, request.Description, request.WorkflowType, request.IsSequential, request.RequiresAllApprovals, request.AutoArchiveOnComplete, request.NotifyOnStart, request.NotifyOnComplete, request.TriggerOnUpload, request.TriggerOnCategory, request.TriggerOnDocType, request.SortOrder, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateWorkflowTemplateAsync(UpdateWorkflowTemplateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentWorkflowTemplate
SET TemplateName = @TemplateName, Description = @Description, IsSequential = @IsSequential, RequiresAllApprovals = @RequiresAllApprovals,
    AutoArchiveOnComplete = @AutoArchiveOnComplete, NotifyOnStart = @NotifyOnStart, NotifyOnComplete = @NotifyOnComplete,
    TriggerOnUpload = @TriggerOnUpload, TriggerOnCategory = @TriggerOnCategory, TriggerOnDocType = @TriggerOnDocType,
    SortOrder = @SortOrder, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE WorkflowTemplateId = @WorkflowTemplateId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task DeleteWorkflowTemplateAsync(DeleteWorkflowTemplateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentWorkflowTemplate SET IsDeleted = 1, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE WorkflowTemplateId = @WorkflowTemplateId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task ActivateWorkflowTemplateAsync(ActivateWorkflowTemplateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentWorkflowTemplate SET IsActive = 1, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE WorkflowTemplateId = @WorkflowTemplateId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task DeactivateWorkflowTemplateAsync(DeactivateWorkflowTemplateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentWorkflowTemplate SET IsActive = 0, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE WorkflowTemplateId = @WorkflowTemplateId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    // ══════════════════════════════════════════════════════════════════════
    // WORKFLOW STEP TEMPLATES
    // ══════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<DocumentWorkflowStepTemplateDto>> GetStepTemplatesByWorkflowIdAsync(Guid workflowTemplateId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentWorkflowStepTemplate WHERE WorkflowTemplateId = @WorkflowTemplateId AND IsDeleted = 0 ORDER BY StepOrder;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentWorkflowStepTemplateDto>(new CommandDefinition(sql, new { WorkflowTemplateId = workflowTemplateId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<DocumentWorkflowStepTemplateDto?> GetStepTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentWorkflowStepTemplate WHERE StepTemplateId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentWorkflowStepTemplateDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateStepTemplateAsync(CreateWorkflowStepTemplateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentWorkflowStepTemplate
    (StepTemplateId, TenantId, WorkflowTemplateId, StepName, StepType, StepOrder, Description, AssignedToRoleCode, AssignedToUserId,
     AssignToBranchAdmin, AssignToDocOwner, IsRequired, DueDays, EscalateDays, EscalateToRoleCode, RequiresPreviousApproval,
     SkipIfCondition, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@Id, @TenantId, @WorkflowTemplateId, @StepName, @StepType, @StepOrder, @Description, @AssignedToRoleCode, @AssignedToUserId,
     @AssignToBranchAdmin, @AssignToDocOwner, @IsRequired, @DueDays, @EscalateDays, @EscalateToRoleCode, @RequiresPreviousApproval,
     @SkipIfCondition, GETUTCDATE(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.WorkflowTemplateId, request.StepName, request.StepType, request.StepOrder, request.Description, request.AssignedToRoleCode, request.AssignedToUserId, request.AssignToBranchAdmin, request.AssignToDocOwner, request.IsRequired, request.DueDays, request.EscalateDays, request.EscalateToRoleCode, request.RequiresPreviousApproval, request.SkipIfCondition, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateStepTemplateAsync(UpdateWorkflowStepTemplateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentWorkflowStepTemplate
SET StepName = @StepName, Description = @Description, AssignedToRoleCode = @AssignedToRoleCode, AssignedToUserId = @AssignedToUserId,
    AssignToBranchAdmin = @AssignToBranchAdmin, AssignToDocOwner = @AssignToDocOwner, IsRequired = @IsRequired,
    DueDays = @DueDays, EscalateDays = @EscalateDays, EscalateToRoleCode = @EscalateToRoleCode,
    RequiresPreviousApproval = @RequiresPreviousApproval, SkipIfCondition = @SkipIfCondition,
    ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE StepTemplateId = @StepTemplateId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task DeleteStepTemplateAsync(DeleteWorkflowStepTemplateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentWorkflowStepTemplate SET IsDeleted = 1, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE StepTemplateId = @StepTemplateId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    // ══════════════════════════════════════════════════════════════════════
    // WORKFLOW INSTANCES
    // ══════════════════════════════════════════════════════════════════════

    public async Task<DocumentWorkflowInstanceDto?> GetWorkflowInstanceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentWorkflowInstance WHERE WorkflowInstanceId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentWorkflowInstanceDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DocumentWorkflowInstanceDto>> SearchWorkflowInstancesAsync(Guid tenantId, string? workflowStatus, Guid? documentId, Guid? initiatedByUserId, DateTime? startDateFrom, DateTime? startDateTo, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT * FROM DMS.DocumentWorkflowInstance
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@WorkflowStatus IS NULL OR @WorkflowStatus = '' OR WorkflowStatus = @WorkflowStatus)
      AND (@DocumentId IS NULL OR DocumentId = @DocumentId)
      AND (@InitiatedByUserId IS NULL OR InitiatedByUserId = @InitiatedByUserId)
      AND (@StartDateFrom IS NULL OR StartedDateUtc >= @StartDateFrom)
      AND (@StartDateTo IS NULL OR StartedDateUtc <= @StartDateTo)
)
SELECT * FROM Cte ORDER BY StartedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM DMS.DocumentWorkflowInstance
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@WorkflowStatus IS NULL OR @WorkflowStatus = '' OR WorkflowStatus = @WorkflowStatus)
  AND (@DocumentId IS NULL OR DocumentId = @DocumentId)
  AND (@InitiatedByUserId IS NULL OR InitiatedByUserId = @InitiatedByUserId)
  AND (@StartDateFrom IS NULL OR StartedDateUtc >= @StartDateFrom)
  AND (@StartDateTo IS NULL OR StartedDateUtc <= @StartDateTo);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, WorkflowStatus = workflowStatus, DocumentId = documentId, InitiatedByUserId = initiatedByUserId, StartDateFrom = startDateFrom, StartDateTo = startDateTo, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DocumentWorkflowInstanceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DocumentWorkflowInstanceDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<DocumentWorkflowInstanceDto>> GetWorkflowInstancesByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentWorkflowInstance WHERE DocumentId = @DocumentId AND IsDeleted = 0 ORDER BY StartedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentWorkflowInstanceDto>(new CommandDefinition(sql, new { DocumentId = documentId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<DocumentWorkflowInstanceDto>> GetActiveWorkflowInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentWorkflowInstance WHERE TenantId = @TenantId AND WorkflowStatus IN ('Pending', 'InProgress', 'Escalated') AND IsDeleted = 0 ORDER BY DueDateUtc, StartedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentWorkflowInstanceDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateWorkflowInstanceAsync(CreateWorkflowInstanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentWorkflowInstance
    (WorkflowInstanceId, TenantId, DocumentId, WorkflowTemplateId, InstanceName, WorkflowStatus, InitiatedByUserId, InitiatedByName,
     Comments, Priority, DueDateUtc, StartedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@Id, @TenantId, @DocumentId, @WorkflowTemplateId, @InstanceName, 'Pending', @InitiatedByUserId, @InitiatedByName,
     @Comments, @Priority, @DueDateUtc, GETUTCDATE(), GETUTCDATE(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.DocumentId, request.WorkflowTemplateId, request.InstanceName, request.InitiatedByUserId, request.InitiatedByName, request.Comments, request.Priority, request.DueDateUtc, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task StartWorkflowInstanceAsync(StartWorkflowInstanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentWorkflowInstance SET WorkflowStatus = 'InProgress', CurrentStepOrder = @FirstStepOrder, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE WorkflowInstanceId = @WorkflowInstanceId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task AdvanceWorkflowInstanceAsync(AdvanceWorkflowInstanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentWorkflowInstance SET CurrentStepOrder = @NextStepOrder, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE WorkflowInstanceId = @WorkflowInstanceId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task CompleteWorkflowInstanceAsync(CompleteWorkflowInstanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentWorkflowInstance
SET WorkflowStatus = 'Completed', FinalOutcome = @FinalOutcome, FinalComments = @FinalComments,
    CompletedByUserId = @CompletedByUserId, CompletedByName = @CompletedByName, CompletedDateUtc = GETUTCDATE(),
    ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE WorkflowInstanceId = @WorkflowInstanceId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task RejectWorkflowInstanceAsync(RejectWorkflowInstanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentWorkflowInstance
SET WorkflowStatus = 'Rejected', FinalOutcome = 'Rejected', FinalComments = @FinalComments,
    CompletedByUserId = @CompletedByUserId, CompletedByName = @CompletedByName, CompletedDateUtc = GETUTCDATE(),
    ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE WorkflowInstanceId = @WorkflowInstanceId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task CancelWorkflowInstanceAsync(CancelWorkflowInstanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentWorkflowInstance SET WorkflowStatus = 'Cancelled', FinalOutcome = 'Cancelled', FinalComments = @Reason, CompletedDateUtc = GETUTCDATE(), ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE WorkflowInstanceId = @WorkflowInstanceId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task EscalateWorkflowInstanceAsync(EscalateWorkflowInstanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentWorkflowInstance SET WorkflowStatus = 'Escalated', ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE WorkflowInstanceId = @WorkflowInstanceId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    // ══════════════════════════════════════════════════════════════════════
    // APPROVALS
    // ══════════════════════════════════════════════════════════════════════

    public async Task<DocumentApprovalDto?> GetApprovalByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentApproval WHERE ApprovalId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentApprovalDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DocumentApprovalDto>> SearchApprovalsAsync(Guid tenantId, string? approvalStatus, Guid? assignedToUserId, Guid? documentId, DateTime? dueDateFrom, DateTime? dueDateTo, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT * FROM DMS.DocumentApproval
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@ApprovalStatus IS NULL OR @ApprovalStatus = '' OR ApprovalStatus = @ApprovalStatus)
      AND (@AssignedToUserId IS NULL OR AssignedToUserId = @AssignedToUserId)
      AND (@DocumentId IS NULL OR DocumentId = @DocumentId)
      AND (@DueDateFrom IS NULL OR DueDateUtc >= @DueDateFrom)
      AND (@DueDateTo IS NULL OR DueDateUtc <= @DueDateTo)
)
SELECT * FROM Cte ORDER BY DueDateUtc, AssignedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM DMS.DocumentApproval
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@ApprovalStatus IS NULL OR @ApprovalStatus = '' OR ApprovalStatus = @ApprovalStatus)
  AND (@AssignedToUserId IS NULL OR AssignedToUserId = @AssignedToUserId)
  AND (@DocumentId IS NULL OR DocumentId = @DocumentId)
  AND (@DueDateFrom IS NULL OR DueDateUtc >= @DueDateFrom)
  AND (@DueDateTo IS NULL OR DueDateUtc <= @DueDateTo);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ApprovalStatus = approvalStatus, AssignedToUserId = assignedToUserId, DocumentId = documentId, DueDateFrom = dueDateFrom, DueDateTo = dueDateTo, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DocumentApprovalDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DocumentApprovalDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<DocumentApprovalDto>> GetApprovalsByWorkflowInstanceIdAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentApproval WHERE WorkflowInstanceId = @WorkflowInstanceId AND IsDeleted = 0 ORDER BY StepOrder;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentApprovalDto>(new CommandDefinition(sql, new { WorkflowInstanceId = workflowInstanceId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<DocumentApprovalDto>> GetPendingApprovalsByUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentApproval WHERE TenantId = @TenantId AND AssignedToUserId = @UserId AND ApprovalStatus = 'Pending' AND IsDeleted = 0 ORDER BY DueDateUtc, AssignedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentApprovalDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateApprovalAsync(CreateApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentApproval
    (ApprovalId, TenantId, WorkflowInstanceId, DocumentId, StepTemplateId, ApprovalName, ApprovalType, StepOrder,
     AssignedToUserId, AssignedToName, AssignedToRoleCode, DueDateUtc, ApprovalStatus, AssignedDateUtc,
     CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@Id, @TenantId, @WorkflowInstanceId, @DocumentId, @StepTemplateId, @ApprovalName, @ApprovalType, @StepOrder,
     @AssignedToUserId, @AssignedToName, @AssignedToRoleCode, @DueDateUtc, 'Pending', GETUTCDATE(),
     GETUTCDATE(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.WorkflowInstanceId, request.DocumentId, request.StepTemplateId, request.ApprovalName, request.ApprovalType, request.StepOrder, request.AssignedToUserId, request.AssignedToName, request.AssignedToRoleCode, request.DueDateUtc, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ApproveDocumentAsync(ApproveDocumentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentApproval
SET ApprovalStatus = 'Approved', ResponseDateUtc = GETUTCDATE(), ResponseByUserId = @ResponseByUserId,
    ResponseByName = @ResponseByName, Comments = @Comments, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE ApprovalId = @ApprovalId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task RejectDocumentAsync(RejectDocumentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentApproval
SET ApprovalStatus = 'Rejected', ResponseDateUtc = GETUTCDATE(), ResponseByUserId = @ResponseByUserId,
    ResponseByName = @ResponseByName, Comments = @Comments, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE ApprovalId = @ApprovalId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task DeferApprovalAsync(DeferApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentApproval SET ApprovalStatus = 'Deferred', Comments = @Comments, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ApprovalId = @ApprovalId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task EscalateApprovalAsync(EscalateApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentApproval SET ApprovalStatus = 'Escalated', EscalatedDateUtc = GETUTCDATE(), EscalatedToUserId = @EscalatedToUserId, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ApprovalId = @ApprovalId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task ReassignApprovalAsync(ReassignApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentApproval SET AssignedToUserId = @NewAssignedToUserId, AssignedToName = @NewAssignedToName, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ApprovalId = @ApprovalId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    // ══════════════════════════════════════════════════════════════════════
    // REVIEWS - Continued in next section
    // ══════════════════════════════════════════════════════════════════════

    public async Task<DocumentReviewDto?> GetReviewByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentReview WHERE ReviewId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentReviewDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DocumentReviewDto>> SearchReviewsAsync(Guid tenantId, string? reviewStatus, Guid? assignedToUserId, Guid? documentId, DateTime? dueDateFrom, DateTime? dueDateTo, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT * FROM DMS.DocumentReview
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@ReviewStatus IS NULL OR @ReviewStatus = '' OR ReviewStatus = @ReviewStatus)
      AND (@AssignedToUserId IS NULL OR AssignedToUserId = @AssignedToUserId)
      AND (@DocumentId IS NULL OR DocumentId = @DocumentId)
      AND (@DueDateFrom IS NULL OR DueDateUtc >= @DueDateFrom)
      AND (@DueDateTo IS NULL OR DueDateUtc <= @DueDateTo)
)
SELECT * FROM Cte ORDER BY DueDateUtc, AssignedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM DMS.DocumentReview
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@ReviewStatus IS NULL OR @ReviewStatus = '' OR ReviewStatus = @ReviewStatus)
  AND (@AssignedToUserId IS NULL OR AssignedToUserId = @AssignedToUserId)
  AND (@DocumentId IS NULL OR DocumentId = @DocumentId)
  AND (@DueDateFrom IS NULL OR DueDateUtc >= @DueDateFrom)
  AND (@DueDateTo IS NULL OR DueDateUtc <= @DueDateTo);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ReviewStatus = reviewStatus, AssignedToUserId = assignedToUserId, DocumentId = documentId, DueDateFrom = dueDateFrom, DueDateTo = dueDateTo, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DocumentReviewDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DocumentReviewDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<DocumentReviewDto>> GetReviewsByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentReview WHERE DocumentId = @DocumentId AND IsDeleted = 0 ORDER BY AssignedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentReviewDto>(new CommandDefinition(sql, new { DocumentId = documentId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<DocumentReviewDto>> GetPendingReviewsByUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentReview WHERE TenantId = @TenantId AND AssignedToUserId = @UserId AND ReviewStatus IN ('Pending', 'InReview') AND IsDeleted = 0 ORDER BY DueDateUtc, AssignedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentReviewDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateReviewAsync(CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentReview
    (ReviewId, TenantId, DocumentId, WorkflowInstanceId, ReviewName, ReviewType, ReviewPurpose,
     AssignedToUserId, AssignedToName, DueDateUtc, ReviewStatus, AssignedDateUtc,
     CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@Id, @TenantId, @DocumentId, @WorkflowInstanceId, @ReviewName, @ReviewType, @ReviewPurpose,
     @AssignedToUserId, @AssignedToName, @DueDateUtc, 'Pending', GETUTCDATE(),
     GETUTCDATE(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.DocumentId, request.WorkflowInstanceId, request.ReviewName, request.ReviewType, request.ReviewPurpose, request.AssignedToUserId, request.AssignedToName, request.DueDateUtc, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task StartReviewAsync(StartReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentReview SET ReviewStatus = 'InReview', ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ReviewId = @ReviewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task CompleteReviewAsync(CompleteReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentReview
SET ReviewStatus = 'Completed', CompletedDateUtc = GETUTCDATE(), CompletedByUserId = @CompletedByUserId,
    CompletedByName = @CompletedByName, ReviewNotes = @ReviewNotes, Rating = @Rating,
    IssuesFound = @IssuesFound, RecommendChanges = @RecommendChanges, ChangesDescription = @ChangesDescription,
    ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE ReviewId = @ReviewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task ReturnReviewAsync(ReturnReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentReview SET ReviewStatus = 'Returned', ReviewNotes = @ReviewNotes, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ReviewId = @ReviewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task CancelReviewAsync(CancelReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentReview SET ReviewStatus = 'Cancelled', ReviewNotes = @Reason, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ReviewId = @ReviewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task ReassignReviewAsync(ReassignReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentReview SET AssignedToUserId = @NewAssignedToUserId, AssignedToName = @NewAssignedToName, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ReviewId = @ReviewId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    // ══════════════════════════════════════════════════════════════════════
    // RETENTION POLICIES
    // ══════════════════════════════════════════════════════════════════════

    public async Task<DocumentRetentionPolicyDto?> GetRetentionPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentRetentionPolicy WHERE RetentionPolicyId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentRetentionPolicyDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<DocumentRetentionPolicyDto?> GetRetentionPolicyByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentRetentionPolicy WHERE TenantId = @TenantId AND PolicyCode = @PolicyCode AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentRetentionPolicyDto>(new CommandDefinition(sql, new { TenantId = tenantId, PolicyCode = policyCode }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DocumentRetentionPolicyDto>> SearchRetentionPoliciesAsync(Guid tenantId, bool? isActive, string? applicableCategory, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT * FROM DMS.DocumentRetentionPolicy
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@IsActive IS NULL OR IsActive = @IsActive)
      AND (@ApplicableCategory IS NULL OR @ApplicableCategory = '' OR ApplicableCategory = @ApplicableCategory)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR PolicyName LIKE '%' + @SearchTerm + '%' OR PolicyCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY PolicyName OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM DMS.DocumentRetentionPolicy
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@IsActive IS NULL OR IsActive = @IsActive)
  AND (@ApplicableCategory IS NULL OR @ApplicableCategory = '' OR ApplicableCategory = @ApplicableCategory)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR PolicyName LIKE '%' + @SearchTerm + '%' OR PolicyCode LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, IsActive = isActive, ApplicableCategory = applicableCategory, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DocumentRetentionPolicyDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DocumentRetentionPolicyDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<DocumentRetentionPolicyDto>> GetActiveRetentionPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentRetentionPolicy WHERE TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0 AND EffectiveDate <= CAST(GETUTCDATE() AS DATE) AND (ExpiryDate IS NULL OR ExpiryDate >= CAST(GETUTCDATE() AS DATE)) ORDER BY PolicyName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentRetentionPolicyDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentRetentionPolicy
    (RetentionPolicyId, TenantId, PolicyName, PolicyCode, Description, ApplicableCategory, ApplicableDocType, ApplicableEntityType,
     RetentionPeriodYears, RetentionStartTrigger, ActionOnExpiry, RequireApprovalToDelete, NotifyBeforeDays, NotifyRoleCode,
     RegulatoryBasis, ComplianceNotes, IsActive, EffectiveDate, ExpiryDate, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@Id, @TenantId, @PolicyName, @PolicyCode, @Description, @ApplicableCategory, @ApplicableDocType, @ApplicableEntityType,
     @RetentionPeriodYears, @RetentionStartTrigger, @ActionOnExpiry, @RequireApprovalToDelete, @NotifyBeforeDays, @NotifyRoleCode,
     @RegulatoryBasis, @ComplianceNotes, 1, @EffectiveDate, @ExpiryDate, GETUTCDATE(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.PolicyName, request.PolicyCode, request.Description, request.ApplicableCategory, request.ApplicableDocType, request.ApplicableEntityType, request.RetentionPeriodYears, request.RetentionStartTrigger, request.ActionOnExpiry, request.RequireApprovalToDelete, request.NotifyBeforeDays, request.NotifyRoleCode, request.RegulatoryBasis, request.ComplianceNotes, request.EffectiveDate, request.ExpiryDate, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateRetentionPolicyAsync(UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentRetentionPolicy
SET PolicyName = @PolicyName, Description = @Description, ApplicableCategory = @ApplicableCategory, ApplicableDocType = @ApplicableDocType,
    ApplicableEntityType = @ApplicableEntityType, RetentionPeriodYears = @RetentionPeriodYears, RetentionStartTrigger = @RetentionStartTrigger,
    ActionOnExpiry = @ActionOnExpiry, RequireApprovalToDelete = @RequireApprovalToDelete, NotifyBeforeDays = @NotifyBeforeDays,
    NotifyRoleCode = @NotifyRoleCode, RegulatoryBasis = @RegulatoryBasis, ComplianceNotes = @ComplianceNotes,
    ExpiryDate = @ExpiryDate, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE RetentionPolicyId = @RetentionPolicyId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task DeleteRetentionPolicyAsync(DeleteRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentRetentionPolicy SET IsDeleted = 1, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE RetentionPolicyId = @RetentionPolicyId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task ActivateRetentionPolicyAsync(ActivateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentRetentionPolicy SET IsActive = 1, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE RetentionPolicyId = @RetentionPolicyId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task DeactivateRetentionPolicyAsync(DeactivateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentRetentionPolicy SET IsActive = 0, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE RetentionPolicyId = @RetentionPolicyId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    // ══════════════════════════════════════════════════════════════════════
    // AUDIT TRAIL
    // ══════════════════════════════════════════════════════════════════════

    public async Task<PagedResult<DocumentAuditTrailDto>> SearchAuditTrailAsync(Guid tenantId, Guid? documentId, Guid? workflowInstanceId, string? eventType, Guid? performedByUserId, DateTime? eventDateFrom, DateTime? eventDateTo, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT audit.*, COALESCE(NULLIF(tenant.TenantName, N''), N'Unknown tenant') AS TenantName,
           CASE
               WHEN audit.PerformedByUserId IS NULL THEN COALESCE(NULLIF(audit.PerformedByName, N''), N'System')
               ELSE COALESCE(NULLIF([user].DisplayName, N''), NULLIF([user].FullName, N''), NULLIF([user].UserName, N''), NULLIF([user].Email, N''), NULLIF(audit.PerformedByName, N''), N'Former user')
           END AS ResolvedPerformedByName
    FROM DMS.DocumentAuditTrail audit
    LEFT JOIN Core.Tenant tenant ON tenant.TenantId = audit.TenantId
    LEFT JOIN IAM.[User] [user] ON [user].UserId = audit.PerformedByUserId AND [user].TenantId = audit.TenantId
    WHERE audit.TenantId = @TenantId
      AND (@DocumentId IS NULL OR audit.DocumentId = @DocumentId)
      AND (@WorkflowInstanceId IS NULL OR audit.WorkflowInstanceId = @WorkflowInstanceId)
      AND (@EventType IS NULL OR @EventType = '' OR audit.EventType = @EventType)
      AND (@PerformedByUserId IS NULL OR audit.PerformedByUserId = @PerformedByUserId)
      AND (@EventDateFrom IS NULL OR audit.EventDateUtc >= @EventDateFrom)
      AND (@EventDateTo IS NULL OR audit.EventDateUtc <= @EventDateTo)
)
SELECT AuditId, TenantId, TenantName, DocumentId, WorkflowInstanceId, EventType, EventCategory, EventDescription,
       PerformedByUserId, ResolvedPerformedByName AS PerformedByName, PerformedByRoleCode, EventDateUtc,
       OldValue, NewValue, ChangesSummary, IpAddress, UserAgent, SessionId, RetentionYears, IsArchived, CreatedDateUtc
FROM Cte ORDER BY EventDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM DMS.DocumentAuditTrail
WHERE TenantId = @TenantId
  AND (@DocumentId IS NULL OR DocumentId = @DocumentId)
  AND (@WorkflowInstanceId IS NULL OR WorkflowInstanceId = @WorkflowInstanceId)
  AND (@EventType IS NULL OR @EventType = '' OR EventType = @EventType)
  AND (@PerformedByUserId IS NULL OR PerformedByUserId = @PerformedByUserId)
  AND (@EventDateFrom IS NULL OR EventDateUtc >= @EventDateFrom)
  AND (@EventDateTo IS NULL OR EventDateUtc <= @EventDateTo);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, DocumentId = documentId, WorkflowInstanceId = workflowInstanceId, EventType = eventType, PerformedByUserId = performedByUserId, EventDateFrom = eventDateFrom, EventDateTo = eventDateTo, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DocumentAuditTrailDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DocumentAuditTrailDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<DocumentAuditTrailDto>> GetAuditTrailByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 100 audit.AuditId, audit.TenantId,
       COALESCE(NULLIF(tenant.TenantName, N''), N'Unknown tenant') AS TenantName,
       audit.DocumentId, audit.WorkflowInstanceId, audit.EventType, audit.EventCategory, audit.EventDescription,
       audit.PerformedByUserId,
       CASE
           WHEN audit.PerformedByUserId IS NULL THEN COALESCE(NULLIF(audit.PerformedByName, N''), N'System')
           ELSE COALESCE(NULLIF([user].DisplayName, N''), NULLIF([user].FullName, N''), NULLIF([user].UserName, N''), NULLIF([user].Email, N''), NULLIF(audit.PerformedByName, N''), N'Former user')
       END AS PerformedByName,
       audit.PerformedByRoleCode, audit.EventDateUtc, audit.OldValue, audit.NewValue, audit.ChangesSummary,
       audit.IpAddress, audit.UserAgent, audit.SessionId, audit.RetentionYears, audit.IsArchived, audit.CreatedDateUtc
FROM DMS.DocumentAuditTrail audit
LEFT JOIN Core.Tenant tenant ON tenant.TenantId = audit.TenantId
LEFT JOIN IAM.[User] [user] ON [user].UserId = audit.PerformedByUserId AND [user].TenantId = audit.TenantId
WHERE audit.DocumentId = @DocumentId
ORDER BY audit.EventDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentAuditTrailDto>(new CommandDefinition(sql, new { DocumentId = documentId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task CreateAuditTrailAsync(CreateAuditTrailRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentAuditTrail
    (AuditId, TenantId, DocumentId, WorkflowInstanceId, EventType, EventCategory, EventDescription,
     PerformedByUserId, PerformedByName, PerformedByRoleCode, EventDateUtc, OldValue, NewValue, ChangesSummary,
     IpAddress, UserAgent, SessionId, RetentionYears, IsArchived, CreatedDateUtc)
VALUES
    (NEWID(), @TenantId, @DocumentId, @WorkflowInstanceId, @EventType, @EventCategory, @EventDescription,
     @PerformedByUserId, @PerformedByName, @PerformedByRoleCode, GETUTCDATE(), @OldValue, @NewValue, @ChangesSummary,
     @IpAddress, @UserAgent, @SessionId, @RetentionYears, 0, GETUTCDATE());";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    // ══════════════════════════════════════════════════════════════════════
    // CLASSIFICATION QUEUE
    // ══════════════════════════════════════════════════════════════════════

    public async Task<DocumentClassificationQueueDto?> GetClassificationQueueByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentClassificationQueue WHERE ClassificationQueueId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentClassificationQueueDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DocumentClassificationQueueDto>> SearchClassificationQueueAsync(Guid tenantId, string? queueStatus, Guid? assignedToUserId, string? priority, DateTime? dueDateFrom, DateTime? dueDateTo, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT * FROM DMS.DocumentClassificationQueue
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@QueueStatus IS NULL OR @QueueStatus = '' OR QueueStatus = @QueueStatus)
      AND (@AssignedToUserId IS NULL OR AssignedToUserId = @AssignedToUserId)
      AND (@Priority IS NULL OR @Priority = '' OR Priority = @Priority)
      AND (@DueDateFrom IS NULL OR DueDateUtc >= @DueDateFrom)
      AND (@DueDateTo IS NULL OR DueDateUtc <= @DueDateTo)
)
SELECT * FROM Cte ORDER BY Priority DESC, DueDateUtc, CreatedDateUtc OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM DMS.DocumentClassificationQueue
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@QueueStatus IS NULL OR @QueueStatus = '' OR QueueStatus = @QueueStatus)
  AND (@AssignedToUserId IS NULL OR AssignedToUserId = @AssignedToUserId)
  AND (@Priority IS NULL OR @Priority = '' OR Priority = @Priority)
  AND (@DueDateFrom IS NULL OR DueDateUtc >= @DueDateFrom)
  AND (@DueDateTo IS NULL OR DueDateUtc <= @DueDateTo);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, QueueStatus = queueStatus, AssignedToUserId = assignedToUserId, Priority = priority, DueDateFrom = dueDateFrom, DueDateTo = dueDateTo, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DocumentClassificationQueueDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DocumentClassificationQueueDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<DocumentClassificationQueueDto>> GetPendingClassificationsByUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM DMS.DocumentClassificationQueue WHERE TenantId = @TenantId AND AssignedToUserId = @UserId AND QueueStatus = 'InReview' AND IsDeleted = 0 ORDER BY Priority DESC, DueDateUtc, CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<DocumentClassificationQueueDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateClassificationQueueAsync(CreateClassificationQueueRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO DMS.DocumentClassificationQueue
    (ClassificationQueueId, TenantId, DocumentId, QueueStatus, ClassificationMethod, OcrConfidence, SuggestedCategory, SuggestedDocType,
     ExtractedText, ExtractedMetadata, Priority, DueDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@Id, @TenantId, @DocumentId, 'Pending', @ClassificationMethod, @OcrConfidence, @SuggestedCategory, @SuggestedDocType,
     @ExtractedText, @ExtractedMetadata, @Priority, @DueDateUtc, GETUTCDATE(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.DocumentId, request.ClassificationMethod, request.OcrConfidence, request.SuggestedCategory, request.SuggestedDocType, request.ExtractedText, request.ExtractedMetadata, request.Priority, request.DueDateUtc, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task AssignClassificationAsync(AssignClassificationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentClassificationQueue SET QueueStatus = 'InReview', AssignedToUserId = @AssignedToUserId, AssignedToName = @AssignedToName, AssignedDateUtc = GETUTCDATE(), ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ClassificationQueueId = @ClassificationQueueId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task ClassifyDocumentAsync(ClassifyDocumentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentClassificationQueue
SET QueueStatus = 'Classified', ClassifiedByUserId = @ClassifiedByUserId, ClassifiedByName = @ClassifiedByName,
    ClassifiedDateUtc = GETUTCDATE(), FinalCategory = @FinalCategory, FinalDocType = @FinalDocType,
    ClassificationNotes = @ClassificationNotes, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId
WHERE ClassificationQueueId = @ClassificationQueueId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task MarkClassificationFailedAsync(MarkClassificationFailedRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentClassificationQueue SET QueueStatus = 'Failed', ClassificationNotes = @Reason, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ClassificationQueueId = @ClassificationQueueId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task SkipClassificationAsync(SkipClassificationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE DMS.DocumentClassificationQueue SET QueueStatus = 'Skipped', ClassificationNotes = @Reason, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ModifiedByUserId WHERE ClassificationQueueId = @ClassificationQueueId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }
}
