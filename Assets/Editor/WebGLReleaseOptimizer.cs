using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Автоматизирует финальные настройки проекта под WebGL-релиз (Яндекс.Игры).
/// </summary>
public static class WebGLReleaseOptimizer
{
    private const string MenuPath = "Tools/Prepare WebGL Release";
    private const float ShortClipMaxDuration = 3f;
    private const float VorbisQualityMin = 0.5f;
    private const float VorbisQualityMax = 0.7f;

    [MenuItem(MenuPath)]
    public static void PrepareWebGLRelease()
    {
        int audioChanged = OptimizeAudioAssets();
        string playerSettingsReport = ApplyPlayerSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "WebGL Release",
            $"Подготовка завершена.\n\nАудио: изменено файлов — {audioChanged}.\n\n{playerSettingsReport}",
            "OK");
    }

    private static int OptimizeAudioAssets()
    {
        string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip");
        int changedCount = 0;

        for (int i = 0; i < audioGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(audioGuids[i]);
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;

            if (importer == null)
                continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            bool isShortClip = clip != null && clip.length <= ShortClipMaxDuration;
            float vorbisQuality = Random.Range(VorbisQualityMin, VorbisQualityMax);
            bool fileChanged = false;

            if (ApplyAudioSampleSettings(importer.defaultSampleSettings, isShortClip, false, vorbisQuality,
                    out AudioImporterSampleSettings defaultSettings))
            {
                importer.defaultSampleSettings = defaultSettings;
                fileChanged = true;
            }

            if (ApplyAudioSampleSettings(importer.GetOverrideSampleSettings("WebGL"), isShortClip, true, vorbisQuality,
                    out AudioImporterSampleSettings webGlSettings))
            {
                importer.SetOverrideSampleSettings("WebGL", webGlSettings);
                fileChanged = true;
            }

            if (!fileChanged)
                continue;

            importer.SaveAndReimport();
            changedCount++;
        }

        return changedCount;
    }

    private static bool ApplyAudioSampleSettings(
        AudioImporterSampleSettings source,
        bool isShortClip,
        bool isWebGlOverride,
        float vorbisQuality,
        out AudioImporterSampleSettings result)
    {
        result = source;
        bool changed = false;

        AudioClipLoadType targetLoadType;
        if (isShortClip)
        {
            targetLoadType = AudioClipLoadType.DecompressOnLoad;
        }
        else if (isWebGlOverride)
        {
            targetLoadType = AudioClipLoadType.Streaming;
        }
        else
        {
            targetLoadType = AudioClipLoadType.DecompressOnLoad;
        }

        if (result.loadType != targetLoadType)
        {
            result.loadType = targetLoadType;
            changed = true;
        }

        if (result.compressionFormat != AudioCompressionFormat.Vorbis)
        {
            result.compressionFormat = AudioCompressionFormat.Vorbis;
            changed = true;
        }

        if (Mathf.Abs(result.quality - vorbisQuality) > 0.001f)
        {
            result.quality = vorbisQuality;
            changed = true;
        }

        return changed;
    }

    private static string ApplyPlayerSettings()
    {
        var report = new StringBuilder();

        if (PlayerSettings.colorSpace != ColorSpace.Gamma)
        {
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            report.AppendLine("Color Space → Gamma");
        }
        else
        {
            report.AppendLine("Color Space уже Gamma");
        }

        if (!PlayerSettings.stripEngineCode)
        {
            PlayerSettings.stripEngineCode = true;
            report.AppendLine("Strip Engine Code → включено");
        }
        else
        {
            report.AppendLine("Strip Engine Code уже включён");
        }

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        report.AppendLine($"WebGL Compression → {PlayerSettings.WebGL.compressionFormat}");

        return report.ToString();
    }
}
