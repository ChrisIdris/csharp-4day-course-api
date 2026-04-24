using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AuthLesson1.Services;

public class Slugifier
{
    private static readonly Regex NonSlugChars = new(@"[^a-z0-9-]+", RegexOptions.Compiled);
    private static readonly Regex MultipleHyphens = new(@"-{2,}", RegexOptions.Compiled);

    public string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalised = input.Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(capacity: normalised.Length);
        foreach (var ch in normalised)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                stripped.Append(ch);
            }
        }

        var lower = stripped.ToString().ToLowerInvariant();
        var replaced = NonSlugChars.Replace(lower, "-");
        var collapsed = MultipleHyphens.Replace(replaced, "-");
        return collapsed.Trim('-');
    }
}
