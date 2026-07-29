using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class PhraseCsvLoader
{
    private const char CsvSeparator = ',';
    private const char FragmentSeparator = '|';

    public static List<PhraseDefinition> Load(TextAsset csvFile)
    {
        List<PhraseDefinition> validPhrases = new List<PhraseDefinition>();

        if (csvFile == null)
        {
            Debug.LogError("No se asignó un archivo CSV de frases.");
            return validPhrases;
        }

        if (string.IsNullOrWhiteSpace(csvFile.text))
        {
            Debug.LogError($"El archivo CSV '{csvFile.name}' está vacío.");
            return validPhrases;
        }

        List<List<string>> rows = ParseCsv(csvFile.text);

        if (rows.Count < 2)
        {
            Debug.LogError(
                $"El archivo CSV '{csvFile.name}' no contiene filas de datos."
            );

            return validPhrases;
        }

        Dictionary<string, int> headerMap = BuildHeaderMap(rows[0]);

        string[] requiredHeaders =
        {
            "id",
            "category",
            "fullPhrase",
            "fragments"
        };

        for (int i = 0; i < requiredHeaders.Length; i++)
        {
            if (!headerMap.ContainsKey(requiredHeaders[i]))
            {
                Debug.LogError(
                    $"El CSV '{csvFile.name}' no contiene la columna obligatoria " +
                    $"'{requiredHeaders[i]}'."
                );

                return validPhrases;
            }
        }

        HashSet<string> loadedIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            List<string> row = rows[rowIndex];
            int visibleRowNumber = rowIndex + 1;

            if (IsEmptyRow(row))
            {
                continue;
            }

            string id = GetColumnValue(row, headerMap["id"]);
            string category = GetColumnValue(row, headerMap["category"]);
            string fullPhrase = GetColumnValue(row, headerMap["fullPhrase"]);
            string rawFragments = GetColumnValue(row, headerMap["fragments"]);

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning(
                    $"CSV '{csvFile.name}', fila {visibleRowNumber}: ID vacío. " +
                    "La fila fue ignorada."
                );

                continue;
            }

            if (!loadedIds.Add(id.Trim()))
            {
                Debug.LogWarning(
                    $"CSV '{csvFile.name}', fila {visibleRowNumber}: " +
                    $"el ID '{id}' está repetido. La fila fue ignorada."
                );

                continue;
            }

            string[] fragments = SplitFragments(rawFragments);

            PhraseDefinition phrase = new PhraseDefinition(
                id,
                category,
                fullPhrase,
                fragments
            );

            if (!phrase.IsValid(out string validationError))
            {
                Debug.LogWarning(
                    $"CSV '{csvFile.name}', fila {visibleRowNumber}: " +
                    validationError + " La fila fue ignorada."
                );

                loadedIds.Remove(id.Trim());
                continue;
            }

            validPhrases.Add(phrase);
        }

        Debug.Log(
            $"CSV '{csvFile.name}' cargado correctamente: " +
            $"{validPhrases.Count} frases válidas."
        );

        return validPhrases;
    }

    private static string[] SplitFragments(string rawFragments)
    {
        if (string.IsNullOrWhiteSpace(rawFragments))
        {
            return new string[0];
        }

        string[] fragments = rawFragments.Split(
            new[] { FragmentSeparator },
            StringSplitOptions.None
        );

        for (int i = 0; i < fragments.Length; i++)
        {
            fragments[i] = fragments[i].Trim();
        }

        return fragments;
    }

    private static Dictionary<string, int> BuildHeaderMap(
        List<string> headerRow)
    {
        Dictionary<string, int> headerMap = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase
        );

        for (int i = 0; i < headerRow.Count; i++)
        {
            string header = headerRow[i]
                .Trim()
                .TrimStart('\uFEFF');

            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (!headerMap.ContainsKey(header))
            {
                headerMap.Add(header, i);
            }
        }

        return headerMap;
    }

    private static string GetColumnValue(List<string> row, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= row.Count)
        {
            return string.Empty;
        }

        return row[columnIndex].Trim();
    }

    private static bool IsEmptyRow(List<string> row)
    {
        if (row == null || row.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < row.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(row[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static List<List<string>> ParseCsv(string csvText)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> currentRow = new List<string>();
        StringBuilder currentField = new StringBuilder();

        bool insideQuotedField = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char currentCharacter = csvText[i];

            if (currentCharacter == '"')
            {
                bool isEscapedQuote =
                    insideQuotedField &&
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '"';

                if (isEscapedQuote)
                {
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    insideQuotedField = !insideQuotedField;
                }

                continue;
            }

            if (currentCharacter == CsvSeparator && !insideQuotedField)
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
                continue;
            }

            bool isLineBreak =
                currentCharacter == '\n' ||
                currentCharacter == '\r';

            if (isLineBreak && !insideQuotedField)
            {
                if (
                    currentCharacter == '\r' &&
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '\n'
                )
                {
                    i++;
                }

                currentRow.Add(currentField.ToString());
                currentField.Clear();

                if (!IsEmptyRow(currentRow))
                {
                    rows.Add(currentRow);
                }

                currentRow = new List<string>();
                continue;
            }

            currentField.Append(currentCharacter);
        }

        if (insideQuotedField)
        {
            Debug.LogWarning(
                "El CSV terminó dentro de un campo entre comillas. " +
                "Revisa que las comillas estén correctamente cerradas."
            );
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());

            if (!IsEmptyRow(currentRow))
            {
                rows.Add(currentRow);
            }
        }

        return rows;
    }
}