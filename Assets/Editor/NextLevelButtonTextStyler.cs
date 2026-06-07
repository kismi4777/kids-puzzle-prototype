using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Применяет единый стиль надписи «СЛЕДУЮЩИЙ УРОВЕНЬ» на кнопке NextLevelButton.
/// </summary>
public static class NextLevelButtonTextStyler
{
    private const string MenuPath = "Tools/Apply Next Level Button Text Style";
    private const string RobotoBoldSdfPath =
        "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset";

    private static readonly Color TextColor = new Color(0.96862745f, 0.9529412f, 0.9019608f, 1f);

    [MenuItem(MenuPath)]
    public static void ApplyToAllGameScenes()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RobotoBoldSdfPath);
        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog("Next Level Text", "Не найден Roboto-Bold SDF.", "OK");
            return;
        }

        string[] scenePaths =
        {
            "Assets/Scenes/Lvl_1.unity",
            "Assets/Scenes/Lvl_2.unity",
            "Assets/Lvl_1.unity"
        };

        int updatedScenes = 0;

        for (int i = 0; i < scenePaths.Length; i++)
        {
            if (!System.IO.File.Exists(scenePaths[i]))
                continue;

            Scene scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
            if (!ApplyInActiveScene(fontAsset))
                continue;

            EditorSceneManager.SaveScene(scene);
            updatedScenes++;
        }

        EditorUtility.DisplayDialog(
            "Next Level Text",
            $"Стиль применён на {updatedScenes} сцен(ах).",
            "OK");
    }

    private static bool ApplyInActiveScene(TMP_FontAsset fontAsset)
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        bool changed = false;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform transform = allTransforms[i];
            if (transform == null || transform.name != "NextLevelButton")
                continue;

            Transform textTransform = transform.Find("Text (TMP)");
            if (textTransform == null)
                continue;

            TextMeshProUGUI tmp = textTransform.GetComponent<TextMeshProUGUI>();
            RectTransform rectTransform = textTransform.GetComponent<RectTransform>();
            if (tmp == null || rectTransform == null)
                continue;

            tmp.text = "СЛЕДУЮЩИЙ УРОВЕНЬ";
            tmp.font = fontAsset;
            tmp.fontSharedMaterial = fontAsset.material;
            tmp.color = TextColor;
            tmp.fontSize = 22.65f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 18f;
            tmp.fontSizeMax = 72f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            SerializedObject serializedTmp = new SerializedObject(tmp);
            serializedTmp.FindProperty("m_fontAsset").objectReferenceValue = fontAsset;
            serializedTmp.FindProperty("m_sharedMaterial").objectReferenceValue = fontAsset.material;
            serializedTmp.FindProperty("m_fontSizeBase").floatValue = 36f;
            serializedTmp.ApplyModifiedPropertiesWithoutUndo();

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(204.304f, 50f);

            EditorUtility.SetDirty(tmp);
            EditorUtility.SetDirty(rectTransform.gameObject);
            changed = true;
        }

        return changed;
    }
}
