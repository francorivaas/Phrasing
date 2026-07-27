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

    private RoundController roundController;
    private RectTransform movementArea;

    private Vector2 positionBeforeDrag;
    private Vector2 pointerOffset;

    private bool canDrag = true;
    private bool isDragging;

    public int StartIndex { get; private set; }
    public int EndIndex { get; private set; }
    public string PieceText { get; private set; }

    public RectTransform RectTransform => rectTransform;
    public Vector2 AnchoredPosition => rectTransform.anchoredPosition;
    public float MovementPadding => movementPadding;

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
                $"La pieza {name} no tiene asignado un componente TMP_Text."
            );

            return;
        }

        label.text = PieceText;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;

        ResizeToText();
        SnapInsideMovementArea();
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

        /*
         * Todas las piezas utilizan el centro del PlayArea
         * como origen de coordenadas.
         */
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
        positionBeforeDrag = rectTransform.anchoredPosition;

        /*
         * Coloca la pieza delante de las demás mientras se arrastra.
         */
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

        canvasGroup.blocksRaycasts = canDrag;
        canvasGroup.alpha = 1f;

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

        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
        canvasGroup.alpha = 1f;
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

    private Vector2 GetClampedPosition(Vector2 desiredPosition)
    {
        if (movementArea == null || rectTransform == null)
        {
            return desiredPosition;
        }

        Rect areaRect = movementArea.rect;
        Rect pieceRect = rectTransform.rect;

        float safePadding = Mathf.Max(
            0f,
            movementPadding
        );

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

        /*
         * Si una pieza es demasiado ancha para respetar el margen,
         * se mantiene centrada horizontalmente.
         */
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

        /*
         * Si una pieza es demasiado alta para respetar el margen,
         * se mantiene centrada verticalmente.
         */
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