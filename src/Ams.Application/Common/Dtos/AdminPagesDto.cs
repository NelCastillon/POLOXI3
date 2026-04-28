using System;

namespace Ams.Application.Common.Dtos;

/// <summary>
/// Business Rule DTO for managing workflow rules and business logic
/// </summary>
public record BusinessRuleDto
{
    public Guid BusinessRuleId { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Category { get; set; } // Policy, Billing, Claims, Compliance, Workflow
    public required string Trigger { get; set; }
    public string? Condition { get; set; }
    public required string Action { get; set; }
    public string? Priority { get; set; } // High, Medium, Low
    public string? Status { get; set; } // Active, Inactive, Draft
    public bool IsSystemRule { get; set; }
    public int ExecutionOrder { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

/// <summary>
/// Department/Team DTO for organizational hierarchy
/// </summary>
public record DepartmentTeamDto
{
    public Guid TeamId { get; set; }
    public Guid TenantId { get; set; }
    public required string TeamName { get; set; }
    public string? Description { get; set; }
    public Guid? DepartmentId { get; set; }
    public int MemberCount { get; set; }
    public string? Status { get; set; } // Active, Inactive
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

/// <summary>
/// Producer/Staff DTO for managing agency personnel
/// </summary>
public record ProducerStaffDto
{
    public Guid StaffId { get; set; }
    public Guid TenantId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public required string Role { get; set; } // Producer, CSR, Manager
    public string? NpnLicense { get; set; }
    public DateTime? LicenseExpiryDate { get; set; }
    public string? Status { get; set; } // Active, Inactive, Terminated
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

/// <summary>
/// System Settings/Configuration DTO
/// </summary>
public record SystemSettingsDto
{
    public Guid SettingId { get; set; }
    public Guid TenantId { get; set; }
    public required string SettingKey { get; set; }
    public required string SettingValue { get; set; }
    public required string Category { get; set; }
    public string? Description { get; set; }
    public string? DataType { get; set; } // String, Integer, Boolean, DateTime
    public bool IsEncrypted { get; set; }
    public DateTime ModifiedDateUtc { get; set; }
}

/// <summary>
/// Notification Policy DTO
/// </summary>
public record NotificationPolicyDto
{
    public Guid PolicyId { get; set; }
    public Guid TenantId { get; set; }
    public required string PolicyName { get; set; }
    public string? Description { get; set; }
    public required string TriggerEvent { get; set; }
    public string? NotificationChannels { get; set; } // Email, SMS, InApp
    public string? Recipients { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

/// <summary>
/// Queue Routing Rule DTO
/// </summary>
public record QueueRoutingRuleDto
{
    public Guid RuleId { get; set; }
    public Guid TenantId { get; set; }
    public required string RoutingKey { get; set; }
    public required string SourceQueue { get; set; }
    public required string DestinationQueue { get; set; }
    public int Priority { get; set; }
    public string? Condition { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

/// <summary>
/// Data Quality Rule DTO
/// </summary>
public record DataQualityRuleDto
{
    public Guid RuleId { get; set; }
    public Guid TenantId { get; set; }
    public required string RuleName { get; set; }
    public required string TableName { get; set; }
    public required string RuleDefinition { get; set; }
    public string? Category { get; set; } // Completeness, Accuracy, Validity
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

/// <summary>
/// Data Center Configuration DTO
/// </summary>
public record DataCenterConfigDto
{
    public Guid ConfigId { get; set; }
    public Guid TenantId { get; set; }
    public required string DataCenterName { get; set; }
    public string? Region { get; set; }
    public string? Environment { get; set; } // Development, Staging, Production
    public string? ConnectionString { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

/// <summary>
/// SLA Policy Setup DTO
/// </summary>
public record SlaPolicySetupDto
{
    public Guid PolicyId { get; set; }
    public Guid TenantId { get; set; }
    public required string PolicyName { get; set; }
    public string? Description { get; set; }
    public required string SeverityLevel { get; set; } // Critical, High, Medium, Low
    public int ResponseTimeMinutes { get; set; }
    public int ResolutionTimeMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
