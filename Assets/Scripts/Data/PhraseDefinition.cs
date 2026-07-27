using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PhraseDefinition
{
    [Header("Identificación")]
    [SerializeField] private string id = "phrase_001";
    [SerializeField] private string category = "Futurama";

    [Header("Fragmentos en el orden correcto")]
    [SerializeField]
    private string[] fragments =
    {
        "¡",
        "Lávate",
        "las",
        "orejas",
        ",",
        "terrícola",
        "!"
    };

    public string Id => id;
    public string Category => category;
    public IReadOnlyList<string> Fragments => fragments;
    public int FragmentCount => fragments != null ? fragments.Length : 0;

    public string GetFragment(int index)
    {
        if (fragments == null)
        {
            return string.Empty;
        }

        if (index < 0 || index >= fragments.Length)
        {
            return string.Empty;
        }

        return fragments[index];
    }

    public bool IsValid(out string errorMessage)
    {
        if (fragments == null || fragments.Length < 2)
        {
            errorMessage = "La frase debe contener al menos dos fragmentos.";
            return false;
        }

        for (int i = 0; i < fragments.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(fragments[i]))
            {
                errorMessage = $"El fragmento de índice {i} está vacío.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }
}