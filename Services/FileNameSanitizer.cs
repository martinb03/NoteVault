namespace NoteVault.Services;

public class FileNameSanitizer
{
    private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
 
    public static string Sanitize(string input, string fallback = "untitled")
    {
        if (string.IsNullOrWhiteSpace(input))
            return fallback;
 
        var cleaned = input;
 
        // Replace illegal characters with underscore
        foreach (var c in InvalidChars)
        {
            cleaned = cleaned.Replace(c, '_');
        }
 
        // Strip control characters
        cleaned = new string(cleaned.Where(c => !char.IsControl(c)).ToArray());
 
        // Trim trailing dots and spaces (Windows-illegal)
        cleaned = cleaned.TrimEnd(' ', '.');
 
        // Cap length (Windows MAX_PATH considerations)
        if (cleaned.Length > 100)
            cleaned = cleaned.Substring(0, 100);
 
        // Fallback if empty after sanitization
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = fallback;
 
        return cleaned;
    }
}