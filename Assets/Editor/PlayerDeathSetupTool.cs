using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayerDeathSetupTool
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string GravePath = "Assets/Player/Death/grave.aseprite";

    [MenuItem("Tools/Player/묘비와 사망 복기 설정")]
    public static void ApplySetup()
    {
        ConfigureGraveImporter();
        Sprite graveSprite = LoadGraveSprite();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Move playerMovement = FindComponentInScene<Move>(scene);

        if (playerMovement == null)
            throw new InvalidOperationException("SampleScene에서 player의 Move를 찾을 수 없습니다.");

        PlayerDeathMarker marker = playerMovement.GetComponent<PlayerDeathMarker>();

        if (marker == null)
        {
            marker = playerMovement.gameObject.AddComponent<PlayerDeathMarker>();
        }

        SerializedObject serializedMarker = new SerializedObject(marker);
        serializedMarker.FindProperty("graveSprite").objectReferenceValue = graveSprite;
        serializedMarker.FindProperty("hidePlayerVisual").boolValue = true;
        serializedMarker.FindProperty("sortingOrderOffset").intValue = 1;
        serializedMarker.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(marker);

        GameHudController hud = FindComponentInScene<GameHudController>(scene);

        if (hud == null)
            throw new InvalidOperationException("SampleScene에서 GameHudController를 찾을 수 없습니다.");

        Text reviewHint = hud.GetComponentsInChildren<Text>(true)
            .FirstOrDefault(text => text.name == "QuitHint");

        if (reviewHint != null)
        {
            reviewHint.text = "ESC 전장 확인  ·  REPLAY 버튼으로 다시 도전";
            reviewHint.rectTransform.sizeDelta = new Vector2(460f, 28f);
            EditorUtility.SetDirty(reviewHint);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (marker.GetComponent<CharacterHealth>() == null || graveSprite == null)
            throw new InvalidOperationException("플레이어 사망 묘비 설정 검증에 실패했습니다.");

        Debug.Log(
            $"PLAYER_DEATH_SETUP_PASS sprite={graveSprite.name} "
            + $"size={graveSprite.rect.width}x{graveSprite.rect.height}"
        );
    }

    private static void ConfigureGraveImporter()
    {
        AssetDatabase.ImportAsset(
            GravePath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate
        );

        if (AssetImporter.GetAtPath(GravePath) is not AsepriteImporter importer)
            throw new InvalidOperationException($"묘비 Aseprite 에셋을 불러올 수 없습니다: {GravePath}");

        importer.importMode = FileImportModes.AnimatedSprite;
        importer.layerImportMode = LayerImportModes.MergeFrame;
        importer.spritePixelsPerUnit = 32f;
        importer.spriteMeshType = SpriteMeshType.FullRect;
        importer.pivotAlignment = SpriteAlignment.Center;
        importer.includeHiddenLayers = false;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.generateAnimationClips = false;
        importer.generateModelPrefab = false;
        importer.generatePhysicsShape = false;
        importer.SaveAndReimport();
    }

    private static Sprite LoadGraveSprite()
    {
        Sprite graveSprite = AssetDatabase.LoadAllAssetsAtPath(GravePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (graveSprite == null)
            throw new InvalidOperationException("grave.aseprite에서 Sprite를 찾을 수 없습니다.");

        return graveSprite;
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
