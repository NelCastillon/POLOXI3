using Legal.Application.Features.Intelligence.Decision.Core;
using Microsoft.Playwright;
using System.Collections.Concurrent;

namespace Legal.Infrastructure.Intelligence;

public sealed class PlaywrightWebSourceInspector : IWebSourceInspector, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, Task<WebSourceInspectionResult>> _inspections =
        new(StringComparer.OrdinalIgnoreCase);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<WebSourceInspectionResult> InspectAsync(
        string sourceRef,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(sourceRef, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            return Failure("SOURCE_REFERENCE_NOT_HTTP", attempted: false);

        return await _inspections.GetOrAdd(uri.AbsoluteUri, _ => InspectCoreAsync(uri, cancellationToken));
    }

    private async Task<WebSourceInspectionResult> InspectCoreAsync(Uri uri, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _playwright ??= await Playwright.CreateAsync();
            _browser ??= await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure($"PLAYWRIGHT_BROWSER_UNAVAILABLE:{ex.GetType().Name}", attempted: true);
        }
        finally
        {
            _gate.Release();
        }

        await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = false,
            JavaScriptEnabled = true,
        });
        var page = await context.NewPageAsync();
        try
        {
            var response = await page.GotoAsync(uri.AbsoluteUri, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 20_000,
            });
            var status = response?.Status;
            var resolved = response is not null && response.Ok;
            var text = resolved ? await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5_000 }) : null;
            return new WebSourceInspectionResult
            {
                Attempted = true,
                Resolved = resolved,
                StatusCode = status,
                FinalUrl = page.Url,
                DocumentTitle = await page.TitleAsync(),
                VisibleText = string.IsNullOrWhiteSpace(text) ? null : Truncate(text, 100_000),
                FailureReason = resolved ? null : $"HTTP_STATUS_{status?.ToString() ?? "UNKNOWN"}",
                VerificationMethod = "MICROSOFT_PLAYWRIGHT_CHROMIUM_V1",
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure($"PLAYWRIGHT_NAVIGATION_FAILED:{ex.GetType().Name}", attempted: true);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
        _gate.Dispose();
    }

    private static WebSourceInspectionResult Failure(string reason, bool attempted) => new()
    {
        Attempted = attempted,
        Resolved = false,
        FailureReason = reason,
        VerificationMethod = "MICROSOFT_PLAYWRIGHT_CHROMIUM_V1",
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
