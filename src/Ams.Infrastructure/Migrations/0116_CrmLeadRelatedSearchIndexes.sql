IF OBJECT_ID(N'CRM.Opportunity', N'U') IS NOT NULL
   AND COL_LENGTH(N'CRM.Opportunity', N'LeadId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'CRM.Opportunity')
         AND name = N'IX_CRM_Opportunity_Tenant_Lead'
   )
BEGIN
    CREATE INDEX IX_CRM_Opportunity_Tenant_Lead
        ON CRM.Opportunity(TenantId, LeadId, IsDeleted)
        INCLUDE (OpportunityNumber, OpportunityName, StageName, ForecastCategoryCode);
END;
