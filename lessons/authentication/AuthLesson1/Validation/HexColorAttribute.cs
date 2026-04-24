using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AuthLesson1.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class HexColorAttribute : ValidationAttribute
{
    private static readonly Regex Pattern = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        return value is string s && Pattern.IsMatch(s);
    }

    public override string FormatErrorMessage(string name) =>
        $"The {name} field must be a hex colour like #ff0000 or #fc0.";
}
