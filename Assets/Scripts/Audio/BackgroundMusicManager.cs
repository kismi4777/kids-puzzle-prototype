using System.Collections;
using UnityEngine;

/// <summary>
/// Фоновая музыка с плавным стартом и бесшовным crossfade-лупом (два AudioSource).
/// </summary>
public class BackgroundMusicManager : MonoBehaviour
{
    public static BackgroundMusicManager Instance { get; private set; }

    [SerializeField] private AudioClip bgmClip;

    [Range(0f, 1f)]
    [SerializeField] private float targetVolume = 0.0225f;

    [SerializeField] private float fadeInDuration = 2f;
    [SerializeField] private float crossfadeDuration = 3f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private AudioSource inactiveSource;

    private Coroutine musicRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateAudioSources();
    }

    private void Start()
    {
        if (bgmClip == null)
            return;

        if (musicRoutine != null)
            StopCoroutine(musicRoutine);

        musicRoutine = StartCoroutine(MusicLoopRoutine());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void CreateAudioSources()
    {
        sourceA = CreateConfiguredSource("BgmSourceA");
        sourceB = CreateConfiguredSource("BgmSourceB");

        activeSource = sourceA;
        inactiveSource = sourceB;
    }

    private AudioSource CreateConfiguredSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.priority = 128;
        source.volume = 0f;

        return source;
    }

    /// <summary>
    /// Fade-In → ожидание точки crossfade → наложение конца и начала → смена ролей источников.
    /// </summary>
    private IEnumerator MusicLoopRoutine()
    {
        PrepareSource(activeSource, bgmClip);
        activeSource.Play();

        yield return FadeVolumeRoutine(activeSource, 0f, targetVolume, fadeInDuration);

        while (true)
        {
            yield return WaitUntilCrossfadePoint();

            PrepareSource(inactiveSource, bgmClip);
            inactiveSource.volume = 0f;
            inactiveSource.time = 0f;
            inactiveSource.Play();

            float outgoingStartVolume = activeSource.volume;
            float elapsed = 0f;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / crossfadeDuration);

                activeSource.volume = Mathf.Lerp(outgoingStartVolume, 0f, t);
                inactiveSource.volume = Mathf.Lerp(0f, targetVolume, t);

                yield return null;
            }

            activeSource.Stop();
            activeSource.volume = 0f;
            inactiveSource.volume = targetVolume;

            SwapSources();
        }
    }

    /// <summary>
    /// Ждёт момент, когда до конца трека остаётся crossfadeDuration секунд.
    /// </summary>
    private IEnumerator WaitUntilCrossfadePoint()
    {
        float crossfadeStartTime = GetCrossfadeStartTime();

        while (activeSource.isPlaying && activeSource.time < crossfadeStartTime)
            yield return null;
    }

    private float GetCrossfadeStartTime()
    {
        if (bgmClip == null)
            return 0f;

        return Mathf.Max(0f, bgmClip.length - crossfadeDuration);
    }

    private static void PrepareSource(AudioSource source, AudioClip clip)
    {
        source.clip = clip;
        source.volume = 0f;
    }

    private static IEnumerator FadeVolumeRoutine(
        AudioSource source,
        float fromVolume,
        float toVolume,
        float duration)
    {
        if (duration <= 0f)
        {
            source.volume = toVolume;
            yield break;
        }

        source.volume = fromVolume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(fromVolume, toVolume, t);
            yield return null;
        }

        source.volume = toVolume;
    }

    private void SwapSources()
    {
        AudioSource previousActive = activeSource;
        activeSource = inactiveSource;
        inactiveSource = previousActive;
    }
}
