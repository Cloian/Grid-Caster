using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 도주·저격 루프 대응 프로토타입을 SampleScene에 적용한다.
// - 박쥐·뱀을 폰 하나로 통합: 뱀(4방향) 프리팹을 복제해 근위병 그림·모션으로 바꾼 Pawn.prefab을 만들고 웨이브 프리팹을 교체한다.
// - 플레이어에 주도권(InitiativeTracker)을 붙인다. 여왕의 시계·폭탄병은 MonsterSpawner 설정과 웨이브 구성에서 켜진다.
// 먼저 Tools/Art/Apply Lively Motions로 근위병·폭탄병 시트와 컨트롤러를 만들어 둬야 한다.
public static class TempoPrototypeSetupTool
{
    private const string SnakePrefabPath = "Assets/Monsters/Prefabs/Snake.prefab";
    private const string PawnPrefabPath = "Assets/Monsters/Prefabs/Pawn.prefab";
    private const string PawnSheet = "Assets/Monsters/PawnGuard/Lively/pawn_guard_idle.png";
    private const string PawnControllerPath = "Assets/Monsters/Controllers/PawnGuard.controller";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Playtest/Apply Tempo Prototype")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Play를 종료한 뒤 적용하세요.");
        MonsterMovement pawn = CreatePawnPrefab();
        ApplyToSampleScene(pawn);
        AssetDatabase.SaveAssets();
        Debug.Log("TEMPO_PROTOTYPE_SETUP_PASS pawn=" + PawnPrefabPath);
    }

    private static MonsterMovement CreatePawnPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PawnPrefabPath) == null
            && !AssetDatabase.CopyAsset(SnakePrefabPath, PawnPrefabPath))
            throw new InvalidOperationException("뱀 프리팹을 복제하지 못했습니다.");
        Sprite idle = AssetDatabase.LoadAllAssetsAtPath(PawnSheet).OfType<Sprite>()
            .OrderBy(s => s.name, StringComparer.Ordinal).FirstOrDefault();
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PawnControllerPath);
        if (idle == null || controller == null)
            throw new InvalidOperationException("근위병 시트·컨트롤러가 없습니다. Tools/Art/Apply Lively Motions를 먼저 실행하세요.");

        GameObject root = PrefabUtility.LoadPrefabContents(PawnPrefabPath);
        try
        {
            root.name = "Pawn";
            root.GetComponentInChildren<SpriteRenderer>(true).sprite = idle;
            root.GetComponentInChildren<Animator>(true).runtimeAnimatorController = controller;
            // 64px 시트는 나이트·룩처럼 정확히 1/2 배율로 한 칸에 맞춘다(타일과 같은 화면 픽셀 격자).
            root.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var serialized = new SerializedObject(root.GetComponent<MonsterMovement>());
            serialized.FindProperty("movementPattern").enumValueIndex = (int)MonsterMovementPattern.CardinalFour;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PawnPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<MonsterMovement>(PawnPrefabPath);
    }

    private static void ApplyToSampleScene(MonsterMovement pawn)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(SampleScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (MonsterSpawner spawner in roots.SelectMany(r => r.GetComponentsInChildren<MonsterSpawner>(true)))
            {
                var serialized = new SerializedObject(spawner);
                SerializedProperty prefabs = serialized.FindProperty("monsterPrefabs");
                prefabs.arraySize = 1;
                prefabs.GetArrayElementAtIndex(0).objectReferenceValue = pawn;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (Move player in roots.SelectMany(r => r.GetComponentsInChildren<Move>(true)))
                if (player.GetComponent<InitiativeTracker>() == null)
                    player.gameObject.AddComponent<InitiativeTracker>();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("SampleScene을 저장하지 못했습니다.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
