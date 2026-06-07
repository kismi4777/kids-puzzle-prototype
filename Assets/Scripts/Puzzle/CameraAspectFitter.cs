using UnityEngine;

/// <summary>
/// Подгоняет камеру под узкие/портретные экраны, чтобы пазл влезал по ширине.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraAspectFitter : MonoBehaviour
{
    [SerializeField] private float referenceAspect = 16f / 9f;
    [SerializeField] private bool adjustPerspectiveByFov = true;
    [SerializeField] private float perspectiveHeightPullPerScale = 2f;

    private Camera targetCamera;
    private float baseOrthographicSize;
    private float baseFieldOfView;
    private Vector3 basePosition;
    private float lastAppliedAspect;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        baseOrthographicSize = targetCamera.orthographicSize;
        baseFieldOfView = targetCamera.fieldOfView;
        basePosition = transform.position;
    }

    private void Start()
    {
        ApplyAspectFit();
    }

    private void LateUpdate()
    {
        float aspect = GetCurrentAspect();
        if (Mathf.Abs(aspect - lastAppliedAspect) < 0.001f)
            return;

        ApplyAspectFit();
    }

    private void ApplyAspectFit()
    {
        float currentAspect = GetCurrentAspect();
        lastAppliedAspect = currentAspect;

        if (currentAspect >= referenceAspect)
        {
            RestoreBaseCamera();
            return;
        }

        float fitScale = referenceAspect / currentAspect;

        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = baseOrthographicSize * fitScale;
            return;
        }

        if (adjustPerspectiveByFov)
        {
            float baseVertRad = baseFieldOfView * Mathf.Deg2Rad;
            float newVertRad = 2f * Mathf.Atan(Mathf.Tan(baseVertRad * 0.5f) * fitScale);
            targetCamera.fieldOfView = newVertRad * Mathf.Rad2Deg;
            return;
        }

        transform.position = basePosition + Vector3.up * (fitScale - 1f) * perspectiveHeightPullPerScale;
    }

    private void RestoreBaseCamera()
    {
        if (targetCamera.orthographic)
            targetCamera.orthographicSize = baseOrthographicSize;
        else
        {
            targetCamera.fieldOfView = baseFieldOfView;
            transform.position = basePosition;
        }
    }

    private static float GetCurrentAspect()
    {
        int height = Mathf.Max(1, Screen.height);
        return (float)Screen.width / height;
    }
}
