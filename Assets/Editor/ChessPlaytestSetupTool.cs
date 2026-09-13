using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ChessPlaytestSetupTool
{
    public const string ScenePath = "Assets/Scenes/ChessPlaytest.unity";
    private const string SourcePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Playtest/Open Chess Playtest")]
    public static void OpenPlaytest()
    {
        if (EditorApplication.isPlaying) return;
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) CreateScene();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Tools/Playtest/Create Chess Playtest Scene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 생성하세요.");
        // 이미 만든 씬과 사용자가 조절한 Inspector 값을 덮어쓰지 않는다.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
        {
            Debug.Log("CHESS_PLAYTEST_SCENE_EXISTS: " + ScenePath);
            return;
        }
        Scene previous = SceneManager.GetActiveScene();
        Scene source = SceneManager.GetSceneByPath(SourcePath);
        bool openedSource = !source.IsValid() || !source.isLoaded;
        if (openedSource) source = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
        Scene created = default;
        try
        {
            Move sourcePlayer = Find<Move>(source);
            MonsterSpawner sourceSpawner = Find<MonsterSpawner>(source);
            Camera sourceCamera = Find<Camera>(source);
            Tilemap sourceMap = Find<Tilemap>(source);
            if (!sourcePlayer || !sourceSpawner || !sourceMap || !sourceCamera)
                throw new InvalidOperationException("SampleScene에 플레이어/스포너/카메라/Tilemap이 필요합니다.");
            BoundsInt sourceBounds = sourceMap.cellBounds;
            TileBase floorTile = sourceMap.GetTile(new Vector3Int(
                sourceBounds.xMin + sourceBounds.size.x / 2, sourceBounds.yMin + sourceBounds.size.y / 2, 0));
            if (!floorTile) throw new InvalidOperationException("중앙 바닥 Tile을 찾지 못했습니다.");

            created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(created);
            GameObject gridRoot = new GameObject("Grid", typeof(Grid));
            GameObject ground = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            ground.transform.SetParent(gridRoot.transform, false);
            Tilemap map = ground.GetComponent<Tilemap>();
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    map.SetTile(cell, floorTile);
                    map.SetTileFlags(cell, TileFlags.None);
                    bool boundary = x == 0 || x == 11 || y == 0 || y == 11;
                    map.SetColor(cell, boundary ? new Color(0.18f, 0.2f, 0.25f) :
                        (x + y) % 2 == 0 ? Color.white : new Color(0.8f, 0.85f, 0.9f));
                }
            }
            map.CompressBounds();
            GameObject light = new GameObject("Global Light 2D", typeof(Light2D));
            light.GetComponent<Light2D>().lightType = Light2D.LightType.Global;

            GameObject cameraObject = UnityEngine.Object.Instantiate(sourceCamera.gameObject);
            SceneManager.MoveGameObjectToScene(cameraObject, created);
            cameraObject.name = "Main Camera";
            if (cameraObject.TryGetComponent(out CameraController cameraController)) UnityEngine.Object.DestroyImmediate(cameraController);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(6f, 6f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 6.5f;
            camera.rect = new Rect(0, 0, 0.72f, 1);
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.09f);

            GameObject playerObject = UnityEngine.Object.Instantiate(sourcePlayer.gameObject);
            SceneManager.MoveGameObjectToScene(playerObject, created);
            playerObject.name = "player";
            playerObject.transform.position = map.GetCellCenterWorld(new Vector3Int(5, 5, 0));
            foreach (Component component in playerObject.GetComponents<Component>())
                if (component is PlayerTraitSystem || component is UltimateGauge)
                    UnityEngine.Object.DestroyImmediate(component);
            Move player = playerObject.GetComponent<Move>();
            SerializedObject playerSettings = new SerializedObject(player);
            playerSettings.FindProperty("worldCamera").objectReferenceValue = camera;
            playerSettings.FindProperty("cardinalOnly").boolValue = false;
            playerSettings.FindProperty("maxHealth").intValue = 10;
            playerSettings.FindProperty("attackDamage").intValue = 1;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();

            GameObject testRoot = new GameObject("ChessPlaytest");
            GridManager gridManager = testRoot.AddComponent<GridManager>();
            gridManager.Initialize(map, 1);
            ProjectileManager projectiles = testRoot.AddComponent<ProjectileManager>();
            EditorUtility.CopySerialized(sourceSpawner.GetComponent<ProjectileManager>(), projectiles);
            ChessPlaytest test = testRoot.AddComponent<ChessPlaytest>();
            MonsterSpawner spawner = testRoot.AddComponent<MonsterSpawner>();
            SerializedObject spawnSettings = new SerializedObject(spawner);
            spawnSettings.FindProperty("player").objectReferenceValue = player.transform;
            spawnSettings.FindProperty("mapTilemap").objectReferenceValue = map;
            spawnSettings.FindProperty("monsterPrefabs").arraySize = 0;
            spawnSettings.ApplyModifiedPropertiesWithoutUndo();
            Sprite monsterSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Monsters/Bat/bat_frame.aseprite")
                .OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).FirstOrDefault();
            if (!monsterSprite) throw new InvalidOperationException("Bat 독립 Sprite를 찾지 못했습니다.");
            SerializedObject testSettings = new SerializedObject(test);
            testSettings.FindProperty("monsterSprite").objectReferenceValue = monsterSprite;
            testSettings.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(created, ScenePath))
                throw new InvalidOperationException("테스트 씬을 저장하지 못했습니다.");
            Debug.Log("CHESS_PLAYTEST_SETUP_PASS: " + ScenePath);
        }
        finally
        {
            if (created.IsValid() && created.isLoaded) EditorSceneManager.CloseScene(created, true);
            if (openedSource) EditorSceneManager.CloseScene(source, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
    }
}
