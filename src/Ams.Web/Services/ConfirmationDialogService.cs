namespace Ams.Web.Services;

public sealed class ConfirmationDialogService
{
    public event Action<ConfirmationRequest>? OnShow;

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Delete", string cancelText = "Cancel")
    {
        var tcs = new TaskCompletionSource<bool>();
        OnShow?.Invoke(new ConfirmationRequest(title, message, confirmText, cancelText, tcs));
        return tcs.Task;
    }
}

public sealed record ConfirmationRequest(
    string Title,
    string Message,
    string ConfirmText,
    string CancelText,
    TaskCompletionSource<bool> CompletionSource);
