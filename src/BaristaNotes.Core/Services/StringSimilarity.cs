namespace BaristaNotes.Core.Services;

/// <summary>
/// Small string similarity helpers used by fuzzy matching.
/// Simple O(n*m) Levenshtein DP — names are short so this is fine.
/// </summary>
internal static class StringSimilarity
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var lowered = value.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lowered.Length);
        var prevSpace = false;
        foreach (var c in lowered)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!prevSpace && sb.Length > 0)
                    sb.Append(' ');
                prevSpace = true;
            }
            else
            {
                sb.Append(c);
                prevSpace = false;
            }
        }

        if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
            sb.Length -= 1;

        return sb.ToString();
    }

    public static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = System.Math.Min(
                    System.Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
