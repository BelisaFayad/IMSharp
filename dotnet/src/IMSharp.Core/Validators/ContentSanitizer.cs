using System.Text.RegularExpressions;

namespace IMSharp.Core.Validators;

public static class ContentSanitizer
{
    private static readonly Regex[] ScriptPatterns =
    [
        new Regex(@"<\s*script[\s\S]*?>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"<\s*/\s*script\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"javascript\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"vbscript\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bon\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"data\s*:\s*text/html", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public static bool ContainsScript(string? content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return ScriptPatterns.Any(p => p.IsMatch(content));
    }
}
