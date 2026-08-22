using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DamageFeedbackSetupTool
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string BatPrefabPath = "Assets/Monsters/Prefabs/Bat.prefab";
    private const string SnakePrefabPath = "Assets/Monsters/Prefabs/Snake.prefab";

    [MenuItem("Tools/Combat/피해 붉은색 피드백 설정 적용")]
    public static void ApplySetup()
    {
        ConfigurePrefab(BatPrefabPath);
        ConfigurePrefab(SnakePrefabPath);
        ConfigurePlayer();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("DAMAGE_FEEDBACK_SETUP_COMPLETE");
    }

    private static void ConfigurePrefab(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            ConfigureDamageFlash(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigurePlayer()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;

        if (openedForSetup)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            Move player = FindComponentInScene<Move>(scene);

            if (player == null)
                throw new InvalidOperationException("SampleScene에서 플레이어를 찾을 수 없습니다.");

            ConfigureDamageFlash(player.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (player.GetComponent<DamageFlash>() == null)
                throw new InvalidOperationException("플레이어 DamageFlash가 저장되지 않았습니다.");
        }
        finally
        {
            if (openedForSetup)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ConfigureDamageFlash(GameObject target)
    {
        DamageFlash damageFlash = target.GetComponent<DamageFlash>();

        if (damageFlash == null)
        {
            damageFlash = target.AddComponent<DamageFlash>();
        }

        SerializedObject serializedFlash = new SerializedObject(damageFlash);
        serializedFlash.FindProperty("flashColor").colorValue =
            new Color(1f, 0.12f, 0.12f, 1f);
        serializedFlash.FindProperty("flashDuration").floatValue = 0.22f;
        serializedFlash.FindProperty("flashStrength").floatValue = 0.8f;
        serializedFlash.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageFlash);
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            T component = rootObject.GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        return null;
    }
}
