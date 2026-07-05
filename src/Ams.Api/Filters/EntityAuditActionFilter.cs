using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Audit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ams.Api.Filters;

public sealed class EntityAuditActionFilter : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SkippedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Audit",
        "AuditLogs",
        "EnterpriseAudit",
        "UserAuditTrail",
        "Auth",
        "HealthCheck"
    };

    private readonly IEnterpriseAuditService _enterpriseAuditService;
    private readonly ILogger<EntityAuditActionFilter> _logger;

    public EntityAuditActionFilter(IEnterpriseAuditService enterpriseAuditService, ILogger<EntityAuditActionFilter> logger)
    {
        _enterpriseAuditService = enterpriseAuditService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ShouldAudit(context))
        {
            await next();
            return;
        }

        var entityName = GetEntityName(context);
        var entityId = GetEntityId(context);
        var before = entityId.HasValue ? await TryGetEntitySnapshotAsync(context, entityId.Value) : null;

        var executedContext = await next();
        if (executedContext.Exception is not null || !IsSuccessful(executedContext.HttpContext.Response.StatusCode))
        {
            return;
        }

        entityId ??= GetEntityId(executedContext) ?? GetEntityIdFromResult(executedContext.Result);
        var after = entityId.HasValue && !IsDelete(context) ? await TryGetEntitySnapshotAsync(context, entityId.Value) : null;
        var tenantId = GetTenantId(context, before, after);
        if (!tenantId.HasValue)
        {
            return;
        }

        var actorUserId = GetActorUserId(context);
        var actorUserNameHint = GetActorUserNameHint(context);
        var actionType = GetActionType(context);
        var userActionCode = GetUserActionCode(context, entityName);
        var displayName = GetDisplayName(after) ?? GetDisplayName(before) ?? entityName;
        var oldJson = before is null ? null : SerializeSnapshot(before);
        var newJson = after is null ? SerializeRequest(context) : SerializeSnapshot(after);
        var changes = GetChangedFields(before, after, context)
            .Select(change => new EntityAuditFieldChange(change.FieldName, change.OldValue, change.NewValue, change.DataTypeCode)
            {
                ActionType = BuildFieldActionType(context, entityName, change.FieldName)
            })
            .ToList();
        if (changes.Count == 0)
        {
            changes.Add(new EntityAuditFieldChange(entityName, oldJson, newJson)
            {
                ActionType = userActionCode,
                IsSnapshot = true
            });
        }

        try
        {
            await _enterpriseAuditService.LogEntityAuditAsync(new LogEntityAuditRequest
            {
                TenantId = tenantId.Value,
                ActorUserId = actorUserId,
                ActorUserNameHint = actorUserNameHint,
                ActionType = actionType,
                UserActionCode = userActionCode,
                UserActionDescription = BuildUserActionDescription(entityName, displayName, actionType, changes),
                ActionCategory = GetActionCategory(actionType),
                ModuleName = entityName,
                EntityName = entityName,
                EntityId = entityId,
                EntityDisplayName = displayName,
                ParentEntityName = GetParentEntityName(before, after, context),
                ParentEntityId = GetParentEntityId(before, after, context),
                OldValue = oldJson,
                NewValue = newJson,
                IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.HttpContext.Request.Headers.UserAgent.ToString(),
                SessionId = GetSessionId(context),
                CorrelationId = GetCorrelationId(context),
                RequestId = context.HttpContext.TraceIdentifier,
                SourceSystem = GetSourceSystem(context),
                Severity = actionType.EndsWith("DELETED", StringComparison.OrdinalIgnoreCase) ? "High" : actionType.EndsWith("UPDATED", StringComparison.OrdinalIgnoreCase) ? "Medium" : "Info",
                StatusCode = "Success",
                VersionNumber = GetVersionNumber(before, after),
                ControllerName = GetControllerName(context),
                ActionName = GetRouteValue(context, "action"),
                HttpMethod = context.HttpContext.Request.Method,
                Changes = changes
            }, context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit event for {EntityName} {ActionType}.", entityName, actionType);
        }
    }

    private static bool ShouldAudit(ActionExecutingContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (!HttpMethods.IsPost(method) && !HttpMethods.IsPut(method) && !HttpMethods.IsDelete(method) && !HttpMethods.IsPatch(method))
        {
            return false;
        }

        var controller = GetControllerName(context);
        if (SkippedControllers.Contains(controller))
        {
            return false;
        }

        var actionName = GetRouteValue(context, "action");
        if (actionName.Contains("Search", StringComparison.OrdinalIgnoreCase) ||
            actionName.Contains("Validate", StringComparison.OrdinalIgnoreCase) ||
            actionName.Contains("Preview", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsSuccessful(int statusCode) => statusCode is >= 200 and < 400;

    private static bool IsDelete(ActionExecutingContext context) => HttpMethods.IsDelete(context.HttpContext.Request.Method);

    private static string GetActionType(ActionExecutingContext context)
    {
        if (HttpMethods.IsPost(context.HttpContext.Request.Method)) return "ENTITY_CREATED";
        if (HttpMethods.IsDelete(context.HttpContext.Request.Method)) return "ENTITY_DELETED";
        return "ENTITY_UPDATED";
    }

    private static string GetUserActionCode(ActionExecutingContext context, string entityName)
    {
        var entityCode = ToScreamingSnake(entityName);
        if (HttpMethods.IsPost(context.HttpContext.Request.Method)) return $"{entityCode}_CREATED";
        if (HttpMethods.IsDelete(context.HttpContext.Request.Method)) return $"{entityCode}_DELETED";
        return $"{entityCode}_UPDATED";
    }

    private static string BuildFieldActionType(ActionExecutingContext context, string entityName, string fieldName)
    {
        var entityCode = ToScreamingSnake(entityName);
        if (HttpMethods.IsPost(context.HttpContext.Request.Method)) return $"{entityCode}_CREATED";
        if (HttpMethods.IsDelete(context.HttpContext.Request.Method)) return $"{entityCode}_DELETED";
        var fieldCode = ToScreamingSnake(fieldName);
        if (fieldCode.StartsWith($"{entityCode}_", StringComparison.OrdinalIgnoreCase))
        {
            fieldCode = fieldCode[(entityCode.Length + 1)..];
        }

        return $"{entityCode}_{fieldCode}_CHANGED";
    }

    private static string BuildUserActionDescription(string entityName, string displayName, string actionType, IReadOnlyCollection<EntityAuditFieldChange> changes)
    {
        var fieldNames = changes.Where(change => !change.IsSnapshot).Select(change => change.FieldName).ToList();
        var fieldText = fieldNames.Count > 0 ? $" Fields: {string.Join(", ", fieldNames)}." : string.Empty;
        return $"{entityName} '{displayName}' was {ActionVerb(actionType)}.{fieldText}";
    }

    private static string GetActionCategory(string actionType)
        => actionType.EndsWith("_UPDATED", StringComparison.OrdinalIgnoreCase) ? "Data Change" : "Configuration";

    private static string ActionVerb(string actionType)
    {
        if (actionType.EndsWith("_CREATED", StringComparison.OrdinalIgnoreCase)) return "created";
        if (actionType.EndsWith("_DELETED", StringComparison.OrdinalIgnoreCase)) return "deleted";
        return "updated";
    }

    private static string GetEntityName(ActionExecutingContext context)
    {
        var controller = GetControllerName(context);
        return Singularize(controller);
    }

    private static string GetControllerName(ActionContext context)
        => GetRouteValue(context, "controller");

    private static string GetRouteValue(ActionContext context, string key)
        => context.ActionDescriptor.RouteValues.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

    private static string Singularize(string value)
    {
        if (value.EndsWith("ies", StringComparison.OrdinalIgnoreCase)) return value[..^3] + "y";
        if (value.EndsWith("ses", StringComparison.OrdinalIgnoreCase)) return value[..^2];
        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return value[..^1];
        return value;
    }

    private static string ToScreamingSnake(string value)
    {
        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsUpper(ch) && i > 0 && value[i - 1] != '_') chars.Add('_');
            chars.Add(char.ToUpperInvariant(ch));
        }

        return new string(chars.ToArray()).Replace("__", "_", StringComparison.Ordinal);
    }

    private static Guid? GetEntityId(ActionExecutingContext context)
    {
        foreach (var key in new[] { "id", "entityId", "Id" })
        {
            if (context.RouteData.Values.TryGetValue(key, out var routeValue) && Guid.TryParse(routeValue?.ToString(), out var routeId)) return routeId;
            if (context.ActionArguments.TryGetValue(key, out var actionValue) && Guid.TryParse(actionValue?.ToString(), out var actionId)) return actionId;
        }

        return context.ActionArguments.Values.Select(GetObjectId).FirstOrDefault(id => id.HasValue);
    }

    private static Guid? GetEntityId(ActionExecutedContext context)
    {
        foreach (var key in new[] { "id", "entityId", "Id" })
        {
            if (context.RouteData.Values.TryGetValue(key, out var routeValue) && Guid.TryParse(routeValue?.ToString(), out var routeId)) return routeId;
        }

        return null;
    }

    private static Guid? GetEntityIdFromResult(IActionResult? result)
    {
        var value = result switch
        {
            ObjectResult objectResult => objectResult.Value,
            JsonResult jsonResult => jsonResult.Value,
            _ => null
        };

        if (value is Guid guid) return guid;
        if (Guid.TryParse(value?.ToString(), out var parsed)) return parsed;
        return GetObjectId(value);
    }

    private static Guid? GetObjectId(object? value)
    {
        if (value is null) return null;
        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead) continue;
            if (!property.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && !string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase)) continue;
            var propertyValue = property.GetValue(value);
            if (propertyValue is Guid guid && guid != Guid.Empty) return guid;
            if (Guid.TryParse(propertyValue?.ToString(), out var parsed) && parsed != Guid.Empty) return parsed;
        }

        return null;
    }

    private static Guid? GetTenantId(ActionExecutingContext context, object? before, object? after)
        => GetGuidProperty(after, "TenantId") ?? GetGuidProperty(before, "TenantId") ?? GetGuidFromArguments(context, "TenantId", "tenantId");

    private static Guid? GetActorUserId(ActionExecutingContext context)
        => GetClaimGuid(context.HttpContext.User, ClaimTypes.NameIdentifier, "sub", "userId", "UserId")
           ?? GetGuidFromArguments(context, "PerformedByUserId", "performedByUserId", "ModifiedByUserId", "modifiedByUserId", "CreatedByUserId", "createdByUserId", "userId", "changedByUserId");

    private static string? GetActorUserNameHint(ActionExecutingContext context)
        => FirstNonEmpty(
            context.HttpContext.User.Identity?.Name,
            context.HttpContext.User.FindFirstValue(ClaimTypes.Name),
            context.HttpContext.User.FindFirstValue("name"),
            context.HttpContext.User.FindFirstValue("preferred_username"),
            context.HttpContext.User.FindFirstValue("email"),
            GetStringFromArguments(context, "PerformedByUserName", "performedByUserName", "ModifiedByUserName", "modifiedByUserName", "CreatedByUserName", "createdByUserName", "UserName", "userName", "ChangedByUserName", "changedByUserName"));

    private static string? GetSessionId(ActionExecutingContext context)
        => FirstNonEmpty(
            context.HttpContext.User.FindFirstValue("sid"),
            context.HttpContext.User.FindFirstValue("session_id"),
            context.HttpContext.Request.Headers["X-Session-Id"].ToString(),
            context.HttpContext.Connection.Id);

    private static Guid? GetClaimGuid(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            if (Guid.TryParse(user.FindFirstValue(claimType), out var id) && id != Guid.Empty) return id;
        }

        return null;
    }

    private static Guid? GetGuidFromArguments(ActionExecutingContext context, params string[] names)
    {
        foreach (var name in names)
        {
            if (context.ActionArguments.TryGetValue(name, out var value) && Guid.TryParse(value?.ToString(), out var directId) && directId != Guid.Empty) return directId;

            foreach (var argument in context.ActionArguments.Values)
            {
                var id = GetGuidProperty(argument, name);
                if (id.HasValue) return id;
            }

            if (context.HttpContext.Request.Query.TryGetValue(name, out var queryValue) && Guid.TryParse(queryValue.ToString(), out var queryId) && queryId != Guid.Empty) return queryId;
        }

        return null;
    }

    private static string? GetStringFromArguments(ActionExecutingContext context, params string[] names)
    {
        foreach (var name in names)
        {
            if (context.ActionArguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())) return value.ToString();

            foreach (var argument in context.ActionArguments.Values)
            {
                var text = GetStringProperty(argument, name);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            if (context.HttpContext.Request.Query.TryGetValue(name, out var queryValue) && !string.IsNullOrWhiteSpace(queryValue.ToString())) return queryValue.ToString();
        }

        return null;
    }

    private static string? GetStringProperty(object? value, string propertyName)
    {
        if (value is null) return null;
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return property?.GetValue(value)?.ToString();
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static Guid? GetGuidProperty(object? value, string propertyName)
    {
        if (value is null) return null;
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var propertyValue = property?.GetValue(value);
        if (propertyValue is Guid guid && guid != Guid.Empty) return guid;
        return Guid.TryParse(propertyValue?.ToString(), out var parsed) && parsed != Guid.Empty ? parsed : null;
    }

    private static async Task<object?> TryGetEntitySnapshotAsync(ActionExecutingContext context, Guid id)
    {
        var service = GetControllerService(context.Controller);
        if (service is null) return null;

        var methods = service.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => string.Equals(method.Name, "GetByIdAsync", StringComparison.Ordinal))
            .OrderBy(method => method.GetParameters().Length);

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Guid))
            {
                return await AwaitObjectAsync(method.Invoke(service, [id]));
            }

            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Guid) && parameters[1].ParameterType == typeof(CancellationToken))
            {
                return await AwaitObjectAsync(method.Invoke(service, [id, context.HttpContext.RequestAborted]));
            }
        }

        return null;
    }

    private static object? GetControllerService(object controller)
    {
        var fields = controller.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => field.Name.Contains("service", StringComparison.OrdinalIgnoreCase));
        return fields.Select(field => field.GetValue(controller)).FirstOrDefault(value => value is not null);
    }

    private static async Task<object?> AwaitObjectAsync(object? value)
    {
        if (value is null) return null;
        if (value is Task task)
        {
            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            return resultProperty?.GetValue(task);
        }

        return value;
    }

    private static string? SerializeSnapshot(object? value)
        => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static string? SerializeRequest(ActionExecutingContext context)
    {
        var body = context.ActionArguments.Values.FirstOrDefault(value => value is not null && value.GetType().IsClass && value is not string);
        return body is null ? null : JsonSerializer.Serialize(body, JsonOptions);
    }

    private static string? GetDisplayName(object? value)
    {
        if (value is null) return null;
        foreach (var name in new[] { "Name", "DisplayName", "Title", "CarrierName", "AccountName", "FullName", "PolicyNumber", "SubmissionNumber", "OpportunityName" })
        {
            var property = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var text = property?.GetValue(value)?.ToString();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return null;
    }

    private static string GetSourceSystem(ActionExecutingContext context)
        => FirstNonEmpty(context.HttpContext.Request.Headers["X-Source-System"].ToString(), "API") ?? "API";

    private static string? GetCorrelationId(ActionExecutingContext context)
        => FirstNonEmpty(
            context.HttpContext.Request.Headers["X-Correlation-ID"].ToString(),
            context.HttpContext.Request.Headers["X-Correlation-Id"].ToString(),
            context.HttpContext.TraceIdentifier);

    private static string? GetParentEntityName(object? before, object? after, ActionExecutingContext context)
    {
        var source = after ?? before;
        foreach (var propertyName in new[] { "ParentEntityName", "ParentType", "ParentName" })
        {
            var value = GetStringProperty(source, propertyName) ?? GetStringFromArguments(context, propertyName);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        var parentIdProperty = source?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => property.CanRead && property.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && property.Name.StartsWith("Parent", StringComparison.OrdinalIgnoreCase));

        return parentIdProperty is null ? null : parentIdProperty.Name[..^2];
    }

    private static Guid? GetParentEntityId(object? before, object? after, ActionExecutingContext context)
    {
        var source = after ?? before;
        foreach (var propertyName in new[] { "ParentEntityId", "ParentId", "AccountId", "PolicyId", "SubmissionId", "OpportunityId", "ContactId", "DocumentId", "WorkflowInstanceId" })
        {
            var value = GetGuidProperty(source, propertyName) ?? GetGuidFromArguments(context, propertyName);
            if (value.HasValue) return value;
        }

        return null;
    }

    private static int? GetVersionNumber(object? before, object? after)
    {
        var source = after ?? before;
        foreach (var propertyName in new[] { "VersionNumber", "Version", "RowVersion" })
        {
            var property = source?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var value = property?.GetValue(source);
            if (value is int intValue) return intValue;
            if (int.TryParse(value?.ToString(), out var parsed)) return parsed;
        }

        return null;
    }

    private static IEnumerable<EntityFieldChange> GetChangedFields(object? before, object? after, ActionExecutingContext context)
    {
        if (before is null || after is null)
        {
            yield break;
        }

        var beforeProperties = before.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanRead).ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var afterProperty in after.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanRead))
        {
            if (!beforeProperties.TryGetValue(afterProperty.Name, out var beforeProperty)) continue;
            if (afterProperty.Name.EndsWith("DateUtc", StringComparison.OrdinalIgnoreCase) || afterProperty.Name.Equals("ModifiedDateUtc", StringComparison.OrdinalIgnoreCase)) continue;

            var oldValue = beforeProperty.GetValue(before)?.ToString();
            var newValue = afterProperty.GetValue(after)?.ToString();
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                yield return new EntityFieldChange(afterProperty.Name, oldValue, newValue, GetDataTypeCode(afterProperty.PropertyType));
            }
        }
    }

    private static string GetDataTypeCode(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (actualType == typeof(bool)) return "Boolean";
        if (actualType == typeof(DateTime) || actualType == typeof(DateOnly) || actualType == typeof(TimeOnly)) return "DateTime";
        if (actualType == typeof(Guid)) return "Guid";
        if (actualType == typeof(int) || actualType == typeof(long) || actualType == typeof(short)) return "Integer";
        if (actualType == typeof(decimal) || actualType == typeof(double) || actualType == typeof(float)) return "Decimal";
        return "String";
    }

    private sealed record EntityFieldChange(string FieldName, string? OldValue, string? NewValue, string DataTypeCode = "String");
}
