using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ExtraLesson2.Validation;

// Custom validation attribute — reusable rule that says "this string must be a hex
// colour of the form #RGB or #RRGGBB."
//
// Inherits from ValidationAttribute (the base class for every data-annotation rule
// including [Required], [StringLength], [Range] etc.). Overriding IsValid is the
// simplest path for single-value rules; for cross-field rules you'd override the
// richer `IsValid(object?, ValidationContext)` overload.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class HexColorAttribute : ValidationAttribute
{
    // Compiled once, shared across every invocation. `^#` and `$` anchor the whole
    // string — "##ff0000 extra stuff" fails. Three- or six-digit hex accepted.
    private static readonly Regex Pattern = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        // Validation runs on nullable strings too. When the field is null the rule
        // "passes" — use [Required] alongside to forbid null. Having the two concerns
        // composable is the whole point of attributes.
        if (value is null) return true;
        return value is string s && Pattern.IsMatch(s);
    }

    // Default message when IsValid returns false. The caller can override via the
    // attribute's ErrorMessage property: [HexColor(ErrorMessage = "...")] on a DTO.
    public override string FormatErrorMessage(string name) =>
        $"The {name} field must be a hex colour like #ff0000 or #fc0.";
}
