using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Communications;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommTemplateRepository : ICommTemplateRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommTemplateRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = @"
        TemplateId, TenantId, Name, Channel, Category, Language, Status,
        Subject, Body, IncludeOptOutFooter, TcpaNotice, UsageCount,
        ApprovalStatus, ApprovedBy, ApprovedDateUtc, ComplianceStatus,
        OwnerTeam, SourceSystem, VersionNumber, LastSyncedDateUtc,
        CreatedDateUtc, ModifiedDateUtc AS UpdatedAt";

    private async Task EnsureTemplateSchemaAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Comms') EXEC(N'CREATE SCHEMA Comms');

IF OBJECT_ID(N'Comms.Template', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.Template (TemplateId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(160) NOT NULL, Channel NVARCHAR(40) NOT NULL, Category NVARCHAR(80) NOT NULL, Language NVARCHAR(40) NOT NULL DEFAULT N'English', Status NVARCHAR(40) NOT NULL DEFAULT N'Active', Subject NVARCHAR(200) NULL, Body NVARCHAR(4000) NOT NULL, IncludeOptOutFooter BIT NOT NULL DEFAULT 0, TcpaNotice BIT NOT NULL DEFAULT 0, UsageCount INT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF COL_LENGTH('Comms.Template','ApprovalStatus') IS NULL ALTER TABLE Comms.Template ADD ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_CommTemplate_ApprovalStatus DEFAULT N'Approved';
IF COL_LENGTH('Comms.Template','ApprovedBy') IS NULL ALTER TABLE Comms.Template ADD ApprovedBy NVARCHAR(150) NOT NULL CONSTRAINT DF_CommTemplate_ApprovedBy DEFAULT N'Tenant Admin';
IF COL_LENGTH('Comms.Template','ApprovedDateUtc') IS NULL ALTER TABLE Comms.Template ADD ApprovedDateUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.Template','ComplianceStatus') IS NULL ALTER TABLE Comms.Template ADD ComplianceStatus NVARCHAR(80) NOT NULL CONSTRAINT DF_CommTemplate_ComplianceStatus DEFAULT N'Clear';
IF COL_LENGTH('Comms.Template','OwnerTeam') IS NULL ALTER TABLE Comms.Template ADD OwnerTeam NVARCHAR(120) NOT NULL CONSTRAINT DF_CommTemplate_OwnerTeam DEFAULT N'Communications';
IF COL_LENGTH('Comms.Template','SourceSystem') IS NULL ALTER TABLE Comms.Template ADD SourceSystem NVARCHAR(80) NOT NULL CONSTRAINT DF_CommTemplate_SourceSystem DEFAULT N'AMS';
IF COL_LENGTH('Comms.Template','VersionNumber') IS NULL ALTER TABLE Comms.Template ADD VersionNumber INT NOT NULL CONSTRAINT DF_CommTemplate_VersionNumber DEFAULT 1;
IF COL_LENGTH('Comms.Template','LastSyncedDateUtc') IS NULL ALTER TABLE Comms.Template ADD LastSyncedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommTemplate_LastSyncedDateUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.Template','CreatedDateUtc') IS NULL ALTER TABLE Comms.Template ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommTemplate_CreatedDateUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.Template','ModifiedDateUtc') IS NULL ALTER TABLE Comms.Template ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.Template','IsDeleted') IS NULL ALTER TABLE Comms.Template ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommTemplate_IsDeleted DEFAULT 0;

IF OBJECT_ID(N'Comms.TemplateVersion', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.TemplateVersion (TemplateVersionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, TemplateId UNIQUEIDENTIFIER NOT NULL, VersionNumber INT NOT NULL, Name NVARCHAR(160) NOT NULL, Subject NVARCHAR(200) NULL, Body NVARCHAR(4000) NOT NULL, ChangeSummary NVARCHAR(500) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.TemplateAuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.TemplateAuditLog (TemplateAuditLogId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, TemplateId UNIQUEIDENTIFIER NULL, ActionName NVARCHAR(80) NOT NULL, ActorName NVARCHAR(150) NOT NULL, Details NVARCHAR(1000) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.TemplateVariable', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.TemplateVariable (TemplateVariableId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, VariableName NVARCHAR(120) NOT NULL, DisplayName NVARCHAR(160) NOT NULL, DataSource NVARCHAR(120) NOT NULL, IsRequired BIT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;";

        const string seedSql = @"
IF @TenantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Comms.Template WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
    INSERT INTO Comms.Template (TemplateId,TenantId,Name,Channel,Category,Language,Status,Subject,Body,IncludeOptOutFooter,TcpaNotice,UsageCount,ApprovalStatus,ApprovedBy,ApprovedDateUtc,ComplianceStatus,OwnerTeam,SourceSystem,VersionNumber,LastSyncedDateUtc,CreatedDateUtc,ModifiedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Policy Renewal Reminder — Email',N'Email',N'Renewal',N'English',N'Active',N'Your policy renewal is approaching',N'Dear [Client Name],

Your [Policy Type] policy [Policy #] is approaching renewal on [Renewal Date]. Please review the attached proposal and contact [Agent Name] with any questions.

Best regards,
[Agency Name]',1,0,24,N'Approved',N'Tenant Admin',SYSUTCDATETIME(),N'Clear',N'Communications',N'AMS',1,SYSUTCDATETIME(),DATEADD(day,-28,SYSUTCDATETIME()),SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Claim Acknowledgement — SMS',N'SMS',N'Claims',N'English',N'Active',NULL,N'Hi [Client Name], we received claim [Claim #]. Your claims advocate [Agent Name] will follow up within 1 business day. Reply STOP to opt out.',0,1,18,N'Approved',N'Compliance Admin',SYSUTCDATETIME(),N'Clear',N'Claims',N'AMS',1,SYSUTCDATETIME(),DATEADD(day,-18,SYSUTCDATETIME()),SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Certificate Request Confirmation',N'Email',N'Policy Service',N'English',N'Active',N'Certificate request received',N'Dear [Client Name],

We received your certificate request for [Certificate Holder]. Our service team will deliver the COI within [SLA Hours] business hours.

Thank you,
[Agency Name]',1,0,31,N'Approved',N'Tenant Admin',SYSUTCDATETIME(),N'Clear',N'Service',N'AMS',1,SYSUTCDATETIME(),DATEADD(day,-14,SYSUTCDATETIME()),SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'CAT Event Check-In',N'Portal Message',N'CAT / Emergency',N'English',N'Draft',N'Checking in after [Event Name]',N'Dear [Client Name],

We are checking in after [Event Name]. If you have damage or need claims help, please contact us immediately.

[Agent Name]',0,0,5,N'Pending Review',N'',NULL,N'Review Required',N'Claims',N'AMS',1,SYSUTCDATETIME(),DATEADD(day,-5,SYSUTCDATETIME()),SYSUTCDATETIME(),0);
END;

IF @TenantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Comms.TemplateVariable WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
    INSERT INTO Comms.TemplateVariable (TemplateVariableId,TenantId,VariableName,DisplayName,DataSource,IsRequired,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Client Name',N'Client Name',N'Account/Contact',1,0),
    (NEWID(),@TenantId,N'Policy #',N'Policy Number',N'Policy',0,0),
    (NEWID(),@TenantId,N'Agent Name',N'Assigned Agent',N'User',1,0),
    (NEWID(),@TenantId,N'Renewal Date',N'Renewal Date',N'Policy',0,0),
    (NEWID(),@TenantId,N'Agency Name',N'Agency Name',N'Tenant',1,0);
END;

IF @TenantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Comms.TemplateAuditLog WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
    INSERT INTO Comms.TemplateAuditLog (TemplateAuditLogId,TenantId,TemplateId,ActionName,ActorName,Details,IsDeleted)
    SELECT NEWID(), TenantId, TemplateId, N'Seeded', N'System', N'Initial enterprise template governance seed.', 0
    FROM Comms.Template WHERE TenantId=@TenantId AND IsDeleted=0;
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CommTemplateDto>> GetByTenantAsync(Guid tenantId, string? channel = null, string? category = null, string? status = null, CancellationToken cancellationToken = default)
    {
        await EnsureTemplateSchemaAsync(tenantId, cancellationToken);
        var sql = $@"
SELECT {SelectColumns}
FROM Comms.Template
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@Channel  IS NULL OR Channel  = @Channel)
  AND (@Category IS NULL OR Category = @Category)
  AND (@Status   IS NULL OR Status   = @Status)
ORDER BY Category, Name;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QueryAsync<CommTemplateDto>(new CommandDefinition(sql,
            new { TenantId = tenantId, Channel = channel, Category = category, Status = status },
            cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<CommTemplateDto?> GetByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureTemplateSchemaAsync(null, cancellationToken);
        var sql = $"SELECT {SelectColumns} FROM Comms.Template WHERE TemplateId = @TemplateId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommTemplateDto>(new CommandDefinition(sql, new { TemplateId = templateId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateCommTemplateRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTemplateSchemaAsync(request.TenantId, cancellationToken);
        var id  = Guid.NewGuid();
        var sql = @"
INSERT INTO Comms.Template
    (TemplateId, TenantId, Name, Channel, Category, Language, Status,
     Subject, Body, IncludeOptOutFooter, TcpaNotice, UsageCount, ApprovalStatus, ApprovedBy, ApprovedDateUtc,
     ComplianceStatus, OwnerTeam, SourceSystem, VersionNumber, LastSyncedDateUtc, IsDeleted, CreatedDateUtc, ModifiedDateUtc)
VALUES
    (@TemplateId, @TenantId, @Name, @Channel, @Category, @Language, @Status,
     @Subject, @Body, @IncludeOptOutFooter, @TcpaNotice, 0, @ApprovalStatus, @ApprovedBy, @ApprovedDateUtc,
     @ComplianceStatus, @OwnerTeam, 'AMS', 1, SYSUTCDATETIME(), 0, SYSUTCDATETIME(), SYSUTCDATETIME());

INSERT INTO Comms.TemplateVersion (TemplateVersionId,TenantId,TemplateId,VersionNumber,Name,Subject,Body,ChangeSummary,IsDeleted)
VALUES (NEWID(),@TenantId,@TemplateId,1,@Name,@Subject,@Body,N'Initial version',0);

INSERT INTO Comms.TemplateAuditLog (TemplateAuditLogId,TenantId,TemplateId,ActionName,ActorName,Details,IsDeleted)
VALUES (NEWID(),@TenantId,@TemplateId,N'Created',N'Tenant Admin',CONCAT('Created template ',@Name),0);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TemplateId = id,
            request.TenantId,
            request.Name,
            request.Channel,
            request.Category,
            request.Language,
            request.Status,
            Subject = request.Subject,
            request.Body,
            request.IncludeOptOutFooter,
            request.TcpaNotice,
            ApprovalStatus = request.Status == "Active" ? "Approved" : "Pending Review",
            ApprovedBy = request.Status == "Active" ? "Tenant Admin" : string.Empty,
            ApprovedDateUtc = request.Status == "Active" ? DateTime.UtcNow : (DateTime?)null,
            ComplianceStatus = ResolveComplianceStatus(request.Channel, request.IncludeOptOutFooter, request.TcpaNotice),
            OwnerTeam = ResolveOwnerTeam(request.Category)
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(UpdateCommTemplateRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTemplateSchemaAsync(null, cancellationToken);
        var sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER, @VersionNumber INT;
SELECT @TenantId = TenantId, @VersionNumber = VersionNumber + 1 FROM Comms.Template WHERE TemplateId = @TemplateId AND IsDeleted = 0;

UPDATE Comms.Template
SET Name = @Name, Channel = @Channel, Category = @Category, Language = @Language,
    Status = @Status, Subject = @Subject, Body = @Body,
    IncludeOptOutFooter = @IncludeOptOutFooter, TcpaNotice = @TcpaNotice,
    ApprovalStatus = @ApprovalStatus, ApprovedBy = @ApprovedBy, ApprovedDateUtc = @ApprovedDateUtc,
    ComplianceStatus = @ComplianceStatus, OwnerTeam = @OwnerTeam, VersionNumber = @VersionNumber,
    LastSyncedDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE TemplateId = @TemplateId AND IsDeleted = 0;";

        sql += @"

IF @TenantId IS NOT NULL
BEGIN
    INSERT INTO Comms.TemplateVersion (TemplateVersionId,TenantId,TemplateId,VersionNumber,Name,Subject,Body,ChangeSummary,IsDeleted)
    VALUES (NEWID(),@TenantId,@TemplateId,@VersionNumber,@Name,@Subject,@Body,N'Template updated',0);

    INSERT INTO Comms.TemplateAuditLog (TemplateAuditLogId,TenantId,TemplateId,ActionName,ActorName,Details,IsDeleted)
    VALUES (NEWID(),@TenantId,@TemplateId,N'Updated',N'Tenant Admin',CONCAT('Updated template ',@Name),0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.TemplateId,
            request.Name,
            request.Channel,
            request.Category,
            request.Language,
            request.Status,
            Subject = request.Subject,
            request.Body,
            request.IncludeOptOutFooter,
            request.TcpaNotice,
            ApprovalStatus = request.Status == "Active" ? "Approved" : "Pending Review",
            ApprovedBy = request.Status == "Active" ? "Tenant Admin" : string.Empty,
            ApprovedDateUtc = request.Status == "Active" ? DateTime.UtcNow : (DateTime?)null,
            ComplianceStatus = ResolveComplianceStatus(request.Channel, request.IncludeOptOutFooter, request.TcpaNotice),
            OwnerTeam = ResolveOwnerTeam(request.Category)
        }, cancellationToken: cancellationToken));
    }

    public async Task IncrementUsageAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureTemplateSchemaAsync(null, cancellationToken);
        var sql = @"
UPDATE Comms.Template
SET UsageCount = UsageCount + 1, LastSyncedDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE TemplateId = @TemplateId AND IsDeleted = 0;

INSERT INTO Comms.TemplateAuditLog (TemplateAuditLogId,TenantId,TemplateId,ActionName,ActorName,Details,IsDeleted)
SELECT NEWID(), TenantId, TemplateId, N'Used', N'Tenant Admin', N'Template selected for compose workflow.', 0 FROM Comms.Template WHERE TemplateId=@TemplateId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TemplateId = templateId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureTemplateSchemaAsync(null, cancellationToken);
        var sql = @"
INSERT INTO Comms.TemplateAuditLog (TemplateAuditLogId,TenantId,TemplateId,ActionName,ActorName,Details,IsDeleted)
SELECT NEWID(), TenantId, TemplateId, N'Deleted', N'Tenant Admin', CONCAT('Deleted template ',Name), 0 FROM Comms.Template WHERE TemplateId=@TemplateId;

UPDATE Comms.Template SET IsDeleted = 1, LastSyncedDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE TemplateId = @TemplateId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TemplateId = templateId }, cancellationToken: cancellationToken));
    }

    private static string ResolveComplianceStatus(string channel, bool includeOptOutFooter, bool tcpaNotice)
        => channel switch
        {
            "SMS" when !tcpaNotice => "Review Required",
            "Email" when !includeOptOutFooter => "Review Required",
            _ => "Clear"
        };

    private static string ResolveOwnerTeam(string category)
        => category switch
        {
            "Claims" or "CAT / Emergency" => "Claims",
            "Billing / Payment" => "Billing",
            "Marketing" or "Welcome / Onboarding" => "Marketing",
            "Internal / Staff" => "Operations",
            _ => "Communications"
        };
}
