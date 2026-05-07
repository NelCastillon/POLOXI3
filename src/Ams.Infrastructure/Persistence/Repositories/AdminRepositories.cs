using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public class BusinessRuleRepository : IBusinessRuleRepository
{
    private static readonly List<BusinessRuleDto> _data = new();
    public Task<IReadOnlyList<BusinessRuleDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BusinessRuleDto>)_data);
    public Task<IReadOnlyList<BusinessRuleDto>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BusinessRuleDto>)_data.Where(r => r.Category == category).ToList());
    public Task<BusinessRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(r => r.BusinessRuleId == id));
    public async Task<Guid> CreateAsync(BusinessRuleDto rule, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(rule with { BusinessRuleId = id }); return id; }
    public Task UpdateAsync(BusinessRuleDto rule, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(r => r.BusinessRuleId == rule.BusinessRuleId); if (i >= 0) _data[i] = rule; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(r => r.BusinessRuleId == id); return Task.CompletedTask; }
    public Task ToggleStatusAsync(Guid id, CancellationToken ct = default) { var r = _data.FirstOrDefault(x => x.BusinessRuleId == id); if (r != null) { var i = _data.IndexOf(r); _data[i] = r with { Status = r.Status == "Active" ? "Inactive" : "Active" }; } return Task.CompletedTask; }
}

public class DepartmentTeamRepository : IDepartmentTeamRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public DepartmentTeamRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<DepartmentTeamDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT MIN(UserId) AS TeamId,
       TenantId,
       COALESCE(NULLIF(JobTitle, ''), COALESCE(NULLIF(Department, ''), 'General') + ' Team') AS TeamName,
       UPPER(REPLACE(COALESCE(NULLIF(JobTitle, ''), COALESCE(NULLIF(Department, ''), 'General') + ' Team'), ' ', '-')) AS TeamCode,
       NULL AS Description,
       NULL AS DepartmentId,
       COALESCE(NULLIF(Department, ''), 'Unassigned') AS DepartmentName,
       MAX(CASE WHEN JobTitle LIKE '%Manager%' THEN FullName END) AS ManagerName,
       COUNT(1) AS MemberCount,
       CASE WHEN SUM(CASE WHEN StatusCode = 'Active' THEN 1 ELSE 0 END) > 0 THEN 'Active' ELSE 'Inactive' END AS Status,
       CAST(CASE WHEN SUM(CASE WHEN StatusCode = 'Active' THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS bit) AS IsActive,
       MIN(CreatedDateUtc) AS CreatedDateUtc
FROM IAM.[User]
WHERE TenantId = @TenantId AND IsDeleted = 0
GROUP BY TenantId, COALESCE(NULLIF(Department, ''), 'Unassigned'), COALESCE(NULLIF(JobTitle, ''), COALESCE(NULLIF(Department, ''), 'General') + ' Team')
ORDER BY DepartmentName, TeamName";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var results = await cn.QueryAsync<DepartmentTeamDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return results.ToList();
    }

    public async Task<DepartmentTeamDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT TeamId, TenantId, TeamName, Description, DepartmentId, MemberCount, 
       TeamCode,
       ManagerName,
       (SELECT DepartmentName FROM Agency.Department d WHERE d.DepartmentId = t.DepartmentId) AS DepartmentName,
       CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status, IsActive, CreatedDateUtc
FROM Agency.Team t
WHERE TeamId = @TeamId AND IsDeleted = 0";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<DepartmentTeamDto>(new CommandDefinition(sql, new { TeamId = id }, cancellationToken: ct));
    }

    public async Task<Guid> CreateAsync(DepartmentTeamDto team, Guid userId, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Agency.Team (TeamId, TenantId, TeamName, TeamCode, Description, DepartmentId, ManagerName, MemberCount, IsActive, CreatedDateUtc, CreatedByUserId)
VALUES (@TeamId, @TenantId, @TeamName, @TeamCode, @Description, @DepartmentId, @ManagerName, @MemberCount, @IsActive, GETUTCDATE(), @UserId)";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TeamId = id,
            team.TenantId,
            team.TeamName,
            team.TeamCode,
            team.Description,
            team.DepartmentId,
            team.ManagerName,
            MemberCount = team.MemberCount,
            team.IsActive,
            UserId = userId
        }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(DepartmentTeamDto team, Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Agency.Team
SET TeamName = @TeamName, TeamCode = @TeamCode, Description = @Description, DepartmentId = @DepartmentId, 
    ManagerName = @ManagerName, MemberCount = @MemberCount, IsActive = @IsActive, ModifiedDateUtc = GETUTCDATE()
WHERE TeamId = @TeamId AND IsDeleted = 0";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            team.TeamId,
            team.TeamName,
            team.TeamCode,
            team.Description,
            team.DepartmentId,
            team.ManagerName,
            team.MemberCount,
            team.IsActive,
            UserId = userId
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Agency.Team
SET IsDeleted = 1, ModifiedDateUtc = GETUTCDATE()
WHERE TeamId = @TeamId";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TeamId = id }, cancellationToken: ct));
    }
}

public class DepartmentRepository : IDepartmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public DepartmentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT MIN(UserId) AS DepartmentId,
       TenantId,
       COALESCE(NULLIF(Department, ''), 'Unassigned') AS DepartmentName,
       UPPER(REPLACE(COALESCE(NULLIF(Department, ''), 'Unassigned'), ' ', '-')) AS DepartmentCode,
       NULL AS Description,
       MAX(CASE WHEN JobTitle LIKE '%Manager%' THEN FullName END) AS ManagerName,
       COUNT(DISTINCT COALESCE(NULLIF(JobTitle, ''), 'General')) AS TeamCount,
       CASE WHEN SUM(CASE WHEN StatusCode = 'Active' THEN 1 ELSE 0 END) > 0 THEN 'Active' ELSE 'Inactive' END AS Status,
       CAST(CASE WHEN SUM(CASE WHEN StatusCode = 'Active' THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS bit) AS IsActive,
       MIN(CreatedDateUtc) AS CreatedDateUtc
FROM IAM.[User]
WHERE TenantId = @TenantId AND IsDeleted = 0
GROUP BY TenantId, COALESCE(NULLIF(Department, ''), 'Unassigned')
ORDER BY DepartmentName";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var results = await cn.QueryAsync<DepartmentDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return results.ToList();
    }

    public async Task<DepartmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT d.DepartmentId, d.TenantId, d.DepartmentName, d.DepartmentCode, d.Description, d.ManagerName,
       (SELECT COUNT(1) FROM Agency.Team t WHERE t.DepartmentId = d.DepartmentId AND t.IsDeleted = 0) AS TeamCount,
       CASE WHEN d.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status,
       d.IsActive, d.CreatedDateUtc
FROM Agency.Department d
WHERE d.DepartmentId = @DepartmentId AND d.IsDeleted = 0";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<DepartmentDto>(new CommandDefinition(sql, new { DepartmentId = id }, cancellationToken: ct));
    }

    public async Task<Guid> CreateAsync(DepartmentDto department, Guid userId, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
DECLARE @BranchId UNIQUEIDENTIFIER = (SELECT TOP 1 BranchId FROM Agency.Branch WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);

INSERT INTO Agency.Department (DepartmentId, TenantId, BranchId, DepartmentName, DepartmentCode, Description, ManagerName, IsActive, CreatedDateUtc, CreatedByUserId)
VALUES (@DepartmentId, @TenantId, @BranchId, @DepartmentName, @DepartmentCode, @Description, @ManagerName, @IsActive, GETUTCDATE(), @UserId)";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            DepartmentId = id,
            department.TenantId,
            department.DepartmentName,
            department.DepartmentCode,
            department.Description,
            department.ManagerName,
            department.IsActive,
            UserId = userId
        }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(DepartmentDto department, Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Agency.Department
SET DepartmentName = @DepartmentName, DepartmentCode = @DepartmentCode, Description = @Description,
    ManagerName = @ManagerName, IsActive = @IsActive, ModifiedDateUtc = GETUTCDATE()
WHERE DepartmentId = @DepartmentId AND IsDeleted = 0";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            department.DepartmentId,
            department.DepartmentName,
            department.DepartmentCode,
            department.Description,
            department.ManagerName,
            department.IsActive
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Agency.Department
SET IsDeleted = 1, ModifiedDateUtc = GETUTCDATE()
WHERE DepartmentId = @DepartmentId";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { DepartmentId = id }, cancellationToken: ct));
    }
}

public class ProducerStaffRepository : IProducerStaffRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ProducerStaffRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private static readonly List<ProducerStaffDto> _fallbackData = [];

    public async Task<IReadOnlyList<ProducerStaffDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Agency.Staff', N'U') IS NOT NULL
BEGIN
SELECT StaffId,
       TenantId,
       FirstName,
       LastName,
       Email,
       Phone,
       Role,
       Department,
       Team AS TeamName,
       LicenseNumber AS NpnLicense,
       LicenseStates,
       LicenseExpiryDate,
       CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status,
       IsActive,
       CreatedDateUtc
FROM Agency.Staff
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY LastName, FirstName
END
ELSE
BEGIN
SELECT UserId AS StaffId,
       TenantId,
       LEFT(FullName, CASE WHEN CHARINDEX(' ', FullName + ' ') = 0 THEN LEN(FullName) ELSE CHARINDEX(' ', FullName + ' ') - 1 END) AS FirstName,
       LTRIM(SUBSTRING(FullName, CHARINDEX(' ', FullName + ' '), LEN(FullName))) AS LastName,
       Email,
       PhoneNumber AS Phone,
       CASE
            WHEN UserTypeCode IN ('Producer', 'CSR', 'Manager') THEN UserTypeCode
            ELSE COALESCE(NULLIF(UserTypeCode, ''), 'Staff')
       END AS Role,
       Department,
       JobTitle AS TeamName,
       CAST(NULL AS nvarchar(100)) AS NpnLicense,
       CAST(NULL AS nvarchar(500)) AS LicenseStates,
       CAST(NULL AS datetime2) AS LicenseExpiryDate,
       StatusCode AS Status,
       CAST(CASE WHEN StatusCode = 'Active' THEN 1 ELSE 0 END AS bit) AS IsActive,
       CreatedDateUtc
FROM IAM.[User]
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY FullName
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var results = await cn.QueryAsync<ProducerStaffDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        var list = results.ToList();
        if (list.Count > 0)
            return list;

        EnsureFallbackData(tenantId);
        return _fallbackData.Where(s => s.TenantId == tenantId).OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
    }

    public async Task<IReadOnlyList<ProducerStaffDto>> GetByRoleAsync(Guid tenantId, string role, CancellationToken ct = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Agency.Staff', N'U') IS NOT NULL
BEGIN
SELECT StaffId,
       TenantId,
       FirstName,
       LastName,
       Email,
       Phone,
       Role,
       Department,
       Team AS TeamName,
       LicenseNumber AS NpnLicense,
       LicenseStates,
       LicenseExpiryDate,
       CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status,
       IsActive,
       CreatedDateUtc
FROM Agency.Staff
WHERE TenantId = @TenantId AND IsDeleted = 0 AND Role = @Role
ORDER BY LastName, FirstName
END
ELSE
BEGIN
SELECT UserId AS StaffId,
       TenantId,
       LEFT(FullName, CASE WHEN CHARINDEX(' ', FullName + ' ') = 0 THEN LEN(FullName) ELSE CHARINDEX(' ', FullName + ' ') - 1 END) AS FirstName,
       LTRIM(SUBSTRING(FullName, CHARINDEX(' ', FullName + ' '), LEN(FullName))) AS LastName,
       Email,
       PhoneNumber AS Phone,
       CASE
            WHEN UserTypeCode IN ('Producer', 'CSR', 'Manager') THEN UserTypeCode
            ELSE COALESCE(NULLIF(UserTypeCode, ''), 'Staff')
       END AS Role,
       Department,
       JobTitle AS TeamName,
       CAST(NULL AS nvarchar(100)) AS NpnLicense,
       CAST(NULL AS nvarchar(500)) AS LicenseStates,
       CAST(NULL AS datetime2) AS LicenseExpiryDate,
       StatusCode AS Status,
       CAST(CASE WHEN StatusCode = 'Active' THEN 1 ELSE 0 END AS bit) AS IsActive,
       CreatedDateUtc
FROM IAM.[User]
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND CASE
           WHEN UserTypeCode IN ('Producer', 'CSR', 'Manager') THEN UserTypeCode
           ELSE COALESCE(NULLIF(UserTypeCode, ''), 'Staff')
      END = @Role
ORDER BY FullName
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var results = await cn.QueryAsync<ProducerStaffDto>(new CommandDefinition(sql, new { TenantId = tenantId, Role = role }, cancellationToken: ct));
        var list = results.ToList();
        if (list.Count > 0)
            return list;

        EnsureFallbackData(tenantId);
        return _fallbackData.Where(s => s.TenantId == tenantId && RoleEquals(s.Role, role)).OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
    }

    public async Task<ProducerStaffDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Agency.Staff', N'U') IS NOT NULL
BEGIN
SELECT StaffId, TenantId, FirstName, LastName, Email, Phone, Role, 
       Department, Team AS TeamName, LicenseNumber AS NpnLicense, LicenseStates, LicenseExpiryDate, 
       CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status, IsActive, CreatedDateUtc
FROM Agency.Staff
WHERE StaffId = @StaffId AND IsDeleted = 0
END
ELSE
BEGIN
SELECT CAST(NULL AS uniqueidentifier) AS StaffId WHERE 1 = 0
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var staff = await cn.QuerySingleOrDefaultAsync<ProducerStaffDto>(new CommandDefinition(sql, new { StaffId = id }, cancellationToken: ct));
        return staff ?? _fallbackData.FirstOrDefault(s => s.StaffId == id);
    }

    public async Task<Guid> CreateAsync(ProducerStaffDto staff, Guid userId, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
IF OBJECT_ID(N'Agency.Staff', N'U') IS NOT NULL
BEGIN
INSERT INTO Agency.Staff (StaffId, TenantId, FirstName, LastName, Email, Phone, Role, Department, Team,
                          LicenseNumber, LicenseStates, LicenseExpiryDate, IsActive, CreatedDateUtc, CreatedByUserId)
VALUES (@StaffId, @TenantId, @FirstName, @LastName, @Email, @Phone, @Role, 
        @Department, @TeamName, @NpnLicense, @LicenseStates, @LicenseExpiryDate, @IsActive, GETUTCDATE(), @UserId)
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            StaffId = id,
            staff.TenantId,
            staff.FirstName,
            staff.LastName,
            staff.Email,
            staff.Phone,
            staff.Role,
            staff.Department,
            staff.TeamName,
            staff.NpnLicense,
            staff.LicenseStates,
            staff.LicenseExpiryDate,
            staff.IsActive,
            UserId = userId
        }, cancellationToken: ct));
        if (await cn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN OBJECT_ID(N'Agency.Staff', N'U') IS NOT NULL THEN 1 ELSE 0 END", cancellationToken: ct)) == 0)
        {
            _fallbackData.Add(staff with { StaffId = id, CreatedDateUtc = DateTime.UtcNow, Status = staff.IsActive ? "Active" : "Inactive" });
        }
        return id;
    }

    public async Task UpdateAsync(ProducerStaffDto staff, Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Agency.Staff', N'U') IS NOT NULL
BEGIN
UPDATE Agency.Staff
SET FirstName = @FirstName, LastName = @LastName, Email = @Email, Phone = @Phone, 
    Role = @Role, Department = @Department, Team = @TeamName, LicenseNumber = @NpnLicense, LicenseStates = @LicenseStates, LicenseExpiryDate = @LicenseExpiryDate, 
    IsActive = @IsActive, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @UserId
WHERE StaffId = @StaffId AND IsDeleted = 0
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            staff.StaffId,
            staff.FirstName,
            staff.LastName,
            staff.Email,
            staff.Phone,
            staff.Role,
            staff.Department,
            staff.TeamName,
            staff.NpnLicense,
            staff.LicenseStates,
            staff.LicenseExpiryDate,
            staff.IsActive,
            UserId = userId
        }, cancellationToken: ct));
        var index = _fallbackData.FindIndex(s => s.StaffId == staff.StaffId);
        if (index >= 0)
            _fallbackData[index] = staff with { Status = staff.IsActive ? "Active" : "Inactive" };
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Agency.Staff', N'U') IS NOT NULL
BEGIN
UPDATE Agency.Staff
SET IsDeleted = 1, ModifiedDateUtc = GETUTCDATE()
WHERE StaffId = @StaffId
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { StaffId = id }, cancellationToken: ct));
        _fallbackData.RemoveAll(s => s.StaffId == id);
    }

    public async Task<IReadOnlyList<ProducerStaffDto>> GetExpiringLicensesAsync(Guid tenantId, int days, CancellationToken ct = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Agency.Staff', N'U') IS NOT NULL
BEGIN
SELECT StaffId, TenantId, FirstName, LastName, Email, Phone, Role, 
       Department, Team AS TeamName, LicenseNumber AS NpnLicense, LicenseStates, LicenseExpiryDate, 
       CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status, IsActive, CreatedDateUtc
FROM Agency.Staff
WHERE TenantId = @TenantId AND IsDeleted = 0 
  AND LicenseExpiryDate IS NOT NULL 
  AND LicenseExpiryDate <= DATEADD(DAY, @Days, GETUTCDATE())
ORDER BY LicenseExpiryDate, LastName, FirstName
END
ELSE
BEGIN
SELECT CAST(NULL AS uniqueidentifier) AS StaffId,
       CAST(NULL AS uniqueidentifier) AS TenantId,
       CAST(NULL AS nvarchar(100)) AS FirstName,
       CAST(NULL AS nvarchar(100)) AS LastName,
       CAST(NULL AS nvarchar(200)) AS Email,
       CAST(NULL AS nvarchar(20)) AS Phone,
       CAST(NULL AS nvarchar(100)) AS Role,
       CAST(NULL AS nvarchar(100)) AS Department,
       CAST(NULL AS nvarchar(100)) AS TeamName,
       CAST(NULL AS nvarchar(100)) AS NpnLicense,
       CAST(NULL AS nvarchar(500)) AS LicenseStates,
       CAST(NULL AS datetime2) AS LicenseExpiryDate,
       CAST(NULL AS nvarchar(50)) AS Status,
       CAST(0 AS bit) AS IsActive,
       CAST(NULL AS datetime2) AS CreatedDateUtc
WHERE 1 = 0
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var results = await cn.QueryAsync<ProducerStaffDto>(new CommandDefinition(sql, new { TenantId = tenantId, Days = days }, cancellationToken: ct));
        var list = results.ToList();
        if (list.Count > 0)
            return list;

        EnsureFallbackData(tenantId);
        var cutoff = DateTime.UtcNow.AddDays(days);
        return _fallbackData.Where(s => s.TenantId == tenantId && s.LicenseExpiryDate.HasValue && s.LicenseExpiryDate <= cutoff).OrderBy(s => s.LicenseExpiryDate).ToList();
    }

    private static void EnsureFallbackData(Guid tenantId)
    {
        if (_fallbackData.Any(s => s.TenantId == tenantId))
            return;

        _fallbackData.AddRange([
            new ProducerStaffDto { StaffId = Guid.NewGuid(), TenantId = tenantId, FirstName = "Avery", LastName = "Johnson", Email = "avery.johnson@agencybinder.local", Phone = "555-0101", Role = "Producer", Department = "Commercial Lines", TeamName = "Growth Team", NpnLicense = "1289345", LicenseStates = "CA, OR, WA", LicenseExpiryDate = DateTime.UtcNow.AddDays(45), Status = "Active", IsActive = true, CreatedDateUtc = DateTime.UtcNow.AddDays(-120) },
            new ProducerStaffDto { StaffId = Guid.NewGuid(), TenantId = tenantId, FirstName = "Morgan", LastName = "Reed", Email = "morgan.reed@agencybinder.local", Phone = "555-0102", Role = "Producer", Department = "Personal Lines", TeamName = "Retention Team", NpnLicense = "2398456", LicenseStates = "TX, OK", LicenseExpiryDate = DateTime.UtcNow.AddDays(180), Status = "Active", IsActive = true, CreatedDateUtc = DateTime.UtcNow.AddDays(-95) },
            new ProducerStaffDto { StaffId = Guid.NewGuid(), TenantId = tenantId, FirstName = "Taylor", LastName = "Chen", Email = "taylor.chen@agencybinder.local", Phone = "555-0103", Role = "Producer", Department = "Benefits", TeamName = "Benefits Team", NpnLicense = "3487561", LicenseStates = "NY, NJ", LicenseExpiryDate = DateTime.UtcNow.AddDays(-12), Status = "Inactive", IsActive = false, CreatedDateUtc = DateTime.UtcNow.AddDays(-210) },
            new ProducerStaffDto { StaffId = Guid.NewGuid(), TenantId = tenantId, FirstName = "Jordan", LastName = "Smith", Email = "jordan.smith@agencybinder.local", Phone = "555-0110", Role = "CSR", Department = "Commercial Lines", TeamName = "Service Team A", Status = "Active", IsActive = true, CreatedDateUtc = DateTime.UtcNow.AddDays(-70) },
            new ProducerStaffDto { StaffId = Guid.NewGuid(), TenantId = tenantId, FirstName = "Riley", LastName = "Garcia", Email = "riley.garcia@agencybinder.local", Phone = "555-0111", Role = "CSR", Department = "Personal Lines", TeamName = "Service Team B", Status = "Active", IsActive = true, CreatedDateUtc = DateTime.UtcNow.AddDays(-80) },
            new ProducerStaffDto { StaffId = Guid.NewGuid(), TenantId = tenantId, FirstName = "Casey", LastName = "Patel", Email = "casey.patel@agencybinder.local", Phone = "555-0112", Role = "CSR", Department = "Claims", TeamName = "Service Team A", Status = "Inactive", IsActive = false, CreatedDateUtc = DateTime.UtcNow.AddDays(-40) }
        ]);
    }

    private static bool RoleEquals(string? actual, string expected) => string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}

public class SystemSettingsRepository : ISystemSettingsRepository
{
    private static readonly List<SystemSettingsDto> _data = new();
    public Task<IReadOnlyList<SystemSettingsDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<SystemSettingsDto>)_data);
    public Task<IReadOnlyList<SystemSettingsDto>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<SystemSettingsDto>)_data.Where(s => s.Category == category).ToList());
    public Task<SystemSettingsDto?> GetByKeyAsync(Guid tenantId, string key, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(s => s.SettingKey == key));
    public Task UpdateAsync(SystemSettingsDto setting, CancellationToken ct = default) { var i = _data.FindIndex(s => s.SettingId == setting.SettingId); if (i >= 0) _data[i] = setting; return Task.CompletedTask; }
}

public class NotificationPolicyRepository : INotificationPolicyRepository
{
    private static readonly List<NotificationPolicyDto> _data = new();
    public Task<IReadOnlyList<NotificationPolicyDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<NotificationPolicyDto>)_data);
    public Task<NotificationPolicyDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(p => p.PolicyId == id));
    public async Task<Guid> CreateAsync(NotificationPolicyDto policy, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(policy with { PolicyId = id }); return id; }
    public Task UpdateAsync(NotificationPolicyDto policy, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(p => p.PolicyId == policy.PolicyId); if (i >= 0) _data[i] = policy; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(p => p.PolicyId == id); return Task.CompletedTask; }
}

public class QueueRoutingRepository : IQueueRoutingRepository
{
    private static readonly List<QueueRoutingRuleDto> _data = new();
    public Task<IReadOnlyList<QueueRoutingRuleDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<QueueRoutingRuleDto>)_data);
    public Task<QueueRoutingRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(r => r.RuleId == id));
    public async Task<Guid> CreateAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(rule with { RuleId = id }); return id; }
    public Task UpdateAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(r => r.RuleId == rule.RuleId); if (i >= 0) _data[i] = rule; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(r => r.RuleId == id); return Task.CompletedTask; }
}

public class DataQualityRepository : IDataQualityRepository
{
    private static readonly List<DataQualityRuleDto> _data = new();
    public Task<IReadOnlyList<DataQualityRuleDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<DataQualityRuleDto>)_data);
    public Task<DataQualityRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(r => r.RuleId == id));
    public async Task<Guid> CreateAsync(DataQualityRuleDto rule, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(rule with { RuleId = id }); return id; }
    public Task UpdateAsync(DataQualityRuleDto rule, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(r => r.RuleId == rule.RuleId); if (i >= 0) _data[i] = rule; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(r => r.RuleId == id); return Task.CompletedTask; }
}

public class DataCenterRepository : IDataCenterRepository
{
    private static readonly List<DataCenterConfigDto> _data = new();
    public Task<IReadOnlyList<DataCenterConfigDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<DataCenterConfigDto>)_data);
    public Task<DataCenterConfigDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(c => c.ConfigId == id));
    public async Task<Guid> CreateAsync(DataCenterConfigDto config, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(config with { ConfigId = id }); return id; }
    public Task UpdateAsync(DataCenterConfigDto config, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(c => c.ConfigId == config.ConfigId); if (i >= 0) _data[i] = config; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(c => c.ConfigId == id); return Task.CompletedTask; }
}

public class SlaPolicyRepository : ISlaPolicyRepository
{
    private static readonly List<SlaPolicySetupDto> _data = new();
    public Task<IReadOnlyList<SlaPolicySetupDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<SlaPolicySetupDto>)_data);
    public Task<SlaPolicySetupDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(p => p.PolicyId == id));
    public async Task<Guid> CreateAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(policy with { PolicyId = id }); return id; }
    public Task UpdateAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(p => p.PolicyId == policy.PolicyId); if (i >= 0) _data[i] = policy; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(p => p.PolicyId == id); return Task.CompletedTask; }
}
