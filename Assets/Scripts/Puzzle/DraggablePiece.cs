using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Перетаскиваемый кусочек пазла с проверкой снаппинга к целевому слоту.
/// </summary>
public class DraggablePiece : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Snap")]
    [SerializeField] private PuzzleSlot targetSlot;
    [SerializeField] private float snapDistance = 1.5f;
    [SerializeField] private float returnDuration = 0.35f;
    [Tooltip("Скрывает кусочек после снаппинга — на подложке уже нарисован тот же объект.")]
    [SerializeField] private bool hideWhenPlaced = true;

    [Header("Drag Plane")]
    [SerializeField] private bool useHorizontalPlane = true;

    [Header("Juice — Tactile Drag")]
    [Tooltip("Насколько плавно позиция объекта следует за курсором. Выше = быстрее догоняет.")]
    [SerializeField] private float movementSmoothSpeed = 15f;
    [Tooltip("Сила наклона от скорости движения.")]
    [SerializeField] private float tiltAngleMultiplier = 5f;
    [Tooltip("Максимально допустимый угол наклона в градусах.")]
    [SerializeField] private float maxTiltAngle = 15f;
    [Tooltip("Плавность интерполяции наклона.")]
    [SerializeField] private float tiltLerpSpeed = 10f;

    [Header("Sorting")]
    [SerializeField] private int dragSortingOrder = 100;

    [Header("Feedback")]
    [SerializeField] private UnityEvent onSnapped;
    [SerializeField] private UnityEvent onReturned;

    private Transform pieceTransform;
    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private Image uiImage;
    private Collider pieceCollider3D;
    private Collider2D pieceCollider2D;

    private Vector3 homePosition;
    private Quaternion homeLocalRotation;
    private int defaultSortingOrder;
    private int defaultSiblingIndex;
    private Transform defaultParent;

    private Quaternion originalRotation;
    private Vector3 targetDragPosition;
    private Vector3 lastDragFramePosition;
    private Vector3 currentTiltEuler;
    private Vector3 dragWorldOffset;

    private bool isDragging;
    private bool isPlaced;
    private bool isAnimatingReturn;
    private float dragPlaneHeight;
    private Plane dragPlane;
    private Coroutine returnCoroutine;

    public bool IsPlaced => isPlaced;
    /// <summary>Собран ли кусочек (алиас для HintManager и UI).</summary>
    public bool IsSnapped => isPlaced;
    public bool IsDragging => isDragging;
    public Vector3 CurrentWorldPosition => pieceTransform.position;
    public Vector3 HomeWorldPosition => homePosition;
    public PuzzleSlot TargetSlot => targetSlot;

    public event Action<DraggablePiece> Snapped;
    public event Action<DraggablePiece> Returned;
    public event Action<DraggablePiece> DragStarted;

    private void Awake()
    {
        pieceTransform = transform;
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
        uiImage = GetComponent<Image>();
        pieceCollider3D = GetComponent<Collider>();
        pieceCollider2D = GetComponent<Collider2D>();

#if UNITY_EDITOR
        if (pieceCollider3D == null && pieceCollider2D == null)
            Debug.LogError($"[{nameof(DraggablePiece)}] На объекте {name} нужен Collider или Collider2D.", this);
#endif

        homePosition = pieceTransform.position;
        homeLocalRotation = pieceTransform.localRotation;
        dragPlaneHeight = homePosition.y;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneHeight, 0f));

        if (spriteRenderer != null)
            defaultSortingOrder = spriteRenderer.sortingOrder;

        if (uiImage != null)
        {
            defaultParent = pieceTransform.parent;
            defaultSiblingIndex = pieceTransform.GetSiblingIndex();
        }
    }

    private void Update()
    {
        if (isDragging)
            UpdateDragJuice();
    }

    private void OnDisable()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
            isAnimatingReturn = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanInteract())
            return;

        BeginDrag(eventData);
        UpdateTargetDragPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        UpdateTargetDragPosition(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;
        RestoreVisualOrder();
        EvaluateDrop();
    }

    /// <summary>
    /// Сбрасывает кусочек на стартовую позицию панели (для рестарта уровня).
    /// </summary>
    public void ResetPiece()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        gameObject.SetActive(true);

        isDragging = false;
        isAnimatingReturn = false;
        isPlaced = false;
        currentTiltEuler = Vector3.zero;
        SetColliderEnabled(true);
        pieceTransform.position = homePosition;
        pieceTransform.localRotation = homeLocalRotation;
        RestoreVisualOrder();

        if (targetSlot != null)
            targetSlot.Release();
    }

    private bool CanInteract()
    {
        return !isPlaced && !isAnimatingReturn && targetSlot != null && mainCamera != null;
    }

    private void BeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        RefreshDragPlane();
        originalRotation = pieceTransform.localRotation;
        targetDragPosition = pieceTransform.position;
        lastDragFramePosition = pieceTransform.position;
        currentTiltEuler = Vector3.zero;

        if (TryGetWorldPointOnDragPlane(eventData, out Vector3 worldPoint))
            dragWorldOffset = pieceTransform.position - worldPoint;
        else
            dragWorldOffset = Vector3.zero;

        BringToFront();
        DragStarted?.Invoke(this);
    }

    /// <summary>
    /// Плавное следование за курсором и динамический наклон «тактильной карточки».
    /// </summary>
    private void UpdateDragJuice()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        pieceTransform.position = Vector3.Lerp(
            pieceTransform.position,
            targetDragPosition,
            movementSmoothSpeed * deltaTime);

        // Скорость за текущий кадр — основа для расчёта наклона.
        Vector3 frameVelocity = (pieceTransform.position - lastDragFramePosition) / deltaTime;
        lastDragFramePosition = pieceTransform.position;

        // Парадигма 3D-карточки на плоскости XZ (вид сверху):
        // движение по Z → наклон вокруг локальной X;
        // движение по X → наклон вокруг локальной Z.
        // Знак «−» у Z-скорости даёт естественный «перевес» в сторону движения.
        float targetTiltX = Mathf.Clamp(-frameVelocity.z * tiltAngleMultiplier, -maxTiltAngle, maxTiltAngle);
        float targetTiltZ = Mathf.Clamp(frameVelocity.x * tiltAngleMultiplier, -maxTiltAngle, maxTiltAngle);
        Vector3 targetTiltEuler = new Vector3(targetTiltX, 0f, targetTiltZ);

        currentTiltEuler = Vector3.Lerp(currentTiltEuler, targetTiltEuler, tiltLerpSpeed * deltaTime);

        Vector3 baseEuler = originalRotation.eulerAngles;
        Vector3 tiltedEuler = baseEuler + currentTiltEuler;

        pieceTransform.localRotation = Quaternion.Lerp(
            pieceTransform.localRotation,
            Quaternion.Euler(tiltedEuler),
            tiltLerpSpeed * deltaTime);
    }

    private void BringToFront()
    {
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = dragSortingOrder;
        else if (uiImage != null)
            pieceTransform.SetAsLastSibling();
    }

    private void RestoreVisualOrder()
    {
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = defaultSortingOrder;
        else if (uiImage != null && defaultParent != null)
            pieceTransform.SetSiblingIndex(defaultSiblingIndex);
    }

    private void RefreshDragPlane()
    {
        dragPlaneHeight = pieceTransform.position.y;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneHeight, 0f));
    }

    /// <summary>
    /// Пересечение луча из eventData.position с горизонтальной плоскостью XZ.
    /// </summary>
    private bool TryGetWorldPointOnDragPlane(PointerEventData eventData, out Vector3 worldPoint)
    {
        worldPoint = default;

        if (mainCamera == null)
            return false;

        Ray ray = mainCamera.ScreenPointToRay(eventData.position);

        if (!dragPlane.Raycast(ray, out float distance))
            return false;

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    private void UpdateTargetDragPosition(PointerEventData eventData)
    {
        if (!TryGetWorldPointOnDragPlane(eventData, out Vector3 worldPoint))
            return;

        if (useHorizontalPlane)
            targetDragPosition = worldPoint + dragWorldOffset;
        else
            targetDragPosition = worldPoint;
    }

    private void EvaluateDrop()
    {
        if (targetSlot == null)
        {
            StartReturnHome();
            return;
        }

        float sqrDistance = (pieceTransform.position - targetSlot.SnapPosition).sqrMagnitude;
        float sqrSnapDistance = snapDistance * snapDistance;

        if (sqrDistance <= sqrSnapDistance && !targetSlot.IsOccupied)
            SnapToSlot();
        else
            StartReturnHome();
    }

    private void SnapToSlot()
    {
        if (!targetSlot.TryOccupy(this))
        {
            StartReturnHome();
            return;
        }

        isPlaced = true;
        pieceTransform.position = targetSlot.SnapPosition;
        pieceTransform.localRotation = homeLocalRotation;
        currentTiltEuler = Vector3.zero;
        SetColliderEnabled(false);

        Snapped?.Invoke(this);
        onSnapped?.Invoke();

        if (hideWhenPlaced)
            gameObject.SetActive(false);
    }

    private void StartReturnHome()
    {
        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);

        returnCoroutine = StartCoroutine(ReturnHomeRoutine());
    }

    private IEnumerator ReturnHomeRoutine()
    {
        isAnimatingReturn = true;

        Vector3 startPosition = pieceTransform.position;
        Quaternion startRotation = pieceTransform.localRotation;
        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnDuration);
            float smoothT = t * t * (3f - 2f * t);

            pieceTransform.position = Vector3.Lerp(startPosition, homePosition, smoothT);
            pieceTransform.localRotation = Quaternion.Slerp(startRotation, homeLocalRotation, smoothT);
            currentTiltEuler = Vector3.Lerp(currentTiltEuler, Vector3.zero, smoothT);

            yield return null;
        }

        pieceTransform.position = homePosition;
        pieceTransform.localRotation = homeLocalRotation;
        currentTiltEuler = Vector3.zero;
        isAnimatingReturn = false;
        returnCoroutine = null;

        Returned?.Invoke(this);
        onReturned?.Invoke();
    }

    private void SetColliderEnabled(bool enabled)
    {
        if (pieceCollider3D != null)
            pieceCollider3D.enabled = enabled;

        if (pieceCollider2D != null)
            pieceCollider2D.enabled = enabled;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (targetSlot == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, targetSlot.SnapPosition);
        Gizmos.DrawWireSphere(targetSlot.SnapPosition, snapDistance);
    }
#endif
}
