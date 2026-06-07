using UnityEngine;

/// <summary>
/// Автоматически связывает снап-фидбек и панель победы с игровыми событиями.
/// </summary>
public class PuzzleFeedbackBinder : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private SnapFeedbackPlayer snapFeedback;
    [SerializeField] private DraggablePiece[] pieces;

    [Header("Sorting")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private int backgroundSortingOrder = 0;
    [SerializeField] private int pieceSortingOrder = 10;

    private void Awake()
    {
        if (levelManager == null)
            levelManager = FindFirstObjectByType<LevelManager>();

        if (snapFeedback == null)
            snapFeedback = FindFirstObjectByType<SnapFeedbackPlayer>();

        if (pieces == null || pieces.Length == 0)
            pieces = FindObjectsByType<DraggablePiece>(FindObjectsSortMode.None);

        ApplySortingOrders();
        BindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void ApplySortingOrders()
    {
        if (backgroundRenderer != null)
            backgroundRenderer.sortingOrder = backgroundSortingOrder;

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null)
                continue;

            SpriteRenderer renderer = pieces[i].GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sortingOrder = pieceSortingOrder;
        }
    }

    private void BindEvents()
    {
        if (snapFeedback == null)
            return;

        for (int i = 0; i < pieces.Length; i++)
        {
            DraggablePiece piece = pieces[i];
            if (piece == null)
                continue;

            piece.Snapped += HandlePieceSnapped;
            piece.DragStarted += HandlePieceDragStarted;
        }
    }

    private void UnbindEvents()
    {
        if (snapFeedback == null)
            return;

        for (int i = 0; i < pieces.Length; i++)
        {
            DraggablePiece piece = pieces[i];
            if (piece == null)
                continue;

            piece.Snapped -= HandlePieceSnapped;
            piece.DragStarted -= HandlePieceDragStarted;
        }
    }

    private void HandlePieceDragStarted(DraggablePiece piece)
    {
        if (snapFeedback == null)
            return;

        snapFeedback.PlayPickupSound();
    }

    private void HandlePieceSnapped(DraggablePiece piece)
    {
        if (snapFeedback == null || piece == null)
            return;

        Vector3 feedbackPosition = piece.TargetSlot != null
            ? piece.TargetSlot.SnapPosition
            : piece.transform.position;

        snapFeedback.PlayAtPosition(feedbackPosition);
    }
}
