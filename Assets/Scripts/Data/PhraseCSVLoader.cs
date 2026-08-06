using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class PhraseCsvLoader
{
    private const char CsvSeparator = ',';
    private const char FragmentSeparator = '|';

    public static List<PhraseDefinition> LoadFromTextAsset(TextAsset csvFile)
    {
        List<PhraseDefinition> loadedPhrases =
            new List<PhraseDefinition>();

        if (csvFile == null)
        {
            Debug.LogError(
                "PhraseCsvLoader: el TextAsset del CSV es null."
            );

            return loadedPhrases;
        }

        string csvText = csvFile.text;

        if (string.IsNullOrWhiteSpace(csvText))
        {
            Debug.LogError(
                $"PhraseCsvLoader: el archivo CSV '{csvFile.name}' está vacío."
            );

            return loadedPhrases;
        }

        List<string[]> rows = ParseCsv(csvText);

        if (rows.Count <= 1)
        {
            Debug.LogWarning(
                $"PhraseCsvLoader: el archivo CSV '{csvFile.name}' no contiene filas de datos."
            );

            return loadedPhrases;
        }

        HashSet<string> usedIds = new HashSet<string>();

        for (int i = 1; i < rows.Count; i++)
        {
            string[] columns = rows[i];
            int rowNumber = i + 1;

            if (columns == null || columns.Length == 0)
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} vacía. Se omite."
                );
                continue;
            }

            if (columns.Length < 4)
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} inválida. " +
                    $"Se esperaban 4 columnas y llegaron {columns.Length}. Se omite."
                );
                continue;
            }

            string id = SafeTrim(columns[0]);
            string category = SafeTrim(columns[1]);
            string fullPhrase = SafeTrim(columns[2]);
            string fragmentsRaw = SafeTrim(columns[3]);

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} sin ID. Se omite."
                );
                continue;
            }

            if (usedIds.Contains(id))
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: ID duplicado '{id}' en fila {rowNumber}. Se omite."
                );
                continue;
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} con categoría vacía. Se omite."
                );
                continue;
            }

            if (string.IsNullOrWhiteSpace(fullPhrase))
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} con frase completa vacía. Se omite."
                );
                continue;
            }

            if (string.IsNullOrWhiteSpace(fragmentsRaw))
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} con fragmentos vacíos. Se omite."
                );
                continue;
            }

            string[] fragments = ParseFragments(fragmentsRaw);

            if (fragments.Length < 2)
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} debe tener al menos 2 fragmentos. Se omite."
                );
                continue;
            }

            // NUEVO:
            // Si el último fragmento es puntuación final independiente,
            // lo unimos automáticamente al fragmento anterior.
            fragments = MergeTrailingTerminalPunctuation(fragments);

            if (fragments.Length < 2)
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} quedó con menos de 2 fragmentos tras normalizar. Se omite."
                );
                continue;
            }

            if (ContainsEmptyFragment(fragments))
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: fila {rowNumber} contiene fragmentos vacíos. Se omite."
                );
                continue;
            }

            string rebuiltPhrase =
                RebuildPhraseFromFragments(fragments);

            if (!StringEqualsNormalized(rebuiltPhrase, fullPhrase))
            {
                Debug.LogWarning(
                    $"PhraseCsvLoader: la fila {rowNumber} no coincide con la frase completa.\n" +
                    $"ID: {id}\n" +
                    $"FullPhrase: '{fullPhrase}'\n" +
                    $"Reconstruida: '{rebuiltPhrase}'\n" +
                    $"Se omite."
                );
                continue;
            }

            PhraseDefinition definition =
                new PhraseDefinition(
                    id,
                    category,
                    fullPhrase,
                    fragments
                );

            loadedPhrases.Add(definition);
            usedIds.Add(id);
        }

        Debug.Log(
            $"CSV '{csvFile.name}' cargado correctamente: {loadedPhrases.Count} frases válidas."
        );

        return loadedPhrases;
    }

    private static string[] ParseFragments(string fragmentsRaw)
    {
        string[] rawParts =
            fragmentsRaw.Split(FragmentSeparator);

        List<string> cleanParts =
            new List<string>();

        for (int i = 0; i < rawParts.Length; i++)
        {
            string fragment = SafeTrim(rawParts[i]);

            if (!string.IsNullOrWhiteSpace(fragment))
            {
                cleanParts.Add(fragment);
            }
        }

        return cleanParts.ToArray();
    }

    /// <summary>
    /// Si el último fragmento es "." "!" "?" "..." o "…",
    /// se pega al fragmento anterior para que no aparezca
    /// como pieza independiente en el gameplay.
    /// </summary>
    private static string[] MergeTrailingTerminalPunctuation(string[] fragments)
    {
        if (fragments == null || fragments.Length < 2)
        {
            return fragments;
        }

        int lastIndex = fragments.Length - 1;
        string lastFragment = SafeTrim(fragments[lastIndex]);

        if (!IsTerminalClosingPunctuation(lastFragment))
        {
            return fragments;
        }

        string previousFragment = SafeTrim(fragments[lastIndex - 1]);

        if (string.IsNullOrWhiteSpace(previousFragment))
        {
            return fragments;
        }

        List<string> merged = new List<string>();

        for (int i = 0; i < fragments.Length - 2; i++)
        {
            merged.Add(SafeTrim(fragments[i]));
        }

        merged.Add(previousFragment + lastFragment);

        return merged.ToArray();
    }

    private static bool IsTerminalClosingPunctuation(string value)
    {
        return value == "." ||
               value == "!" ||
               value == "?" ||
               value == "..." ||
               value == "…";
    }

    private static bool ContainsEmptyFragment(string[] fragments)
    {
        for (int i = 0; i < fragments.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(fragments[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string RebuildPhraseFromFragments(string[] fragments)
    {
        if (fragments == null || fragments.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        string previous = string.Empty;

        for (int i = 0; i < fragments.Length; i++)
        {
            string current = SafeTrim(fragments[i]);

            if (string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.Append(current);
            }
            else
            {
                bool addSpace =
                    !IsPunctuationThatAvoidsSpaceBefore(current) &&
                    !IsPunctuationThatAvoidsSpaceAfter(previous);

                if (addSpace)
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            previous = current;
        }

        return builder.ToString();
    }

    private static bool IsPunctuationThatAvoidsSpaceBefore(string value)
    {
        return value == "." ||
               value == "," ||
               value == ";" ||
               value == ":" ||
               value == "!" ||
               value == "?" ||
               value == "%" ||
               value == ")" ||
               value == "]" ||
               value == "}" ||
               value == "…" ||
               value == "...";
    }

    private static bool IsPunctuationThatAvoidsSpaceAfter(string value)
    {
        return value == "¿" ||
               value == "¡" ||
               value == "(" ||
               value == "[" ||
               value == "{";
    }

    private static bool StringEqualsNormalized(string a, string b)
    {
        string normalizedA = NormalizeWhitespace(a);
        string normalizedB = NormalizeWhitespace(b);

        return string.Equals(
            normalizedA,
            normalizedB,
            StringComparison.Ordinal
        );
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        StringBuilder builder = new StringBuilder();

        bool previousWasWhitespace = false;

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];

            if (char.IsWhiteSpace(c))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(c);
                previousWasWhitespace = false;
            }
        }

        return builder.ToString();
    }

    private static string SafeTrim(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Trim();
    }

    private static List<string[]> ParseCsv(string csvText)
    {
        List<string[]> rows = new List<string[]>();
        List<string> currentRow = new List<string>();
        StringBuilder currentCell = new StringBuilder();

        bool insideQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char currentChar = csvText[i];

            if (currentChar == '"')
            {
                if (insideQuotes &&
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '"')
                {
                    currentCell.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (currentChar == CsvSeparator && !insideQuotes)
            {
                currentRow.Add(currentCell.ToString());
                currentCell.Clear();
                continue;
            }

            if ((currentChar == '\n' || currentChar == '\r') && !insideQuotes)
            {
                if (currentChar == '\r' &&
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '\n')
                {
                    i++;
                }

                currentRow.Add(currentCell.ToString());
                currentCell.Clear();

                rows.Add(currentRow.ToArray());
                currentRow.Clear();
                continue;
            }

            currentCell.Append(currentChar);
        }

        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }
}