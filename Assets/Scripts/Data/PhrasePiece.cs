using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class PhrasePiece : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Referencias")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Tamaño")]
    [SerializeField, Min(0f)] private float horizontalPadding = 60f;
    [SerializeField, Min(0f)] private float verticalPadding = 30f;

    [SerializeField, Min(1f)] private float minWidth = 150f;
    [SerializeField, Min(1f)] private float maxWidth = 700f;

    [SerializeField, Min(1f)] private float minHeight = 100f;
    [SerializeField, Min(1f)] private float maxHeight = 260f;

    [Header("Límites de movimiento")]
    [Tooltip("Separación mínima entre la pieza y los bordes del PlayArea.")]
    [SerializeField, Min(0f)] private float movementPadding = 40f;

    [Header("Opacidad durante el arrastre")]
    [Tooltip(
        "Opacidad aplicada a la pieza arrastrada cuando " +
        "se superpone con otra pieza."
    )]
    [SerializeField, Range(0.05f, 1f)]
    private float overlappingDragAlpha = 0.35f;

    [Tooltip(
        "Opacidad de la pieza mientras se arrastra, " +
        "pero todavía no está sobre otra pieza."
    )]
    [SerializeField, Range(0.05f, 1f)]
    private float normalDragAlpha = 1f;

    [Tooltip(
        "Velocidad de transición de la opacidad. " +
        "Usa 0 para un cambio instantáneo."
    )]
    [SerializeField, Min(0f)]
    private float alphaTransitionSpeed = 10f;

    [Header("Animación de conexión correcta")]
    [Tooltip("Escala máxima alcanzada durante el pop.")]
    [SerializeField, Min(1f)]
    private float popScaleMultiplier = 1.12f;

    [Tooltip("Tiempo que tarda la pieza en agrandarse.")]
    [SerializeField, Min(0.01f)]
    private float popGrowDuration = 0.10f;

    [Tooltip("Tiempo que tarda la pieza en volver a su escala normal.")]
    [SerializeField, Min(0.01f)]
    private float popReturnDuration = 0.16f;

    [Header("Animación de conexión incorrecta")]
    [Tooltip("Distancia horizontal máxima del shake.")]
    [SerializeField, Min(0f)]
    private float shakeDistance = 16f;

    [Tooltip("Duración total del shake.")]
    [SerializeField, Min(0.01f)]
    private float shakeDuration = 0.26f;

    [Tooltip("Cantidad aproximada de oscilaciones por segundo.")]
    [SerializeField, Min(1f)]
    private float shakeFrequency = 22f;

    [Header("Desafío de memoria")]
    [Tooltip("Texto mostrado cuando el contenido de la pieza está oculto.")]
    [SerializeField]
    private string hiddenMemoryPlaceholder = "•••";

    [Tooltip("Duración total del fundido al ocultar o revelar el texto.")]
    [SerializeField, Min(0f)]
    private float memoryTextTransitionDuration = 0.24f;

    [Header("Advertencia antes de ocultarse")]
    [Tooltip(
        "Elemento visual que vibrará antes de ocultarse. " +
        "Si queda vacío, se utilizará el texto de la pieza."
    )]
    [SerializeField]
    private RectTransform memoryWarningTarget;

    [Tooltip("Rotación máxima de la vibración, en grados.")]
    [SerializeField, Min(0f)]
    private float memoryWarningRotation = 1.6f;

    [Tooltip("Velocidad de la vibración.")]
    [SerializeField, Min(1f)]
    private float memoryWarningFrequency = 12f;

    [Tooltip("Escala máxima alcanzada durante el pequeño pulso.")]
    [SerializeField, Min(1f)]
    private float memoryWarningScaleMultiplier = 1.025f;

    private RoundController roundController;
    private RectTransform movementArea;

    private Vector2 positionBeforeDrag;
    private Vector2 pointerOffset;
    private Vector3 restingScale = Vector3.one;

    private bool canDrag = true;
    private bool isDragging;
    private bool isOverlappingWhileDragging;

    private float targetAlpha = 1f;

    private Coroutine popCoroutine;
    private Coroutine shakeCoroutine;
    private Vector2 shakeBasePosition;
    private bool hasShakeBasePosition;

    private Coroutine memoryTextCoroutine;
    private Color visibleLabelColor = Color.white;
    private bool isMemoryHidden;

    private Coroutine memoryWarningCoroutine;
    private Vector3 memoryWarningBaseScale = Vector3.one;
    private Quaternion memoryWarningBaseRotation = Quaternion.identity;

    public int StartIndex { get; private set; }
    public int EndIndex { get; private set; }
    public string PieceText { get; private set; }

    public RectTransform RectTransform => rectTransform;
    public Vector2 AnchoredPosition => rectTransform.anchoredPosition;
    public float MovementPadding => movementPadding;
    public bool IsDragging => isDragging;
    public bool IsMemoryHidden => isMemoryHidden;
    public bool IsMemoryWarningActive =>
        memoryWarningCoroutine != null;

    public float MemoryTextTransitionDuration =>
        memoryTextTransitionDuration;

    public float PopAnimationDuration =>
        popGrowDuration + popReturnDuration;

    private void Awake()
    {
        FindMissingReferences();

        if (rectTransform != null)
        {
            restingScale = rectTransform.localScale;
        }

        ApplyAlphaImmediately(1f);
    }

    private void Update()
    {
        UpdateVisualAlpha();
    }

    private void OnDisable()
    {
        StopAllFeedbackAnimations(false);
        StopMemoryTextAnimation(false);
        StopMemoryWarningAnimation(false);
    }

    public void Initialize(
        RoundController controller,
        Canvas canvas,
        RectTransform area,
        string text,
        int startIndex,
        int endIndex)
    {
        roundController = controller;
        movementArea = area;

        StartIndex = startIndex;
        EndIndex = endIndex;
        PieceText = text;

        FindMissingReferences();
        ConfigureRectTransform();

        if (rectTransform != null)
        {
            restingScale = rectTransform.localScale;
        }

        if (label == null)
        {
            Debug.LogError(
                $"La pieza {name} no tiene asignado un TMP_Text."
            );

            return;
        }

        label.text = PieceText;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;

        visibleLabelColor = label.color;
        isMemoryHidden = false;

        ConfigureMemoryWarningTarget();
        StopMemoryWarningAnimation(true);

        isDragging = false;
        isOverlappingWhileDragging = false;

        StopMemoryTextAnimation(false);
        StopAllFeedbackAnimations(true);
        ResizeToText();
        SnapInsideMovementArea();

        RefreshTargetAlpha();
        ApplyAlphaImmediately(targetAlpha);
        RestoreVisibleMemoryTextImmediately();
    }

    private void FindMissingReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void ConfigureRectTransform()
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void ResizeToText()
    {
        if (label == null || rectTransform == null)
        {
            return;
        }

        /*
         * Calculamos cuánto espacio real tiene disponible
         * la pieza dentro del área de juego.
         *
         * Esto evita que una frase fusionada pueda llegar
         * a ser más ancha que el propio PlayArea.
         */
        float maximumAllowedWidth = maxWidth;
        float maximumAllowedHeight = maxHeight;

        if (movementArea != null)
        {
            float availableAreaWidth =
                movementArea.rect.width -
                movementPadding * 2f;

            float availableAreaHeight =
                movementArea.rect.height -
                movementPadding * 2f;

            maximumAllowedWidth = Mathf.Min(
                maxWidth,
                Mathf.Max(1f, availableAreaWidth)
            );

            maximumAllowedHeight = Mathf.Min(
                maxHeight,
                Mathf.Max(1f, availableAreaHeight)
            );
        }

        /*
         * Si por alguna resolución extremadamente pequeña
         * el espacio disponible fuera menor que minWidth,
         * permitimos reducir la pieza por debajo del mínimo
         * antes que dejarla salir del tablero.
         */
        float effectiveMinWidth =
            Mathf.Min(minWidth, maximumAllowedWidth);

        float effectiveMinHeight =
            Mathf.Min(minHeight, maximumAllowedHeight);

        /*
         * Primero intentamos mostrar el texto en una sola línea.
         */
        Vector2 singleLinePreferredSize =
            label.GetPreferredValues(PieceText);

        float desiredWidth =
            singleLinePreferredSize.x +
            horizontalPadding;

        float finalWidth = Mathf.Clamp(
            desiredWidth,
            effectiveMinWidth,
            maximumAllowedWidth
        );

        /*
         * Si no entra horizontalmente, TMP calcula cuánto
         * alto necesita al hacer wrapping.
         */
        float availableTextWidth = Mathf.Max(
            1f,
            finalWidth - horizontalPadding
        );

        Vector2 wrappedPreferredSize =
            label.GetPreferredValues(
                PieceText,
                availableTextWidth,
                0f
            );

        float desiredHeight =
            wrappedPreferredSize.y +
            verticalPadding;

        float finalHeight = Mathf.Clamp(
            desiredHeight,
            effectiveMinHeight,
            maximumAllowedHeight
        );

        rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            finalWidth
        );

        rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            finalHeight
        );
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!canDrag ||
            movementArea == null ||
            roundController == null ||
            !roundController.CanInteract)
        {
            return;
        }

        /*
         * Si la pieza estaba ejecutando un shake anterior,
         * restauramos su posición antes de comenzar a arrastrarla.
         */
        StopShakeAnimation(true);

        isDragging = true;
        isOverlappingWhileDragging = false;

        positionBeforeDrag =
            rectTransform.anchoredPosition;

        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        RefreshTargetAlpha();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                movementArea,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
        {
            pointerOffset =
                rectTransform.anchoredPosition -
                localPointerPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!canDrag ||
            !isDragging ||
            movementArea == null ||
            roundController == null ||
            !roundController.CanInteract)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                movementArea,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
        {
            Vector2 desiredPosition =
                localPointerPosition + pointerOffset;

            rectTransform.anchoredPosition =
                GetClampedPosition(desiredPosition);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        isOverlappingWhileDragging = false;

        canvasGroup.blocksRaycasts = canDrag;
        RefreshTargetAlpha();

        if (!canDrag ||
            roundController == null ||
            !roundController.CanInteract)
        {
            return;
        }

        roundController.HandlePieceDropped(this);
    }

    public void ReturnToDragOrigin()
    {
        SetAnchoredPosition(positionBeforeDrag);
    }

    public void SetAnchoredPosition(Vector2 position)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition =
            GetClampedPosition(position);
    }

    public void CenterInMovementArea()
    {
        SetAnchoredPosition(Vector2.zero);
    }

    public void SetInteractable(bool interactable)
    {
        canDrag = interactable;
        isDragging = false;
        isOverlappingWhileDragging = false;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }

        RefreshTargetAlpha();
    }

    public void SetDraggingOverlapState(bool isOverlapping)
    {
        if (!isDragging)
        {
            isOverlapping = false;
        }

        if (isOverlappingWhileDragging == isOverlapping)
        {
            return;
        }

        isOverlappingWhileDragging = isOverlapping;
        RefreshTargetAlpha();
    }

    /// <summary>
    /// Reproduce un pop breve en la nueva pieza resultante
    /// de una conexión correcta.
    /// </summary>
    public void PlayPopAnimation()
    {
        if (!isActiveAndEnabled || rectTransform == null)
        {
            return;
        }

        StopPopAnimation(true);

        popCoroutine = StartCoroutine(
            PopAnimationRoutine()
        );
    }

    /// <summary>
    /// Reproduce un shake horizontal en la pieza de destino
    /// cuando la conexión intentada es incorrecta.
    /// </summary>
    public void PlayShakeAnimation()
    {
        if (!isActiveAndEnabled || rectTransform == null)
        {
            return;
        }

        StopShakeAnimation(true);

        shakeCoroutine = StartCoroutine(
            ShakeAnimationRoutine()
        );
    }

    /// <summary>
    /// Indica si el texto de esta pieza es suficientemente significativo
    /// para utilizarlo como fragmento de memoria.
    /// </summary>
    public bool IsEligibleForMemoryChallenge(
        int minimumReadableCharacters)
    {
        if (string.IsNullOrWhiteSpace(PieceText))
        {
            return false;
        }

        int readableCharacters = 0;

        for (int i = 0; i < PieceText.Length; i++)
        {
            if (char.IsLetterOrDigit(PieceText[i]))
            {
                readableCharacters++;
            }
        }

        return readableCharacters >=
               Mathf.Max(1, minimumReadableCharacters);
    }

    /// <summary>
    /// Inicia una vibración sutil que advierte que el contenido
    /// de esta pieza está a punto de desaparecer.
    /// </summary>
    public void StartMemoryWarningAnimation()
    {
        if (
            !isActiveAndEnabled ||
            label == null ||
            isMemoryHidden
        )
        {
            return;
        }

        ConfigureMemoryWarningTarget();
        StopMemoryWarningAnimation(true);

        if (memoryWarningTarget == null)
        {
            return;
        }

        memoryWarningCoroutine = StartCoroutine(
            MemoryWarningRoutine()
        );
    }

    /// <summary>
    /// Detiene la advertencia y restaura el texto a su
    /// rotación y escala originales.
    /// </summary>
    public void StopMemoryWarningAnimation(
        bool restoreTransform = true)
    {
        if (memoryWarningCoroutine != null)
        {
            StopCoroutine(memoryWarningCoroutine);
            memoryWarningCoroutine = null;
        }

        if (
            restoreTransform &&
            memoryWarningTarget != null
        )
        {
            memoryWarningTarget.localScale =
                memoryWarningBaseScale;

            memoryWarningTarget.localRotation =
                memoryWarningBaseRotation;
        }
    }

    /// <summary>
    /// Oculta el contenido visible sin cambiar el tamaño,
    /// la posición ni el texto interno usado por el gameplay.
    /// </summary>
    public void HideMemoryContent(bool animated = true)
    {
        if (label == null || isMemoryHidden)
        {
            return;
        }

        StopMemoryWarningAnimation(true);
        isMemoryHidden = true;

        StartMemoryTextTransition(
            string.IsNullOrWhiteSpace(hiddenMemoryPlaceholder)
                ? "•••"
                : hiddenMemoryPlaceholder,
            animated
        );
    }

    /// <summary>
    /// Vuelve a mostrar el texto real de la pieza.
    /// </summary>
    public void RevealMemoryContent(bool animated = true)
    {
        if (label == null)
        {
            return;
        }

        StopMemoryWarningAnimation(true);
        isMemoryHidden = false;

        StartMemoryTextTransition(
            PieceText,
            animated
        );
    }

    public void SnapInsideMovementArea()
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition =
            GetClampedPosition(
                rectTransform.anchoredPosition
            );
    }

    private IEnumerator PopAnimationRoutine()
    {
        Vector3 enlargedScale =
            restingScale * popScaleMultiplier;

        float elapsed = 0f;

        while (elapsed < popGrowDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / popGrowDuration
            );

            /*
             * Curva suave con salida rápida para que el pop
             * se sienta ágil y no pesado.
             */
            float easedProgress =
                1f - Mathf.Pow(1f - progress, 3f);

            rectTransform.localScale = Vector3.LerpUnclamped(
                restingScale,
                enlargedScale,
                easedProgress
            );

            yield return null;
        }

        rectTransform.localScale = enlargedScale;
        elapsed = 0f;

        while (elapsed < popReturnDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / popReturnDuration
            );

            float easedProgress =
                progress * progress * (3f - 2f * progress);

            rectTransform.localScale = Vector3.LerpUnclamped(
                enlargedScale,
                restingScale,
                easedProgress
            );

            yield return null;
        }

        rectTransform.localScale = restingScale;
        popCoroutine = null;
    }

    private IEnumerator ShakeAnimationRoutine()
    {
        shakeBasePosition =
            rectTransform.anchoredPosition;

        hasShakeBasePosition = true;

        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / shakeDuration
            );

            float damping = 1f - progress;

            float horizontalOffset =
                Mathf.Sin(
                    elapsed *
                    shakeFrequency *
                    Mathf.PI * 2f
                ) *
                shakeDistance *
                damping;

            rectTransform.anchoredPosition =
                shakeBasePosition +
                Vector2.right * horizontalOffset;

            yield return null;
        }

        rectTransform.anchoredPosition =
            shakeBasePosition;

        hasShakeBasePosition = false;
        shakeCoroutine = null;
    }

    private void StopAllFeedbackAnimations(bool restoreVisuals)
    {
        StopPopAnimation(restoreVisuals);
        StopShakeAnimation(restoreVisuals);
    }

    private void StopPopAnimation(bool restoreScale)
    {
        if (popCoroutine != null)
        {
            StopCoroutine(popCoroutine);
            popCoroutine = null;
        }

        if (restoreScale && rectTransform != null)
        {
            rectTransform.localScale = restingScale;
        }
    }

    private void StopShakeAnimation(bool restorePosition)
    {
        if (shakeCoroutine == null)
        {
            return;
        }

        StopCoroutine(shakeCoroutine);
        shakeCoroutine = null;

        if (
            restorePosition &&
            rectTransform != null &&
            hasShakeBasePosition
        )
        {
            rectTransform.anchoredPosition =
                shakeBasePosition;
        }

        hasShakeBasePosition = false;
    }

    private void ConfigureMemoryWarningTarget()
    {
        if (
            memoryWarningTarget == null &&
            label != null
        )
        {
            memoryWarningTarget =
                label.rectTransform;
        }

        if (memoryWarningTarget == null)
        {
            return;
        }

        memoryWarningBaseScale =
            memoryWarningTarget.localScale;

        memoryWarningBaseRotation =
            memoryWarningTarget.localRotation;
    }

    private IEnumerator MemoryWarningRoutine()
    {
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            float wave = Mathf.Sin(
                elapsed *
                memoryWarningFrequency *
                Mathf.PI * 2f
            );

            float secondaryWave = Mathf.Sin(
                elapsed *
                memoryWarningFrequency *
                Mathf.PI
            );

            float rotation =
                wave * memoryWarningRotation;

            float pulseProgress =
                (secondaryWave + 1f) * 0.5f;

            float scaleMultiplier = Mathf.Lerp(
                1f,
                memoryWarningScaleMultiplier,
                pulseProgress
            );

            memoryWarningTarget.localRotation =
                memoryWarningBaseRotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    rotation
                );

            memoryWarningTarget.localScale =
                memoryWarningBaseScale *
                scaleMultiplier;

            yield return null;
        }
    }

    private void StartMemoryTextTransition(
        string targetText,
        bool animated)
    {
        StopMemoryTextAnimation(false);

        if (
            !animated ||
            memoryTextTransitionDuration <= 0f ||
            !isActiveAndEnabled
        )
        {
            label.text = targetText;
            SetLabelAlpha(visibleLabelColor.a);
            return;
        }

        memoryTextCoroutine = StartCoroutine(
            MemoryTextTransitionRoutine(targetText)
        );
    }

    private IEnumerator MemoryTextTransitionRoutine(
        string targetText)
    {
        float halfDuration =
            Mathf.Max(0.01f, memoryTextTransitionDuration * 0.5f);

        float startingAlpha = label.color.a;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / halfDuration
            );

            SetLabelAlpha(
                Mathf.Lerp(
                    startingAlpha,
                    0f,
                    progress
                )
            );

            yield return null;
        }

        label.text = targetText;
        SetLabelAlpha(0f);

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / halfDuration
            );

            SetLabelAlpha(
                Mathf.Lerp(
                    0f,
                    visibleLabelColor.a,
                    progress
                )
            );

            yield return null;
        }

        SetLabelAlpha(visibleLabelColor.a);
        memoryTextCoroutine = null;
    }

    private void StopMemoryTextAnimation(
        bool restoreVisibleAlpha)
    {
        if (memoryTextCoroutine != null)
        {
            StopCoroutine(memoryTextCoroutine);
            memoryTextCoroutine = null;
        }

        if (restoreVisibleAlpha && label != null)
        {
            SetLabelAlpha(visibleLabelColor.a);
        }
    }

    private void RestoreVisibleMemoryTextImmediately()
    {
        if (label == null)
        {
            return;
        }

        isMemoryHidden = false;
        label.text = PieceText;
        label.color = visibleLabelColor;
    }

    private void SetLabelAlpha(float alpha)
    {
        if (label == null)
        {
            return;
        }

        Color currentColor = label.color;
        currentColor.a = Mathf.Clamp01(alpha);
        label.color = currentColor;
    }

    private void RefreshTargetAlpha()
    {
        if (!isDragging)
        {
            targetAlpha = 1f;
            return;
        }

        targetAlpha = isOverlappingWhileDragging
            ? overlappingDragAlpha
            : normalDragAlpha;
    }

    private void UpdateVisualAlpha()
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (alphaTransitionSpeed <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            return;
        }

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            targetAlpha,
            alphaTransitionSpeed * Time.unscaledDeltaTime
        );
    }

    private void ApplyAlphaImmediately(float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private Vector2 GetClampedPosition(Vector2 desiredPosition)
    {
        if (movementArea == null || rectTransform == null)
        {
            return desiredPosition;
        }

        Rect areaRect = movementArea.rect;
        Rect pieceRect = rectTransform.rect;

        float safePadding =
            Mathf.Max(0f, movementPadding);

        float halfWidth =
            pieceRect.width * 0.5f;

        float halfHeight =
            pieceRect.height * 0.5f;

        float minimumX =
            areaRect.xMin +
            halfWidth +
            safePadding;

        float maximumX =
            areaRect.xMax -
            halfWidth -
            safePadding;

        float minimumY =
            areaRect.yMin +
            halfHeight +
            safePadding;

        float maximumY =
            areaRect.yMax -
            halfHeight -
            safePadding;

        Vector2 clampedPosition = desiredPosition;

        if (minimumX <= maximumX)
        {
            clampedPosition.x = Mathf.Clamp(
                desiredPosition.x,
                minimumX,
                maximumX
            );
        }
        else
        {
            clampedPosition.x = 0f;
        }

        if (minimumY <= maximumY)
        {
            clampedPosition.y = Mathf.Clamp(
                desiredPosition.y,
                minimumY,
                maximumY
            );
        }
        else
        {
            clampedPosition.y = 0f;
        }

        return clampedPosition;
    }
}