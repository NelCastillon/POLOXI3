public sealed class ToastModel
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? CssClass { get; set; }
    public string? Icon { get; set; }
    public int Timeout { get; set; }
    public bool ShowCloseButton { get; set; } = true;
}
