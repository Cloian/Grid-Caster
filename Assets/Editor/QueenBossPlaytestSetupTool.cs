using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class QueenBossPlaytestSetupTool
{
    public const string ScenePath = "Assets/Scenes/QueenBossPlaytest.unity";
    private const string SourcePath = "Assets/Scenes/SampleScene.unity";
    private const string AssetFolder = "Assets/Monsters/Playtest";

    [MenuItem("Tools/Playtest/Open Queen Boss Playtest")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) return;
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) Create();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }
    [MenuItem("Tools/Playtest/Create Queen Boss Playtest Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 생성하세요.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) { Debug.Log("QUEEN_SCENE_EXISTS: " + ScenePath); return; }
        string pipeline = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.GetType().FullName;
        if (!pipeline.Contains("Universal")) throw new InvalidOperationException("URP 2D 설정을 확인하세요: " + pipeline);
        Scene previous = SceneManager.GetActiveScene();
        Scene source = SceneManager.GetSceneByPath(SourcePath);
        bool openedSource = !source.IsValid() || !source.isLoaded;
        if (openedSource) source = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
        Scene created = default;
        try
        {
            Move sourcePlayer = Find<Move>(source); MonsterSpawner sourceSpawner = Find<MonsterSpawner>(source);
            Camera sourceCamera = Find<Camera>(source); Tilemap sourceMap = Find<Tilemap>(source);
            if (!sourcePlayer || !sourceSpawner || !sourceCamera || !sourceMap) throw new InvalidOperationException("SampleScene의 기본 참조가 필요합니다.");
            BoundsInt bounds = sourceMap.cellBounds;
            TileBase floor = sourceMap.GetTile(new Vector3Int(bounds.xMin+bounds.size.x/2,bounds.yMin+bounds.size.y/2,0));
            if (!floor) throw new InvalidOperationException("바닥 Tile이 없습니다.");
            Sprite square = CreateSquare(); TMP_FontAsset font = CreateFont();
            created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(created);
            var gridObject = new GameObject("Grid",typeof(Grid));
            var ground = new GameObject("Ground",typeof(Tilemap),typeof(TilemapRenderer)); ground.transform.SetParent(gridObject.transform,false);
            Tilemap map = ground.GetComponent<Tilemap>();
            for (int y=0;y<15;y++) for(int x=0;x<15;x++)
            {
                Vector3Int cell = new Vector3Int(x,y,0); map.SetTile(cell,floor);map.SetTileFlags(cell,TileFlags.None);
                map.SetColor(cell,x==0||y==0||x==14||y==14?new Color(.18f,.2f,.25f):Color.white);
            }
            map.CompressBounds();
            var light = new GameObject("Global Light 2D",typeof(Light2D)); light.GetComponent<Light2D>().lightType=Light2D.LightType.Global;
            var root = new GameObject("Queen Boss Playtest");
            GridManager grid = root.AddComponent<GridManager>();grid.Initialize(map,1);
            ProjectileManager projectiles = root.AddComponent<ProjectileManager>();
            EditorUtility.CopySerialized(sourceSpawner.GetComponent<ProjectileManager>(),projectiles);
            QueenBossPlaytest test = root.AddComponent<QueenBossPlaytest>(); root.AddComponent<QueenBossBehaviour>();
            var cameraObject = UnityEngine.Object.Instantiate(sourceCamera.gameObject); SceneManager.MoveGameObjectToScene(cameraObject,created);
            cameraObject.name="Main Camera";
            Camera camera = cameraObject.GetComponent<Camera>();camera.allowHDR=false;camera.allowMSAA=false;
            camera.backgroundColor=new Color(.035f,.045f,.065f);
            CameraController cameraController=cameraObject.GetComponent<CameraController>();
            if (!cameraController) cameraController=cameraObject.AddComponent<CameraController>();
            Set(cameraController,"gridManager",grid);
            // PC 전체 보드의 기존 카메라 배율을 사용한다. 픽셀 스냅이 카메라 크기를 덮어쓰지 않게 한다.
            if (cameraObject.TryGetComponent(out PixelPerfectCamera pixelCamera)) pixelCamera.enabled=false;
            var playerObject=UnityEngine.Object.Instantiate(sourcePlayer.gameObject);SceneManager.MoveGameObjectToScene(playerObject,created);
            playerObject.name="player";playerObject.transform.position=map.GetCellCenterWorld(new Vector3Int(7,3,0));
            if(playerObject.TryGetComponent(out UltimateGauge gauge))UnityEngine.Object.DestroyImmediate(gauge);
            if(!playerObject.GetComponent<PlayerTraitSystem>())playerObject.AddComponent<PlayerTraitSystem>();
            Move player=playerObject.GetComponent<Move>();
            Set(player,"worldCamera",camera);
            var moveSettings=new SerializedObject(player);moveSettings.FindProperty("cardinalOnly").boolValue=false;
            moveSettings.FindProperty("maxHealth").intValue=18;moveSettings.FindProperty("attackDamage").intValue=2;moveSettings.ApplyModifiedPropertiesWithoutUndo();
            MonsterSpawner spawner=root.AddComponent<MonsterSpawner>();
            var spawnSettings=new SerializedObject(spawner);
            spawnSettings.FindProperty("player").objectReferenceValue=player.transform;spawnSettings.FindProperty("mapTilemap").objectReferenceValue=map;
            spawnSettings.FindProperty("monsterPrefabs").arraySize=0;spawnSettings.FindProperty("enableChessWaves").boolValue=false;
            spawnSettings.FindProperty("edgeInsetTiles").intValue=1;spawnSettings.ApplyModifiedPropertiesWithoutUndo();
            Set(test,"grayboxSprite",square);
            var hud=new GameObject("Queen Playtest HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            hud.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler=hud.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=PcBoardLayout.ReferenceResolution;scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            QueenBossPlaytestUi ui=hud.AddComponent<QueenBossPlaytestUi>();Set(ui,"playtest",test);Set(ui,"font",font);Set(ui,"markerSprite",square);Set(ui,"worldCamera",camera);
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            if(!EditorSceneManager.SaveScene(created,ScenePath))throw new InvalidOperationException("보스 테스트 씬 저장 실패");
            AssetDatabase.SaveAssets();Debug.Log("QUEEN_SETUP_PASS: "+ScenePath+"; "+pipeline+"; 13×13; 32 PPU Point; PC board camera");
        }
        finally
        {
            if(created.IsValid()&&created.isLoaded)EditorSceneManager.CloseScene(created,true);
            if(openedSource)EditorSceneManager.CloseScene(source,true);
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
        }
    }
    private static Sprite CreateSquare()
    {
        string path=AssetFolder+"/QueenGraybox.png";
        if(!File.Exists(path))
        {
            Directory.CreateDirectory(AssetFolder);AssetDatabase.Refresh();
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.SetPixels(Enumerable.Repeat(Color.white,1024).ToArray());texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        }
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=32;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static TMP_FontAsset CreateFont()
    {
        const string path="Assets/Monsters/Playtest/QueenPlaytestFont.asset";
        TMP_FontAsset existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(existing)return existing;
        Font source=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/Galmuri/Galmuri11.ttf");
        TMP_FontAsset font=TMP_FontAsset.CreateFontAsset(source,36,6,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
        string text=string.Join("",new[]{"Assets/Codes/QueenBossPlaytestUi.cs","Assets/Codes/QueenBossBehaviour.cs"}.Select(File.ReadAllText));
        string chars=new string(text.Where(c=>(c>=32&&c<=126)||(c>='가'&&c<='힣')).Concat("↓■›−·×→".ToCharArray()).Distinct().ToArray());
        if(!font.TryAddCharacters(chars,out string missing)||!string.IsNullOrEmpty(missing))throw new InvalidOperationException("보스 HUD 글리프 누락: "+missing);
        font.name="QueenPlaytestFont";font.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(Texture2D atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
        AssetDatabase.SaveAssets();return font;
    }
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    { var settings=new SerializedObject(target);settings.FindProperty(field).objectReferenceValue=value;settings.ApplyModifiedPropertiesWithoutUndo(); }
    private static T Find<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).FirstOrDefault();
}
