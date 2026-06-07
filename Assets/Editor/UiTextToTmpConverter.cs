using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Конвертирует UnityEngine.UI.Text в TextMeshProUGUI на игровых сценах.
/// </summary>
public static class UiTextToTmpConverter
{
    private const string RobotoBoldSdfPath =
        "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset";

    private static readonly Color TextColor = new Color(0f, 0f, 0f, 0.45f);

    [MenuItem("Tools/Convert UI Text To TMP")]
    public static void ConvertAllGameScenes()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RobotoBoldSdfPath);
        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog("Convert UI Text To TMP", "Не найден Roboto-Bold SDF.", "OK");
            return;
        }

        string[] scenePaths =
        {
            "Assets/Scenes/Lvl_1.unity",
            "Assets/Scenes/Lvl_2.unity",
            "Assets/Lvl_1.unity"
        };

        int convertedTotal = 0;

        for (int i = 0; i < scenePaths.Length; i++)
        {
            if (!System.IO.File.Exists(scenePaths[i]))
                continue;

            Scene scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
            int convertedInScene = ConvertSceneTexts(fontAsset);
            convertedTotal += convertedInScene;

            if (convertedInScene > 0)
                EditorSceneManager.SaveScene(scene);
        }

        EditorUtility.DisplayDialog(
            "Convert UI Text To TMP",
            $"Готово. Конвертировано компонентов: {convertedTotal}.",
            "OK");
    }

    private static int ConvertSceneTexts(TMP_FontAsset fontAsset)
    {
        int converted = 0;
        Text[] legacyTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < legacyTexts.Length; i++)
        {
            Text legacyText = legacyTexts[i];
            if (legacyText == null)
                continue;

            GameObject targetObject = legacyText.gameObject;
            string preservedText = legacyText.text;
            int preservedFontSize = legacyText.fontSize;
            TextAnchor preservedAlignment = legacyText.alignment;
            bool preserveRaycast = legacyText.raycastTarget;

            Object.DestroyImmediate(legacyText);

            TextMeshProUGUI tmp = targetObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
                tmp = targetObject.AddComponent<TextMeshProUGUI>();

            tmp.text = preservedText;
            tmp.font = fontAsset;
            tmp.fontSharedMaterial = fontAsset.material;
            tmp.fontSize = preservedFontSize;
            tmp.enableAutoSizing = false;
            tmp.color = TextColor;

            SerializedObject serializedTmp = new SerializedObject(tmp);
            serializedTmp.FindProperty("m_fontAsset").objectReferenceValue = fontAsset;
            serializedTmp.FindProperty("m_sharedMaterial").objectReferenceValue = fontAsset.material;
            serializedTmp.ApplyModifiedPropertiesWithoutUndo();
            tmp.raycastTarget = preserveRaycast;
            tmp.alignment = ConvertAlignment(preservedAlignment);

            if (targetObject.name == "Text")
                targetObject.name = "Text (TMP)";

            EditorUtility.SetDirty(targetObject);
            converted++;
        }

        UpdateExistingTmpTexts(fontAsset);
        return converted;
    }

    private static void UpdateExistingTmpTexts(TMP_FontAsset fontAsset)
    {
        TextMeshProUGUI[] tmpTexts = Object.FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TextMeshProUGUI tmp = tmpTexts[i];
            if (tmp == null)
                continue;

            tmp.font = fontAsset;
            tmp.fontSharedMaterial = fontAsset.material;
            tmp.color = TextColor;
            EditorUtility.SetDirty(tmp);
        }
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }
}
