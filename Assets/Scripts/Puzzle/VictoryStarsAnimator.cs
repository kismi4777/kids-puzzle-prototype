using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Поочерёдная pop-анимация звёзд на панели победы: 0% → 120% → 95% → 100%.
/// </summary>
public class VictoryStarsAnimator : MonoBehaviour
{
    [Header("Звёзды")]
    [SerializeField] private Transform starsContainer;
    [SerializeField] private RectTransform[] stars;

    [Header("Тайминг")]
    [SerializeField] private float staggerDelay = 0.12f;
    [SerializeField] private float popUpDuration = 0.22f;
    [SerializeField] private float overshootDuration = 0.1f;
    [SerializeField] private float settleDuration = 0.08f;

    [Header("Масштаб")]
    [SerializeField] private float overshootScale = 1.2f;
    [SerializeField] private float undershootScale = 0.95f;

    [Header("Кривые")]
    [SerializeField] private AnimationCurve popUpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve overshootCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve settleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3[] homeScales;
    private Coroutine playRoutine;

    private void Awake()
    {
        if (starsContainer == null)
            starsContainer = transform;

        PrepareStars();
    }

    /// <summary>
    /// Пересобирает звёзды после активации панели победы.
    /// </summary>
    public void PrepareStars()
    {
        stars = null;
        CollectStarsIfNeeded();

        if (homeScales == null || homeScales.Length != stars.Length)
            CacheHomeScales();
        else
            RefreshHomeScalesFromScene();

        ResetStarsImmediate();
    }

    /// <summary>
    /// Корутина появления звёзд для встраивания в общую последовательность панели.
    /// </summary>
    public IEnumerator PlayStarsAnimationRoutine()
    {
        PrepareStars();

        if (stars == null || stars.Length == 0)
        {
            playRoutine = null;
            yield break;
        }

        yield return null;

        yield return PlayStarsRoutine();
        playRoutine = null;
    }

    /// <summary>
    /// Сбрасывает звёзды в нулевой масштаб (перед скрытием панели).
    /// </summary>
    public void ResetStars()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        ResetStarsImmediate();
    }

    private void CollectStarsIfNeeded()
    {
        if (stars != null && stars.Length > 0)
            return;

        var collected = new List<RectTransform>(3);
        TryCollectStarsFrom(transform, collected);

        if (collected.Count == 0 && starsContainer != null && starsContainer != transform)
            TryCollectStarsFrom(starsContainer, collected);

        if (collected.Count > 1)
            collected.Sort(CompareStarsByHorizontalPosition);

        stars = collected.ToArray();
    }

    private static int CompareStarsByHorizontalPosition(RectTransform a, RectTransform b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        return a.anchoredPosition.x.CompareTo(b.anchoredPosition.x);
    }

    private static void TryCollectStarsFrom(Transform container, List<RectTransform> collected)
    {
        if (container == null)
            return;

        int childCount = container.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (!child.name.StartsWith("Image"))
                continue;

            if (!child.TryGetComponent<Image>(out _))
                continue;

            collected.Add((RectTransform)child);
        }
    }

    private void CacheHomeScales()
    {
        if (stars == null || stars.Length == 0)
        {
            homeScales = null;
            return;
        }

        homeScales = new Vector3[stars.Length];
        for (int i = 0; i < stars.Length; i++)
            homeScales[i] = ResolveHomeScale(stars[i], Vector3.one);
    }

    private void RefreshHomeScalesFromScene()
    {
        if (stars == null || homeScales == null)
            return;

        for (int i = 0; i < stars.Length; i++)
        {
            Vector3 resolved = ResolveHomeScale(stars[i], homeScales[i]);
            if (resolved.sqrMagnitude > 0.0001f)
                homeScales[i] = resolved;
        }
    }

    private static Vector3 ResolveHomeScale(RectTransform star, Vector3 fallback)
    {
        if (star == null)
            return fallback.sqrMagnitude > 0.0001f ? fallback : Vector3.one;

        Vector3 scale = star.localScale;
        if (scale.sqrMagnitude > 0.0001f)
            return scale;

        return fallback.sqrMagnitude > 0.0001f ? fallback : Vector3.one;
    }

    private void ResetStarsImmediate()
    {
        if (stars == null)
            return;

        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null)
                stars[i].localScale = Vector3.zero;
        }
    }

    private IEnumerator PlayStarsRoutine()
    {
        ResetStarsImmediate();

        for (int i = 0; i < stars.Length; i++)
        {
            RectTransform star = stars[i];
            if (star == null)
                continue;

            Vector3 homeScale = homeScales != null && i < homeScales.Length ? homeScales[i] : Vector3.one;
            yield return PopStarRoutine(star, homeScale);

            if (i < stars.Length - 1 && staggerDelay > 0f)
                yield return new WaitForSecondsRealtime(staggerDelay);
        }

        playRoutine = null;
    }

    private IEnumerator PopStarRoutine(RectTransform star, Vector3 homeScale)
    {
        Vector3 overshoot = homeScale * overshootScale;
        Vector3 undershoot = homeScale * undershootScale;

        float elapsed = 0f;
        while (elapsed < popUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popUpDuration);
            float eased = popUpCurve != null ? popUpCurve.Evaluate(t) : t;
            star.localScale = Vector3.LerpUnclamped(Vector3.zero, overshoot, eased);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < overshootDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / overshootDuration);
            float eased = overshootCurve != null ? overshootCurve.Evaluate(t) : t;
            star.localScale = Vector3.Lerp(overshoot, undershoot, eased);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            float eased = settleCurve != null ? settleCurve.Evaluate(t) : t;
            star.localScale = Vector3.Lerp(undershoot, homeScale, eased);
            yield return null;
        }

        star.localScale = homeScale;
    }
}
