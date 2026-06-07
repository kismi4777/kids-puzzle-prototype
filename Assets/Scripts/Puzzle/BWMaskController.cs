using System.Collections;
using UnityEngine;

/// <summary>
/// Управляет «дырками» в ЧБ-фоне: подсказки под слотами и финальное растворение уровня.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BWMaskController : MonoBehaviour
{
    private const int MaxHoles = 10;

    private static readonly int HolesId = Shader.PropertyToID("_Holes");
    private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int GlobalAlphaId = Shader.PropertyToID("_GlobalAlpha");

    [Header("Слоты")]
    [SerializeField] private PuzzleSlot[] puzzleSlots;

    [Header("Радиусы дырок (World Space)")]
    [SerializeField] private float initialRadius = 1.5f;
    [SerializeField] private float expandedRadius = 4f;

    [Header("Анимация")]
    [SerializeField] private float expandDuration = 0.6f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float edgeSoftness = 0.5f;

    /// <summary>Длительность финального растворения ЧБ-слоя.</summary>
    public float FadeDuration => fadeDuration;

    private SpriteRenderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private readonly Vector4[] holes = new Vector4[MaxHoles];
    private readonly float[] holeRadii = new float[MaxHoles];
    private readonly Coroutine[] expandRoutines = new Coroutine[MaxHoles];

    private int holeCount;
    private float globalAlpha = 1f;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        CollectSlots();
        InitializeHoles();
        PushShaderProperties();
    }

    /// <summary>
    /// Плавно увеличивает дырку под слотом по индексу.
    /// </summary>
    public void ExpandHole(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= holeCount)
            return;

        if (expandRoutines[slotIndex] != null)
            StopCoroutine(expandRoutines[slotIndex]);

        expandRoutines[slotIndex] = StartCoroutine(ExpandHoleRoutine(slotIndex));
    }

    /// <summary>
    /// Плавно увеличивает дырку под указанным слотом.
    /// </summary>
    public void ExpandHole(PuzzleSlot slot)
    {
        int index = GetSlotIndex(slot);
        if (index >= 0)
            ExpandHole(index);
    }

    /// <summary>
    /// Плавно скрывает весь ЧБ-слой (Global Alpha 1 → 0).
    /// </summary>
    public void FadeOutEntireMask()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    /// <summary>
    /// Сбрасывает маску для рестарта уровня.
    /// </summary>
    public void ResetMask()
    {
        StopAllMaskRoutines();
        globalAlpha = 1f;

        for (int i = 0; i < holeCount; i++)
            holeRadii[i] = initialRadius;

        WriteHoleRadii();
        PushShaderProperties();
    }

    /// <summary>
    /// Возвращает индекс слота в массиве дырок или -1.
    /// </summary>
    public int GetSlotIndex(PuzzleSlot slot)
    {
        if (slot == null)
            return -1;

        for (int i = 0; i < holeCount; i++)
        {
            if (puzzleSlots[i] == slot)
                return i;
        }

        return -1;
    }

    private void CollectSlots()
    {
        if (puzzleSlots == null || puzzleSlots.Length == 0)
            puzzleSlots = FindObjectsByType<PuzzleSlot>(FindObjectsSortMode.None);

        if (puzzleSlots == null || puzzleSlots.Length == 0)
        {
            holeCount = 0;
            return;
        }

        System.Array.Sort(puzzleSlots, CompareSlotsById);
        holeCount = Mathf.Min(puzzleSlots.Length, MaxHoles);
    }

    private static int CompareSlotsById(PuzzleSlot a, PuzzleSlot b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        return string.CompareOrdinal(a.SlotId, b.SlotId);
    }

    private void InitializeHoles()
    {
        globalAlpha = 1f;

        for (int i = 0; i < holeCount; i++)
        {
            Vector3 slotPosition = puzzleSlots[i].SnapPosition;
            holeRadii[i] = initialRadius;
            holes[i] = new Vector4(slotPosition.x, slotPosition.z, 0f, initialRadius);
        }

        for (int i = holeCount; i < MaxHoles; i++)
            holes[i] = Vector4.zero;
    }

    private IEnumerator ExpandHoleRoutine(int slotIndex)
    {
        float startRadius = holeRadii[slotIndex];
        float elapsed = 0f;

        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / expandDuration);
            float smoothT = t * t * (3f - 2f * t);

            holeRadii[slotIndex] = Mathf.Lerp(startRadius, expandedRadius, smoothT);
            holes[slotIndex].w = holeRadii[slotIndex];
            PushShaderProperties();

            yield return null;
        }

        holeRadii[slotIndex] = expandedRadius;
        holes[slotIndex].w = expandedRadius;
        PushShaderProperties();
        expandRoutines[slotIndex] = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        float startAlpha = globalAlpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            globalAlpha = Mathf.Lerp(startAlpha, 0f, t);
            PushShaderProperties();
            yield return null;
        }

        globalAlpha = 0f;
        PushShaderProperties();
        fadeRoutine = null;
    }

    private void WriteHoleRadii()
    {
        for (int i = 0; i < holeCount; i++)
            holes[i].w = holeRadii[i];
    }

    private void PushShaderProperties()
    {
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVectorArray(HolesId, holes);
        propertyBlock.SetInt(HoleCountId, holeCount);
        propertyBlock.SetFloat(EdgeSoftnessId, edgeSoftness);
        propertyBlock.SetFloat(GlobalAlphaId, globalAlpha);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void StopAllMaskRoutines()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        for (int i = 0; i < MaxHoles; i++)
        {
            if (expandRoutines[i] == null)
                continue;

            StopCoroutine(expandRoutines[i]);
            expandRoutines[i] = null;
        }
    }
}
