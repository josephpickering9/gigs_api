using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Gigs.Utils;

public static class StringExtensions
{
    public static string ToSlug(this string phrase)
    {
        // Remove all accents and make the string lower case
        var output = phrase.ToLowerInvariant();
        output = RemoveDiacritics(output);

        // Replace spaces with hyphens
        output = Regex.Replace(output, @"\s", "-", RegexOptions.Compiled);

        // Remove invalid chars
        output = Regex.Replace(output, @"[^a-z0-9\s-_]", string.Empty, RegexOptions.Compiled);

        // Trim dashes from the end
        output = output.Trim('-', '_');

        // Replace double occurrences of - or \_
        output = Regex.Replace(output, @"([-_]){2,}", "$1", RegexOptions.Compiled);

        return output;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
