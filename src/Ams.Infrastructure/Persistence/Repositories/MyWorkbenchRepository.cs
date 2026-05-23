using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Workbench;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class MyWorkbenchRepository : IMyWorkbenchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public MyWorkbenchRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<MyWorkbenchDto> GetAsync(MyWorkbenchRequest request, CancellationToken cancellationToken = default)
    {
        var workDate = request.WorkDate ?? DateOnly.FromDateTime(DateTime.Today);
        var startUtc = workDate.ToDateTime(TimeOnly.MinValue);
        var endUtc = workDate.ToDateTime(TimeOnly.MaxValue);

        const string sql = @"
SELECT TOP 50 TaskItemId, TaskNumber, Title, Description, TaskTypeCode, PriorityCode, StatusCode, StageCode,
       RelatedEntityName, RelatedEntityId, DueDate, CompletedDate,
       CAST(CASE WHEN DueDate < @WorkDate AND StatusCode NOT IN ('Completed','Done','Closed') THEN 1 ELSE 0 END AS bit) AS IsOverdue,
       CAST(CASE WHEN StatusCode IN ('Completed','Done','Closed') OR CompletedDate IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsCompleted,
       CreatedDateUtc
FROM OPS.TaskItem
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@UserId IS NULL OR AssignedToUserId = @UserId OR CreatedByUserId = @UserId OR AssignedToUserId IS NULL)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Title LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR TaskTypeCode LIKE '%' + @SearchTerm + '%' OR RelatedEntityName LIKE '%' + @SearchTerm + '%')
  AND (@PriorityCode IS NULL OR @PriorityCode = '' OR PriorityCode = @PriorityCode)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
ORDER BY CASE WHEN DueDate < @WorkDate AND StatusCode NOT IN ('Completed','Done','Closed') THEN 0 ELSE 1 END, DueDate, CreatedDateUtc DESC;

SELECT TOP 20 EventId, Title, Notes AS AccountName, EventTypeCode, StartDateTimeUtc AS StartDateTime, EndDateTimeUtc AS EndDateTime, TimeZoneId AS Location
FROM OPS.CalendarEvent
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND StartDateTimeUtc >= @StartUtc
  AND StartDateTimeUtc <= @EndUtc
  AND (@UserId IS NULL OR AssignedToUserId = @UserId OR OrganizerUserId = @UserId OR AssignedToUserId IS NULL)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Title LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%' OR EventTypeCode LIKE '%' + @SearchTerm + '%')
ORDER BY StartDateTimeUtc;

SELECT TOP 30 ActivityId, Subject, NULL AS AccountName, ActivityTypeCode, CAST(ActivityDate AS DATETIME2) AS ActivityDateTime, Notes
FROM OPS.OperationalActivityLog
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@UserId IS NULL OR PerformedByUserId = @UserId OR CreatedByUserId = @UserId OR PerformedByUserId IS NULL)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Subject LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%' OR ActivityTypeCode LIKE '%' + @SearchTerm + '%')
ORDER BY ActivityDate DESC, CreatedDateUtc DESC;

SELECT TOP 30 NotificationId, COALESCE(Subject, Category) AS Title, Body AS Message, Priority AS SeverityCode, NULL AS TargetUrl, IsRead, CreatedDateUtc
FROM Core.Notification
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@UserId IS NULL OR RecipientUserId = @UserId OR RecipientUserId = '00000000-0000-0000-0000-000000000000')
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Subject LIKE '%' + @SearchTerm + '%' OR Body LIKE '%' + @SearchTerm + '%' OR Category LIKE '%' + @SearchTerm + '%')
ORDER BY IsRead, CreatedDateUtc DESC;

SELECT
    SUM(CASE WHEN t.StatusCode NOT IN ('Completed','Done','Closed') THEN 1 ELSE 0 END) AS OpenTasks,
    SUM(CASE WHEN t.DueDate < @WorkDate AND t.StatusCode NOT IN ('Completed','Done','Closed') THEN 1 ELSE 0 END) AS OverdueTasks,
    SUM(CASE WHEN t.CompletedDate = @WorkDate OR (CAST(t.ModifiedDateUtc AS date) = CAST(@StartUtc AS date) AND t.StatusCode IN ('Completed','Done','Closed')) THEN 1 ELSE 0 END) AS CompletedToday
FROM OPS.TaskItem t
WHERE t.TenantId = @TenantId AND t.IsDeleted = 0 AND (@UserId IS NULL OR t.AssignedToUserId = @UserId OR t.CreatedByUserId = @UserId OR t.AssignedToUserId IS NULL);

SELECT COUNT(1) FROM OPS.OperationalActivityLog WHERE TenantId = @TenantId AND IsDeleted = 0 AND ActivityDate = @WorkDate;
SELECT COUNT(1) FROM Core.Notification WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsRead = 0 AND (@UserId IS NULL OR RecipientUserId = @UserId OR RecipientUserId = '00000000-0000-0000-0000-000000000000');
SELECT COUNT(1) FROM OPS.TaskItem WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode NOT IN ('Completed','Done','Closed') AND (TaskTypeCode LIKE '%Renewal%' OR Title LIKE '%Renewal%');
SELECT COUNT(1) FROM OPS.TaskItem WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode NOT IN ('Completed','Done','Closed') AND (TaskTypeCode LIKE '%Service%' OR Title LIKE '%Service%' OR StageCode LIKE '%Service%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.UserId,
            request.SearchTerm,
            request.PriorityCode,
            request.StatusCode,
            WorkDate = workDate,
            StartUtc = startUtc,
            EndUtc = endUtc
        }, cancellationToken: cancellationToken));

        var tasks = (await multi.ReadAsync<MyWorkbenchTaskDto>()).AsList();
        var schedule = (await multi.ReadAsync<MyWorkbenchScheduleEventDto>()).AsList();
        var activities = (await multi.ReadAsync<MyWorkbenchActivityDto>()).AsList();
        var notifications = (await multi.ReadAsync<MyWorkbenchNotificationDto>()).AsList();
        var taskKpis = await multi.ReadSingleOrDefaultAsync<TaskKpiRow>() ?? new TaskKpiRow();
        var activitiesToday = await multi.ReadSingleAsync<int>();
        var unreadNotifications = await multi.ReadSingleAsync<int>();
        var renewalsDue = await multi.ReadSingleAsync<int>();
        var openServiceRequests = await multi.ReadSingleAsync<int>();
        var quickLinks = await GetQuickLinksAsync(cn, request.TenantId, cancellationToken);

        return new MyWorkbenchDto
        {
            Tasks = tasks,
            Schedule = schedule,
            Activities = activities,
            Notifications = notifications,
            QuickLinks = quickLinks,
            Kpis = new MyWorkbenchKpiDto
            {
                OpenTasks = taskKpis.OpenTasks,
                OverdueTasks = taskKpis.OverdueTasks,
                CompletedToday = taskKpis.CompletedToday,
                ActivitiesToday = activitiesToday,
                UnreadNotifications = unreadNotifications,
                RenewalsDue = renewalsDue,
                OpenServiceRequests = openServiceRequests
            }
        };
    }

    public async Task SetTaskStatusAsync(Guid taskItemId, MyWorkbenchTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.TaskItem
SET StatusCode = @StatusCode,
    CompletedDate = CASE WHEN @StatusCode IN ('Completed','Done','Closed') THEN CONVERT(date, SYSUTCDATETIME()) ELSE NULL END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TaskItemId = @TaskItemId
  AND TenantId = @TenantId
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TaskItemId = taskItemId, request.TenantId, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task SetNotificationReadAsync(Guid notificationId, MyWorkbenchNotificationStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Notification
SET IsRead = @IsRead,
    ReadDateUtc = CASE WHEN @IsRead = 1 THEN SYSUTCDATETIME() ELSE NULL END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE NotificationId = @NotificationId
  AND TenantId = @TenantId
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = notificationId, request.TenantId, request.IsRead, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    private static async Task<List<MyWorkbenchQuickLinkDto>> GetQuickLinksAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        const string existsSql = "SELECT CASE WHEN OBJECT_ID(N'OPS.WorkbenchQuickLink', N'U') IS NULL THEN 0 ELSE 1 END;";
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(existsSql, cancellationToken: cancellationToken));
        if (exists == 0)
        {
            return [];
        }

        const string sql = @"
SELECT QuickLinkId, TenantId, LinkCode, Label, IconCssClass, Url, CategoryCode, SortOrder, IsActive
FROM OPS.WorkbenchQuickLink
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY SortOrder, Label;";

        return (await connection.QueryAsync<MyWorkbenchQuickLinkDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    private sealed class TaskKpiRow
    {
        public int OpenTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int CompletedToday { get; set; }
    }
}
