using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// 한 배치로 두 판만 진행한다. 테스트 조건과 최소 기록을 관리하며 전투 판정은 맡지 않는다.
[DisallowMultipleComponent]
public sealed class ChessPlaytest : MonoBehaviour
{
    public const int TurnLimit = 20;
    public const float RoundSeconds = 180f;
    private static readonly Vector3Int PlayerStart = new Vector3Int(5, 5, 0);
    private static readonly Vector3Int[] EnemyStarts =
    {
        new Vector3Int(3, 6, 0), new Vector3Int(2, 2, 0), new Vector3Int(5, 10, 0)
    };
    private static readonly Color[] EnemyColors =
    {
        new Color(0.2f, 0.85f, 1f), new Color(1f, 0.75f, 0.15f), new Color(1f, 0.3f, 0.7f)
    };

    [Header("두 판 모두 같은 값으로 고정됩니다 (Play 전에 조절)")]
    [SerializeField, Range(2, 6)] private int monsterHealth = 3;
    [SerializeField, Range(1, 3)] private int knightActionInterval = 1;
    [SerializeField, Range(1, 4)] private int bishopMoveDistance = 2;
    [SerializeField, Range(1, 5)] private int bishopOrbitDistance = 3;
    [SerializeField, Range(1, 6)] private int bishopFireTurns = 3;
    [SerializeField, Min(1)] private int bishopFireDamage = 1;
    [Header("독립 Sprite 참조")]
    [SerializeField] private Sprite monsterSprite;

    private MonsterSpawner spawner;
    private Move player;
    private GridManager grid;
    private ProjectileManager projectiles;
    private readonly List<MonsterMovement> encounter = new List<MonsterMovement>();
    private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
    private readonly string[] summaries = new string[2];
    private readonly int[] exposure = new int[3];
    private int sessionHealth;
    private int sessionKnightInterval;
    private int sessionBishopTravel;
    private int sessionBishopOrbit;
    private float startTime;
    private bool ready;
    private bool running;
    private bool showResult = true;
    private string result;
    private string momentOne = "";
    private string momentTwo = "";
    private string unexpected = "";
    private string participantQuote = "";
    private string nextChange = "";
    private Sprite markerSprite;
    private Texture2D markerTexture;
    private Font uiFont;
    private bool previousRunInBackground;

    public int RoundNumber { get; private set; } = 1;
    public int TurnCount { get; private set; }
    public bool IsRunning => running;
    public bool IsReady => ready;
    public IReadOnlyList<MonsterMovement> Encounter => encounter;
    public string Result => result;

    public void Initialize(MonsterSpawner owner, Move controlledPlayer, GridManager map, ProjectileManager manager)
    {
        spawner = owner;
        player = controlledPlayer;
        grid = map;
        projectiles = manager;
        previousRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        sessionHealth = monsterHealth;
        sessionKnightInterval = knightActionInterval;
        sessionBishopTravel = bishopMoveDistance;
        sessionBishopOrbit = bishopOrbitDistance;
        spawner.ConfigureFire(bishopFireTurns, bishopFireDamage);
        uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Arial" }, 17);
        // 기존 Tile 에셋의 색 잠금은 씬 로드 시 복구되므로 테스트 맵에서만 해제한다.
        foreach (Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
        {
            grid.GroundTilemap.SetTileFlags(cell, TileFlags.None);
            bool wall = !grid.IsWalkableCell(cell);
            grid.GroundTilemap.SetColor(cell, wall ? new Color(0.18f, 0.2f, 0.25f) :
                (cell.x + cell.y) % 2 == 0 ? Color.white : new Color(0.8f, 0.85f, 0.9f));
        }
        CreateMarkerSprite();
        spawner.WorldTurnCompleted += OnWorldTurnCompleted;
        PrepareRound();
    }

    private void PrepareRound()
    {
        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(PlayerStart);
        player.SetInputEnabled(false);
        encounter.Clear();
        for (int i = 0; i < 3; i++)
        {
            exposure[i] = 0;
            encounter.Add(spawner.SpawnPlaytestMonster(
                (MonsterMovementPattern)((int)MonsterMovementPattern.Knight + i),
                EnemyStarts[i], monsterSprite, EnemyColors[i], sessionHealth, i,
                sessionKnightInterval, sessionBishopTravel, sessionBishopOrbit));
        }
        TurnCount = 0;
        ready = true;
        running = false;
        result = null;
        showResult = true;
        RefreshMarkers();
    }

    public void StartRound()
    {
        if (!ready) return;
        ready = false;
        running = true;
        startTime = Time.unscaledTime;
        player.SetInputEnabled(true);
    }

    public void StartSecondRound()
    {
        if (running || ready || RoundNumber != 1) return;
        RoundNumber = 2;
        PrepareRound();
    }

    private void Update()
    {
        if (player == null) return;
        if (!running && !ready && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            showResult = !showResult;
        // 제한 시간은 세션 종료용이다. 적/탄의 게임 규칙은 시간으로 진행시키지 않는다.
        if (running && Time.unscaledTime - startTime >= RoundSeconds
            && !spawner.IsWorldTurnInProgress && player.CanAct)
            EndRound("3분 종료");
    }

    private void OnWorldTurnCompleted()
    {
        if (!running) return;
        TurnCount++;
        int alive = 0;
        for (int i = 0; i < encounter.Count; i++)
        {
            MonsterMovement monster = encounter[i];
            if (monster == null) continue;
            exposure[i] = monster.GetComponent<ChessMonsterBehaviour>().PatternExecutions;
            if (!monster.IsDead) alive++;
        }
        if (player.CurrentHealth <= 0) EndRound("플레이어 사망");
        else if (alive == 0) EndRound("모든 몬스터 처치");
        else if (TurnCount >= TurnLimit) EndRound("20턴 종료");
        else if (Time.unscaledTime - startTime >= RoundSeconds) EndRound("3분 종료");
        RefreshMarkers();
    }

    private void EndRound(string reason)
    {
        running = false;
        result = reason;
        player.SetInputEnabled(false);
        summaries[RoundNumber - 1] = $"{RoundNumber}회차: {reason}, {TurnCount}턴, 체력 {player.CurrentHealth}/10"
            + $" / 패턴 실행 N {exposure[0]}, B {exposure[1]}, R {exposure[2]}";
        Debug.Log("CHESS_PLAYTEST_ROUND: " + summaries[RoundNumber - 1]);
    }

    private void CreateMarkerSprite()
    {
        markerTexture = new Texture2D(32, 32) { filterMode = FilterMode.Point, name = "ChessThreatOutline" };
        Color[] pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = x < 2 || x > 29 || y < 2 || y > 29 ? Color.white : Color.clear;
        markerTexture.SetPixels(pixels);
        markerTexture.Apply();
        markerSprite = Sprite.Create(markerTexture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f);
    }

    private void RefreshMarkers()
    {
        int index = 0;
        for (int i = 0; i < encounter.Count; i++)
        {
            MonsterMovement monster = encounter[i];
            if (monster == null || monster.IsDead) continue;
            ChessMonsterBehaviour behaviour = monster.GetComponent<ChessMonsterBehaviour>();
            foreach (Vector3Int cell in behaviour.GetThreatCells(spawner.IsOccupied))
            {
                if (index == markers.Count)
                {
                    GameObject marker = new GameObject("ThreatTile");
                    marker.transform.SetParent(transform, false);
                    SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
                    renderer.sprite = markerSprite;
                    renderer.sortingOrder = 4;
                    markers.Add(renderer);
                }
                markers[index].gameObject.SetActive(true);
                markers[index].transform.position = grid.GetCellCenterWorld(cell);
                markers[index].transform.localScale = Vector3.one * (0.96f - i * 0.1f);
                markers[index].color = EnemyColors[i];
                index++;
            }
        }
        for (; index < markers.Count; index++) markers[index].gameObject.SetActive(false);
    }

    private string FacingArrow(MonsterMovement monster)
    {
        Vector3 delta = player.transform.position - monster.transform.position;
        int x = delta.x > 0.1f ? 1 : delta.x < -0.1f ? -1 : 0;
        int y = delta.y > 0.1f ? 1 : delta.y < -0.1f ? -1 : 0;
        if (x == 0) return y >= 0 ? "↑" : "↓";
        if (y == 0) return x > 0 ? "→" : "←";
        return x > 0 ? (y > 0 ? "↗" : "↘") : (y > 0 ? "↖" : "↙");
    }

    private void OnGUI()
    {
        if (player == null) return;
        bool prepareSecondRound = false;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1280f, Screen.height / 800f, 1));
        GUIStyle text = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 17, wordWrap = true };
        GUIStyle title = new GUIStyle(text) { fontSize = 23, fontStyle = FontStyle.Bold };
        GUIStyle button = new GUIStyle(GUI.skin.button) { font = uiFont, fontSize = 17, wordWrap = true };
        GUIStyle field = new GUIStyle(GUI.skin.textArea) { font = uiFont, fontSize = 15, wordWrap = true };
        Camera camera = Camera.main;
        for (int i = 0; i < encounter.Count; i++)
        {
            MonsterMovement monster = encounter[i];
            if (monster == null || monster.IsDead || camera == null) continue;
            Vector3 point = camera.WorldToScreenPoint(monster.transform.position);
            GUI.color = EnemyColors[i];
            GUI.Label(new Rect(point.x * 1280f / Screen.width - 28, (Screen.height - point.y) * 800f / Screen.height - 48, 95, 30),
                $"{new[] { "N", "B", "R" }[i]} {FacingArrow(monster)} {monster.GetComponent<CharacterHealth>().CurrentHealth}", title);
        }
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(936, 16, 328, 768), GUI.skin.box);
        GUILayout.Label("체스 몬스터 · 짧은 테스트", title);
        GUILayout.Label($"{RoundNumber}/2회차  ·  {TurnCount}/20턴  ·  HP {player.CurrentHealth}/10", text);
        GUILayout.Label(running ? $"남은 시간 {Mathf.Max(0, Mathf.CeilToInt(RoundSeconds - (Time.unscaledTime - startTime)))}초" : "같은 배치 · 같은 수치", text);
        if (running || ready || RoundNumber == 1)
        {
            GUILayout.Space(8);
            GUILayout.Label("S 이동 / A 공격 → 화살표 타일 클릭\n우클릭 취소 · 대각선 포함 8방향", text);
            GUILayout.Space(8);
            GUILayout.Label("하늘색 N: 착지 십자 공격\n노란색 B: 대각선 이동·불길\n분홍색 R: 추적 이동 + 사격\n테두리는 현재 위치 기준 예상\n불길: 진입 피해 / 숫자는 남은 턴", text);
            for (int i = 0; i < encounter.Count; i++)
            {
                if (encounter[i] != null && !encounter[i].IsDead)
                    GUILayout.Label(encounter[i].GetComponent<ChessMonsterBehaviour>().StatusText, text);
            }
        }
        GUILayout.Space(8);
        if (running)
        {
            GUILayout.Label($"선택: {player.SelectionMode}", text);
            if (GUILayout.Button("이동 선택 (S)", button)) player.SelectMoveMode();
            if (GUILayout.Button("공격 선택 (A)", button)) player.SelectAttackMode();
        }
        if (ready)
        {
            GUILayout.Label("이동 또는 공격을 선택해 몬스터를 모두 처치하세요. 각 판은 최대 3분/20턴입니다.", text);
            if (GUILayout.Button($"{RoundNumber}회차 시작", button, GUILayout.Height(40))) StartRound();
        }
        else if (!running)
        {
            GUILayout.Label("ESC: 결과 표시/숨기기", text);
            if (showResult)
            {
                GUILayout.Label(result, title);
                if (RoundNumber == 1 && GUILayout.Button("같은 조건으로 2회차 준비", button, GUILayout.Height(40))) prepareSecondRound = true;
                if (RoundNumber == 2)
                {
                    GUILayout.Label("선택 장면 2개 · 행동을 고른 이유", text);
                    momentOne = GUILayout.TextArea(momentOne, field, GUILayout.Height(35));
                    momentTwo = GUILayout.TextArea(momentTwo, field, GUILayout.Height(35));
                    GUILayout.Label("예상과 달랐던 장면", text);
                    unexpected = GUILayout.TextArea(unexpected, field, GUILayout.Height(35));
                    GUILayout.Label("계속/중단하고 싶었던 이유 (원문)", text);
                    participantQuote = GUILayout.TextArea(participantQuote, field, GUILayout.Height(35));
                    GUILayout.Label("다음 변경 1개 또는 판단 보류 이유", text);
                    nextChange = GUILayout.TextArea(nextChange, field, GUILayout.Height(35));
                    if (GUILayout.Button("이번 테스트 기록 복사", button)) GUIUtility.systemCopyBuffer = BuildRecord();
                }
            }
        }
        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
        if (prepareSecondRound) StartSecondRound();
    }

    public string BuildRecord()
    {
        return $"Grid-Caster 체스 패턴 테스트\n고정 배치 P(5,5), N(3,6), B(2,2), R(5,10)"
            + $"\n설정: 적 HP {sessionHealth}, N 간격 {sessionKnightInterval}, B 이동 {sessionBishopTravel}, B 거리 {sessionBishopOrbit}, 불길 {bishopFireTurns}턴/피해 {bishopFireDamage}"
            + $"\n{summaries[0]}\n{summaries[1]}\n선택1: {momentOne}\n선택2: {momentTwo}"
            + $"\n예상과 다른 장면: {unexpected}\n참가자 표현: {participantQuote}\n다음 변경/보류: {nextChange}";
    }

    private void OnDestroy()
    {
        Application.runInBackground = previousRunInBackground;
        if (spawner != null) spawner.WorldTurnCompleted -= OnWorldTurnCompleted;
        if (markerSprite != null) Destroy(markerSprite);
        if (markerTexture != null) Destroy(markerTexture);
        if (uiFont != null) Destroy(uiFont);
    }
}
