namespace NoteVault.ViewModels;
 
public class BreadcrumbItem
{
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }
 
    public BreadcrumbItem(string text, string? url = null)
    {
        Text = text;
        Url = url;
    }
}