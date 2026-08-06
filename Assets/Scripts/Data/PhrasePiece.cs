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

    private RoundController roundController;
    private RectTransform movementArea;

    private Vector2 positionBeforeDrag;
    private Vector2 pointerOffset;

    private bool canDrag = true;
    private bool isDragging;
    private bool isOverlappingWhileDragging;

    private float targetAlpha = 1f;

    public int StartIndex { get; private set; }
    public int EndIndex { get; private set; }
    public string PieceText { get; private set; }

    public RectTransform RectTransform => rectTransform;
    public Vector2 AnchoredPosition => rectTransform.anchoredPosition;
    public float MovementPadding => movementPadding;
    public bool IsDragging => isDragging;

    private void Awake()
    {
        FindMissingReferences();
        ApplyAlphaImmediately(1f);
    }

    private void Update()
    {
        UpdateVisualAlpha();
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

        isDragging = false;
        isOverlappingWhileDragging = false;

        ResizeToText();
        SnapInsideMovementArea();

        RefreshTargetAlpha();
        ApplyAlphaImmediately(targetAlpha);
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

        Vector2 singleLinePreferredSize =
            label.GetPreferredValues(PieceText);

        float desiredWidth =
            singleLinePreferredSize.x + horizontalPadding;

        float finalWidth = Mathf.Clamp(
            desiredWidth,
            minWidth,
            maxWidth
        );

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
            wrappedPreferredSize.y + verticalPadding;

        float finalHeight = Mathf.Clamp(
            desiredHeight,
            minHeight,
            maxHeight
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

        isDragging = true;
        isOverlappingWhileDragging = false;

        positionBeforeDrag =
            rectTransform.anchoredPosition;

        /*
         * La pieza arrastrada se dibuja delante.
         * Después bajará su opacidad al superponerse,
         * permitiendo ver claramente la pieza inferior.
         */
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

    /// <summary>
    /// El controlador de superposición llama a este método
    /// para indicar si la pieza arrastrada está sobre otra.
    /// </summary>
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