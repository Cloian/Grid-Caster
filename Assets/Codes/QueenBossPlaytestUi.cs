using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 테스트 조작과 예고 표시만 담당한다. 시간 경과로 전투 규칙을 진행하지 않는다.
public sealed class QueenBossPlaytestUi : MonoBehaviour
{
    [SerializeField] private QueenBossPlaytest playtest;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private Camera worldCamera;
    private RectTransform canvas;
    private TMP_Text traitLabel, artLabel, levelLabel, buildLabel, phaseLabel, status, detail, outcome, charge;
    private GameObject pausePanel;
    private Button startButton;
    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly List<TMP_Text> labels = new List<TMP_Text>();
    private string lastSignature;
    private int traitIndex, artIndex = 3, level = 3;
    private bool strong, phaseTwo;
    private static readonly string[] Traits = { "double_cast", "pierce", "damage_boost", "knockback" };
    private static readonly string[] TraitNames = { "더블 캐스트", "관통", "범위 공격", "넉백" };
    private static readonly PlayerMovementArt[] Arts = { PlayerMovementArt.None, PlayerMovementArt.Knight, PlayerMovementArt.Bishop, PlayerMovementArt.Rook };
    private static readonly string[] ArtNames = { "이동술 없음", "나이트 도약", "비숍 활보", "룩 돌진" };

    private void Awake()
    {
        canvas = GetComponent<RectTransform>();
        RectTransform left = Panel("Build", -583, 40, 330, 820);
        Label(left, "보스전 플레이테스트", 0, 370, 294, 42, 27);
        Label(left, "조건을 고르고 바로 전투\n변경은 재시작할 때 적용", 0, 309, 288, 65, 21);
        traitLabel = Choice(left, "특성", 223, () => { traitIndex = (traitIndex + 1) % Traits.Length; RefreshChoices(); });
        artLabel = Choice(left, "이동술", 134, () => { artIndex = (artIndex + 1) % Arts.Length; RefreshChoices(); });
        levelLabel = Choice(left, "이동술 단계", 45, () => { level = level % 3 + 1; RefreshChoices(); });
        buildLabel = Choice(left, "빌드", -44, () => { strong = !strong; RefreshChoices(); });
        phaseLabel = Choice(left, "시작 페이즈", -133, () => { phaseTwo = !phaseTwo; RefreshChoices(); });
        startButton = Button(left, "StartEncounter", "설정으로 시작", 0, -227, 288, 52,
            () => playtest.StartEncounter(Traits[traitIndex], Arts[artIndex], level, strong, phaseTwo));
        Button(left, "RestartSame", "같은 조건으로 다시", 0, -293, 288, 48, () => playtest.RestartEncounter());
        Label(left, "기본 HP 18 / 공격 2\n강한 빌드 HP 24 / 공격 3\n특성 강화 포함 · 유물 없음", 0, -361, 288, 76, 19);

        RectTransform right = Panel("Encounter", 583, 40, 330, 820);
        Label(right, "여왕 · 체크메이트", 0, 371, 288, 42, 29);
        status = Label(right, "전투 대기", 0, 272, 288, 150, 25);
        detail = Label(right, "", 0, 87, 288, 200, 21);
        outcome = Label(right, "", 0, -66, 288, 85, 21);
        charge = Label(right, "", 0, -144, 288, 60, 22);
        Button(right, "Move", "S  이동 / 이동술", 0, -212, 288, 45, () => playtest.Player?.SelectMoveMode());
        Button(right, "Attack", "A  일반 공격", 0, -266, 288, 45, () => playtest.Player?.SelectAttackMode());
        Button(right, "Special", "F  준비된 특성 공격", 0, -320, 288, 45, () => playtest.Player?.SelectTraitAttackMode());
        Button(right, "Wait", "Space  대기", 0, -374, 288, 45, () => playtest.Player?.TryWaitForPlaytest());
        Label(canvas, "빨강: 공격 예고   파랑 A → 금색 B: 순서대로 피신   회색: 성벽   근위병: 아래로 전진", 0, -396, 800, 35, 19);
        Label(canvas, "방향 / 착지 칸 클릭으로 확정  ·  우클릭 취소  ·  ESC 일시정지 / 효과음", 0, -438, 900, 38, 22);
        RectTransform pause = Panel("Pause", 0, 40, 720, 300);
        pausePanel = pause.gameObject;
        Label(pause, "일시정지", 0, 97, 650, 44, 31);
        Label(pause, "왼쪽에서 조건을 변경하고 재시작할 수 있습니다", 0, 35, 670, 42, 22);
        Button(pause, "VolumeDown", "효과음 −", -215, -31, 190, 46, () => Volume(-.1f));
        Button(pause, "VolumeUp", "효과음 +", 0, -31, 190, 46, () => Volume(.1f));
        Button(pause, "Resume", "계속 / ESC", 215, -31, 190, 46, () => playtest.TogglePause());
        Label(pause, "볼륨은 기존 게임 설정과 함께 저장됩니다", 0, -100, 650, 36, 20);
        pausePanel.SetActive(false);
        RefreshChoices();
    }
    private void Volume(float delta)
    {
        GameAudio audio = GameAudio.GetOrCreate(); audio.SetSfxVolume(audio.SfxVolume + delta); audio.SaveSettings();
        outcome.text = $"효과음 볼륨 {Mathf.RoundToInt(audio.SfxVolume * 100)}%";
    }
    private void RefreshChoices()
    {
        traitLabel.text = TraitNames[traitIndex] + "  ›"; artLabel.text = ArtNames[artIndex] + "  ›";
        levelLabel.text = level + "단계  ›"; buildLabel.text = (strong ? "강한 빌드" : "기본 빌드") + "  ›";
        phaseLabel.text = (phaseTwo ? "2페이즈부터" : "1페이즈부터") + "  ›";
    }
    private void Start()
    {
        GridManager grid=playtest.GetComponent<GridManager>();
        // 원본 바닥 Tile의 색 잠금과 무관하게 테스트 외벽을 명확히 구분한다.
        for(int y=0;y<15;y++)for(int x=0;x<15;x++)
            if(x==0||y==0||x==14||y==14)Marker(grid,new Vector3Int(x,y,0),new Color(.18f,.2f,.25f),false);
    }
    private void LateUpdate()
    {
        if (playtest.Player == null) return;
        QueenBossBehaviour boss = playtest.Encounter;
        pausePanel.SetActive(playtest.Paused);
        startButton.interactable = playtest.Paused || !playtest.HasStarted || boss.IsFinished || !playtest.Player.IsActionInProgress;
        if (!playtest.HasStarted)
        {
            status.text = "전투 대기\n여왕 HP 60";
            detail.text = "1페이즈\n사격 · 포격 · 근위대\n패턴마다 여왕이 퀸처럼 이동\n\n2페이즈 (HP 50% 이하)\n성벽 봉쇄 · 돌진 · 체크메이트 추가";
            outcome.text = "왼쪽에서 설정으로 시작"; return;
        }
        int hp = boss.Queen == null ? 0 : boss.Queen.GetComponent<CharacterHealth>().CurrentHealth;
        status.text = $"{boss.Phase}페이즈 · 행동 {boss.Turn}\n여왕 {hp} / {boss.MaximumHealth}\n플레이어 {playtest.Player.CurrentHealth} / {playtest.Player.GetComponent<CharacterHealth>().MaxHealth}\n\n{boss.PatternName}";
        detail.text = boss.TimingText + "\n\n" + (boss.CurrentPattern switch
        {
            QueenBossBehaviour.Pattern.Checkmate => "예고 중 2행동 뒤 A\n그 다음 행동 뒤 B\n안전구역 밖 피해 8\n근위병 정지\n반격 기회 2행동",
            QueenBossBehaviour.Pattern.Guards => "근위병은 아래로만 전진 · 정면이면 피해 1\n맨 아래 줄에 닿으면 머리 위 말로 승격\n(나이트 2·비숍 1·폭탄병 1, 최대 2)\n승격 전에 쓰러뜨리면 저지",
            QueenBossBehaviour.Pattern.Bombardment => "예고 위치에 3×3 포격\n두 번째 행동 뒤 피해 4\n발동 중 목표 변경 없음\n다음 1행동 반격",
            QueenBossBehaviour.Pattern.Fortress => "회색 성벽은 이동과 탄을 차단\n빨간 직선 / 대각선에 피해 3\n반격 기회가 끝나면 성벽 소멸",
            QueenBossBehaviour.Pattern.Charge => "빨간 줄을 따라 여왕이 돌진\n두 번째 행동 뒤 줄 위에 있으면 피해 5\n(여왕은 플레이어 앞에서 멈춤)\n줄 밖으로 비켜서기",
            _ => "직선과 대각선 3줄 사격\n두 번째 행동 뒤 피해 3\n빨강 밖으로 이동\n다음 1행동 반격"
        });
        if (!playtest.Paused) outcome.text = boss.LastOutcome;
        PlayerTraitSystem trait = playtest.Player.GetComponent<PlayerTraitSystem>();
        charge.text = trait.IsReady ? "특성 준비 완료 · F" : $"특성 충전 {trait.ChargeProgress} / {trait.ChargeRequired}";
        string signature = $"{boss.Turn}/{boss.PatternStep}/{boss.IsFinished}/{Screen.width}/{Screen.height}/{hp}/{playtest.Player.GridPosition}";
        if (signature == lastSignature) return;
        lastSignature = signature; DrawBoard(boss);
    }
    private void DrawBoard(QueenBossBehaviour boss)
    {
        foreach (GameObject marker in markers) Destroy(marker); markers.Clear();
        foreach (TMP_Text label in labels) Destroy(label.gameObject); labels.Clear();
        GridManager grid = playtest.GetComponent<GridManager>();
        if (!boss.IsFinished && !boss.IsRecovery)
        {
            if (boss.SafeZones != null)
            {
                HashSet<Vector3Int> safe = boss.PatternStep < 2 ? boss.SafeZones.A : boss.SafeZones.B;
                for (int y=1;y<=13;y++) for(int x=1;x<=13;x++)
                { var cell = new Vector3Int(x,y,0); if (!safe.Contains(cell) && !boss.ContainsQueenCell(cell)) Marker(grid, cell, new Color(1f,.18f,.25f,.20f)); }
            }
            else foreach (Vector3Int cell in boss.Danger) Marker(grid,cell,new Color(1f,.18f,.25f,.55f));
        }
        foreach (Vector3Int cell in boss.Walls) { Marker(grid,cell,new Color(.48f,.53f,.60f,1f)); CellLabel(grid,cell,"■",24); }
        if (boss.SafeZones != null && !boss.IsRecovery && !boss.IsFinished)
        {
            foreach(Vector3Int cell in boss.SafeZones.A) { Marker(grid,cell,new Color(.1f,.65f,1f,boss.PatternStep<2?.85f:.25f)); if(CanShowTileLetter(cell))CellLabel(grid,cell,"A",22); }
            foreach(Vector3Int cell in boss.SafeZones.B) { Marker(grid,cell,new Color(1f,.7f,.12f,boss.PatternStep==2?.85f:.35f)); if(CanShowTileLetter(cell))CellLabel(grid,cell,"B",22); }
        }
        foreach(MonsterMovement guard in boss.Guards) if(guard!=null&&!guard.IsDead)
        {
            CellLabel(grid,guard.GridPosition,"↓\n"+guard.GetComponent<CharacterHealth>().CurrentHealth,18);
            Vector3Int next=guard.GridPosition+Vector3Int.down;
            if(!boss.IsRecovery&&boss.CurrentPattern!=QueenBossBehaviour.Pattern.Checkmate&&grid.IsWalkableCell(next))Marker(grid,next,new Color(1f,.5f,.15f,.4f));
        }
        if(boss.Queen!=null&&!boss.Queen.IsDead)CellLabel(grid,boss.Center,"Q",64);
    }
    private bool CanShowTileLetter(Vector3Int cell)=>cell!=playtest.Player.GridPosition&&!playtest.GetComponent<MonsterSpawner>().IsOccupied(cell);
    private void Marker(GridManager grid, Vector3Int cell, Color color, bool transient = true)
    {
        var obj=new GameObject("Telegraph "+cell,typeof(SpriteRenderer)); obj.transform.SetParent(playtest.transform,false);
        obj.transform.position=grid.GetCellCenterWorld(cell); obj.transform.localScale=Vector3.one*.92f;
        SpriteRenderer renderer=obj.GetComponent<SpriteRenderer>();renderer.sprite=markerSprite;renderer.color=color;renderer.sortingOrder=2;
        if(transient)markers.Add(obj);
    }
    private void CellLabel(GridManager grid, Vector3Int cell, string value, int size)
    {
        Vector3 screen=worldCamera.WorldToScreenPoint(grid.GetCellCenterWorld(cell));
        Vector2 point;RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,screen,null,out point);
        TMP_Text text=Label(canvas,value,point.x,point.y,100,85,size); text.alignment=TextAlignmentOptions.Center;labels.Add(text);
        // 보드 표기가 일시정지 패널을 가리지 않도록 한다.
        text.transform.SetSiblingIndex(pausePanel.transform.GetSiblingIndex());
    }
    private RectTransform Panel(string name,float x,float y,float width,float height)
    {
        var obj=new GameObject(name,typeof(RectTransform),typeof(Image));obj.transform.SetParent(transform,false);
        RectTransform rect=obj.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(x,y);
        obj.GetComponent<Image>().color=new Color(.07f,.085f,.12f,.97f);return rect;
    }
    private TMP_Text Choice(RectTransform parent,string title,float y,UnityEngine.Events.UnityAction action)
    {
        Label(parent,title,0,y+29,288,26,19);
        return Button(parent,title,"",0,y-11,288,46,action).GetComponentInChildren<TMP_Text>();
    }
    private Button Button(RectTransform parent,string name,string title,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
    {
        var obj=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));obj.transform.SetParent(parent,false);
        RectTransform rect=obj.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(w,h);rect.anchoredPosition=new Vector2(x,y);
        Image image=obj.GetComponent<Image>();image.color=new Color(.2f,.24f,.34f,1f);
        Button button=obj.GetComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
        var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;
        TMP_Text label=Label(rect,title,0,0,w-12,h,22);label.alignment=TextAlignmentOptions.Center;return button;
    }
    private TMP_Text Label(RectTransform parent,string value,float x,float y,float w,float h,int size)
    {
        var obj=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));obj.transform.SetParent(parent,false);
        RectTransform rect=obj.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(w,h);rect.anchoredPosition=new Vector2(x,y);
        TMP_Text text=obj.GetComponent<TMP_Text>();text.font=font;text.fontSize=size;text.enableAutoSizing=false;
        text.textWrappingMode=TextWrappingModes.Normal;text.alignment=TextAlignmentOptions.MidlineLeft;
        text.color=new Color(.92f,.94f,1f);text.raycastTarget=false;text.text=value;return text;
    }
}
