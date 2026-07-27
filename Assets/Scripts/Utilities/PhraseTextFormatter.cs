using System.Collections.Generic;
using System.Text;

public static class PhraseTextFormatter
{
    private static readonly HashSet<string> NoSpaceBefore =
        new HashSet<string>
        {
            ".",
            ",",
            ";",
            ":",
            "?",
            "!",
            "%",
            ")",
            "]",
            "}",
            "…",
            "..."
        };

    private static readonly HashSet<string> NoSpaceAfter =
        new HashSet<string>
        {
            "¿",
            "¡",
            "(",
            "[",
            "{"
        };

    public static string BuildText(
        IReadOnlyList<string> fragments,
        int startIndex,
        int endIndex)
    {
        if (fragments == null || fragments.Count == 0)
        {
            return string.Empty;
        }

        startIndex = System.Math.Max(0, startIndex);
        endIndex = System.Math.Min(fragments.Count - 1, endIndex);

        StringBuilder builder = new StringBuilder();
        string previousFragment = string.Empty;

        for (int i = startIndex; i <= endIndex; i++)
        {
            string currentFragment = fragments[i]?.Trim();

            if (string.IsNullOrEmpty(currentFragment))
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.Append(currentFragment);
            }
            else
            {
                bool shouldAddSpace =
                    !NoSpaceBefore.Contains(currentFragment) &&
                    !NoSpaceAfter.Contains(previousFragment);

                if (shouldAddSpace)
                {
                    builder.Append(' ');
                }

                builder.Append(currentFragment);
            }

            previousFragment = currentFragment;
        }

        return builder.ToString();
    }
}