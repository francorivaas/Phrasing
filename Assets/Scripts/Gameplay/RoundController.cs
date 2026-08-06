using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundController : MonoBehaviour
{
    [Header("Base de frases CSV")]
    [SerializeField] private TextAsset phrasesCsv;

    [Tooltip("Mezcla el orden de las frases antes de comenzar cada ciclo.")]
    [SerializeField] private bool randomizePhraseOrder = true;

    [Tooltip("Evita repetir una frase hasta haber usado toda la base de datos.")]
    [SerializeField] private bool avoidRepeatsUntilExhausted = true;

    [Header("Referencias")]
    [SerializeField] private PhrasePiece phrasePiecePrefab;
    [SerializeField] private RectTransform playArea;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private HUDController hudController;
    [SerializeField] private MemoryChallengeController memoryChallengeController;

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

    private readonly List<PhraseDefinition> phraseDatabase =
        new List<PhraseDefinition>();

    private readonly List<int> phraseOrder =
        new List<int>();

    private readonly List<PhrasePiece> activePieces =
        new List<PhrasePiece>();

    private PhraseDefinition currentPhrase;
    private int currentPhraseDatabaseIndex = -1;
    private int phraseOrderCursor;

    private int moves;
    private float elapsedTime;
    private int sessionRoundNumber;

    private bool roundIsRunning;
    private bool roundHasEnded;
    private bool isResolvingMove;

    private Coroutine endRoundCoroutine;
    private Coroutine mergeResolutionCoroutine;

    public bool CanInteract =>
        roundIsRunning &&
        !roundHasEnded &&
        !isResolvingMove;

    public int SessionRoundNumber => sessionRoundNumber;

    public string CurrentPhraseId =>
        currentPhrase != null ? currentPhrase.Id : string.Empty;

    public int LoadedPhraseCount => phraseDatabase.Count;

    private IEnumerator Start()
    {
        if (!startAutomatically)
        {
            yield break;
        }

        yield return null;
        Canvas.ForceUpdateCanvases();

        if (!EnsureDatabaseLoaded())
        {
            yield break;
        }

        NextRound();
    }

    private void Update()
    {
        if (
            !roundIsRunning ||
            roundHasEnded
        )
        {
            return;
        }

        elapsedTime += Time.deltaTime;

        if (hudController != null)
        {
            hudController.SetTime(elapsedTime);
        }
    }

    [ContextMenu("Start Current Round")]
    public void StartRound()
    {
        if (!EnsureDatabaseLoaded())
        {
            return;
        }

        if (currentPhrase == null)
        {
            if (!TrySelectNextPhrase())
            {
                return;
            }

            sessionRoundNumber++;
        }

        StartCurrentPhrase();
    }

    public void RestartRound()
    {
        RestartCurrentPhrase();
    }

    public void RestartCurrentPhrase()
    {
        if (!EnsureDatabaseLoaded())
        {
            return;
        }

        if (currentPhrase == null)
        {
            NextRound();
            return;
        }

        StartCurrentPhrase();
    }

    public void NextRound()
    {
        if (!EnsureDatabaseLoaded())
        {
            return;
        }

        if (!TrySelectNextPhrase())
        {
            Debug.LogError("No se pudo seleccionar una nueva frase.");
            return;
        }

        sessionRoundNumber++;
        StartCurrentPhrase();
    }

    [ContextMenu("Reload CSV Database")]
    public void ReloadDatabase()
    {
        ClearCurrentRound();

        phraseDatabase.Clear();
        phraseOrder.Clear();

        currentPhrase = null;
        currentPhraseDatabaseIndex = -1;
        phraseOrderCursor = 0;
        sessionRoundNumber = 0;

        if (EnsureDatabaseLoaded())
        {
            NextRound();
        }
    }

    public void HandlePieceDropped(PhrasePiece draggedPiece)
    {
        if (!CanInteract || draggedPiece == null)
        {
            return;
        }

        moves++;

        /*
         * Primero buscamos una conexión válida. Así, si la pieza
         * toca más de un fragmento, priorizamos cualquier opción
         * válida por encima de una superposición incorrecta.
         */
        PhrasePiece validMergeTarget =
            FindBestValidMergeTarget(draggedPiece);

        if (validMergeTarget != null)
        {
            MergePieces(
                draggedPiece,
                validMergeTarget
            );

            return;
        }

        /*
         * No hubo una conexión válida. Buscamos la pieza con la
         * mayor superposición para aplicarle el shake.
         */
        PhrasePiece attemptedTarget =
            FindBestOverlapTarget(draggedPiece);

        draggedPiece.ReturnToDragOrigin();

        if (attemptedTarget != null)
        {
            attemptedTarget.PlayShakeAnimation();
        }

        UpdateHUD();
    }

    private bool EnsureDatabaseLoaded()
    {
        if (phraseDatabase.Count > 0)
        {
            return true;
        }

        List<PhraseDefinition> loadedPhrases =
            PhraseCsvLoader.LoadFromTextAsset(phrasesCsv);

        if (loadedPhrases.Count == 0)
        {
            Debug.LogError(
                "No se cargó ninguna frase válida desde el archivo CSV."
            );

            return false;
        }

        phraseDatabase.AddRange(loadedPhrases);
        RebuildPhraseOrder();

        return true;
    }

    private void RebuildPhraseOrder()
    {
        phraseOrder.Clear();

        for (int i = 0; i < phraseDatabase.Count; i++)
        {
            phraseOrder.Add(i);
        }

        if (randomizePhraseOrder)
        {
            Shuffle(phraseOrder);

            if (
                phraseOrder.Count > 1 &&
                currentPhraseDatabaseIndex >= 0 &&
                phraseOrder[0] == currentPhraseDatabaseIndex
            )
            {
                int temporaryIndex = phraseOrder[0];
                phraseOrder[0] = phraseOrder[1];
                phraseOrder[1] = temporaryIndex;
            }
        }

        phraseOrderCursor = 0;
    }

    private bool TrySelectNextPhrase()
    {
        if (phraseDatabase.Count == 0)
        {
            return false;
        }

        int selectedIndex;

        if (avoidRepeatsUntilExhausted)
        {
            if (
                phraseOrder.Count != phraseDatabase.Count ||
                phraseOrderCursor >= phraseOrder.Count
            )
            {
                RebuildPhraseOrder();
            }

            selectedIndex = phraseOrder[phraseOrderCursor];
            phraseOrderCursor++;
        }
        else if (randomizePhraseOrder)
        {
            selectedIndex = Random.Range(0, phraseDatabase.Count);

            if (
                phraseDatabase.Count > 1 &&
                selectedIndex == currentPhraseDatabaseIndex
            )
            {
                selectedIndex =
                    (selectedIndex + 1) % phraseDatabase.Count;
            }
        }
        else
        {
            selectedIndex =
                (currentPhraseDatabaseIndex + 1) %
                phraseDatabase.Count;
        }

        currentPhraseDatabaseIndex = selectedIndex;
        currentPhrase = phraseDatabase[selectedIndex];

        Debug.Log(
            $"Frase seleccionada: {currentPhrase.Id} " +
            $"({currentPhrase.Category})."
        );

        return true;
    }

    private void StartCurrentPhrase()
    {
        ClearCurrentRound();

        if (currentPhrase == null)
        {
            Debug.LogError(
                "No hay una frase actual para iniciar."
            );

            return;
        }

        if (!currentPhrase.IsValid(out string errorMessage))
        {
            Debug.LogError(
                $"La frase actual no es válida: {errorMessage}"
            );

            return;
        }

        if (!ValidateSceneReferences())
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
                currentPhrase.Category,
                currentPhrase.FragmentCount
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

        Canvas.ForceUpdateCanvases();

        if (memoryChallengeController != null)
        {
            memoryChallengeController.BeginRound(
                activePieces,
                sessionRoundNumber
            );
        }
    }

    private bool ValidateSceneReferences()
    {
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
            rootCanvas =
                playArea.GetComponentInParent<Canvas>();
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
            GetWorldRect(
                draggedPiece.RectTransform
            );

        for (int i = 0; i < activePieces.Count; i++)
        {
            PhrasePiece candidate = activePieces[i];

            if (candidate == null ||
                candidate == draggedPiece)
            {
                continue;
            }

            Rect candidateRect =
                GetWorldRect(
                    candidate.RectTransform
                );

            float overlapArea =
                CalculateOverlapArea(
                    draggedRect,
                    candidateRect
                );

            if (overlapArea <= 0f)
            {
                continue;
            }

            if (!CanMerge(draggedPiece, candidate))
            {
                continue;
            }

            if (overlapArea <= bestOverlapArea)
            {
                continue;
            }

            bestOverlapArea = overlapArea;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    private PhrasePiece FindBestOverlapTarget(
        PhrasePiece draggedPiece)
    {
        PhrasePiece bestTarget = null;
        float bestOverlapArea = 0f;

        Rect draggedRect =
            GetWorldRect(
                draggedPiece.RectTransform
            );

        for (int i = 0; i < activePieces.Count; i++)
        {
            PhrasePiece candidate = activePieces[i];

            if (candidate == null ||
                candidate == draggedPiece)
            {
                continue;
            }

            Rect candidateRect =
                GetWorldRect(
                    candidate.RectTransform
                );

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

        bool resolvedHiddenMemory =
            firstPiece.IsMemoryHidden ||
            secondPiece.IsMemoryHidden;

        Vector2 mergePosition =
            secondPiece.AnchoredPosition;

        firstPiece.SetInteractable(false);
        secondPiece.SetInteractable(false);

        activePieces.Remove(firstPiece);
        activePieces.Remove(secondPiece);

        firstPiece.gameObject.SetActive(false);
        secondPiece.gameObject.SetActive(false);

        Destroy(firstPiece.gameObject);
        Destroy(secondPiece.gameObject);

        PhrasePiece mergedPiece = CreatePiece(
            newStartIndex,
            newEndIndex
        );

        mergedPiece.SetAnchoredPosition(
            mergePosition
        );

        if (memoryChallengeController != null)
        {
            memoryChallengeController.HandlePiecesMerged(
                firstPiece,
                secondPiece,
                mergedPiece
            );
        }

        if (resolvedHiddenMemory)
        {
            mergedPiece.RevealMemoryContent(false);
        }

        UpdateHUD();

        bool victoryDetected =
            CheckVictory(mergedPiece);

        /*
         * El pop se reproduce después de comprobar victoria.
         * Si es la pieza final, primero se centra y luego hace
         * el pop en el centro del tablero.
         */
        mergedPiece.PlayPopAnimation();

        if (!victoryDetected)
        {
            mergeResolutionCoroutine =
                StartCoroutine(
                    UnlockInteractionAfterPop(
                        mergedPiece
                    )
                );
        }
    }

    private IEnumerator UnlockInteractionAfterPop(
        PhrasePiece mergedPiece)
    {
        float delay = mergedPiece != null
            ? mergedPiece.PopAnimationDuration
            : 0.3f;

        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                delay
            );
        }

        isResolvingMove = false;
        mergeResolutionCoroutine = null;
    }

    private PhrasePiece CreatePiece(
        int startIndex,
        int endIndex)
    {
        string pieceText =
            PhraseTextFormatter.BuildText(
                currentPhrase.Fragments,
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

    private void PlacePieceRandomly(
        PhrasePiece piece)
    {
        if (piece == null)
        {
            return;
        }

        Vector2 fallbackPosition =
            Vector2.zero;

        for (
            int attempt = 0;
            attempt < maximumSpawnAttempts;
            attempt++
        )
        {
            Vector2 candidatePosition =
                GetRandomPositionInsidePlayArea(
                    piece
                );

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

        piece.SetAnchoredPosition(
            fallbackPosition
        );
    }

    private Vector2 GetRandomPositionInsidePlayArea(
        PhrasePiece piece)
    {
        Rect areaRect = playArea.rect;
        Rect pieceRect =
            piece.RectTransform.rect;

        float halfWidth =
            pieceRect.width * 0.5f;

        float halfHeight =
            pieceRect.height * 0.5f;

        float effectiveMargin =
            Mathf.Max(
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
                ? Random.Range(
                    minimumX,
                    maximumX
                )
                : 0f;

        float randomY =
            minimumY <= maximumY
                ? Random.Range(
                    minimumY,
                    maximumY
                )
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
            GetWorldRect(
                pieceToCheck.RectTransform
            );

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
                GetWorldRect(
                    otherPiece.RectTransform
                );

            if (pieceRect.Overlaps(otherRect))
            {
                return true;
            }
        }

        return false;
    }

    private bool CheckVictory(
        PhrasePiece finalPiece)
    {
        if (activePieces.Count != 1)
        {
            return false;
        }

        bool containsEntirePhrase =
            finalPiece.StartIndex == 0 &&
            finalPiece.EndIndex ==
            currentPhrase.FragmentCount - 1;

        if (!containsEntirePhrase)
        {
            return false;
        }

        roundHasEnded = true;
        roundIsRunning = false;
        isResolvingMove = true;

        if (memoryChallengeController != null)
        {
            memoryChallengeController.CancelCurrentChallenge();
        }

        finalPiece.SetInteractable(false);
        finalPiece.transform.SetAsLastSibling();

        if (centerFinalPhrase)
        {
            Canvas.ForceUpdateCanvases();
            finalPiece.CenterInMovementArea();
        }

        endRoundCoroutine = StartCoroutine(
            FinishRoundSequence(
                finalPiece,
                finalPiece.PopAnimationDuration
            )
        );

        return true;
    }

    private IEnumerator FinishRoundSequence(
        PhrasePiece finalPiece,
        float initialFeedbackDelay)
    {
        /*
         * Primero dejamos terminar el pop.
         */
        if (initialFeedbackDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                initialFeedbackDelay
            );
        }

        /*
         * Después mantenemos visible la frase final durante
         * el tiempo configurado en el Inspector.
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
                : currentPhrase.FullPhrase;

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

        for (
            int i = 0;
            i < currentPhrase.FragmentCount;
            i++
        )
        {
            indexes.Add(i);
        }

        return indexes;
    }

    private void Shuffle(List<int> indexes)
    {
        for (
            int i = indexes.Count - 1;
            i > 0;
            i--
        )
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

        rectTransform.GetWorldCorners(
            corners
        );

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
        float overlapWidth =
            Mathf.Max(
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

        float overlapHeight =
            Mathf.Max(
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
        if (memoryChallengeController != null)
        {
            memoryChallengeController.CancelCurrentChallenge();
        }

        if (endRoundCoroutine != null)
        {
            StopCoroutine(endRoundCoroutine);
            endRoundCoroutine = null;
        }

        if (mergeResolutionCoroutine != null)
        {
            StopCoroutine(
                mergeResolutionCoroutine
            );

            mergeResolutionCoroutine = null;
        }

        for (
            int i = 0;
            i < activePieces.Count;
            i++
        )
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