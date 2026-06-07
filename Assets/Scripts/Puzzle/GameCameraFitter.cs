using UnityEngine;

/// <summary>
/// Подгоняет ортографическую 3D-камеру (вид сверху, XZ) под размер фона
/// и смещает оптический центр влево, чтобы компенсировать UI-панель справа.
/// </summary>
[RequireComponent(typeof(Camera))]
public class GameCameraFitter : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backgroundSprite;
    [SerializeField, Range(0f, 0.5f)] private float uiPanelWidthPercent = 0.25f;

    [Header("Приближение")]
    [Tooltip("Множитель зума поверх авто-расчёта. Меньше 1 — ближе (крупнее остров), больше 1 — дальше. " +
             "Настраивайте здесь: поле Size у Camera перезаписывается скриптом.")]
    [SerializeField, Range(0.5f, 1.5f)] private float zoomScale = 1f;

    private Camera targetCamera;
    private float fixedCameraY;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        fixedCameraY = transform.position.y;
    }

    private void Start()
    {
        CacheScreenSize();
        ApplyCameraFit();
    }

    private void LateUpdate()
    {
        int width = Screen.width;
        int height = Screen.height;

        if (width == lastScreenWidth && height == lastScreenHeight)
            return;

        CacheScreenSize();
        ApplyCameraFit();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (fixedCameraY == 0f && transform.position.y != 0f)
            fixedCameraY = transform.position.y;

        ApplyCameraFit();
    }
#endif

    private void CacheScreenSize()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    /// <summary>
    /// Пересчитывает orthographicSize и позицию камеры под текущий экран и фон.
    /// </summary>
    private void ApplyCameraFit()
    {
        if (backgroundSprite == null || targetCamera == null)
            return;

        if (!targetCamera.orthographic)
            targetCamera.orthographic = true;

        Bounds bounds = backgroundSprite.bounds;

        // Физический размер острова в мире (спрайт лежит в плоскости XZ).
        float backgroundWidth = bounds.size.x;
        float backgroundDepth = bounds.size.z;

        int screenWidth = Mathf.Max(1, Screen.width);
        int screenHeight = Mathf.Max(1, Screen.height);

        // Полное соотношение сторон экрана и «свободной» зоны слева от UI.
        float fullAspect = (float)screenWidth / screenHeight;
        float freeAspect = (screenWidth * (1f - uiPanelWidthPercent)) / screenHeight;

        // --- Зум ---
        // orthographicSize — это половина видимой высоты (ось Z в мире → вертикаль экрана).
        float targetSizeY = backgroundDepth * 0.5f;

        // Видимая половина-ширина = orthographicSize * freeAspect >= backgroundWidth / 2.
        float targetSizeX = (backgroundWidth * 0.5f) / freeAspect;

        float fittedSize = Mathf.Max(targetSizeY, targetSizeX);
        float orthographicSize = fittedSize * zoomScale;
        targetCamera.orthographicSize = orthographicSize;

        // --- Сдвиг по X ---
        // UI занимает правую часть экрана [W*(1-p) .. W], p = uiPanelWidthPercent.
        // Центр свободной зоны в пикселях от левого края: W*(1-p)/2.
        // Центр всего экрана: W/2.
        // Смещение центра свободной зоны относительно центра экрана (в пикселях):
        //   deltaPx = W*(1-p)/2 - W/2 = -W*p/2  (свободный центр левее экранного).
        //
        // При ортографической проекции видимая ширина мира = 2 * orthographicSize * fullAspect.
        // Один пиксель экрана ≈ (2 * orthographicSize * fullAspect) / W мировых единиц по X.
        //
        // Чтобы мировая точка bounds.center.x оказалась в центре свободной зоны,
        // камеру нужно сдвинуть вправо (+X) на:
        //   shiftX = W*p/2 * (2 * orthographicSize * fullAspect / W)
        //          = orthographicSize * fullAspect * p
        float horizontalShift = orthographicSize * fullAspect * uiPanelWidthPercent;

        Vector3 position = transform.position;
        position.x = bounds.center.x + horizontalShift;
        position.y = fixedCameraY;
        position.z = bounds.center.z;
        transform.position = position;
    }
}
