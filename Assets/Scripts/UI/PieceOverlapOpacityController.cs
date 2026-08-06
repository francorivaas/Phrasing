using System.Collections.Generic;
using UnityEngine;

public class PieceOverlapOpacityController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Área que contiene directamente todas las PhrasePiece.")]
    [SerializeField] private RectTransform playArea;

    [Header("Detección")]
    [Tooltip(
        "Área mínima de intersección necesaria para considerar " +
        "que la pieza arrastrada está sobre otra."
    )]
    [SerializeField, Min(0f)]
    private float minimumOverlapArea = 25f;

    private readonly List<PhrasePiece> pieces =
        new List<PhrasePiece>();

    private void Awake()
    {
        if (playArea == null)
        {
            playArea = transform as RectTransform;
        }
    }

    private void LateUpdate()
    {
        RefreshOverlapOpacity();
    }

    [ContextMenu("Refresh Overlap Opacity")]
    public void RefreshOverlapOpacity()
    {
        if (playArea == null)
        {
            return;
        }

        pieces.Clear();

        playArea.GetComponentsInChildren(
            false,
            pieces
        );

        /*
         * Primero restauramos el estado de todas las piezas.
         * Las piezas que no están siendo arrastradas siempre
         * permanecen con su opacidad normal.
         */
        for (int i = 0; i < pieces.Count; i++)
        {
            PhrasePiece piece = pieces[i];

            if (piece != null)
            {
                piece.SetDraggingOverlapState(false);
            }
        }

        /*
         * Solo necesitamos comprobar la pieza que actualmente
         * está siendo arrastrada.
         */
        for (int i = 0; i < pieces.Count; i++)
        {
            PhrasePiece draggedPiece = pieces[i];

            if (!IsUsable(draggedPiece) ||
                !draggedPiece.IsDragging)
            {
                continue;
            }

            bool overlapsAnotherPiece =
                IsOverlappingAnotherPiece(draggedPiece);

            draggedPiece.SetDraggingOverlapState(
                overlapsAnotherPiece
            );
        }
    }

    private bool IsOverlappingAnotherPiece(
        PhrasePiece draggedPiece)
    {
        Rect draggedRect =
            GetWorldRect(
                draggedPiece.RectTransform
            );

        for (int i = 0; i < pieces.Count; i++)
        {
            PhrasePiece candidate = pieces[i];

            if (!IsUsable(candidate) ||
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

            if (overlapArea >= minimumOverlapArea)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsUsable(PhrasePiece piece)
    {
        return piece != null &&
               piece.gameObject.activeInHierarchy &&
               piece.RectTransform != null;
    }

    private Rect GetWorldRect(
        RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];

        rectTransform.GetWorldCorners(corners);

        float width =
            corners[2].x - corners[0].x;

        float height =
            corners[2].y - corners[0].y;

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
}