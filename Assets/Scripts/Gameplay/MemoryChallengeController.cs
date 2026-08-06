using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MemoryChallengeController : MonoBehaviour
{
    [Header("Activación y progresión")]
    [Tooltip("Número de ronda de la sesión en el que aparece por primera vez.")]
    [SerializeField, Min(1)]
    private int startAtRoundNumber = 5;

    [Tooltip(
        "Cada esta cantidad de rondas se agrega otra pieza oculta. " +
        "Usa 0 para mantener siempre la misma cantidad."
    )]
    [SerializeField, Min(0)]
    private int addHiddenPieceEveryRounds = 7;

    [SerializeField, Min(1)]
    private int maximumHiddenPieces = 3;

    [Tooltip("Porcentaje máximo de las piezas que se puede ocultar.")]
    [SerializeField, Range(0.1f, 1f)]
    private float maximumHiddenFraction = 0.4f;

    [Header("Selección de fragmentos")]
    [Tooltip(
        "Cantidad mínima de letras o números que debe contener " +
        "un fragmento para poder ocultarse."
    )]
    [SerializeField, Min(1)]
    private int minimumReadableCharacters = 4;

    [Tooltip(
        "Evita ocultar dos fragmentos consecutivos mientras " +
        "haya suficientes candidatos disponibles."
    )]
    [SerializeField]
    private bool avoidAdjacentHiddenPieces = true;

    [Tooltip(
        "Evita elegir piezas cuyo texto ya esté tapado " +
        "por otra pieza al comenzar la ronda."
    )]
    [SerializeField]
    private bool excludeInitiallyOverlappedPieces = true;

    [SerializeField, Min(0f)]
    private float minimumOverlapAreaToExclude = 25f;

    [Header("Tiempo antes de ocultarse")]
    [Tooltip(
        "Tiempo durante el cual las piezas seleccionadas permanecen " +
        "visibles y vibrando. El jugador puede seguir jugando."
    )]
    [SerializeField, Min(0.1f)]
    private float initialExposureDuration = 3f;

    [Tooltip(
        "Reducción del tiempo visible por cada ronda posterior " +
        "a la primera ronda con memoria."
    )]
    [SerializeField, Min(0f)]
    private float exposureReductionPerRound = 0.05f;

    [SerializeField, Min(0.1f)]
    private float minimumExposureDuration = 1.8f;

    [Header("Mensaje opcional")]
    [SerializeField] private TMP_Text statusText;

    [SerializeField]
    private string warningMessage =
        "¡Estas piezas van a desaparecer!";

    [SerializeField]
    private string hiddenMessage =
        "¡Ahora recuérdalas!";

    [SerializeField, Min(0f)]
    private float hiddenMessageDuration = 0.8f;

    private readonly List<PhrasePiece> selectedPieces =
        new List<PhrasePiece>();

    private Coroutine challengeCoroutine;
    private bool isWarningPhase;

    /*
     * Se conserva esta propiedad para mantener compatibilidad
     * con versiones anteriores de RoundController. En esta versión
     * nunca bloqueamos el input.
     */
    public bool IsBlockingInput => false;

    public bool IsChallengeActive { get; private set; }
    public bool IsWarningPhase => isWarningPhase;
    public int CurrentHiddenPieceCount => selectedPieces.Count;

    private void Awake()
    {
        HideStatusText();
    }

    private void OnDisable()
    {
        CancelCurrentChallenge();
    }

    public void BeginRound(
        IReadOnlyList<PhrasePiece> roundPieces,
        int roundNumber)
    {
        CancelCurrentChallenge();

        if (
            roundPieces == null ||
            roundPieces.Count == 0 ||
            roundNumber < startAtRoundNumber
        )
        {
            return;
        }

        List<PhrasePiece> candidates =
            BuildEligibleCandidateList(roundPieces);

        int hiddenPieceCount =
            CalculateHiddenPieceCount(
                roundPieces.Count,
                candidates.Count,
                roundNumber
            );

        if (hiddenPieceCount <= 0)
        {
            return;
        }

        SelectPieces(
            candidates,
            hiddenPieceCount
        );

        if (selectedPieces.Count == 0)
        {
            return;
        }

        float exposureDuration =
            CalculateExposureDuration(roundNumber);

        IsChallengeActive = true;
        isWarningPhase = true;

        challengeCoroutine = StartCoroutine(
            RunChallengeSequence(exposureDuration)
        );
    }

    /// <summary>
    /// Mantiene el desafío si una pieza marcada se fusiona antes
    /// de desaparecer. La nueva pieza hereda la advertencia.
    /// </summary>
    public void HandlePiecesMerged(
        PhrasePiece firstPiece,
        PhrasePiece secondPiece,
        PhrasePiece mergedPiece)
    {
        bool firstWasSelected =
            RemoveSelectedPiece(firstPiece);

        bool secondWasSelected =
            RemoveSelectedPiece(secondPiece);

        if (!firstWasSelected && !secondWasSelected)
        {
            return;
        }

        if (
            isWarningPhase &&
            mergedPiece != null
        )
        {
            if (!selectedPieces.Contains(mergedPiece))
            {
                selectedPieces.Add(mergedPiece);
            }

            mergedPiece.StartMemoryWarningAnimation();
        }

        if (
            !isWarningPhase &&
            selectedPieces.Count == 0
        )
        {
            IsChallengeActive = false;
        }
    }

    public void CancelCurrentChallenge()
    {
        if (challengeCoroutine != null)
        {
            StopCoroutine(challengeCoroutine);
            challengeCoroutine = null;
        }

        for (int i = 0; i < selectedPieces.Count; i++)
        {
            PhrasePiece piece = selectedPieces[i];

            if (piece == null)
            {
                continue;
            }

            piece.StopMemoryWarningAnimation(true);
            piece.RevealMemoryContent(false);
        }

        selectedPieces.Clear();

        isWarningPhase = false;
        IsChallengeActive = false;

        HideStatusText();
    }

    private IEnumerator RunChallengeSequence(
        float exposureDuration)
    {
        /*
         * Esperamos un frame para garantizar que el tablero,
         * los tamaños y las posiciones ya estén estabilizados.
         * El input permanece activo.
         */
        yield return null;

        ShowStatusText(warningMessage);

        for (int i = 0; i < selectedPieces.Count; i++)
        {
            PhrasePiece piece = selectedPieces[i];

            if (piece != null)
            {
                piece.StartMemoryWarningAnimation();
            }
        }

        if (exposureDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                exposureDuration
            );
        }

        isWarningPhase = false;

        float longestTransition = 0f;

        /*
         * La lista puede haber cambiado durante el tiempo de
         * exposición porque las piezas pueden fusionarse.
         */
        for (int i = selectedPieces.Count - 1; i >= 0; i--)
        {
            PhrasePiece piece = selectedPieces[i];

            if (piece == null)
            {
                selectedPieces.RemoveAt(i);
                continue;
            }

            piece.StopMemoryWarningAnimation(true);
            piece.HideMemoryContent(true);

            longestTransition = Mathf.Max(
                longestTransition,
                piece.MemoryTextTransitionDuration
            );
        }

        if (longestTransition > 0f)
        {
            yield return new WaitForSecondsRealtime(
                longestTransition
            );
        }

        ShowStatusText(hiddenMessage);

        if (hiddenMessageDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                hiddenMessageDuration
            );
        }

        HideStatusText();

        challengeCoroutine = null;

        if (selectedPieces.Count == 0)
        {
            IsChallengeActive = false;
        }
    }

    private bool RemoveSelectedPiece(
        PhrasePiece piece)
    {
        if (piece == null)
        {
            return false;
        }

        bool removed =
            selectedPieces.Remove(piece);

        if (removed)
        {
            piece.StopMemoryWarningAnimation(true);
        }

        return removed;
    }

    private List<PhrasePiece> BuildEligibleCandidateList(
        IReadOnlyList<PhrasePiece> roundPieces)
    {
        List<PhrasePiece> candidates =
            new List<PhrasePiece>();

        for (int i = 0; i < roundPieces.Count; i++)
        {
            PhrasePiece piece = roundPieces[i];

            if (
                piece == null ||
                !piece.IsEligibleForMemoryChallenge(
                    minimumReadableCharacters
                )
            )
            {
                continue;
            }

            if (
                excludeInitiallyOverlappedPieces &&
                IsOverlappingAnyOtherPiece(
                    piece,
                    roundPieces
                )
            )
            {
                continue;
            }

            candidates.Add(piece);
        }

        Shuffle(candidates);

        return candidates;
    }

    private int CalculateHiddenPieceCount(
        int totalPieceCount,
        int eligiblePieceCount,
        int roundNumber)
    {
        if (eligiblePieceCount <= 0)
        {
            return 0;
        }

        int progressionCount = 1;

        if (addHiddenPieceEveryRounds > 0)
        {
            int roundsSinceActivation =
                Mathf.Max(
                    0,
                    roundNumber - startAtRoundNumber
                );

            progressionCount +=
                roundsSinceActivation /
                addHiddenPieceEveryRounds;
        }

        int fractionLimit = Mathf.Max(
            1,
            Mathf.FloorToInt(
                totalPieceCount *
                maximumHiddenFraction
            )
        );

        return Mathf.Clamp(
            progressionCount,
            1,
            Mathf.Min(
                maximumHiddenPieces,
                fractionLimit,
                eligiblePieceCount
            )
        );
    }

    private float CalculateExposureDuration(
        int roundNumber)
    {
        int roundsSinceActivation =
            Mathf.Max(
                0,
                roundNumber - startAtRoundNumber
            );

        return Mathf.Max(
            minimumExposureDuration,
            initialExposureDuration -
            roundsSinceActivation *
            exposureReductionPerRound
        );
    }

    private void SelectPieces(
        List<PhrasePiece> candidates,
        int requestedCount)
    {
        selectedPieces.Clear();

        for (int i = 0; i < candidates.Count; i++)
        {
            PhrasePiece candidate = candidates[i];

            if (
                candidate == null ||
                selectedPieces.Count >= requestedCount
            )
            {
                break;
            }

            if (
                avoidAdjacentHiddenPieces &&
                IsAdjacentToAnySelected(candidate)
            )
            {
                continue;
            }

            selectedPieces.Add(candidate);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            PhrasePiece candidate = candidates[i];

            if (
                candidate == null ||
                selectedPieces.Count >= requestedCount
            )
            {
                break;
            }

            if (!selectedPieces.Contains(candidate))
            {
                selectedPieces.Add(candidate);
            }
        }
    }

    private bool IsAdjacentToAnySelected(
        PhrasePiece candidate)
    {
        for (int i = 0; i < selectedPieces.Count; i++)
        {
            PhrasePiece selected = selectedPieces[i];

            if (selected == null)
            {
                continue;
            }

            bool candidateImmediatelyBefore =
                candidate.EndIndex + 1 ==
                selected.StartIndex;

            bool candidateImmediatelyAfter =
                selected.EndIndex + 1 ==
                candidate.StartIndex;

            if (
                candidateImmediatelyBefore ||
                candidateImmediatelyAfter
            )
            {
                return true;
            }
        }

        return false;
    }

    private bool IsOverlappingAnyOtherPiece(
        PhrasePiece piece,
        IReadOnlyList<PhrasePiece> roundPieces)
    {
        if (
            piece == null ||
            piece.RectTransform == null
        )
        {
            return true;
        }

        Rect pieceRect =
            GetWorldRect(piece.RectTransform);

        for (int i = 0; i < roundPieces.Count; i++)
        {
            PhrasePiece other = roundPieces[i];

            if (
                other == null ||
                other == piece ||
                other.RectTransform == null
            )
            {
                continue;
            }

            float overlapArea =
                CalculateOverlapArea(
                    pieceRect,
                    GetWorldRect(
                        other.RectTransform
                    )
                );

            if (
                overlapArea >=
                minimumOverlapAreaToExclude
            )
            {
                return true;
            }
        }

        return false;
    }

    private Rect GetWorldRect(
        RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        return new Rect(
            corners[0].x,
            corners[0].y,
            corners[2].x - corners[0].x,
            corners[2].y - corners[0].y
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

    private void ShowStatusText(string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.gameObject.SetActive(true);
    }

    private void HideStatusText()
    {
        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex =
                Random.Range(0, i + 1);

            T temporaryValue = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temporaryValue;
        }
    }
}