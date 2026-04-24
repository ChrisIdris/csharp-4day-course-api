using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExtraLesson3.Services;

// A plain class — no interface — registered with the DI container in Program.cs:
//
//     builder.Services.AddSingleton<Slugifier>();
//
// Controllers ask for it through constructor injection, the same way they ask for
// AppDbContext. This is the smallest possible DI surface — one service, one lifetime,
// injected directly. Adding an ISlugifier interface would buy us swappability for
// testing, which is a worthwhile next step but not the point of THIS lesson.
public class Slugifier
{
    // Matches any run of characters that are NOT ASCII letters, digits, or hyphens.
    // Those runs become a single "-" in the output.
    private static readonly Regex NonSlugChars = new(@"[^a-z0-9-]+", RegexOptions.Compiled);
    private static readonly Regex MultipleHyphens = new(@"-{2,}", RegexOptions.Compiled);

    // "My Weekend Plans!" → "my-weekend-plans"
    // "Café — très bon"  → "cafe-tres-bon"  (accent-stripping via Unicode normalisation)
    public string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Strip accents: decompose into base characters + combining marks, then drop the marks.
        // ("Café" → "Cafe", "é" → "e"). This is the standard .NET pattern.
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
