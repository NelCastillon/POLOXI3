using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Submissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SubmissionWorkflowConfigurationRepository : ISubmissionWorkflowConfigurationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SubmissionWorkflowConfigurationRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private static async Task EnsureSchemaAsync(System.Data.IDbConnection cn, Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Submissions') EXEC(N'CREATE SCHEMA Submissions');

IF OBJECT_ID(N'Submissions.SubmissionIntakeTemplate', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionIntakeTemplate
    (
        IntakeTemplateId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionIntakeTemplate PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        LineOfBusiness NVARCHAR(100) NOT NULL,
        QuestionCode NVARCHAR(100) NOT NULL,
        QuestionText NVARCHAR(500) NOT NULL,
        HelpText NVARCHAR(1000) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_IsRequired DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_SortOrder DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_IsActive DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionIntakeTemplate_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.SubmissionDocumentRequirement', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionDocumentRequirement
    (
        DocumentRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionDocumentRequirement PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        LineOfBusiness NVARCHAR(100) NOT NULL,
        CategoryCode NVARCHAR(100) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsRequired DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_SortOrder DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsActive DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsDeleted DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.SubmissionDocumentRequirement', N'IsActive') IS NULL ALTER TABLE Submissions.SubmissionDocumentRequirement ADD IsActive BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsActive_Runtime DEFAULT 1;
IF COL_LENGTH(N'Submissions.SubmissionDocumentRequirement', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.SubmissionDocumentRequirement ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionDocumentRequirement', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionDocumentRequirement ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionDocumentRequirement', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionDocumentRequirement ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionIntakeTemplate') AND name = N'UX_SubmissionIntakeTemplate_Tenant_Lob_Code')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionIntakeTemplate_Tenant_Lob_Code ON Submissions.SubmissionIntakeTemplate(TenantId, LineOfBusiness, QuestionCode) WHERE IsDeleted = 0;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionDocumentRequirement') AND name = N'UX_SubmissionDocumentRequirement_Tenant_Lob_Code')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionDocumentRequirement_Tenant_Lob_Code ON Submissions.SubmissionDocumentRequirement(TenantId, LineOfBusiness, CategoryCode) WHERE IsDeleted = 0;');

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionIntakeTemplate WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionIntakeTemplate (IntakeTemplateId, TenantId, LineOfBusiness, QuestionCode, QuestionText, HelpText, IsRequired, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
    SELECT NEWID(), @TenantId, lob.LineOfBusiness, src.QuestionCode, src.QuestionText, src.HelpText, src.IsRequired, src.SortOrder, 1, SYSUTCDATETIME(), 0
    FROM (VALUES
        (N'OperationsDescription', N'Operations description complete', N'Confirm operations, locations, exposures, and risk narrative are complete.', CAST(1 AS bit), 10),
        (N'CoverageNeeds', N'Coverage needs confirmed', N'Confirm limits, deductibles, forms, and requested coverage enhancements.', CAST(1 AS bit), 20),
        (N'LossHistoryReviewed', N'Loss history reviewed', N'Confirm loss runs and known claim explanations have been reviewed.', CAST(1 AS bit), 30),
        (N'ExposureDataValidated', N'Exposure data validated', N'Confirm schedules, payroll, sales, vehicles, properties, and other exposure bases are complete.', CAST(1 AS bit), 40),
        (N'ProducerPreference', N'Producer preference documented', N'Capture producer/client preference that may influence recommendation scoring.', CAST(0 AS bit), 50)
    ) src(QuestionCode, QuestionText, HelpText, IsRequired, SortOrder)
    CROSS JOIN (SELECT N'General Liability' AS LineOfBusiness UNION SELECT DISTINCT COALESCE(NULLIF(LineOfBusiness, N''), N'General Liability') FROM Submissions.Submission WHERE TenantId = @TenantId AND IsDeleted = 0) lob;
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionDocumentRequirement WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionDocumentRequirement (DocumentRequirementId, TenantId, LineOfBusiness, CategoryCode, DisplayName, IsRequired, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
    SELECT NEWID(), @TenantId, lob.LineOfBusiness, req.CategoryCode, req.DisplayName, 1, req.SortOrder, 1, SYSUTCDATETIME(), 0
    FROM (VALUES (N'Application', N'Application', 10), (N'LossRuns', N'Loss runs', 20), (N'ExposureSchedules', N'Exposure schedules', 30), (N'PriorPolicies', N'Prior policies', 40), (N'Financials', N'Financials', 50), (N'ACORD', N'ACORD forms', 60)) req(CategoryCode, DisplayName, SortOrder)
    CROSS JOIN (SELECT N'General Liability' AS LineOfBusiness UNION SELECT DISTINCT COALESCE(NULLIF(LineOfBusiness, N''), N'General Liability') FROM Submissions.Submission WHERE TenantId = @TenantId AND IsDeleted = 0) lob;
END;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<SubmissionWorkflowConfigurationSummaryDto> GetSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
SELECT COUNT(1), SUM(CASE WHEN IsRequired = 1 THEN 1 ELSE 0 END) FROM Submissions.SubmissionIntakeTemplate WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1;
SELECT COUNT(1), SUM(CASE WHEN IsRequired = 1 THEN 1 ELSE 0 END) FROM Submissions.SubmissionDocumentRequirement WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1;
SELECT COUNT(DISTINCT LineOfBusiness) FROM (
    SELECT LineOfBusiness FROM Submissions.SubmissionIntakeTemplate WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
    UNION SELECT LineOfBusiness FROM Submissions.SubmissionDocumentRequirement WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
) x;";
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var intake = await multi.ReadSingleAsync<(int Count, int Required)>();
        var docs = await multi.ReadSingleAsync<(int Count, int Required)>();
        return new SubmissionWorkflowConfigurationSummaryDto
        {
            IntakeTemplateCount = intake.Count,
            RequiredIntakeTemplateCount = intake.Required,
            DocumentRequirementCount = docs.Count,
            RequiredDocumentRequirementCount = docs.Required,
            LineOfBusinessCount = await multi.ReadSingleAsync<int>()
        };
    }

    public async Task<IReadOnlyList<SubmissionIntakeTemplateDto>> GetIntakeTemplatesAsync(Guid tenantId, string? lineOfBusiness = null, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
SELECT IntakeTemplateId, TenantId, LineOfBusiness, QuestionCode, QuestionText, COALESCE(HelpText, N'') AS HelpText, IsRequired, SortOrder, IsActive, CreatedDateUtc, ModifiedDateUtc
FROM Submissions.SubmissionIntakeTemplate
WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@LineOfBusiness IS NULL OR @LineOfBusiness = N'' OR LineOfBusiness = @LineOfBusiness)
ORDER BY LineOfBusiness, SortOrder, QuestionText;";
        return (await cn.QueryAsync<SubmissionIntakeTemplateDto>(new CommandDefinition(sql, new { TenantId = tenantId, LineOfBusiness = lineOfBusiness }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> UpsertIntakeTemplateAsync(Guid? intakeTemplateId, UpsertSubmissionIntakeTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var id = intakeTemplateId.GetValueOrDefault(Guid.NewGuid());
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
IF EXISTS (SELECT 1 FROM Submissions.SubmissionIntakeTemplate WHERE IntakeTemplateId = @Id AND TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    UPDATE Submissions.SubmissionIntakeTemplate
    SET LineOfBusiness = @LineOfBusiness, QuestionCode = @QuestionCode, QuestionText = @QuestionText, HelpText = @HelpText, IsRequired = @IsRequired,
        SortOrder = @SortOrder, IsActive = @IsActive, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
    WHERE IntakeTemplateId = @Id AND TenantId = @TenantId;
END
ELSE
BEGIN
    INSERT INTO Submissions.SubmissionIntakeTemplate (IntakeTemplateId, TenantId, LineOfBusiness, QuestionCode, QuestionText, HelpText, IsRequired, SortOrder, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@Id, @TenantId, @LineOfBusiness, @QuestionCode, @QuestionText, @HelpText, @IsRequired, @SortOrder, @IsActive, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.LineOfBusiness, request.QuestionCode, request.QuestionText, request.HelpText, request.IsRequired, request.SortOrder, request.IsActive, request.ModifiedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task DeleteIntakeTemplateAsync(Guid intakeTemplateId, Guid tenantId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(cn, tenantId, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Submissions.SubmissionIntakeTemplate SET IsDeleted = 1, IsActive = 0, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE IntakeTemplateId = @Id AND TenantId = @TenantId;", new { Id = intakeTemplateId, TenantId = tenantId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionDocumentRequirementDto>> GetDocumentRequirementsAsync(Guid tenantId, string? lineOfBusiness = null, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
SELECT DocumentRequirementId, TenantId, LineOfBusiness, CategoryCode, DisplayName, IsRequired, SortOrder, IsActive, CreatedDateUtc, ModifiedDateUtc
FROM Submissions.SubmissionDocumentRequirement
WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@LineOfBusiness IS NULL OR @LineOfBusiness = N'' OR LineOfBusiness = @LineOfBusiness)
ORDER BY LineOfBusiness, SortOrder, DisplayName;";
        return (await cn.QueryAsync<SubmissionDocumentRequirementDto>(new CommandDefinition(sql, new { TenantId = tenantId, LineOfBusiness = lineOfBusiness }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> UpsertDocumentRequirementAsync(Guid? documentRequirementId, UpsertSubmissionDocumentRequirementRequest request, CancellationToken cancellationToken = default)
    {
        var id = documentRequirementId.GetValueOrDefault(Guid.NewGuid());
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
IF EXISTS (SELECT 1 FROM Submissions.SubmissionDocumentRequirement WHERE DocumentRequirementId = @Id AND TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    UPDATE Submissions.SubmissionDocumentRequirement
    SET LineOfBusiness = @LineOfBusiness, CategoryCode = @CategoryCode, DisplayName = @DisplayName, IsRequired = @IsRequired,
        SortOrder = @SortOrder, IsActive = @IsActive, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
    WHERE DocumentRequirementId = @Id AND TenantId = @TenantId;
END
ELSE
BEGIN
    INSERT INTO Submissions.SubmissionDocumentRequirement (DocumentRequirementId, TenantId, LineOfBusiness, CategoryCode, DisplayName, IsRequired, SortOrder, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@Id, @TenantId, @LineOfBusiness, @CategoryCode, @DisplayName, @IsRequired, @SortOrder, @IsActive, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.LineOfBusiness, request.CategoryCode, request.DisplayName, request.IsRequired, request.SortOrder, request.IsActive, request.ModifiedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task DeleteDocumentRequirementAsync(Guid documentRequirementId, Guid tenantId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(cn, tenantId, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Submissions.SubmissionDocumentRequirement SET IsDeleted = 1, IsActive = 0, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE DocumentRequirementId = @Id AND TenantId = @TenantId;", new { Id = documentRequirementId, TenantId = tenantId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
