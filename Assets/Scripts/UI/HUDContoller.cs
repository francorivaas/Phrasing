using TMPro;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    [Header("HUD de la ronda")]
    [SerializeField] private TMP_Text categoryText;
    [SerializeField] private TMP_Text fragmentCountText;
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private TMP_Text timerText;

    [Header("Panel de victoria")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TMP_Text completedPhraseText;
    [SerializeField] private TMP_Text victorySummaryText;

    public void Initialize(string category, int fragmentCount)
    {
        SetCategory(category);
        SetFragmentCount(fragmentCount);
        SetMoves(0);
        SetTime(0f);
        HideVictory();
    }

    public void SetCategory(string category)
    {
        if (categoryText != null)
        {
            categoryText.text = category;
        }
    }

    public void SetFragmentCount(int fragmentCount)
    {
        if (fragmentCountText != null)
        {
            fragmentCountText.text = $"Fragmentos: {fragmentCount}";
        }
    }

    public void SetMoves(int moves)
    {
        if (movesText != null)
        {
            movesText.text = $"Movimientos: {moves}";
        }
    }

    public void SetTime(float elapsedSeconds)
    {
        if (timerText == null)
        {
            return;
        }

        int totalSeconds = Mathf.FloorToInt(elapsedSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void ShowVictory(
        string completedPhrase,
        int moves,
        float elapsedSeconds)
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        if (completedPhraseText != null)
        {
            completedPhraseText.text = completedPhrase;
        }

        if (victorySummaryText != null)
        {
            int totalSeconds = Mathf.FloorToInt(elapsedSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            victorySummaryText.text =
                $"Movimientos: {moves}\n" +
                $"Tiempo: {minutes:00}:{seconds:00}";
        }
    }

    public void HideVictory()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }
}