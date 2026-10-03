using System.Net.Http.Headers;
using System.Text.Json;

namespace Legal.Web.Services.Clio;

/// <summary>
/// Read-only Clio Manage API v4 client implementation. Issues only GET requests.
/// The access token never leaves the server and is never logged.
/// </summary>
public sealed class ClioReadOnlyClient : IClioReadOnlyClient
{
    private const string MatterFields = "id,display_number,description,status,practice_area{name},client{id,name},matter_stage{id,name},custom_field_values{field_name,field_type,value}";
    private const string RelationshipFields = "id,description,contact{id,name,type}";
    private const string DocumentFields = "id,name,document_category{name},latest_document_version{version_number},parent{id,name}";
    private const string TaskFields = "id,name,description,due_at,status,complete";
    private const string CalendarFields = "id,summary,description,start_at,end_at";
    private const string CommunicationFields = "id,subject,body,type,date,received_at";
    private const int MaxPages = 25; // hackathon safety cap

    private readonly HttpClient _http;
    private readonly ClioTokenStore _tokenStore;

    public ClioReadOnlyClient(HttpClient http, ClioTokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    public async Task<ClioSapiniDiscovery> DiscoverSapiniAsync(CancellationToken ct = default)
    {
        var matter = await FindSapiniMatterAsync(ct)
            ?? throw new ClioReadException("Could not find a Sapini matter in the connected Clio account.");

        var contacts = await GetContactsAsync(matter, ct);
        var documents = await GetDocumentsAsync(matter.Id, ct);
        var activities = await GetActivitiesAsync(matter.Id, ct);

        return new ClioSapiniDiscovery(matter, contacts, documents, matter.CustomFields)
        {
            Activities = activities
        };
    }

    public async Task<ClioDocumentContent> DownloadDocumentAsync(long documentId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"documents/{documentId}/download.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetAccessToken());

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new ClioReadException($"Clio document download failed for document {documentId} ({(int)response.StatusCode} {response.ReasonPhrase}).");

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType?.ToString();
        return new ClioDocumentContent(bytes, contentType);
    }

    private async Task<ClioMatter?> FindSapiniMatterAsync(CancellationToken ct)
    {
        // Dynamic discovery: let Clio search by the "Sapini" term; never a hardcoded id.
        var url = $"matters.json?query={Uri.EscapeDataString("Sapini")}&fields={Uri.EscapeDataString(MatterFields)}&limit=50";

        await foreach (var el in EnumerateAsync(url, ct))
        {
            var matter = MapMatter(el);
            if (IsSapini(matter))
                return matter;
        }
        return null;
    }

    private static bool IsSapini(ClioMatter m)
    {
        const string term = "sapini";
        return (m.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (m.DisplayNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (m.ClientName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private async Task<IReadOnlyList<ClioContact>> GetContactsAsync(ClioMatter matter, CancellationToken ct)
    {
        var results = new List<ClioContact>();
        var seen = new HashSet<long>();
        var url = $"relationships.json?matter_id={matter.Id}&fields={Uri.EscapeDataString(RelationshipFields)}&limit=100";

        await foreach (var el in EnumerateAsync(url, ct))
        {
            var relationship = el.TryGetProperty("description", out var t) ? t.GetString() : null;
            if (!el.TryGetProperty("contact", out var contact) || contact.ValueKind != JsonValueKind.Object)
                continue;

            var id = GetLong(contact, "id");
            if (id is null || !seen.Add(id.Value))
                continue;

            results.Add(new ClioContact(
                id.Value,
                GetString(contact, "name"),
                GetString(contact, "type"),
                relationship));
        }

        return results;
    }

    private async Task<IReadOnlyList<ClioDocumentInfo>> GetDocumentsAsync(long matterId, CancellationToken ct)
    {
        var results = new List<ClioDocumentInfo>();
        var url = $"documents.json?matter_id={matterId}&fields={Uri.EscapeDataString(DocumentFields)}&limit=200";

        await foreach (var el in EnumerateAsync(url, ct))
        {
            var id = GetLong(el, "id");
            if (id is null)
                continue;

            string? category = null;
            if (el.TryGetProperty("document_category", out var cat) && cat.ValueKind == JsonValueKind.Object)
                category = GetString(cat, "name");

            string? folder = null;
            if (el.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.Object)
                folder = GetString(parent, "name");

            string? version = null;
            if (el.TryGetProperty("latest_document_version", out var ver) && ver.ValueKind == JsonValueKind.Object)
            {
                if (ver.TryGetProperty("version_number", out var vn))
                    version = vn.ValueKind == JsonValueKind.Number ? vn.GetInt64().ToString() : vn.GetString();
            }

            results.Add(new ClioDocumentInfo(id.Value, GetString(el, "name"), category, folder, version));
        }

        return results;
    }

    /// <summary>
    /// Reads dated activity events (tasks, calendar entries, communications) for the
    /// matter. Each sub-read is independent and defensive: a failure or empty result
    /// in one category never prevents the others from contributing to the timeline.
    /// All requests are GET-only.
    /// </summary>
    private async Task<IReadOnlyList<ClioActivityEvent>> GetActivitiesAsync(long matterId, CancellationToken ct)
    {
        var results = new List<ClioActivityEvent>();

        // Tasks
        try
        {
            var url = $"tasks.json?matter_id={matterId}&fields={Uri.EscapeDataString(TaskFields)}&limit=200";
            await foreach (var el in EnumerateAsync(url, ct))
            {
                var id = GetLong(el, "id");
                if (id is null) continue;
                var when = GetDate(el, "due_at");
                bool? complete = el.TryGetProperty("complete", out var c) && (c.ValueKind == JsonValueKind.True || c.ValueKind == JsonValueKind.False)
                    ? c.GetBoolean()
                    : null;
                results.Add(new ClioActivityEvent("Task", id.Value,
                    GetString(el, "name"), when, GetString(el, "description"), complete));
            }
        }
        catch (ClioReadException) { /* non-fatal: tasks may be unavailable */ }

        // Calendar entries
        try
        {
            var url = $"calendar_entries.json?matter_id={matterId}&fields={Uri.EscapeDataString(CalendarFields)}&limit=200";
            await foreach (var el in EnumerateAsync(url, ct))
            {
                var id = GetLong(el, "id");
                if (id is null) continue;
                var when = GetDate(el, "start_at");
                results.Add(new ClioActivityEvent("Calendar", id.Value,
                    GetString(el, "summary"), when, GetString(el, "description"), null));
            }
        }
        catch (ClioReadException) { /* non-fatal: calendar may be unavailable */ }

        // Communications
        try
        {
            var url = $"communications.json?matter_id={matterId}&fields={Uri.EscapeDataString(CommunicationFields)}&limit=200";
            await foreach (var el in EnumerateAsync(url, ct))
            {
                var id = GetLong(el, "id");
                if (id is null) continue;
                var when = GetDate(el, "date") ?? GetDate(el, "received_at");
                results.Add(new ClioActivityEvent("Communication", id.Value,
                    GetString(el, "subject") ?? GetString(el, "type"), when, GetString(el, "type"), null));
            }
        }
        catch (ClioReadException) { /* non-fatal: communications may be unavailable */ }

        return results;
    }

    /// <summary>
    /// Issues a GET and yields each element of the "data" array, following
    /// Clio's meta.paging.next cursor until exhausted or the page cap is hit.
    /// </summary>
    private async IAsyncEnumerable<JsonElement> EnumerateAsync(
        string relativeUrl,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var next = relativeUrl;
        var pages = 0;

        while (!string.IsNullOrEmpty(next) && pages++ < MaxPages)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, next);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetAccessToken());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeReadBodyAsync(response, ct);
                throw new ClioReadException(
                    $"Clio API request failed ({(int)response.StatusCode} {response.ReasonPhrase}) for '{next}'."
                    + (string.IsNullOrWhiteSpace(body) ? "" : $" Detail: {body}"));
            }

            var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in data.EnumerateArray())
                    yield return el.Clone();
            }

            next = null;
            if (root.TryGetProperty("meta", out var meta) &&
                meta.TryGetProperty("paging", out var paging) &&
                paging.TryGetProperty("next", out var nextEl) &&
                nextEl.ValueKind == JsonValueKind.String)
            {
                // Clio returns an absolute next URL; HttpClient handles it via BaseAddress-agnostic absolute URIs.
                next = nextEl.GetString();
            }
        }
    }

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            return text.Length > 600 ? text[..600] : text;
        }
        catch
        {
            return null;
        }
    }

    private string GetAccessToken()
    {
        var token = _tokenStore.Get();
        if (token is null)
            throw new ClioReadException("Not connected to Clio. Connect first, then retry.");
        if (token.IsExpired)
            throw new ClioReadException("The Clio session has expired. Reconnect to Clio and retry.");
        return token.AccessToken;
    }

    private static ClioMatter MapMatter(JsonElement el)
    {
        string? practiceArea = null;
        if (el.TryGetProperty("practice_area", out var pa) && pa.ValueKind == JsonValueKind.Object)
            practiceArea = GetString(pa, "name");

        string? clientName = null;
        if (el.TryGetProperty("client", out var client) && client.ValueKind == JsonValueKind.Object)
            clientName = GetString(client, "name");

        long? clientId = null;
        if (el.TryGetProperty("client", out var clientForId) && clientForId.ValueKind == JsonValueKind.Object)
            clientId = GetLong(clientForId, "id");

        string? stageName = null;
        long? stageId = null;
        if (el.TryGetProperty("matter_stage", out var stage) && stage.ValueKind == JsonValueKind.Object)
        {
            stageName = GetString(stage, "name");
            stageId = GetLong(stage, "id");
        }

        var customFields = new List<ClioCustomFieldValue>();
        if (el.TryGetProperty("custom_field_values", out var cfvs) && cfvs.ValueKind == JsonValueKind.Array)
        {
            foreach (var cfv in cfvs.EnumerateArray())
            {
                var name = GetString(cfv, "field_name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string? value = null;
                if (cfv.TryGetProperty("value", out var v))
                {
                    value = v.ValueKind switch
                    {
                        JsonValueKind.String => v.GetString(),
                        JsonValueKind.Number => v.ToString(),
                        JsonValueKind.True => "Yes",
                        JsonValueKind.False => "No",
                        JsonValueKind.Null => null,
                        _ => v.ToString()
                    };
                }

                customFields.Add(new ClioCustomFieldValue(name, GetString(cfv, "field_type"), value));
            }
        }

        return new ClioMatter(
            GetLong(el, "id") ?? 0,
            GetString(el, "display_number") ?? string.Empty,
            GetString(el, "description"),
            GetString(el, "status"),
            practiceArea,
            clientName,
            clientId)
        {
            CustomFields = customFields,
            StageName = stageName,
            StageId = stageId
        };
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long? GetLong(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;

    private static DateTimeOffset? GetDate(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(v.GetString(), out var d) ? d : null;
}
