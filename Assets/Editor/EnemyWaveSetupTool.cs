using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyWaveSetupTool
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string BatPrefabPath = "Assets/Monsters/Prefabs/Bat.prefab";
    private const string SnakePrefabPath = "Assets/Monsters/Prefabs/Snake.prefab";

    [MenuItem("Tools/Monster/이동 방식 및 웨이브 구성 적용")]
    public static void ApplySetup()
    {
        ConfigureExistingPrefab(BatPrefabPath, MonsterMovementPattern.EightDirection);
        ConfigureExistingPrefab(SnakePrefabPath, MonsterMovementPattern.CardinalFour);
        ConfigureSceneWaveRatios();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateSetup();
        Debug.Log("ENEMY_WAVE_SETUP_COMPLETE");
    }

    private static void ConfigureExistingPrefab(
        string prefabPath,
        MonsterMovementPattern movementPattern
    )
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            MonsterMovement movement = prefabRoot.GetComponent<MonsterMovement>();

            if (movement == null)
                throw new InvalidOperationException($"MonsterMovement가 없습니다: {prefabPath}");

            SerializedObject serializedMovement = new SerializedObject(movement);
            serializedMovement.FindProperty("movementPattern").enumValueIndex =
                (int)movementPattern;
            serializedMovement.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(movement);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureSceneWaveRatios()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;

        if (openedForSetup)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            MonsterSpawner spawner = FindComponentInScene<MonsterSpawner>(scene);

            if (spawner == null)
                throw new InvalidOperationException("SampleScene에서 MonsterSpawner를 찾을 수 없습니다.");

            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("batRatioWave4To5").floatValue = 0.2f;
            serializedSpawner.FindProperty("batRatioWave6To8").floatValue = 0.3f;
            serializedSpawner.FindProperty("batRatioWave9AndLater").floatValue = 0.4f;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedForSetup)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ValidateSetup()
    {
        MonsterMovement bat = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath)
            .GetComponent<MonsterMovement>();
        MonsterMovement snake = AssetDatabase.LoadAssetAtPath<GameObject>(SnakePrefabPath)
            .GetComponent<MonsterMovement>();

        if (bat.MovementPattern != MonsterMovementPattern.EightDirection)
            throw new InvalidOperationException("Bat 이동 방식이 8방향으로 저장되지 않았습니다.");

        if (snake.MovementPattern != MonsterMovementPattern.CardinalFour)
            throw new InvalidOperationException("Snake 이동 방식이 4방향으로 저장되지 않았습니다.");

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

        if (openedForValidation)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            MonsterSpawner spawner = FindComponentInScene<MonsterSpawner>(scene);

            if (spawner == null
                || !Mathf.Approximately(spawner.GetBatRatioForWave(1), 0f)
                || !Mathf.Approximately(spawner.GetBatRatioForWave(4), 0.2f)
                || !Mathf.Approximately(spawner.GetBatRatioForWave(6), 0.3f)
                || !Mathf.Approximately(spawner.GetBatRatioForWave(9), 0.4f))
            {
                throw new InvalidOperationException("웨이브별 Bat 비율 설정이 올바르지 않습니다.");
            }
        }
        finally
        {
            if (openedForValidation)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        Debug.Log("ENEMY_WAVE_SETUP_VALIDATION_PASS");
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
