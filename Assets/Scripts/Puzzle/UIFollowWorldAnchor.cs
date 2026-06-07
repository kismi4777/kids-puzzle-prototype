using UnityEngine;

/// <summary>
/// Прикрепляет UI-элемент к точке в 3D-мире (XZ), компенсируя сдвиг/зум камеры.
/// </summary>
public class UIFollowWorldAnchor : MonoBehaviour
{
    [SerializeField] private RectTransform uiElement;
    [SerializeField] private Transform worldAnchor;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private bool hideWhenBehindCamera = true;

    private Vector3 screenPosition;

    private void Awake()
    {
        if (uiElement == null)
            TryGetComponent(out uiElement);

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (uiElement == null || worldAnchor == null || mainCamera == null)
            return;

        screenPosition = mainCamera.WorldToScreenPoint(worldAnchor.position);

        if (hideWhenBehindCamera && screenPosition.z < 0f)
        {
            if (uiElement.gameObject.activeSelf)
                uiElement.gameObject.SetActive(false);

            return;
        }

        if (!uiElement.gameObject.activeSelf)
            uiElement.gameObject.SetActive(true);

        uiElement.position = screenPosition;
    }
}
