using System;
using UnityEditor;
using UnityEngine;

public static class CombatAudioSetupTool
{
    [MenuItem("Tools/Audio/Configure Selected Combat SFX")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 종료 후 음원을 설정하세요.");
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/CombatSFX" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
            // 원본 여섯 파일은 이미 Mono다. 강제 변환/정규화 없이 선택 음량을 보존한다.
            importer.forceToMono = false;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        Debug.Log("선택 전투 효과음: PCM / Mono / Decompress On Load / 48kHz 원본 유지.");
    }
}
