using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

// 도트 작업 폴더의 lively 모션 시트를 플레이어·박쥐·뱀 애니메이션으로 덮어쓴다.
// 프레임 길이는 Aseprite 원본(Art/Sources/Lively)의 프레임별 ms 값을 그대로 쓴다.
public static class LivelyMotionSetupTool
{
    private const float PixelsPerUnit = 32f;
    // 기존 박쥐·뱀 Aseprite가 40 PPU(40px 프레임 = 1칸)라 같은 크기를 유지한다.
    private const float MonsterPixelsPerUnit = 40f;
    private const string BatPrefabPath = "Assets/Monsters/Prefabs/Bat.prefab";
    private const string SnakePrefabPath = "Assets/Monsters/Prefabs/Snake.prefab";
    private const float ClipFrameRate = 100f;
    private const string PlayerControllerPath = "Assets/Player/idle-run.controller";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ChessScenePath = "Assets/Scenes/ChessPlaytest.unity";
    private const string BatControllerPath = "Assets/Monsters/Controllers/Bat.controller";
    private const string SnakeControllerPath = "Assets/Monsters/Controllers/Snake.controller";
    private const string ArmadilloControllerPath = "Assets/Monsters/Controllers/ArmadilloBishop.controller";
    private const string KnightControllerPath = "Assets/Monsters/Controllers/Knight.controller";
    private const string RookControllerPath = "Assets/Monsters/Controllers/Rook.controller";
    private const string PawnGuardControllerPath = "Assets/Monsters/Controllers/PawnGuard.controller";
    private const string FireTrailSheet = "Assets/Resources/Effects/fire_trail.png";
    private static readonly string[] QuakeSheets =
        { "Assets/Resources/Effects/knight_quake_crack.png", "Assets/Resources/Effects/knight_quake_dust.png" };

    private sealed class Motion
    {
        public string Sheet;
        public string Clip;
        public int Size;
        public bool Loop;
        public int[] DurationsMs;
        public float PpuOverride;
        // 0이면 프레임 중심. 여왕처럼 발을 점유 칸 바닥에 맞춰야 할 때 세로 기준점(0~1)을 준다.
        public float PivotY;
        public float Ppu => PpuOverride > 0f ? PpuOverride : Size == 50 ? PixelsPerUnit : MonsterPixelsPerUnit;
    }

    private static readonly Motion[] Player =
    {
        new Motion { Sheet = "Assets/Player/Lively/wizard_idle.png", Clip = "Assets/Player/player_idle.anim",
            Size = 50, Loop = true, DurationsMs = new[] { 380, 140, 110, 280, 110, 340, 100, 220, 110, 200, 220, 110 } },
        new Motion { Sheet = "Assets/Player/Lively/wizard_walk.png", Clip = "Assets/Player/player_walk.anim",
            Size = 50, Loop = true, DurationsMs = new[] { 110, 130, 120, 150, 130, 110 } },
        new Motion { Sheet = "Assets/Player/Lively/wizard_attack.png", Clip = "Assets/Player/player_attack.anim",
            Size = 50, DurationsMs = new[] { 70, 90, 60, 100, 110, 170 } },
        new Motion { Sheet = "Assets/Player/Lively/wizard_hit.png", Clip = "Assets/Player/player_hit.anim",
            Size = 50, DurationsMs = new[] { 50, 80, 110, 60, 60, 90, 140 } },
        new Motion { Sheet = "Assets/Player/Lively/wizard_death.png", Clip = "Assets/Player/player_death.anim",
            Size = 50, DurationsMs = new[] { 60, 90, 120, 110, 90, 130, 120, 60, 50, 80, 90, 90, 800 } },
    };

    private static readonly Motion[] Bat =
    {
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_idle.png", Clip = "Assets/Monsters/Animations/Bat/BatIdle.anim",
            Size = 40, Loop = true, DurationsMs = new[] { 90, 50, 60, 70, 90, 90, 50, 60, 70, 90 } },
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_move.png", Clip = "Assets/Monsters/Animations/Bat/BatMove.anim",
            Size = 40, Loop = true, DurationsMs = new[] { 70, 40, 50, 50, 60, 70, 40, 50, 50, 60 } },
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_attack.png", Clip = "Assets/Monsters/Animations/Bat/BatAttack.anim",
            Size = 40, DurationsMs = new[] { 130, 150, 50, 110, 90, 130 } },
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_hit.png", Clip = "Assets/Monsters/Animations/Bat/BatHit.anim",
            Size = 40, DurationsMs = new[] { 50, 80, 100, 60, 60, 90, 130 } },
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_death.png", Clip = "Assets/Monsters/Animations/Bat/BatDeath.anim",
            Size = 40, DurationsMs = new[] { 60, 90, 80, 60, 50, 160, 70, 70, 70, 70, 300 } },
    };

    private static readonly Motion[] Snake =
    {
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_idle.png", Clip = "Assets/Monsters/Animations/Snake/SnakeIdle.anim",
            Size = 40, Loop = true, DurationsMs = new[] { 240, 90, 120, 140, 160, 60, 80, 60, 140, 150, 110, 260, 100, 180, 200 } },
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_move.png", Clip = "Assets/Monsters/Animations/Snake/SnakeMove.anim",
            Size = 40, Loop = true, DurationsMs = new[] { 120, 90, 130, 120, 90, 130 } },
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_attack.png", Clip = "Assets/Monsters/Animations/Snake/SnakeAttack.anim",
            Size = 40, DurationsMs = new[] { 140, 160, 50, 110, 100, 180 } },
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_hit.png", Clip = "Assets/Monsters/Animations/Snake/SnakeHit.anim",
            Size = 40, DurationsMs = new[] { 50, 80, 110, 60, 60, 90, 140 } },
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_death.png", Clip = "Assets/Monsters/Animations/Snake/SnakeDeath.anim",
            Size = 40, DurationsMs = new[] { 60, 90, 120, 110, 180, 70, 70, 70, 70, 300 } },
    };

    // 비숍 아르마딜로: 64px 프레임, 32 PPU(스포너가 타일 안에 맞춰 축소). 이동은 굴러가는 공.
    private static readonly Motion[] Armadillo =
    {
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_idle.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloIdle.anim",
            Size = 64, PpuOverride = 32f, Loop = true, DurationsMs = new[] { 200, 200, 200, 200 } },
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_move.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloMove.anim",
            Size = 64, PpuOverride = 32f, Loop = true, DurationsMs = new[] { 60, 60, 60, 60, 60, 60 } },
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_attack.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloAttack.anim",
            Size = 64, PpuOverride = 32f, DurationsMs = new[] { 80, 100, 120, 140 } },
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_hit.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloHit.anim",
            Size = 64, PpuOverride = 32f, DurationsMs = new[] { 50, 80, 110, 140 } },
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_death.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloDeath.anim",
            Size = 64, PpuOverride = 32f, DurationsMs = new[] { 80, 90, 90, 110, 140, 100, 100, 160 } },
    };

    // 체스 적(퀸 진영): 아르마딜로와 같은 64px 프레임 · 32 PPU. 스포너가 한 칸에 1/2 배율로 맞춘다.
    private static Motion[] ChessMotions(string folder, string prefix, int[] idle, int[] move, int[] attack, int[] hit, int[] death,
        int size = 64, float pivotY = 0f)
    {
        string Sheet(string clip) => $"Assets/Monsters/{folder}/Lively/{prefix}_{clip}.png";
        string Clip(string clip) => $"Assets/Monsters/Animations/{folder}/{folder}{clip}.anim";
        return new[]
        {
            new Motion { Sheet = Sheet("idle"), Clip = Clip("Idle"), Size = size, PpuOverride = 32f, PivotY = pivotY, Loop = true, DurationsMs = idle },
            new Motion { Sheet = Sheet("move"), Clip = Clip("Move"), Size = size, PpuOverride = 32f, PivotY = pivotY, Loop = true, DurationsMs = move },
            new Motion { Sheet = Sheet("attack"), Clip = Clip("Attack"), Size = size, PpuOverride = 32f, PivotY = pivotY, DurationsMs = attack },
            new Motion { Sheet = Sheet("hit"), Clip = Clip("Hit"), Size = size, PpuOverride = 32f, PivotY = pivotY, DurationsMs = hit },
            new Motion { Sheet = Sheet("death"), Clip = Clip("Death"), Size = size, PpuOverride = 32f, PivotY = pivotY, DurationsMs = death },
        };
    }

    // 나이트: 이동 5프레임 합 340ms = MonsterMovement.KnightJumpSeconds. 공격 첫 프레임이 착지 내려찍기다.
    private static readonly Motion[] Knight = ChessMotions("Knight", "knight",
        new[] { 220, 160, 160, 220 }, new[] { 40, 60, 80, 80, 80 }, new[] { 90, 110, 110, 140 },
        new[] { 50, 80, 110, 140 }, new[] { 80, 100, 110, 140, 160, 100, 100, 160 });
    // 룩: 장전은 앞선 턴에 끝났으므로 시위 당김을 짧게 두고 발사 프레임(4번째)을 사선 표시(0.16초) 안에 맞춘다.
    private static readonly Motion[] Rook = ChessMotions("Rook", "rook",
        new[] { 260, 140, 160, 220 }, new[] { 90, 90, 90, 90 }, new[] { 30, 40, 40, 140, 110, 120 },
        new[] { 50, 80, 110, 140 }, new[] { 80, 120, 120, 140, 160, 100, 100, 160 });
    // 근위병: 현재 게임 패턴은 없고 퀸 보스 전투용으로 미리 준비한다. 밀치기는 4번째 프레임(0.3초).
    private static readonly Motion[] PawnGuard = ChessMotions("PawnGuard", "pawn_guard",
        new[] { 240, 160, 160, 240 }, new[] { 90, 90, 110, 90, 90, 110 }, new[] { 70, 80, 150, 80, 110, 100, 90, 120 },
        new[] { 50, 80, 110, 140 }, new[] { 80, 100, 110, 140, 160, 100, 100, 160 });

    // 비숍(향로 사제): 64px · 32 PPU. 이동 6프레임은 미끄러지며 향로를 흔들고 불씨를 떨어뜨린다.
    private const string BishopPriestControllerPath = "Assets/Monsters/Controllers/BishopPriest.controller";
    private static readonly Motion[] BishopPriest = ChessMotions("BishopPriest", "bishop_priest",
        new[] { 260, 200, 260, 200 }, new[] { 70, 70, 70, 70, 70, 70 }, new[] { 70, 90, 110, 120, 120, 140 },
        new[] { 50, 80, 110, 140 }, new[] { 80, 100, 110, 140, 160, 100, 100, 160 });

    // 폭탄병: 64px · 32 PPU. 던지기는 4번째 프레임(70+90+90ms)이지만 폭발은 두 행동 뒤라 피해 시점과 무관하다.
    private const string BomberControllerPath = "Assets/Monsters/Controllers/Bomber.controller";
    private static readonly Motion[] Bomber = ChessMotions("Bomber", "bomber",
        new[] { 260, 120, 120, 200 }, new[] { 90, 90, 110, 90, 90, 110 }, new[] { 70, 90, 90, 120, 120, 140 },
        new[] { 50, 80, 110, 140 }, new[] { 80, 100, 110, 140, 160, 100, 100, 160 });

    // 여왕 보스: 승인 v10 120px 원본 크기(32 PPU). 발(캔버스 아래 4px)이 3×3 점유의 아래 가장자리(중심 -48px)에 오도록
    // 기준점을 (4+48)/120에 둔다. 시전 발동은 4번째 프레임(70+90+90ms).
    private const string QueenControllerPath = "Assets/Monsters/Controllers/Queen.controller";
    private const string BossScenePath = "Assets/Scenes/QueenBossPlaytest.unity";
    private static readonly Motion[] Queen = ChessMotions("Queen", "queen",
        new[] { 320, 220, 200, 300 }, new[] { 320, 220, 200, 300 }, new[] { 70, 90, 90, 130, 120, 150 },
        new[] { 50, 80, 110, 140 }, new[] { 80, 120, 120, 140, 160, 100, 100, 200 }, 120, 52f / 120f);

    // 여왕 기믹: 패턴마다 준비(예고 자세에서 멈춤)와 발동(발사 → 대기 복귀) 클립. 체크메이트는 A·B 타격과 탈진 반복.
    // 시트는 Art/Concepts/ChessCast-v2-clean/queen/queen_gimmicks.py --game 출력(120px · 원본 여왕과 같은 캔버스·발 위치).
    // 프레임 시간은 그 스크립트의 연출 시간(60ms 프레임 = 키포즈 사이 잔상)과 같다.
    private static readonly (string State, Motion Motion)[] QueenGimmicks =
    {
        QueenGimmick("RoyalLines_Windup", "royal_lines_windup", new[] { 60, 160, 300, 60, 260 }),
        QueenGimmick("RoyalLines_Release", "royal_lines_release", new[] { 130, 160, 60, 240 }),
        QueenGimmick("Bombardment_Windup", "bombardment_windup", new[] { 60, 180, 340 }),
        QueenGimmick("Bombardment_Release", "bombardment_release", new[] { 60, 150, 200, 60, 240 }),
        QueenGimmick("Guards_Windup", "guard_advance_windup", new[] { 60, 200, 180, 240 }),
        QueenGimmick("Guards_Release", "guard_advance_release", new[] { 200, 200, 60, 240 }),
        QueenGimmick("Fortress_Windup", "fortress_windup", new[] { 60, 140, 220, 160, 300 }),
        QueenGimmick("Fortress_Release", "fortress_release", new[] { 130, 60, 240 }),
        QueenGimmick("Checkmate_Windup", "checkmate_windup", new[] { 60, 160, 60, 380, 260 }),
        QueenGimmick("Checkmate_StrikeA", "checkmate_strike_a", new[] { 60, 150 }),
        QueenGimmick("Checkmate_StrikeB", "checkmate_strike_b", new[] { 60, 150 }),
        QueenGimmick("Checkmate_Exhausted", "checkmate_exhausted", new[] { 440, 440 }, true),
    };

    private static (string, Motion) QueenGimmick(string state, string sheet, int[] durations, bool loop = false) =>
        (state, new Motion
        {
            Sheet = $"Assets/Monsters/Queen/Lively/queen_gm_{sheet}.png",
            Clip = $"Assets/Monsters/Animations/Queen/Queen{state}.anim",
            Size = 120, PpuOverride = 32f, PivotY = 52f / 120f, Loop = loop, DurationsMs = durations
        });

    // 기믹 상태는 QueenBossBehaviour가 패턴 단계에 맞춰 Animator.Play로 직접 고른다.
    // 준비 클립은 마지막 프레임(예고 자세)에서 멈추고, 발동 클립은 끝나면 대기로, B 타격은 탈진으로 넘어간다.
    private static void ConfigureQueenGimmicks(AnimatorController controller)
    {
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.defaultState;
        var states = new Dictionary<string, AnimatorState>();
        for (int i = 0; i < QueenGimmicks.Length; i++)
        {
            (string name, Motion motion) = QueenGimmicks[i];
            AnimatorState state = State(machine, name, Build(motion), new Vector3(820, -320 + i * 60));
            foreach (AnimatorStateTransition transition in state.transitions.ToArray()) state.RemoveTransition(transition);
            states[name] = state;
        }
        foreach (KeyValuePair<string, AnimatorState> pair in states)
        {
            AnimatorState next = pair.Key.EndsWith("_Release") ? idle
                : pair.Key == "Checkmate_StrikeB" ? states["Checkmate_Exhausted"] : null;
            if (next == null) continue;
            AnimatorStateTransition back = Immediate(pair.Value.AddTransition(next));
            back.hasExitTime = true;
            back.exitTime = 1f;
        }
        EditorUtility.SetDirty(controller);
    }

    [MenuItem("Tools/Art/Apply Lively Motions")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Play를 종료한 뒤 모션을 적용하세요.");

        EnsureFolder("Assets/Monsters/Animations", "Bat");
        EnsureFolder("Assets/Monsters/Animations", "Snake");

        AnimationClip[] playerClips = Player.Select(Build).ToArray();
        AnimationClip[] batClips = Bat.Select(Build).ToArray();
        AnimationClip[] snakeClips = Snake.Select(Build).ToArray();
        AnimationClip[] armadilloClips = Armadillo.Select(Build).ToArray();
        foreach (string folder in new[] { "Knight", "Rook", "PawnGuard", "Queen", "Bomber", "BishopPriest" })
            EnsureFolder("Assets/Monsters/Animations", folder);
        AnimationClip[] knightClips = Knight.Select(Build).ToArray();
        AnimationClip[] rookClips = Rook.Select(Build).ToArray();
        AnimationClip[] pawnClips = PawnGuard.Select(Build).ToArray();
        AnimationClip[] queenClips = Queen.Select(Build).ToArray();
        AnimationClip[] bomberClips = Bomber.Select(Build).ToArray();
        AnimationClip[] bishopClips = BishopPriest.Select(Build).ToArray();
        ConfigureSheet(new Motion { Sheet = FireTrailSheet, Size = 32, PpuOverride = 32f });
        foreach (string sheet in QuakeSheets)
            ConfigureSheet(new Motion { Sheet = sheet, Size = 32, PpuOverride = 32f });

        AnimatorController player = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        if (player == null)
            throw new InvalidOperationException("플레이어 Animator Controller를 찾을 수 없습니다.");
        ConfigureController(player, playerClips, "player_idle", "player_walk");
        AnimatorController bat = LoadOrCreate(BatControllerPath);
        ConfigureController(bat, batClips, "Idle", "Move");
        AnimatorController snake = LoadOrCreate(SnakeControllerPath);
        ConfigureController(snake, snakeClips, "Idle", "Move");
        AnimatorController armadillo = LoadOrCreate(ArmadilloControllerPath);
        ConfigureController(armadillo, armadilloClips, "Idle", "Move");
        AnimatorController knight = LoadOrCreate(KnightControllerPath);
        ConfigureController(knight, knightClips, "Idle", "Move");
        AnimatorController rook = LoadOrCreate(RookControllerPath);
        ConfigureController(rook, rookClips, "Idle", "Move");
        AnimatorController pawnGuard = LoadOrCreate(PawnGuardControllerPath);
        ConfigureController(pawnGuard, pawnClips, "Idle", "Move");
        AnimatorController bishopPriest = LoadOrCreate(BishopPriestControllerPath);
        ConfigureController(bishopPriest, bishopClips, "Idle", "Move");
        AnimatorController bomber = LoadOrCreate(BomberControllerPath);
        ConfigureController(bomber, bomberClips, "Idle", "Move");
        AnimatorController queen = LoadOrCreate(QueenControllerPath);
        ConfigureController(queen, queenClips, "Idle", "Move");
        ConfigureQueenGimmicks(queen);
        ApplyToBossScene(new QueenBossBehaviour.Visuals
        {
            QueenSprite = LoadFrames(Queen[0])[0], QueenMotion = queen,
            GuardSprite = LoadFrames(PawnGuard[0])[0], GuardMotion = pawnGuard
        });
        var chess = new ChessVisuals
        {
            KnightIdle = LoadFrames(Knight[0])[0], Knight = knight,
            RookIdle = LoadFrames(Rook[0])[0], Rook = rook,
            BomberIdle = LoadFrames(Bomber[0])[0], Bomber = bomber,
            BishopIdle = LoadFrames(BishopPriest[0])[0], Bishop = bishopPriest,
            Queen = new QueenBossBehaviour.Visuals
            {
                QueenSprite = LoadFrames(Queen[0])[0], QueenMotion = queen,
                GuardSprite = LoadFrames(PawnGuard[0])[0], GuardMotion = pawnGuard
            }
        };

        Sprite batIdle = LoadFrames(Bat[0])[0];
        Sprite playerIdle = LoadFrames(Player[0])[0];
        ApplyToScene(SampleScenePath, batIdle, bat, playerIdle, chess);
        ApplyToScene(ChessScenePath, batIdle, bat, playerIdle, chess);
        // 일반 웨이브(4방향·8방향)는 기존 Bat/Snake 프리팹으로 생성되므로 외형만 교체한다.
        ApplyToPrefab(BatPrefabPath, batIdle, bat);
        ApplyToPrefab(SnakePrefabPath, LoadFrames(Snake[0])[0], snake);
        AssetDatabase.SaveAssets();
        Validate(playerClips.Concat(batClips).Concat(snakeClips).Concat(armadilloClips)
            .Concat(knightClips).Concat(rookClips).Concat(pawnClips).Concat(queenClips).Concat(bomberClips).Concat(bishopClips).ToArray(),
            player, bat, snake, armadillo, knight, rook, pawnGuard, queen, bomber, bishopPriest);
    }

    private sealed class ChessVisuals
    {
        public Sprite KnightIdle;
        public RuntimeAnimatorController Knight;
        public Sprite RookIdle;
        public RuntimeAnimatorController Rook;
        public Sprite BomberIdle;
        public RuntimeAnimatorController Bomber;
        public Sprite BishopIdle;
        public RuntimeAnimatorController Bishop;
        // 본 게임 마지막 웨이브의 여왕 보스(보스 테스트 씬과 같은 외형)
        public QueenBossBehaviour.Visuals Queen;
    }

    // Temp/LivelyMotionSetup.pending 이 있으면 다음 스크립트 로드 때 한 번만 자동 적용한다.
    private const string PendingMarker = "Temp/LivelyMotionSetup.pending";

    [InitializeOnLoadMethod]
    private static void RunPendingOnce()
    {
        if (System.IO.File.Exists(PendingMarker)) EditorApplication.update += TryRunPending;
    }

    private static void TryRunPending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating) return;
        EditorApplication.update -= TryRunPending;
        System.IO.File.Delete(PendingMarker);
        try { Apply(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public static void RunBatch()
    {
        try
        {
            Apply();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static AnimationClip Build(Motion motion)
    {
        ConfigureSheet(motion);
        Sprite[] frames = LoadFrames(motion);
        if (frames.Length != motion.DurationsMs.Length)
            throw new InvalidOperationException(
                $"{motion.Sheet} 프레임 {frames.Length}장과 길이 {motion.DurationsMs.Length}개가 다릅니다.");

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motion.Clip);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, motion.Clip);
        }

        clip.frameRate = ClipFrameRate;
        var keys = new List<ObjectReferenceKeyframe>();
        int elapsed = 0;
        for (int i = 0; i < frames.Length; i++)
        {
            keys.Add(new ObjectReferenceKeyframe { time = elapsed / 1000f, value = frames[i] });
            elapsed += motion.DurationsMs[i];
        }
        // 마지막 프레임도 자기 길이만큼 유지되도록 끝 시각에 같은 Sprite를 한 번 더 둔다.
        keys.Add(new ObjectReferenceKeyframe { time = elapsed / 1000f, value = frames[frames.Length - 1] });

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = motion.Loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void ConfigureSheet(Motion motion)
    {
        AssetDatabase.ImportAsset(motion.Sheet, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(motion.Sheet) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"PNG 시트를 불러올 수 없습니다: {motion.Sheet}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = motion.Ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        TextureImporterSettings textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(textureSettings);
        importer.SaveAndReimport();

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
            throw new InvalidOperationException("Sprite 데이터 공급자를 만들 수 없습니다.");
        dataProvider.InitSpriteEditorDataProvider();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(motion.Sheet);
        int count = texture.width / motion.Size;
        string prefix = System.IO.Path.GetFileNameWithoutExtension(motion.Sheet);
        SpriteRect[] previous = dataProvider.GetSpriteRects();
        var rects = new SpriteRect[count];
        for (int i = 0; i < count; i++)
        {
            string name = $"{prefix}_{i:00}";
            SpriteRect old = previous.FirstOrDefault(rect => rect.name == name);
            // 프레임 중심을 기준점으로 둬야 기존 시트(50px 프레임의 가운데 40px)와 발 위치가 같다.
            rects[i] = new SpriteRect
            {
                name = name,
                rect = new Rect(i * motion.Size, 0f, motion.Size, motion.Size),
                alignment = motion.PivotY > 0f ? SpriteAlignment.Custom : SpriteAlignment.Center,
                pivot = new Vector2(0.5f, motion.PivotY > 0f ? motion.PivotY : 0.5f),
                border = Vector4.zero,
                spriteID = old != null ? old.spriteID : GUID.Generate()
            };
        }
        dataProvider.SetSpriteRects(rects);
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadFrames(Motion motion) => AssetDatabase.LoadAllAssetsAtPath(motion.Sheet)
        .OfType<Sprite>()
        .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
        .ToArray();

    private static AnimatorController LoadOrCreate(string path)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        return controller != null ? controller : AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    // clips 순서: idle, move, attack, hit, death
    private static void ConfigureController(
        AnimatorController controller, AnimationClip[] clips, string idleName, string moveName)
    {
        AddParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = State(machine, idleName, clips[0], new Vector3(300, 0));
        AnimatorState move = State(machine, moveName, clips[1], new Vector3(300, -100));
        AnimatorState attack = State(machine, "Attack", clips[2], new Vector3(560, -160));
        AnimatorState hit = State(machine, "Hit", clips[3], new Vector3(560, -60));
        AnimatorState death = State(machine, ActorMotion.DeathState, clips[4], new Vector3(560, 60));
        machine.defaultState = idle;

        foreach (AnimatorState state in new[] { idle, move, attack, hit, death })
            foreach (AnimatorStateTransition transition in state.transitions.ToArray())
                state.RemoveTransition(transition);
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
            machine.RemoveAnyStateTransition(transition);

        Immediate(idle.AddTransition(move)).AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        Immediate(move.AddTransition(idle)).AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        foreach (AnimatorState action in new[] { attack, hit })
        {
            // 다음 이동 행동은 이전 행동의 남은 모션에 가로막히지 않는다.
            Immediate(action.AddTransition(move)).AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            AnimatorStateTransition back = Immediate(action.AddTransition(idle));
            back.hasExitTime = true;
            back.exitTime = 1f;
        }

        AnimatorStateTransition toDeath = Immediate(machine.AddAnyStateTransition(death));
        toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
        Immediate(machine.AddAnyStateTransition(hit)).AddCondition(AnimatorConditionMode.If, 0, "Hit");
        Immediate(machine.AddAnyStateTransition(attack)).AddCondition(AnimatorConditionMode.If, 0, "Attack");
        EditorUtility.SetDirty(controller);
    }

    private static AnimatorStateTransition Immediate(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
        transition.exitTime = 0f;
        return transition;
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(s => s.name == name);
        if (state == null) state = machine.AddState(name, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (controller.parameters.All(p => p.name != name)) controller.AddParameter(name, type);
    }

    private static void ApplyToScene(string scenePath, Sprite batIdle, RuntimeAnimatorController bat, Sprite playerIdle,
        ChessVisuals chess)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
        if (openedForSetup)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (MonsterSpawner spawner in roots.SelectMany(r => r.GetComponentsInChildren<MonsterSpawner>(true)))
            {
                var serialized = new SerializedObject(spawner);
                serialized.FindProperty("chessMonsterSprite").objectReferenceValue = batIdle;
                serialized.FindProperty("chessMonsterAnimatorController").objectReferenceValue = bat;
                serialized.FindProperty("knightMonsterSprite").objectReferenceValue = chess.KnightIdle;
                serialized.FindProperty("knightMonsterAnimatorController").objectReferenceValue = chess.Knight;
                serialized.FindProperty("rookMonsterSprite").objectReferenceValue = chess.RookIdle;
                serialized.FindProperty("rookMonsterAnimatorController").objectReferenceValue = chess.Rook;
                serialized.FindProperty("bomberMonsterSprite").objectReferenceValue = chess.BomberIdle;
                serialized.FindProperty("bomberMonsterAnimatorController").objectReferenceValue = chess.Bomber;
                // 비숍은 아르마딜로에서 향로 사제로 교체했다(불길 규칙은 그대로).
                serialized.FindProperty("bishopMonsterSprite").objectReferenceValue = chess.BishopIdle;
                serialized.FindProperty("bishopMonsterAnimatorController").objectReferenceValue = chess.Bishop;
                SerializedProperty queenVisuals = serialized.FindProperty("queenBossVisuals");
                queenVisuals.FindPropertyRelative("QueenSprite").objectReferenceValue = chess.Queen.QueenSprite;
                queenVisuals.FindPropertyRelative("QueenMotion").objectReferenceValue = chess.Queen.QueenMotion;
                queenVisuals.FindPropertyRelative("GuardSprite").objectReferenceValue = chess.Queen.GuardSprite;
                queenVisuals.FindPropertyRelative("GuardMotion").objectReferenceValue = chess.Queen.GuardMotion;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (ChessPlaytest playtest in roots.SelectMany(r => r.GetComponentsInChildren<ChessPlaytest>(true)))
            {
                var serialized = new SerializedObject(playtest);
                serialized.FindProperty("monsterSprite").objectReferenceValue = batIdle;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            // 에디터 정지 화면에서도 새 대기 첫 프레임이 보이게 한다.
            foreach (Move player in roots.SelectMany(r => r.GetComponentsInChildren<Move>(true)))
            {
                SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;
                var serialized = new SerializedObject(renderer);
                serialized.FindProperty("m_Sprite").objectReferenceValue = playerIdle;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"씬을 저장하지 못했습니다: {scenePath}");
        }
        finally
        {
            if (openedForSetup)
                EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
        }
    }

    // 보스 테스트 씬의 QueenBossPlaytest에 여왕·근위병 그림과 모션을 연결한다.
    private static void ApplyToBossScene(QueenBossBehaviour.Visuals visuals)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(BossScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
        if (openedForSetup)
            scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (QueenBossPlaytest playtest in scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<QueenBossPlaytest>(true)))
            {
                var serialized = new SerializedObject(playtest);
                SerializedProperty property = serialized.FindProperty("visuals");
                property.FindPropertyRelative("QueenSprite").objectReferenceValue = visuals.QueenSprite;
                property.FindPropertyRelative("QueenMotion").objectReferenceValue = visuals.QueenMotion;
                property.FindPropertyRelative("GuardSprite").objectReferenceValue = visuals.GuardSprite;
                property.FindPropertyRelative("GuardMotion").objectReferenceValue = visuals.GuardMotion;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"씬을 저장하지 못했습니다: {BossScenePath}");
        }
        finally
        {
            if (openedForSetup)
                EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
        }
    }

    private static void ApplyToPrefab(string path, Sprite idle, RuntimeAnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (renderer == null || animator == null)
                throw new InvalidOperationException($"프리팹에 SpriteRenderer/Animator가 없습니다: {path}");
            renderer.sprite = idle;
            animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    private static void Validate(AnimationClip[] clips, params AnimatorController[] controllers)
    {
        foreach (Motion motion in Player.Concat(Bat).Concat(Snake).Concat(Armadillo)
            .Concat(Knight).Concat(Rook).Concat(PawnGuard).Concat(Queen).Concat(Bomber).Concat(BishopPriest))
        {
            TextureImporter importer = AssetImporter.GetAtPath(motion.Sheet) as TextureImporter;
            Sprite[] frames = LoadFrames(motion);
            if (importer == null || !Mathf.Approximately(importer.spritePixelsPerUnit, motion.Ppu)
                || importer.filterMode != FilterMode.Point
                || frames.Length != motion.DurationsMs.Length
                || frames.Any(f => f.rect.width != motion.Size || f.rect.height != motion.Size))
                throw new InvalidOperationException($"시트 검증 실패: {motion.Sheet}");
        }
        foreach (AnimatorController controller in controllers)
        {
            string[] names = controller.parameters.Select(p => p.name).ToArray();
            if (!new[] { "IsMoving", "Attack", "Hit", "Die" }.All(names.Contains)
                || controller.animationClips.Length < 5)
                throw new InvalidOperationException($"컨트롤러 검증 실패: {controller.name}");
        }
        Debug.Log($"LIVELY_MOTION_SETUP_PASS clips={clips.Length} controllers={controllers.Length} "
            + string.Join(" ", clips.Select(c => $"{c.name}={c.length:0.00}s")));
    }
}
