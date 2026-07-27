using System.Collections.Generic;
using UnityEngine;

public class RoundController : MonoBehaviour
{
    [Header("Datos de la ronda")]
    [SerializeField] private PhraseDefinition phrase;

    [Header("Referencias")]
    [SerializeField] private PhrasePiece phrasePiecePrefab;
    [SerializeField] private RectTransform playArea;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private HUDController hudController;

    [Header("Inicio")]
    [SerializeField] private bool startAutomatically = true;
    [SerializeField] private bool shuffleInitialPieces = true;

    [Header("Distribución")]
    [SerializeField] private float spawnMargin = 30f;
    [SerializeField] private int maximumSpawnAttempts = 50;

    private readonly List<PhrasePiece> activePieces =
        new List<PhrasePiece>();

    private int moves;
    private float elapsedTime;
    private bool roundIsRunning;
    private bool roundHasEnded;

    private void Start()
    {
        if (startAutomatically)
        {
            StartRound();
        }
    }

    private void Update()
    {
        if (!roundIsRunning || roundHasEnded)
        {
            return;
        }

        elapsedTime += Time.deltaTime;

        if (hudController != null)
        {
            hudController.SetTime(elapsedTime);
        }
    }

    [ContextMenu("Start Round")]
    public void StartRound()
    {
        ClearCurrentRound();

        if (phrase == null)
        {
            Debug.LogError(
                "RoundController no tiene una PhraseDefinition configurada."
            );
            return;
        }

        if (!phrase.IsValid(out string errorMessage))
        {
            Debug.LogError($"La frase no es válida: {errorMessage}");
            return;
        }

        if (phrasePiecePrefab == null)
        {
            Debug.LogError(
                "No se asignó el prefab PhrasePiece."
            );
            return;
        }

        if (playArea == null)
        {
            Debug.LogError(
                "No se asignó el PlayArea."
            );
            return;
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        moves = 0;
        elapsedTime = 0f;
        roundHasEnded = false;
        roundIsRunning = true;

        if (hudController != null)
        {
            hudController.Initialize(
                phrase.Category,
                phrase.FragmentCount
            );
        }

        List<int> fragmentIndexes = CreateFragmentIndexList();

        if (shuffleInitialPieces)
        {
            Shuffle(fragmentIndexes);
        }

        for (int i = 0; i < fragmentIndexes.Count; i++)
        {
            int fragmentIndex = fragmentIndexes[i];

            PhrasePiece piece = CreatePiece(
                fragmentIndex,
                fragmentIndex
            );

            PlacePieceRandomly(piece);
        }

        UpdateHUD();
    }

    public void RestartRound()
    {
        StartRound();
    }

    public void HandlePieceDropped(PhrasePiece draggedPiece)
    {
        if (!roundIsRunning ||
            roundHasEnded ||
            draggedPiece == null)
        {
            return;
        }

        moves++;

        PhrasePiece mergeTarget =
            FindBestValidMergeTarget(draggedPiece);

        if (mergeTarget == null)
        {
            draggedPiece.ReturnToDragOrigin();
            UpdateHUD();
            return;
        }

        MergePieces(draggedPiece, mergeTarget);
    }

    private PhrasePiece FindBestValidMergeTarget(
        PhrasePiece draggedPiece)
    {
        PhrasePiece bestTarget = null;
        float bestOverlapArea = 0f;

        Rect draggedRect =
            GetWorldRect(draggedPiece.RectTransform);

        for (int i = 0; i < activePieces.Count; i++)
        {
            PhrasePiece candidate = activePieces[i];

            if (candidate == null || candidate == draggedPiece)
            {
                continue;
            }

            Rect candidateRect =
                GetWorldRect(candidate.RectTransform);

            if (!draggedRect.Overlaps(candidateRect))
            {
                continue;
            }

            if (!CanMerge(draggedPiece, candidate))
            {
                continue;
            }

            float overlapArea =
                CalculateOverlapArea(
                    draggedRect,
                    candidateRect
                );

            if (overlapArea > bestOverlapArea)
            {
                bestOverlapArea = overlapArea;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private bool CanMerge(
        PhrasePiece firstPiece,
        PhrasePiece secondPiece)
    {
        bool firstGoesBeforeSecond =
            firstPiece.EndIndex + 1 ==
            secondPiece.StartIndex;

        bool secondGoesBeforeFirst =
            secondPiece.EndIndex + 1 ==
            firstPiece.StartIndex;

        return firstGoesBeforeSecond ||
               secondGoesBeforeFirst;
    }

    private void MergePieces(
        PhrasePiece firstPiece,
        PhrasePiece secondPiece)
    {
        int newStartIndex = Mathf.Min(
            firstPiece.StartIndex,
            secondPiece.StartIndex
        );

        int newEndIndex = Mathf.Max(
            firstPiece.EndIndex,
            secondPiece.EndIndex
        );

        Vector2 mergePosition =
            secondPiece.AnchoredPosition;

        firstPiece.SetInteractable(false);
        secondPiece.SetInteractable(false);

        activePieces.Remove(firstPiece);
        activePieces.Remove(secondPiece);

        Destroy(firstPiece.gameObject);
        Destroy(secondPiece.gameObject);

        PhrasePiece mergedPiece = CreatePiece(
            newStartIndex,
            newEndIndex
        );

        mergedPiece.SetAnchoredPosition(mergePosition);

        UpdateHUD();
        CheckVictory(mergedPiece);
    }

    private PhrasePiece CreatePiece(
        int startIndex,
        int endIndex)
    {
        string pieceText =
            PhraseTextFormatter.BuildText(
                phrase.Fragments,
                startIndex,
                endIndex
            );

        PhrasePiece newPiece = Instantiate(
            phrasePiecePrefab,
            playArea
        );

        newPiece.gameObject.SetActive(true);

        newPiece.Initialize(
            this,
            rootCanvas,
            playArea,
            pieceText,
            startIndex,
            endIndex
        );

        activePieces.Add(newPiece);

        Canvas.ForceUpdateCanvases();

        return newPiece;
    }

    private void PlacePieceRandomly(PhrasePiece piece)
    {
        if (piece == null)
        {
            return;
        }

        Vector2 fallbackPosition = Vector2.zero;

        for (int attempt = 0;
             attempt < maximumSpawnAttempts;
             attempt++)
        {
            Vector2 candidatePosition =
                GetRandomPositionInsidePlayArea(piece);

            piece.SetAnchoredPosition(candidatePosition);

            fallbackPosition = candidatePosition;

            if (!OverlapsAnotherPiece(piece))
            {
                return;
            }
        }

        piece.SetAnchoredPosition(fallbackPosition);
    }

    private Vector2 GetRandomPositionInsidePlayArea(
        PhrasePiece piece)
    {
        Rect areaRect = playArea.rect;
        Rect pieceRect = piece.RectTransform.rect;

        float halfWidth = pieceRect.width * 0.5f;
        float halfHeight = pieceRect.height * 0.5f;

        float minimumX =
            areaRect.xMin + halfWidth + spawnMargin;

        float maximumX =
            areaRect.xMax - halfWidth - spawnMargin;

        float minimumY =
            areaRect.yMin + halfHeight + spawnMargin;

        float maximumY =
            areaRect.yMax - halfHeight - spawnMargin;

        float x = minimumX <= maximumX
            ? Random.Range(minimumX, maximumX)
            : 0f;

        float y = minimumY <= maximumY
            ? Random.Range(minimumY, maximumY)
            : 0f;

        return new Vector2(x, y);
    }

    private bool OverlapsAnotherPiece(
        PhrasePiece pieceToCheck)
    {
        Rect pieceRect =
            GetWorldRect(pieceToCheck.RectTransform);

        for (int i = 0; i < activePieces.Count; i++)
        {
            PhrasePiece otherPiece = activePieces[i];

            if (otherPiece == null ||
                otherPiece == pieceToCheck)
            {
                continue;
            }

            Rect otherRect =
                GetWorldRect(otherPiece.RectTransform);

            if (pieceRect.Overlaps(otherRect))
            {
                return true;
            }
        }

        return false;
    }

    private void CheckVictory(PhrasePiece finalPiece)
    {
        if (activePieces.Count != 1)
        {
            return;
        }

        bool containsEntirePhrase =
            finalPiece.StartIndex == 0 &&
            finalPiece.EndIndex ==
            phrase.FragmentCount - 1;

        if (!containsEntirePhrase)
        {
            return;
        }

        EndRound(finalPiece.PieceText);
    }

    private void EndRound(string completedPhrase)
    {
        roundHasEnded = true;
        roundIsRunning = false;

        for (int i = 0; i < activePieces.Count; i++)
        {
            if (activePieces[i] != null)
            {
                activePieces[i].SetInteractable(false);
            }
        }

        if (hudController != null)
        {
            hudController.ShowVictory(
                completedPhrase,
                moves,
                elapsedTime
            );
        }

        Debug.Log(
            $"Frase completada: {completedPhrase}"
        );
    }

    private void UpdateHUD()
    {
        if (hudController == null)
        {
            return;
        }

        hudController.SetMoves(moves);
        hudController.SetFragmentCount(
            activePieces.Count
        );
        hudController.SetTime(elapsedTime);
    }

    private List<int> CreateFragmentIndexList()
    {
        List<int> indexes = new List<int>();

        for (int i = 0;
             i < phrase.FragmentCount;
             i++)
        {
            indexes.Add(i);
        }

        return indexes;
    }

    private void Shuffle(List<int> indexes)
    {
        for (int i = indexes.Count - 1;
             i > 0;
             i--)
        {
            int randomIndex =
                Random.Range(0, i + 1);

            int temporaryValue = indexes[i];
            indexes[i] = indexes[randomIndex];
            indexes[randomIndex] = temporaryValue;
        }
    }

    private Rect GetWorldRect(
        RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float width = corners[2].x - corners[0].x;
        float height = corners[2].y - corners[0].y;

        return new Rect(
            corners[0].x,
            corners[0].y,
            width,
            height
        );
    }

    private float CalculateOverlapArea(
        Rect firstRect,
        Rect secondRect)
    {
        float overlapWidth = Mathf.Max(
            0f,
            Mathf.Min(firstRect.xMax, secondRect.xMax) -
            Mathf.Max(firstRect.xMin, secondRect.xMin)
        );

        float overlapHeight = Mathf.Max(
            0f,
            Mathf.Min(firstRect.yMax, secondRect.yMax) -
            Mathf.Max(firstRect.yMin, secondRect.yMin)
        );

        return overlapWidth * overlapHeight;
    }

    private void ClearCurrentRound()
    {
        for (int i = 0; i < activePieces.Count; i++)
        {
            if (activePieces[i] != null)
            {
                Destroy(activePieces[i].gameObject);
            }
        }

        activePieces.Clear();

        roundIsRunning = false;
        roundHasEnded = false;

        if (hudController != null)
        {
            hudController.HideVictory();
        }
    }
}