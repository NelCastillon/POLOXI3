SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.Notification',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Core.Notification',N'RecipientAddress') IS NULL ALTER TABLE Core.Notification ADD RecipientAddress NVARCHAR(320) NULL;
	IF COL_LENGTH(N'Core.Notification',N'ReplyToAddress') IS NULL ALTER TABLE Core.Notification ADD ReplyToAddress NVARCHAR(320) NULL;
	IF COL_LENGTH(N'Core.Notification',N'IsBodyHtml') IS NULL ALTER TABLE Core.Notification ADD IsBodyHtml BIT NOT NULL CONSTRAINT DF_Core_Notification_IsBodyHtml_0282 DEFAULT 0;
	IF COL_LENGTH(N'Core.Notification',N'ExternalCorrelationId') IS NULL ALTER TABLE Core.Notification ADD ExternalCorrelationId NVARCHAR(200) NULL;
	IF COL_LENGTH(N'Core.Notification',N'NextAttemptDateUtc') IS NULL ALTER TABLE Core.Notification ADD NextAttemptDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Core.Notification',N'MaxAttempts') IS NULL ALTER TABLE Core.Notification ADD MaxAttempts INT NOT NULL CONSTRAINT DF_Core_Notification_MaxAttempts_0282 DEFAULT 5;
END;
GO

IF OBJECT_ID(N'Core.NotificationDeliveryProvider',N'U') IS NULL
BEGIN
	CREATE TABLE Core.NotificationDeliveryProvider
	(
		NotificationDeliveryProviderId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Core_NotificationDeliveryProvider PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		ProviderCode NVARCHAR(100) NOT NULL,
		ChannelCode NVARCHAR(50) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		EndpointReference NVARCHAR(1000) NOT NULL,
		SenderAddress NVARCHAR(320) NOT NULL,
		SenderDisplayName NVARCHAR(200) NULL,
		CredentialReference NVARCHAR(500) NULL,
		ConfigurationJson NVARCHAR(MAX) NOT NULL,
		MaxAttempts INT NOT NULL CONSTRAINT DF_Core_NotificationDeliveryProvider_MaxAttempts DEFAULT 5,
		RetryDelaySeconds INT NOT NULL CONSTRAINT DF_Core_NotificationDeliveryProvider_Retry DEFAULT 300,
		IsActive BIT NOT NULL CONSTRAINT DF_Core_NotificationDeliveryProvider_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Core_NotificationDeliveryProvider_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Core_NotificationDeliveryProvider_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Core_NotificationDeliveryProvider_Config CHECK(ISJSON(ConfigurationJson)=1),
		CONSTRAINT CK_Core_NotificationDeliveryProvider_Attempts CHECK(MaxAttempts BETWEEN 1 AND 25),
		CONSTRAINT CK_Core_NotificationDeliveryProvider_Retry CHECK(RetryDelaySeconds BETWEEN 10 AND 86400)
	);
	CREATE UNIQUE INDEX UX_Core_NotificationDeliveryProvider_Global ON Core.NotificationDeliveryProvider(ProviderCode,ChannelCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Core_NotificationDeliveryProvider_Tenant ON Core.NotificationDeliveryProvider(TenantId,ProviderCode,ChannelCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;

IF OBJECT_ID(N'Core.NotificationAttachment',N'U') IS NULL
BEGIN
	CREATE TABLE Core.NotificationAttachment
	(
		NotificationAttachmentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Core_NotificationAttachment PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		NotificationId UNIQUEIDENTIFIER NOT NULL,
		DocumentId UNIQUEIDENTIFIER NULL,
		StorageReference NVARCHAR(2000) NULL,
		FileName NVARCHAR(500) NOT NULL,
		ContentType NVARCHAR(200) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Core_NotificationAttachment_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Core_NotificationAttachment_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_Core_NotificationAttachment_Notification FOREIGN KEY(NotificationId) REFERENCES Core.Notification(NotificationId),
		CONSTRAINT CK_Core_NotificationAttachment_Source CHECK(DocumentId IS NOT NULL OR StorageReference IS NOT NULL)
	);
	CREATE INDEX IX_Core_NotificationAttachment_Notification ON Core.NotificationAttachment(TenantId,NotificationId) INCLUDE(DocumentId,StorageReference,FileName,ContentType) WHERE IsDeleted=0;
END;

IF OBJECT_ID(N'Platform.OperationalOption',N'U') IS NULL
BEGIN
	CREATE TABLE Platform.OperationalOption
	(
		OperationalOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Platform_OperationalOption PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		OptionGroupCode NVARCHAR(120) NOT NULL,
		OptionCode NVARCHAR(120) NOT NULL,
		DisplayName NVARCHAR(240) NOT NULL,
		Description NVARCHAR(1000) NULL,
		MetadataJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Platform_OperationalOption_Metadata DEFAULT N'{}',
		SortOrder INT NOT NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_Platform_OperationalOption_Default DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Platform_OperationalOption_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Platform_OperationalOption_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Platform_OperationalOption_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_Platform_OperationalOption_Metadata CHECK(ISJSON(MetadataJson)=1)
	);
	CREATE UNIQUE INDEX UX_Platform_OperationalOption_Global ON Platform.OperationalOption(OptionGroupCode,OptionCode) WHERE TenantId IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_Platform_OperationalOption_Tenant ON Platform.OperationalOption(TenantId,OptionGroupCode,OptionCode) WHERE TenantId IS NOT NULL AND IsDeleted=0;
END;
GO

DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(2000),DataTypeCode NVARCHAR(50),Description NVARCHAR(1000),IsEncrypted BIT);
INSERT @Settings VALUES
(N'DocumentIntelligence.Endpoint',N'',N'String',N'Azure Document Intelligence endpoint URI.',0),
(N'DocumentIntelligence.ModelId',N'prebuilt-layout',N'String',N'Azure Document Intelligence model identifier.',0),
(N'DocumentIntelligence.ApiVersion',N'2024-11-30',N'String',N'Azure Document Intelligence API version.',0),
(N'DocumentIntelligence.CredentialReference',N'',N'String',N'env://VARIABLE_NAME credential reference; blank uses managed identity.',1),
(N'DocumentIntelligence.TimeoutSeconds',N'180',N'Integer',N'Document Intelligence request timeout.',0),
(N'Notification.Smtp.Endpoint',N'smtp://netsol-smtp-oxcs.hostingplatform.com:587',N'String',N'Shared SMTP endpoint URI.',0),
(N'Notification.Smtp.SenderAddress',N'ams_admin@agencybinder.com',N'String',N'Shared SMTP sender address.',0),
(N'Notification.Smtp.SenderDisplayName',N'AgencyBinder',N'String',N'Shared SMTP sender display name.',0),
(N'Notification.Smtp.CredentialReference',N'env://AMS_PROPOSAL_SMTP_PASSWORD',N'String',N'Shared SMTP credential environment reference.',1),
(N'Notification.Smtp.Configuration',N'{"username":"ams_admin@agencybinder.com","enableSsl":true}',N'JSON',N'Shared SMTP transport configuration.',0),
(N'Platform.ContactIntakeNotificationRecipientEmail',N'ams_admin@agencybinder.com',N'String',N'Contact-intake notification recipient.',0);
MERGE Core.ConfigurationSetting target USING @Settings source ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET ModuleCode=N'Platform',DefaultValue=source.SettingValue,DataTypeCode=source.DataTypeCode,Description=source.Description,IsEncrypted=source.IsEncrypted,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc) VALUES(NEWID(),NULL,N'Platform',N'Platform',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,source.IsEncrypted,0,0,SYSUTCDATETIME());

MERGE Core.NotificationDeliveryProvider target USING
(
	SELECT CAST(NULL AS UNIQUEIDENTIFIER) TenantId,N'PLATFORM_SMTP' ProviderCode,N'Email' ChannelCode,N'Platform SMTP' DisplayName,
		   COALESCE(MAX(CASE WHEN SettingKey=N'Notification.Smtp.Endpoint' THEN COALESCE(NULLIF(SettingValue,N''),DefaultValue) END),N'') EndpointReference,
		   COALESCE(MAX(CASE WHEN SettingKey=N'Notification.Smtp.SenderAddress' THEN COALESCE(NULLIF(SettingValue,N''),DefaultValue) END),N'') SenderAddress,
		   MAX(CASE WHEN SettingKey=N'Notification.Smtp.SenderDisplayName' THEN COALESCE(NULLIF(SettingValue,N''),DefaultValue) END) SenderDisplayName,
		   MAX(CASE WHEN SettingKey=N'Notification.Smtp.CredentialReference' THEN COALESCE(NULLIF(SettingValue,N''),DefaultValue) END) CredentialReference,
		   COALESCE(MAX(CASE WHEN SettingKey=N'Notification.Smtp.Configuration' THEN COALESCE(NULLIF(SettingValue,N''),DefaultValue) END),N'{}') ConfigurationJson
	FROM Core.ConfigurationSetting WHERE TenantId IS NULL AND ScopeCode=N'Platform' AND IsDeleted=0
) source ON target.TenantId IS NULL AND target.ProviderCode=source.ProviderCode AND target.ChannelCode=source.ChannelCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,EndpointReference=source.EndpointReference,SenderAddress=source.SenderAddress,SenderDisplayName=source.SenderDisplayName,CredentialReference=source.CredentialReference,ConfigurationJson=source.ConfigurationJson,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(NotificationDeliveryProviderId,TenantId,ProviderCode,ChannelCode,DisplayName,EndpointReference,SenderAddress,SenderDisplayName,CredentialReference,ConfigurationJson,MaxAttempts,RetryDelaySeconds,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.ProviderCode,source.ChannelCode,source.DisplayName,source.EndpointReference,source.SenderAddress,source.SenderDisplayName,source.CredentialReference,source.ConfigurationJson,5,300,1,SYSUTCDATETIME(),0);

DECLARE @Options TABLE(OptionGroupCode NVARCHAR(120),OptionCode NVARCHAR(120),DisplayName NVARCHAR(240),Description NVARCHAR(1000),MetadataJson NVARCHAR(MAX),SortOrder INT,IsDefault BIT);
INSERT @Options VALUES
(N'ActivityType',N'Call',N'Call',N'Phone activity.',N'{"icon":"bi-telephone"}',10,0),
(N'ActivityType',N'Email',N'Email',N'Email activity.',N'{"icon":"bi-envelope"}',20,0),
(N'ActivityType',N'Meeting',N'Meeting',N'Meeting activity.',N'{"icon":"bi-calendar-event"}',30,0),
(N'ActivityType',N'Task',N'Task',N'Task follow-through activity.',N'{"icon":"bi-check2-square"}',40,0),
(N'ActivityType',N'Note',N'Note',N'Internal note activity.',N'{"icon":"bi-sticky"}',50,1),
(N'ActivityType',N'Workflow',N'Workflow',N'Workflow-generated activity.',N'{"icon":"bi-diagram-3"}',60,0),
(N'NotificationChannel',N'Email',N'Email',N'Email delivery channel.',N'{}',10,1),
(N'NotificationChannel',N'SMS',N'SMS',N'SMS delivery channel.',N'{}',20,0),
(N'NotificationChannel',N'Phone',N'Phone',N'Phone outreach channel.',N'{}',30,0),
(N'NotificationChannel',N'Portal',N'Portal',N'Portal delivery channel.',N'{}',40,0),
(N'NotificationChannel',N'Letter',N'Letter',N'Printed-letter delivery channel.',N'{}',50,0),
(N'NotificationStatus',N'Queued',N'Queued',N'Waiting for delivery.',N'{}',10,1),
(N'NotificationStatus',N'Processing',N'Processing',N'Delivery in progress.',N'{}',20,0),
(N'NotificationStatus',N'Sent',N'Sent',N'Provider accepted delivery.',N'{}',30,0),
(N'NotificationStatus',N'Delivered',N'Delivered',N'Delivery confirmed.',N'{}',40,0),
(N'NotificationStatus',N'Failed',N'Failed',N'Delivery failed.',N'{}',50,0),
(N'NotificationStatus',N'Cancelled',N'Cancelled',N'Delivery cancelled.',N'{}',60,0),
(N'WorkPriority',N'Low',N'Low',N'Low priority.',N'{}',10,0),
(N'WorkPriority',N'Medium',N'Medium',N'Medium priority.',N'{}',20,1),
(N'WorkPriority',N'High',N'High',N'High priority.',N'{}',30,0),
(N'WorkPriority',N'Critical',N'Critical',N'Critical priority.',N'{}',40,0);
MERGE Platform.OperationalOption target USING @Options source ON target.TenantId IS NULL AND target.OptionGroupCode=source.OptionGroupCode AND target.OptionCode=source.OptionCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,MetadataJson=source.MetadataJson,SortOrder=source.SortOrder,IsDefault=source.IsDefault,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(OperationalOptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,MetadataJson,SortOrder,IsDefault,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.OptionGroupCode,source.OptionCode,source.DisplayName,source.Description,source.MetadataJson,source.SortOrder,source.IsDefault,1,SYSUTCDATETIME(),0);

MERGE Core.NotificationTemplate target USING(VALUES
(N'PROPOSAL_DELIVERY',N'Proposal Delivery',N'Email',N'{{Subject}}',N'{{Body}}'),
(N'CONTACT_INTAKE',N'Contact Intake',N'Email',N'AgencyBinder demo request {{RequestNumber}} - {{AgencyName}}',N'{{Body}}')) source(TemplateCode,TemplateName,ChannelCode,SubjectTemplate,BodyTemplate)
ON target.TenantId IS NULL AND target.TemplateCode=source.TemplateCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET TemplateName=source.TemplateName,ChannelCode=source.ChannelCode,SubjectTemplate=source.SubjectTemplate,BodyTemplate=source.BodyTemplate,IsSystemTemplate=1,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TemplateId,TenantId,TemplateCode,TemplateName,ChannelCode,SubjectTemplate,BodyTemplate,IsSystemTemplate,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.TemplateCode,source.TemplateName,source.ChannelCode,source.SubjectTemplate,source.BodyTemplate,1,1,SYSUTCDATETIME(),0);
