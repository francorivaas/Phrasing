using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class PhraseDefinition
{
    [SerializeField] private string id;
    [SerializeField] private string category;
    [SerializeField] private string fullPhrase;
    [SerializeField] private string[] fragments = new string[0];

    public string Id => id;
    public string Category => category;
    public string FullPhrase => fullPhrase;
    public IReadOnlyList<string> Fragments => fragments;
    public int FragmentCount => fragments != null ? fragments.Length : 0;

    public PhraseDefinition()
    {
    }

    public PhraseDefinition(
        string id,
        string category,
        string fullPhrase,
        string[] fragments)
    {
        this.id = id != null ? id.Trim() : string.Empty;
        this.category = category != null ? category.Trim() : string.Empty;
        this.fullPhrase = fullPhrase != null ? fullPhrase.Trim() : string.Empty;
        this.fragments = fragments ?? new string[0];
    }

    public string GetFragment(int index)
    {
        if (fragments == null || index < 0 || index >= fragments.Length)
        {
            return string.Empty;
        }

        return fragments[index];
    }

    public bool IsValid(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            errorMessage = "La frase no tiene un ID válido.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errorMessage = $"La frase '{id}' no tiene categoría.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fullPhrase))
        {
            errorMessage = $"La frase '{id}' no tiene el texto completo.";
            return false;
        }

        if (fragments == null || fragments.Length < 2)
        {
            errorMessage = $"La frase '{id}' debe contener al menos dos fragmentos.";
            return false;
        }

        for (int i = 0; i < fragments.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(fragments[i]))
            {
                errorMessage = $"La frase '{id}' contiene un fragmento vacío en el índice {i}.";
                return false;
            }

            fragments[i] = fragments[i].Trim();
        }

        string reconstructedPhrase = PhraseTextFormatter.BuildText(
            fragments,
            0,
            fragments.Length - 1
        );

        if (!string.Equals(
                NormalizeForComparison(reconstructedPhrase),
                NormalizeForComparison(fullPhrase),
                StringComparison.Ordinal))
        {
            errorMessage =
                $"La frase '{id}' no coincide con sus fragmentos. " +
                $"CSV: '{fullPhrase}' | Reconstruida: '{reconstructedPhrase}'.";

            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static string NormalizeForComparison(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        bool previousCharacterWasWhitespace = false;

        for (int i = 0; i < text.Length; i++)
        {
            char currentCharacter = text[i];

            if (char.IsWhiteSpace(currentCharacter))
            {
                if (builder.Length > 0 && !previousCharacterWasWhitespace)
                {
                    builder.Append(' ');
                }

                previousCharacterWasWhitespace = true;
                continue;
            }

            builder.Append(currentCharacter);
            previousCharacterWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }
}