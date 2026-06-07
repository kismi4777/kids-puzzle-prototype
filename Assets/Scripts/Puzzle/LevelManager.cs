using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Отслеживает прогресс уровня и сигнализирует о победе.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [SerializeField] private DraggablePiece[] pieces;

    [Header("Events")]
    [SerializeField] private UnityEvent onLevelComplete;
    [SerializeField] private VictoryPanelController victoryPanel;
    [SerializeField] private BWMaskController bwMaskController;

    [Header("Панель победы")]
    [Tooltip("Пауза после раскрашивания фона, перед появлением VictoryPanel.")]
    [SerializeField] private float admireColorDelay = 0.5f;

    private int placedCount;
    private int totalPieces;
    private bool levelCompleted;
    private Coroutine victoryPanelRoutine;

    public bool IsLevelCompleted => levelCompleted;

    /// <summary>Возвращает случайный кусочек, который ещё не собран.</summary>
    public DraggablePiece GetRandomUnplacedPiece()
    {
        if (pieces == null || pieces.Length == 0)
            return null;

        int unplacedCount = 0;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (IsPieceAvailableForHint(pieces[i]))
                unplacedCount++;
        }

        if (unplacedCount == 0)
            return null;

        int randomIndex = UnityEngine.Random.Range(0, unplacedCount);
        int currentIndex = 0;

        for (int i = 0; i < pieces.Length; i++)
        {
            if (!IsPieceAvailableForHint(pieces[i]))
                continue;

            if (currentIndex == randomIndex)
                return pieces[i];

            currentIndex++;
        }

        return null;
    }

    private static bool IsPieceAvailableForHint(DraggablePiece piece)
    {
        return piece != null
            && piece.gameObject.activeInHierarchy
            && !piece.IsSnapped
            && piece.TargetSlot != null;
    }

    public UnityEvent OnLevelComplete => onLevelComplete;

    private void Awake()
    {
        if (pieces == null || pieces.Length == 0)
            pieces = GetComponentsInChildren<DraggablePiece>(true);

        totalPieces = pieces.Length;

        if (victoryPanel == null)
            victoryPanel = FindFirstObjectByType<VictoryPanelController>(FindObjectsInactive.Include);

        if (bwMaskController == null)
            bwMaskController = FindFirstObjectByType<BWMaskController>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null)
                pieces[i].Snapped += HandlePieceSnapped;
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null)
                pieces[i].Snapped -= HandlePieceSnapped;
        }
    }

    private void HandlePieceSnapped(DraggablePiece piece)
    {
        if (levelCompleted)
            return;

        placedCount++;

        if (piece != null && piece.TargetSlot != null)
            bwMaskController?.ExpandHole(piece.TargetSlot);

        if (placedCount >= totalPieces)
            LevelComplete();
    }

    /// <summary>
    /// Вызывается, когда все кусочки установлены на свои места.
    /// </summary>
    public void LevelComplete()
    {
        if (levelCompleted)
            return;

        levelCompleted = true;

        bwMaskController?.FadeOutEntireMask();
        onLevelComplete?.Invoke();

        if (victoryPanelRoutine != null)
            StopCoroutine(victoryPanelRoutine);

        victoryPanelRoutine = StartCoroutine(ShowVictoryPanelDelayedRoutine());
    }

    private IEnumerator ShowVictoryPanelDelayedRoutine()
    {
        float maskFadeDelay = bwMaskController != null ? bwMaskController.FadeDuration : 0f;
        float totalDelay = maskFadeDelay + admireColorDelay;

        if (totalDelay > 0f)
            yield return new WaitForSecondsRealtime(totalDelay);

        if (victoryPanel == null)
            victoryPanel = FindFirstObjectByType<VictoryPanelController>(FindObjectsInactive.Include);

        victoryPanel?.ShowVictoryPanel();
        victoryPanelRoutine = null;
    }

    /// <summary>
    /// Сбрасывает уровень для повторной игры.
    /// </summary>
    public void RestartLevel()
    {
        levelCompleted = false;
        placedCount = 0;

        if (victoryPanelRoutine != null)
        {
            StopCoroutine(victoryPanelRoutine);
            victoryPanelRoutine = null;
        }

        if (victoryPanel == null)
            victoryPanel = FindFirstObjectByType<VictoryPanelController>(FindObjectsInactive.Include);

        victoryPanel?.HideVictoryPanel();
        bwMaskController?.ResetMask();

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null)
                pieces[i].ResetPiece();
        }
    }
}
