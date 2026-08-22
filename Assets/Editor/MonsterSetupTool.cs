using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MonsterSetupTool
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string BatSourcePath = "Assets/Monsters/Bat/bat_frame.aseprite";
    private const string SnakeSourcePath = "Assets/Monsters/Snake/snake_asprite.aseprite";
    private const string BatPrefabPath = "Assets/Monsters/Prefabs/Bat.prefab";
    private const string SnakePrefabPath = "Assets/Monsters/Prefabs/Snake.prefab";

    [MenuItem("Tools/Monster/몬스터 에셋 및 스포너 설정")]
    public static void BuildAll()
    {
        ConfigureAseprite(BatSourcePath);
        ConfigureAseprite(SnakeSourcePath);

        MonsterMovement batPrefab = CreateMonsterPrefab(BatSourcePath, BatPrefabPath, "Bat");
        MonsterMovement snakePrefab = CreateMonsterPrefab(SnakeSourcePath, SnakePrefabPath, "Snake");

        ConfigureSceneSpawner(batPrefab, snakePrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MONSTER_SETUP_COMPLETE");
    }

    private static void ConfigureAseprite(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        AsepriteImporter importer = AssetImporter.GetAtPath(assetPath) as AsepriteImporter;

        if (importer == null)
            throw new InvalidOperationException($"Aseprite 에셋을 불러올 수 없습니다: {assetPath}");

        importer.importMode = FileImportModes.AnimatedSprite;
        importer.layerImportMode = LayerImportModes.MergeFrame;
        importer.spritePixelsPerUnit = 40f;
        importer.spriteMeshType = SpriteMeshType.FullRect;
        importer.pivotAlignment = SpriteAlignment.Center;
        importer.includeHiddenLayers = false;
        importer.generateAnimationClips = true;
        importer.generateModelPrefab = true;
        importer.generatePhysicsShape = false;
        importer.SaveAndReimport();
    }

    private static MonsterMovement CreateMonsterPrefab(
        string sourcePath,
        string prefabPath,
        string prefabName
    )
    {
        GameObject importedModel = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
            .OfType<GameObject>()
            .FirstOrDefault();

        if (importedModel == null)
            throw new InvalidOperationException($"Aseprite 모델을 찾을 수 없습니다: {sourcePath}");

        GameObject instance = UnityEngine.Object.Instantiate(importedModel);
        instance.name = prefabName;
        instance.hideFlags = HideFlags.None;

        foreach (SpriteRenderer spriteRenderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            spriteRenderer.sortingOrder = 2;
        }

        MonsterMovement movement = instance.GetComponent<MonsterMovement>();

        if (movement == null)
        {
            movement = instance.AddComponent<MonsterMovement>();
        }

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        UnityEngine.Object.DestroyImmediate(instance);

        if (savedPrefab == null)
            throw new InvalidOperationException($"몬스터 프리팹을 저장할 수 없습니다: {prefabPath}");

        return savedPrefab.GetComponent<MonsterMovement>();
    }

    private static void ConfigureSceneSpawner(MonsterMovement batPrefab, MonsterMovement snakePrefab)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;

        if (openedForSetup)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        Move playerMovement = FindComponentInScene<Move>(scene);
        Tilemap mapTilemap = FindComponentInScene<Tilemap>(scene);

        if (playerMovement == null || mapTilemap == null)
            throw new InvalidOperationException("SampleScene에서 플레이어 또는 Tilemap을 찾을 수 없습니다.");

        MonsterSpawner spawner = FindComponentInScene<MonsterSpawner>(scene);

        if (spawner == null)
        {
            GameObject spawnerObject = new GameObject("MonsterSpawner");
            SceneManager.MoveGameObjectToScene(spawnerObject, scene);
            spawner = spawnerObject.AddComponent<MonsterSpawner>();
        }

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        SerializedProperty prefabsProperty = serializedSpawner.FindProperty("monsterPrefabs");
        prefabsProperty.arraySize = 2;
        prefabsProperty.GetArrayElementAtIndex(0).objectReferenceValue = batPrefab;
        prefabsProperty.GetArrayElementAtIndex(1).objectReferenceValue = snakePrefab;
        serializedSpawner.FindProperty("player").objectReferenceValue = playerMovement.transform;
        serializedSpawner.FindProperty("mapTilemap").objectReferenceValue = mapTilemap;
        serializedSpawner.FindProperty("firstWaveMonsterCount").intValue = 4;
        serializedSpawner.FindProperty("monsterIncreasePerWave").intValue = 1;
        serializedSpawner.FindProperty("nextWaveDelay").floatValue = 1f;
        serializedSpawner.FindProperty("edgeInsetTiles").intValue = 1;
        serializedSpawner.FindProperty("minimumPlayerDistance").intValue = 4;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (openedForSetup)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
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
