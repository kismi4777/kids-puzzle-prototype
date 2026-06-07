using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Маркер слота пазла. Хранит точку снаппинга и состояние занятости.
/// </summary>
public class PuzzleSlot : MonoBehaviour
{
    [SerializeField] private string slotId = "slot_0";

    [Header("Hint Visual")]
    [Tooltip("Силуэт/контур слота на подложке. Если пусто — берётся первый дочерний объект.")]
    [SerializeField] private GameObject slotHintVisual;
    [SerializeField] private bool hideHintWhenOccupied = true;

    [Header("Feedback")]
    [SerializeField] private UnityEvent onPiecePlaced;

    private bool hintWasActive;

    public string SlotId => slotId;
    public Vector3 SnapPosition => transform.position;
    public bool IsOccupied { get; private set; }

    public event Action<PuzzleSlot, DraggablePiece> PiecePlaced;

    private void Awake()
    {
        ResolveHintVisual();
    }

    private void ResolveHintVisual()
    {
        if (IsValidHintVisual(slotHintVisual))
            return;

        int childCount = transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            GameObject childObject = transform.GetChild(i).gameObject;
            if (childObject.GetComponent<DraggablePiece>() != null)
                continue;

            slotHintVisual = childObject;
            return;
        }
    }

    private bool IsValidHintVisual(GameObject visual)
    {
        if (visual == null)
            return false;

        if (visual.GetComponent<DraggablePiece>() != null)
            return false;

        return visual.transform.IsChildOf(transform);
    }

    /// <summary>
    /// Резервирует слот для кусочка после успешного снаппинга.
    /// </summary>
    public bool TryOccupy(DraggablePiece piece)
    {
        if (IsOccupied)
            return false;

        IsOccupied = true;
        HideHintVisual();
        PiecePlaced?.Invoke(this, piece);
        onPiecePlaced?.Invoke();
        return true;
    }

    /// <summary>
    /// Освобождает слот (для перезапуска уровня).
    /// </summary>
    public void Release()
    {
        IsOccupied = false;
        ShowHintVisual();
    }

    private void HideHintVisual()
    {
        if (!hideHintWhenOccupied || slotHintVisual == null)
            return;

        hintWasActive = slotHintVisual.activeSelf;
        slotHintVisual.SetActive(false);
    }

    private void ShowHintVisual()
    {
        if (!hideHintWhenOccupied || slotHintVisual == null)
            return;

        slotHintVisual.SetActive(hintWasActive);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsOccupied ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
#endif
}
