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
    [SerializeField] private float horizontalPadding = 60f;
    [SerializeField] private float verticalPadding = 30f;
    [SerializeField] private float minWidth = 130f;
    [SerializeField] private float maxWidth = 700f;
    [SerializeField] private float minHeight = 90f;
    [SerializeField] private float maxHeight = 260f;

    private RoundController roundController;
    private RectTransform movementArea;
    private Canvas rootCanvas;

    private Vector2 positionBeforeDrag;
    private Vector2 pointerOffset;

    private bool canDrag = true;
    private bool isDragging;

    public int StartIndex { get; private set; }
    public int EndIndex { get; private set; }
    public string PieceText { get; private set; }

    public RectTransform RectTransform => rectTransform;
    public Vector2 AnchoredPosition => rectTransform.anchoredPosition;

    public void Initialize(
        RoundController controller,
        Canvas canvas,
        RectTransform area,
        string text,
        int startIndex,
        int endIndex)
    {
        roundController = controller;
        rootCanvas = canvas;
        movementArea = area;

        StartIndex = startIndex;
        EndIndex = endIndex;
        PieceText = text;

        FindMissingReferences();
        ConfigureRectTransform();

        label.text = PieceText;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;

        ResizeToText();
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

        Vector2 singleLineSize = label.GetPreferredValues(PieceText);

        float width = Mathf.Clamp(
            singleLineSize.x + horizontalPadding,
            minWidth,
            maxWidth
        );

        float availableTextWidth = Mathf.Max(
            1f,
            width - horizontalPadding
        );

        Vector2 wrappedTextSize = label.GetPreferredValues(
            PieceText,
            availableTextWidth,
            0f
        );

        float height = Mathf.Clamp(
            wrappedTextSize.y + verticalPadding,
            minHeight,
            maxHeight
        );

        rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width
        );

        rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height
        );
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!canDrag || movementArea == null)
        {
            return;
        }

        isDragging = true;
        positionBeforeDrag = rectTransform.anchoredPosition;

        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.9f;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                movementArea,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
        {
            pointerOffset =
                rectTransform.anchoredPosition - localPointerPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!canDrag || !isDragging || movementArea == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                movementArea,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
        {
            rectTransform.anchoredPosition =
                localPointerPosition + pointerOffset;

            SnapInsideMovementArea();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!canDrag || !isDragging)
        {
            return;
        }

        isDragging = false;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        roundController.HandlePieceDropped(this);
    }

    public void ReturnToDragOrigin()
    {
        rectTransform.anchoredPosition = positionBeforeDrag;
    }

    public void SetAnchoredPosition(Vector2 position)
    {
        rectTransform.anchoredPosition = position;
        SnapInsideMovementArea();
    }

    public void SetInteractable(bool interactable)
    {
        canDrag = interactable;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }
    }

    public void SnapInsideMovementArea()
    {
        if (movementArea == null || rectTransform == null)
        {
            return;
        }

        Rect areaRect = movementArea.rect;
        Rect pieceRect = rectTransform.rect;

        float halfWidth = pieceRect.width * 0.5f;
        float halfHeight = pieceRect.height * 0.5f;

        float minX = areaRect.xMin + halfWidth;
        float maxX = areaRect.xMax - halfWidth;

        float minY = areaRect.yMin + halfHeight;
        float maxY = areaRect.yMax - halfHeight;

        Vector2 position = rectTransform.anchoredPosition;

        position.x = minX <= maxX
            ? Mathf.Clamp(position.x, minX, maxX)
            : 0f;

        position.y = minY <= maxY
            ? Mathf.Clamp(position.y, minY, maxY)
            : 0f;

        rectTransform.anchoredPosition = position;
    }
}