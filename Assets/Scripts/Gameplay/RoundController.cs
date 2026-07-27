using System.Collections;
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
    [Tooltip("Margen adicional utilizado para distribuir las piezas al comenzar.")]
    [SerializeField, Min(0f)] private float spawnMargin = 40f;

    [SerializeField, Min(1)]
    private int maximumSpawnAttempts = 50;

    [Header("Final de ronda")]
    [Tooltip("Tiempo durante el cual se muestra la frase centrada antes del panel de victoria.")]
    [SerializeField, Min(0f)]
    private float finalPhraseDisplayDelay = 0.65f;

    [SerializeField]
    private bool centerFinalPhrase = true;

    private readonly List<PhrasePiece> activePieces =
        new List<PhrasePiece>();

    private int moves;
    private float elapsedTime;

    private bool roundIsRunning;
    private bool roundHasEnded;
    private bool isResolvingMove;

    private Coroutine endRoundCoroutine;

    public bool CanInteract =>
        roundIsRunning &&
        !roundHasEnded &&
        !isResolvingMove;

    private IEnumerator Start()
    {
        if (!startAutomatically)
        {
            yield break;
        }

        /*
         * Esperamos un frame para que el Canvas y el PlayArea
         * calculen correctamente sus dimensiones.
         */
        yield return null;

        Canvas.ForceUpdateCanvases();

        StartRound();
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

        if (!ValidateRoundConfiguration())
        {
            return;
        }

        moves = 0;
        elapsedTime = 0f;

        roundHasEnded = false;
        roundIsRunning = true;
        isResolvingMove = false;

        if (hudController != null)
        {
            hudController.Initialize(
                phrase.Category,
                phrase.FragmentCount
            );
        }

        List<int> fragmentIndexes =
            CreateFragmentIndexList();

        if (shuffleInitialPieces)
        {
            Shuffle(fragmentIndexes);
        }

        for (int i = 0; i < fragmentIndexes.Count; i++)
        {
            int fragmentIndex =
                fragmentIndexes[i];

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
        if (!CanInteract || draggedPiece == null)
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

    private bool ValidateRoundConfiguration()
    {
        if (phrase == null)
        {
            Debug.LogError(
                "RoundController no tiene una PhraseDefinition configurada."
            );

            return false;
        }

        if (!phrase.IsValid(out string errorMessage))
        {
            Debug.LogError(
                $"La frase no es válida: {errorMessage}"
            );

            return false;
        }

        if (phrasePiecePrefab == null)
        {
            Debug.LogError(
                "No se asignó Phrase Piece Prefab en RoundController."
            );

            return false;
        }

        if (playArea == null)
        {
            Debug.LogError(
                "No se asignó Play Area en RoundController."
            );

            return false;
        }

        if (rootCanvas == null)
        {
            rootCanvas = playArea.GetComponentInParent<Canvas>();
        }

        if (rootCanvas == null)
        {
            Debug.LogError(
                "No se encontró un Canvas para la ronda."
            );

            return false;
        }

        return true;
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

            if (candidate == null ||
                candidate == draggedPiece)
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

            if (overlapArea <= bestOverlapArea)
            {
                continue;
            }

            bestOverlapArea = overlapArea;
            bestTarget = candidate;
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
        isResolvingMove = true;

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

        /*
         * Los desactivamos inmediatamente para evitar que continúen
         * visibles durante el frame en el que Unity los destruye.
         */
        firstPiece.gameObject.SetActive(false);
        secondPiece.gameObject.SetActive(false);

        Destroy(firstPiece.gameObject);
        Destroy(secondPiece.gameObject);

        PhrasePiece mergedPiece = CreatePiece(
            newStartIndex,
            newEndIndex
        );

        mergedPiece.SetAnchoredPosition(mergePosition);

        UpdateHUD();

        bool victoryDetected =
            CheckVictory(mergedPiece);

        if (!victoryDetected)
        {
            isResolvingMove = false;
        }
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

        Vector2 fallbackPosition =
            Vector2.zero;

        for (int attempt = 0;
             attempt < maximumSpawnAttempts;
             attempt++)
        {
            Vector2 candidatePosition =
                GetRandomPositionInsidePlayArea(piece);

            piece.SetAnchoredPosition(
                candidatePosition
            );

            fallbackPosition =
                piece.AnchoredPosition;

            if (!OverlapsAnotherPiece(piece))
            {
                return;
            }
        }

        /*
         * Si no encontramos una posición perfecta después
         * de varios intentos, utilizamos la última posición válida.
         */
        piece.SetAnchoredPosition(fallbackPosition);
    }

    private Vector2 GetRandomPositionInsidePlayArea(
        PhrasePiece piece)
    {
        Rect areaRect = playArea.rect;
        Rect pieceRect = piece.RectTransform.rect;

        float halfWidth =
            pieceRect.width * 0.5f;

        float halfHeight =
            pieceRect.height * 0.5f;

        /*
         * Utilizamos el margen más grande entre el spawn
         * y el margen propio de la pieza.
         */
        float effectiveMargin = Mathf.Max(
            spawnMargin,
            piece.MovementPadding
        );

        float minimumX =
            areaRect.xMin +
            halfWidth +
            effectiveMargin;

        float maximumX =
            areaRect.xMax -
            halfWidth -
            effectiveMargin;

        float minimumY =
            areaRect.yMin +
            halfHeight +
            effectiveMargin;

        float maximumY =
            areaRect.yMax -
            halfHeight -
            effectiveMargin;

        float randomX =
            minimumX <= maximumX
                ? Random.Range(minimumX, maximumX)
                : 0f;

        float randomY =
            minimumY <= maximumY
                ? Random.Range(minimumY, maximumY)
                : 0f;

        return new Vector2(
            randomX,
            randomY
        );
    }

    private bool OverlapsAnotherPiece(
        PhrasePiece pieceToCheck)
    {
        Rect pieceRect =
            GetWorldRect(pieceToCheck.RectTransform);

        for (int i = 0; i < activePieces.Count; i++)
        {
            PhrasePiece otherPiece =
                activePieces[i];

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

    private bool CheckVictory(PhrasePiece finalPiece)
    {
        if (activePieces.Count != 1)
        {
            return false;
        }

        bool containsEntirePhrase =
            finalPiece.StartIndex == 0 &&
            finalPiece.EndIndex ==
            phrase.FragmentCount - 1;

        if (!containsEntirePhrase)
        {
            return false;
        }

        roundHasEnded = true;
        roundIsRunning = false;
        isResolvingMove = true;

        finalPiece.SetInteractable(false);
        finalPiece.transform.SetAsLastSibling();

        if (centerFinalPhrase)
        {
            Canvas.ForceUpdateCanvases();
            finalPiece.CenterInMovementArea();
        }

        endRoundCoroutine = StartCoroutine(
            FinishRoundSequence(finalPiece)
        );

        return true;
    }

    private IEnumerator FinishRoundSequence(
        PhrasePiece finalPiece)
    {
        /*
         * Dejamos visible la frase final en el centro antes
         * de mostrar la interfaz de victoria.
         */
        if (finalPhraseDisplayDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                finalPhraseDisplayDelay
            );
        }

        string completedPhrase =
            finalPiece != null
                ? finalPiece.PieceText
                : PhraseTextFormatter.BuildText(
                    phrase.Fragments,
                    0,
                    phrase.FragmentCount - 1
                );

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

        isResolvingMove = false;
        endRoundCoroutine = null;
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

        hudController.SetTime(
            elapsedTime
        );
    }

    private List<int> CreateFragmentIndexList()
    {
        List<int> indexes =
            new List<int>();

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

            int temporaryValue =
                indexes[i];

            indexes[i] =
                indexes[randomIndex];

            indexes[randomIndex] =
                temporaryValue;
        }
    }

    private Rect GetWorldRect(
        RectTransform rectTransform)
    {
        Vector3[] corners =
            new Vector3[4];

        rectTransform.GetWorldCorners(corners);

        float width =
            corners[2].x -
            corners[0].x;

        float height =
            corners[2].y -
            corners[0].y;

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
            Mathf.Min(
                firstRect.xMax,
                secondRect.xMax
            ) -
            Mathf.Max(
                firstRect.xMin,
                secondRect.xMin
            )
        );

        float overlapHeight = Mathf.Max(
            0f,
            Mathf.Min(
                firstRect.yMax,
                secondRect.yMax
            ) -
            Mathf.Max(
                firstRect.yMin,
                secondRect.yMin
            )
        );

        return overlapWidth * overlapHeight;
    }

    private void ClearCurrentRound()
    {
        if (endRoundCoroutine != null)
        {
            StopCoroutine(endRoundCoroutine);
            endRoundCoroutine = null;
        }

        for (int i = 0;
             i < activePieces.Count;
             i++)
        {
            PhrasePiece piece =
                activePieces[i];

            if (piece == null)
            {
                continue;
            }

            piece.gameObject.SetActive(false);
            Destroy(piece.gameObject);
        }

        activePieces.Clear();

        roundIsRunning = false;
        roundHasEnded = false;
        isResolvingMove = false;

        if (hudController != null)
        {
            hudController.HideVictory();
        }
    }
}