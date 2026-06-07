using System.Collections;
using UnityEngine;

/// <summary>
/// Воспроизводит звук и VFX при успешном снаппинге кусочка.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SnapFeedbackPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Экземпляр Particle System в сцене. Если пусто — создаётся из Snap Vfx Prefab.")]
    [SerializeField] private ParticleSystem snapVfx;
    [Tooltip("Префаб VFX из Assets/Vfx (играет в позиции слота при снапе).")]
    [SerializeField] private ParticleSystem snapVfxPrefab;
    [SerializeField] private AudioClip snapClip;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private float winFadeOutDuration = 0.6f;

    private const float WinVolume = 0.5f;

    private AudioSource winAudioSource;
    private Coroutine winFadeRoutine;

    private Transform vfxTransform;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        EnsureSnapVfxInstance();

        if (snapClip == null)
            snapClip = CreateDefaultSnapClip();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.clip = snapClip;
        }

        CreateWinAudioSource();
    }

    private void CreateWinAudioSource()
    {
        GameObject winSourceObject = new GameObject("WinAudioSource");
        winSourceObject.transform.SetParent(transform, false);

        winAudioSource = winSourceObject.AddComponent<AudioSource>();
        winAudioSource.playOnAwake = false;
        winAudioSource.loop = false;
        winAudioSource.spatialBlend = 0f;
        winAudioSource.volume = WinVolume;
    }

    public void PlaySnapSound()
    {
        if (audioSource == null || snapClip == null)
            return;

        audioSource.PlayOneShot(snapClip);
    }

    /// <summary>
    /// Звук поднятия кусочка с панели (вызывается при начале перетаскивания).
    /// </summary>
    public void PlayPickupSound()
    {
        if (audioSource == null || pickupClip == null)
            return;

        audioSource.PlayOneShot(pickupClip);
    }

    /// <summary>
    /// Звук победы при появлении панели (громкость 50%).
    /// </summary>
    public void PlayWinSound()
    {
        if (winAudioSource == null || winClip == null)
            return;

        if (winFadeRoutine != null)
        {
            StopCoroutine(winFadeRoutine);
            winFadeRoutine = null;
        }

        winAudioSource.clip = winClip;
        winAudioSource.volume = WinVolume;
        winAudioSource.time = 0f;
        winAudioSource.Play();
    }

    /// <summary>
    /// Плавно затухает звук победы (при нажатии «Следующий уровень»).
    /// </summary>
    public void FadeOutWinSound()
    {
        if (winAudioSource == null || !winAudioSource.isPlaying)
            return;

        if (winFadeRoutine != null)
            StopCoroutine(winFadeRoutine);

        winFadeRoutine = StartCoroutine(FadeOutWinRoutine());
    }

    private IEnumerator FadeOutWinRoutine()
    {
        float startVolume = winAudioSource.volume;
        float duration = Mathf.Max(0.01f, winFadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            winAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        winAudioSource.Stop();
        winAudioSource.volume = WinVolume;
        winFadeRoutine = null;
    }

    /// <summary>
    /// VFX в позиции слота (вызывается из PuzzleFeedbackBinder при снапе).
    /// </summary>
    public void PlayAtPosition(Vector3 worldPosition)
    {
        PlaySnapSound();

        if (snapVfx == null)
            return;

        vfxTransform.position = worldPosition;
        snapVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        snapVfx.Play();
    }

    private void EnsureSnapVfxInstance()
    {
        if (IsSceneParticleInstance(snapVfx))
        {
            ConfigureParticle(snapVfx);
            vfxTransform = snapVfx.transform;
            return;
        }

        if (snapVfxPrefab == null && snapVfx != null)
            snapVfxPrefab = snapVfx;

        snapVfx = null;

        if (snapVfxPrefab == null)
            return;

        snapVfx = Instantiate(snapVfxPrefab, transform);
        snapVfx.name = "SnapVfx";
        ConfigureParticle(snapVfx);
        vfxTransform = snapVfx.transform;
    }

    private static bool IsSceneParticleInstance(ParticleSystem particleSystem)
    {
        return particleSystem != null && particleSystem.gameObject.scene.IsValid();
    }

    private static void ConfigureParticle(ParticleSystem particleSystem)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
    }

    private static AudioClip CreateDefaultSnapClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.12f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - (t / duration);
            samples[i] = Mathf.Sin(2f * Mathf.PI * 920f * t) * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("DefaultSnap", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
