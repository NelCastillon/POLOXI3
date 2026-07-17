# Accounts Module - Build Fixes Required

Due to some Razor and API client method name issues, here are the fixes needed to make the implementation compile successfully.

## Issues Found

### 1. **Navigation String Interpolation**
- **Problem**: Using `'/accounts'` in Razor causes string literal errors
- **Solution**: Use string concatenation instead of single quotes

```csharp
// ❌ Wrong
@onclick="() => Nav.NavigateTo('/accounts')"

// ✅ Correct  
@onclick="() => Nav.NavigateTo(\"/accounts\")"
// Or better:
@onclick="@(() => Nav.NavigateTo(\"/accounts\"))"
```

### 2. **Array Initialization Syntax**
- **Problem**: `Breadcrumbs.Set([ ... ])` uses C# 12 collection syntax that may not be available
- **Solution**: Use explicit array or List syntax

```csharp
// ❌ Wrong
Breadcrumbs.Set([
    ("Accounts", "/accounts"),
    ("New Account", "/accounts/new")
]);

// ✅ Correct
var breadcrumbs = new (string, string)[] {
    ("Accounts", "/accounts"),
    ("New Account", "/accounts/new")
};
Breadcrumbs.Set(breadcrumbs);
```

### 3. **ChangeEventArgs Ambiguity**
- **Problem**: `ChangeEventArgs` exists in both `Microsoft.AspNetCore.Components` and `Microsoft.AspNetCore.Components`
- **Solution**: Fully qualify the type

```csharp
// ❌ Wrong
private void SearchChildren(ChangeEventArgs e)

// ✅ Correct
private void SearchChildren(Microsoft.AspNetCore.Components.ChangeEventArgs e)
```

### 4. **API Client Methods**
- **Problem**: `Api.GetAsync()`, `Api.DeleteAsync()`, etc. may not exist
- **Solution**: Use standard `HttpClient` methods or check your ApiClient implementation

```csharp
// ❌ Wrong (if these methods don't exist)
var response = await Api.GetAsync($"/api/accounts/{AccountId}/hierarchy");
var response = await Api.DeleteAsync($"/api/accounts/{id}");
var response = await Api.PostAsJsonAsync("/api/accounts", _form);

// ✅ Correct (if you have HttpClient)
var response = await Http.GetAsync($"/api/accounts/{AccountId}/hierarchy");
var response = await Http.DeleteAsync($"/api/accounts/{id}");
var response = await Http.PostAsJsonAsync("/api/accounts", _form);
```

### 5. **Duplicate ValueChanged Parameter**
- **Problem**: Using both `@bind-Value` and `ValueChanged` together causes duplicate parameters
- **Solution**: Remove explicit `ValueChanged` when using `@bind-Value`

```csharp
// ❌ Wrong
<select class="form-select" @bind="_selectedFilter"></select>

// ✅ Correct
<select class="form-select" @bind="_selectedFilter"></select>
```

### 6. **CSS File Association**
- **Problem**: Scoped CSS file needs associated Razor component
- **Solution**: Create separate CSS files for each page or use global stylesheet

```csharp
// For each page, add this to the page file:
@namespace Ams.Web.Components.Pages.Accounts

// Then create individual CSS files:
// AccountNew.razor.css
// AccountHierarchy.razor.css
// AccountTimeline.razor.css
// AccountRelationships.razor.css
// ContactRoles.razor.css
// DecisionMakers.razor.css
```

### 7. **Unclosed Divs**
- **Problem**: Missing closing `</div>` tags
- **Solution**: Ensure all divs in modals and main containers are properly closed

## Quick Fix Instructions

### Option A: Use Your Existing Pattern
Check your existing Accounts.razor and Contacts.razor pages to see how they handle:
1. Navigation calls
2. Breadcrumb setting
3. API client methods
4. Event handlers

Then mirror that exact syntax in the new pages.

### Option B: Manual Fixes

1. **In all 6 pages, replace**:
   ```csharp
   Breadcrumbs.Set([...])
   ```
   with:
   ```csharp
   var items = new (string, string)[] { ... };
   Breadcrumbs.Set(items);
   ```

2. **In all 6 pages, replace navigation strings**:
   - Avoid using `'` quotes in Razor interpolation
   - Use concatenation: `"/accounts/" + id`

3. **Change all API calls** to match your ApiClient:
   - If you have `HttpClient Http`, use `await Http.GetAsync()`
   - If you have custom `Api` methods, check the exact method names

4. **Remove explicit `ValueChanged`** from `@bind-Value` directives

5. **Delete or rename CSS file** - use individual `.razor.css` files per component

### Option C: Create Compatible Wrapper

Add this extension to your `ApiClient` or create an extension class:

```csharp
public static class ApiClientExtensions
{
    public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, string url)
        => await client.GetAsync(url);

    public static async Task<HttpResponseMessage> DeleteAsync(this HttpClient client, string url)
        => await client.DeleteAsync(url);

    public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(this HttpClient client, string url, T content)
        => await client.PostAsJsonAsync(url, content);
}
```

## Files to Update

1. `AccountNew.razor` - ~240 lines
2. `AccountHierarchy.razor` - ~310 lines
3. `AccountTimeline.razor` - ~200 lines
4. `AccountRelationships.razor` - ~420 lines
5. `ContactRoles.razor` - ~160 lines
6. `DecisionMakers.razor` - ~220 lines

## Testing After Fixes

1. Run `dotnet build` to verify no compilation errors
2. Navigate to `/accounts/new` in the browser
3. Verify all pages load without console errors
4. Test form submission (will fail without API, but should show validation)
5. Check breadcrumbs display correctly
6. Verify styling looks good

## Support

If you need help with any of these fixes:

1. Check your existing working pages for the correct pattern
2. Copy the exact syntax from those pages
3. Refer to this document for common issues
4. Consult your project's coding standards

The implementation is solid - it just needs syntax alignment with your existing codebase!
