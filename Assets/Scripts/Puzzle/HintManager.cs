using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Мультяшная подсказка «Tutorial Hand» по раскадровке лид-артиста.
/// </summary>
public class HintManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private Transform handTransform;
    [SerializeField] private SpriteRenderer handRenderer;

    [Header("Auto Hint")]
    [SerializeField] private bool showHintOnStart = true;
    [SerializeField] private float startHintDelay = 3f;
    [SerializeField] private bool enableIdleHint = true;
    [SerializeField] private float idleHintDelay = 10f;

    [Header("Position")]
    [Tooltip("Смещение руки над кусочком/слотом.")]
    [SerializeField] private Vector3 handWorldOffset = new Vector3(0f, 0.15f, -0.25f);
    [Tooltip("Стартовая позиция «сбоку» от кусочка перед Pop-in.")]
    [SerializeField] private Vector3 sideAppearOffset = new Vector3(1.2f, 0f, 0f);

    [Header("Phase 1 — Pop-In (Side)")]
    [SerializeField] private float appearPopDuration = 0.35f;
    [SerializeField] private float popSettleDuration = 0.15f;
    [SerializeField] private float popOvershootScale = 1.1f;
    [SerializeField] private AnimationCurve appearPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve appearSettleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Phase 2 — Fly To Piece")]
    [SerializeField] private float flyToPieceDuration = 0.45f;
    [SerializeField] private AnimationCurve flyToPieceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Phase 3 — Cartoon Tap")]
    [SerializeField] private float tapPressDuration = 0.12f;
    [SerializeField] private float tapReleaseDuration = 0.2f;
    [SerializeField] private float tapSquashScaleX = 1.1f;
    [SerializeField] private float tapSquashScaleY = 0.8f;
    [SerializeField] private float tapTiltAngle = 15f;
    [SerializeField] private AnimationCurve tapPressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve tapReleaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Phase 4 — Drag To Slot")]
    [SerializeField] private float moveToSlotDuration = 1.1f;
    [SerializeField] private AnimationCurve moveToSlotCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Phase 5 — Disappear")]
    [SerializeField] private float disappearDuration = 0.28f;
    [SerializeField] private AnimationCurve disappearCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Visual")]
    [SerializeField] private int handSortingOrder = 200;
    [SerializeField] private float handDisplayScale = 0.375f;

    private Color handBaseColor = Color.white;
    private Vector3 handHomeEuler;
    private Coroutine hintCoroutine;
    private Coroutine startHintCoroutine;
    private bool isHintPlaying;
    private float idleTimer;
    private DraggablePiece[] trackedPieces;

    public bool IsHintPlaying => isHintPlaying;

    private void Awake()
    {
        ResolveReferences();
        PrepareHandRenderer();
        ResetHintState();
    }

    private void Start()
    {
        if (showHintOnStart)
            startHintCoroutine = StartCoroutine(ShowHintAfterDelayRoutine(startHintDelay));
    }

    private void Update()
    {
        UpdateIdleHint();
    }

    private void OnEnable()
    {
        CachePieces();
        SubscribePieceEvents();
    }

    private void OnDisable()
    {
        UnsubscribePieceEvents();

        if (hintCoroutine != null)
        {
            StopCoroutine(hintCoroutine);
            hintCoroutine = null;
        }

        isHintPlaying = false;

        // Не трогаем Transform руки вне Play Mode — иначе Inspector Unity падает с NullReference.
        if (Application.isPlaying)
            HideHandImmediate();
    }

    /// <summary>
    /// Главная точка входа для UI-кнопки: HintManager → TriggerHint()
    /// Прерывает текущую подсказку и запускает заново.
    /// </summary>
    public void TriggerHint()
    {
        ResolveReferences();
        StopHint();

        if (levelManager == null || levelManager.IsLevelCompleted)
            return;

        if (handTransform == null || handRenderer == null)
            return;

        DraggablePiece targetPiece = levelManager.GetRandomUnplacedPiece();
        if (targetPiece == null || targetPiece.TargetSlot == null)
            return;

        idleTimer = 0f;
        isHintPlaying = true;
        hintCoroutine = StartCoroutine(HintSequenceCoroutine(targetPiece));
    }

    /// <summary>Алиас для обратной совместимости.</summary>
    public void ShowHint() => TriggerHint();

    /// <summary>
    /// Останавливает корутину, скрывает руку и сбрасывает флаги.
    /// </summary>
    public void StopHint()
    {
        if (hintCoroutine != null)
        {
            StopCoroutine(hintCoroutine);
            hintCoroutine = null;
        }

        ResetHintState();
    }

    [ContextMenu("Trigger Hint Now")]
    private void TriggerHintFromContextMenu()
    {
        if (!Application.isPlaying)
            return;

        TriggerHint();
    }

    private void ResetHintState()
    {
        isHintPlaying = false;
        HideHandImmediate();
    }

    private void ResolveReferences()
    {
        if (levelManager == null)
            levelManager = FindFirstObjectByType<LevelManager>();

        if (handTransform == null)
        {
            GameObject handObject = GameObject.Find("TutorialHand");
            if (handObject != null)
                handTransform = handObject.transform;
        }

        if (handRenderer == null && handTransform != null)
            handRenderer = handTransform.GetComponent<SpriteRenderer>();
    }

    private void PrepareHandRenderer()
    {
        if (handTransform != null)
            handHomeEuler = handTransform.localEulerAngles;

        if (handRenderer == null)
            return;

        handBaseColor = handRenderer.color;
        handBaseColor.a = 1f;
        handRenderer.sortingOrder = handSortingOrder;
    }

    private IEnumerator ShowHintAfterDelayRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        startHintCoroutine = null;

        if (levelManager != null && !levelManager.IsLevelCompleted)
            TriggerHint();
    }

    private void UpdateIdleHint()
    {
        if (!enableIdleHint || isHintPlaying)
            return;

        if (levelManager == null || levelManager.IsLevelCompleted)
            return;

        if (IsPlayerInteracting())
        {
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer >= idleHintDelay)
        {
            idleTimer = 0f;
            TriggerHint();
        }
    }

    private static bool IsPlayerInteracting()
    {
        if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame))
            return true;

        if (Touchscreen.current != null)
        {
            if (Touchscreen.current.primaryTouch.press.isPressed)
                return true;

            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private void CachePieces()
    {
        trackedPieces = FindObjectsByType<DraggablePiece>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void SubscribePieceEvents()
    {
        if (trackedPieces == null)
            return;

        for (int i = 0; i < trackedPieces.Length; i++)
        {
            if (trackedPieces[i] == null)
                continue;

            trackedPieces[i].DragStarted += HandlePlayerInteracted;
            trackedPieces[i].Snapped += HandlePlayerInteracted;
        }
    }

    private void UnsubscribePieceEvents()
    {
        if (trackedPieces == null)
            return;

        for (int i = 0; i < trackedPieces.Length; i++)
        {
            if (trackedPieces[i] == null)
                continue;

            trackedPieces[i].DragStarted -= HandlePlayerInteracted;
            trackedPieces[i].Snapped -= HandlePlayerInteracted;
        }
    }

    private void HandlePlayerInteracted(DraggablePiece piece)
    {
        idleTimer = 0f;

        if (isHintPlaying)
            StopHint();
    }

    /// <summary>
    /// Полная секвенция по раскадровке:
    /// Pop-in сбоку → подлёт к кусочку → тап → перетаскивание к слоту → исчезновение.
    /// </summary>
    private IEnumerator HintSequenceCoroutine(DraggablePiece targetPiece)
    {
        try
        {
            if (targetPiece == null || targetPiece.TargetSlot == null)
                yield break;

            if (handTransform == null || handRenderer == null)
                yield break;

            Vector3 piecePoint = targetPiece.HomeWorldPosition + handWorldOffset;
            Vector3 slotPoint = targetPiece.TargetSlot.SnapPosition + handWorldOffset;
            Vector3 sidePoint = piecePoint + sideAppearOffset;

            handTransform.position = sidePoint;

            yield return PopInSideRoutine();

            if (!isHintPlaying)
                yield break;

            yield return FlyToPointRoutine(sidePoint, piecePoint, flyToPieceDuration, flyToPieceCurve);

            if (!isHintPlaying)
                yield break;

            yield return CartoonTapRoutine();

            if (!isHintPlaying)
                yield break;

            yield return FlyToPointRoutine(piecePoint, slotPoint, moveToSlotDuration, moveToSlotCurve);

            if (!isHintPlaying)
                yield break;

            yield return DisappearRoutine();
        }
        finally
        {
            hintCoroutine = null;
            ResetHintState();
        }
    }

    private IEnumerator PopInSideRoutine()
    {
        SetHandVisible(true);

        float elapsed = 0f;
        while (elapsed < appearPopDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / appearPopDuration);
            float eased = appearPopCurve.Evaluate(t);
            float scale = Mathf.LerpUnclamped(0f, popOvershootScale, eased);

            ApplyHandPose(scale, 1f, 0f);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < popSettleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popSettleDuration);
            float eased = appearSettleCurve.Evaluate(t);
            float scale = Mathf.Lerp(popOvershootScale, 1f, eased);

            ApplyHandPose(scale, 1f, 0f);
            yield return null;
        }

        ApplyHandPose(1f, 1f, 0f);
    }

    private IEnumerator CartoonTapRoutine()
    {
        float elapsed = 0f;
        while (elapsed < tapPressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / tapPressDuration);
            float eased = tapPressCurve.Evaluate(t);

            float scaleX = Mathf.Lerp(1f, tapSquashScaleX, eased);
            float scaleY = Mathf.Lerp(1f, tapSquashScaleY, eased);
            float tilt = Mathf.Lerp(0f, tapTiltAngle, eased);

            ApplyHandPose(scaleX, scaleY, 1f, 1f, tilt);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < tapReleaseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / tapReleaseDuration);
            float eased = tapReleaseCurve.Evaluate(t);

            float scaleX = Mathf.Lerp(tapSquashScaleX, 1f, eased);
            float scaleY = Mathf.Lerp(tapSquashScaleY, 1f, eased);
            float tilt = Mathf.Lerp(tapTiltAngle, 0f, eased);

            ApplyHandPose(scaleX, scaleY, 1f, 1f, tilt);
            yield return null;
        }

        ApplyHandPose(1f, 1f, 0f);
    }

    private IEnumerator FlyToPointRoutine(Vector3 startPoint, Vector3 endPoint, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = curve != null ? curve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

            handTransform.position = Vector3.Lerp(startPoint, endPoint, eased);
            ApplyHandPose(1f, 1f, 0f);
            yield return null;
        }

        handTransform.position = endPoint;
    }

    private IEnumerator DisappearRoutine()
    {
        float elapsed = 0f;

        while (elapsed < disappearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / disappearDuration);
            float scale = disappearCurve != null ? disappearCurve.Evaluate(t) : 1f - Mathf.SmoothStep(0f, 1f, t);

            ApplyHandPose(Mathf.Max(0f, scale), Mathf.Max(0f, scale), 0f);
            yield return null;
        }
    }

    private void ApplyHandPose(float uniformScale, float alpha, float zTiltDegrees)
    {
        ApplyHandPose(uniformScale, uniformScale, 1f, alpha, zTiltDegrees);
    }

    private void ApplyHandPose(float scaleX, float scaleY, float scaleZ, float alpha, float zTiltDegrees)
    {
        if (handTransform == null || handRenderer == null)
            return;

        handTransform.localScale = new Vector3(
            scaleX * handDisplayScale,
            scaleY * handDisplayScale,
            scaleZ * handDisplayScale);

        Vector3 euler = handHomeEuler;
        euler.z += zTiltDegrees;
        handTransform.localRotation = Quaternion.Euler(euler);

        Color color = handBaseColor;
        color.a = alpha;
        handRenderer.color = color;
    }

    private void SetHandVisible(bool visible)
    {
        if (handRenderer != null)
            handRenderer.enabled = visible;
    }

    private void HideHandImmediate()
    {
        if (handTransform != null)
        {
            handTransform.localScale = Vector3.zero;
            handTransform.localRotation = Quaternion.Euler(handHomeEuler);
        }

        if (handRenderer != null)
        {
            Color color = handBaseColor;
            color.a = 0f;
            handRenderer.color = color;
            handRenderer.enabled = false;
        }
    }
}
