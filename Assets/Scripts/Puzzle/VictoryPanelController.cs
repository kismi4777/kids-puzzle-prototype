using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Панель победы: задержка → появление карточки → звёзды → кнопка «Следующий уровень».
/// </summary>
public class VictoryPanelController : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private VictoryStarsAnimator starsAnimator;
    [SerializeField] private RectTransform cardTransform;
    [SerializeField] private SnapFeedbackPlayer sfxPlayer;

    [Header("Тайминг последовательности")]
    [SerializeField] private float showDelay = 0.35f;
    [SerializeField] private float backdropFadeDuration = 0.28f;

    [Header("Анимация карточки")]
    [SerializeField] private float cardPopUpDuration = 0.26f;
    [SerializeField] private float cardOvershootDuration = 0.1f;
    [SerializeField] private float cardSettleDuration = 0.08f;
    [SerializeField] private float cardOvershootScale = 1.08f;
    [SerializeField] private float cardUndershootScale = 0.97f;
    [SerializeField] private AnimationCurve cardPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Анимация кнопки")]
    [SerializeField] private float buttonPopUpDuration = 0.22f;
    [SerializeField] private float buttonOvershootDuration = 0.1f;
    [SerializeField] private float buttonSettleDuration = 0.08f;
    [SerializeField] private float buttonOvershootScale = 1.12f;
    [SerializeField] private float buttonUndershootScale = 0.95f;
    [SerializeField] private AnimationCurve buttonPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Events")]
    [SerializeField] private UnityEvent onNextLevelRequested;

    private CanvasGroup panelCanvasGroup;
    private RectTransform buttonTransform;
    private Vector3 cardHomeScale = Vector3.one;
    private Vector3 buttonHomeScale = Vector3.one;
    private Coroutine showRoutine;
    private bool isInitialized;

    private void Awake()
    {
        InitializeIfNeeded();
        HideVictoryPanel();

        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(HandleNextLevelClicked);
    }

    private void OnDestroy()
    {
        if (nextLevelButton != null)
            nextLevelButton.onClick.RemoveListener(HandleNextLevelClicked);
    }

    /// <summary>
    /// Публичный обработчик кнопки «Следующий уровень» (для OnClick в Inspector).
    /// </summary>
    public void OnNextLevelButtonClicked()
    {
        HandleNextLevelClicked();
    }

    /// <summary>
    /// Показывает панель победы с поэтапной анимацией.
    /// </summary>
    public void ShowVictoryPanel()
    {
        EnsurePanelActive();
        InitializeIfNeeded();

        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(ShowVictoryPanelRoutine());
    }

    /// <summary>
    /// Скрывает панель победы при старте уровня.
    /// </summary>
    public void HideVictoryPanel()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (starsAnimator != null)
            starsAnimator.ResetStars();

        ResetContentHidden();
        SetPanelVisible(false);
    }

    private void EnsurePanelActive()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void InitializeIfNeeded()
    {
        if (isInitialized)
            return;

        isInitialized = true;

        if (!TryGetComponent(out panelCanvasGroup))
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (panelRoot == null)
            panelRoot = gameObject;

        if (starsAnimator == null)
            TryGetComponent(out starsAnimator);

        if (sfxPlayer == null)
            sfxPlayer = FindFirstObjectByType<SnapFeedbackPlayer>();

        CacheTransforms();
    }

    private void CacheTransforms()
    {
        if (cardTransform == null)
        {
            Transform card = transform.Find("Card");
            if (card != null)
                cardTransform = card as RectTransform;
        }

        if (cardTransform != null)
            cardHomeScale = cardTransform.localScale;

        if (nextLevelButton != null)
        {
            buttonTransform = nextLevelButton.transform as RectTransform;
            if (buttonTransform != null)
                buttonHomeScale = buttonTransform.localScale;
        }
    }

    private void ResetContentHidden()
    {
        if (cardTransform != null)
            cardTransform.localScale = Vector3.zero;

        if (buttonTransform != null)
            buttonTransform.localScale = Vector3.zero;

        SetButtonInteractable(false);
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (nextLevelButton != null)
            nextLevelButton.interactable = interactable;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
            return;
        }

        gameObject.SetActive(visible);
    }

    private IEnumerator ShowVictoryPanelRoutine()
    {
        if (panelRoot != null && panelRoot != gameObject)
            panelRoot.SetActive(true);

        ResetContentHidden();

        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = true;

        if (sfxPlayer != null)
            sfxPlayer.PlayWinSound();

        if (showDelay > 0f)
            yield return new WaitForSecondsRealtime(showDelay);

        yield return AnimatePanelEntranceRoutine();

        if (starsAnimator != null)
        {
            starsAnimator.PrepareStars();
            yield return starsAnimator.PlayStarsAnimationRoutine();
        }

        if (buttonTransform != null)
            yield return AnimatePopRoutine(
                buttonTransform,
                buttonHomeScale,
                buttonPopUpDuration,
                buttonOvershootDuration,
                buttonSettleDuration,
                buttonOvershootScale,
                buttonUndershootScale,
                buttonPopCurve);

        SetButtonInteractable(true);
        panelCanvasGroup.interactable = true;
        showRoutine = null;
    }

    private IEnumerator AnimatePanelEntranceRoutine()
    {
        if (cardTransform == null)
        {
            yield return FadeBackdropRoutine();
            yield break;
        }

        Vector3 overshoot = cardHomeScale * cardOvershootScale;
        Vector3 undershoot = cardHomeScale * cardUndershootScale;
        float cardTotalDuration = cardPopUpDuration + cardOvershootDuration + cardSettleDuration;
        float totalDuration = Mathf.Max(backdropFadeDuration, cardTotalDuration);
        float phase1End = cardPopUpDuration;
        float phase2End = phase1End + cardOvershootDuration;
        float phase3End = phase2End + cardSettleDuration;

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float fadeT = Mathf.Clamp01(elapsed / backdropFadeDuration);
            panelCanvasGroup.alpha = fadeT;

            if (elapsed <= phase1End)
            {
                float t = Mathf.Clamp01(elapsed / cardPopUpDuration);
                float eased = cardPopCurve != null ? cardPopCurve.Evaluate(t) : t;
                cardTransform.localScale = Vector3.LerpUnclamped(Vector3.zero, overshoot, eased);
            }
            else if (elapsed <= phase2End)
            {
                float t = (elapsed - phase1End) / cardOvershootDuration;
                cardTransform.localScale = Vector3.Lerp(overshoot, undershoot, t);
            }
            else if (elapsed <= phase3End)
            {
                float t = (elapsed - phase2End) / cardSettleDuration;
                cardTransform.localScale = Vector3.Lerp(undershoot, cardHomeScale, t);
            }
            else
            {
                cardTransform.localScale = cardHomeScale;
            }

            yield return null;
        }

        panelCanvasGroup.alpha = 1f;
        cardTransform.localScale = cardHomeScale;
    }

    private IEnumerator FadeBackdropRoutine()
    {
        float elapsed = 0f;
        while (elapsed < backdropFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panelCanvasGroup.alpha = Mathf.Clamp01(elapsed / backdropFadeDuration);
            yield return null;
        }

        panelCanvasGroup.alpha = 1f;
    }

    private static IEnumerator AnimatePopRoutine(
        RectTransform target,
        Vector3 homeScale,
        float popUpDuration,
        float overshootDuration,
        float settleDuration,
        float overshootScale,
        float undershootScale,
        AnimationCurve popCurve)
    {
        Vector3 overshoot = homeScale * overshootScale;
        Vector3 undershoot = homeScale * undershootScale;

        float elapsed = 0f;
        while (elapsed < popUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popUpDuration);
            float eased = popCurve != null ? popCurve.Evaluate(t) : t;
            target.localScale = Vector3.LerpUnclamped(Vector3.zero, overshoot, eased);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < overshootDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / overshootDuration);
            target.localScale = Vector3.Lerp(overshoot, undershoot, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            target.localScale = Vector3.Lerp(undershoot, homeScale, t);
            yield return null;
        }

        target.localScale = homeScale;
    }

    private void HandleNextLevelClicked()
    {
        if (sfxPlayer != null)
            sfxPlayer.FadeOutWinSound();

        HideVictoryPanel();
        onNextLevelRequested?.Invoke();

        GameFlowManager.EnsureExists();
        GameFlowManager.Instance.LoadNextLevel();
    }
}
