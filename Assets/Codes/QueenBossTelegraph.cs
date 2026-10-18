using System.Collections.Generic;
using UnityEngine;

// 본 게임 마지막 웨이브용 여왕 예고 표시. 보스 테스트 씬의 QueenBossPlaytestUi와 같은 바닥 규칙을 쓴다:
// 빨강 = 이번 공격 칸, 회색 = 임시 성벽, 파랑 A → 금색 B = 체크메이트 안전지대, 주황 = 근위병 다음 칸.
// 화면 위쪽 가운데에 패턴 이름·발동까지 남은 행동·직전 결과를 적는다. 판정과 무관하다.
public sealed class QueenBossTelegraph : MonoBehaviour
{
    private static Sprite pixel;
    private readonly List<GameObject> markers = new List<GameObject>();
    private QueenBossBehaviour boss;
    private GridManager grid;
    private string signature;
    private GUIStyle style;

    public static QueenBossTelegraph Attach(QueenBossBehaviour boss, GridManager grid)
    {
        var existing = boss.GetComponent<QueenBossTelegraph>();
        if (existing != null) Destroy(existing);
        var telegraph = boss.gameObject.AddComponent<QueenBossTelegraph>();
        telegraph.boss = boss;
        telegraph.grid = grid;
        return telegraph;
    }

    private void LateUpdate()
    {
        if (boss == null) return;
        bool alive = boss.Queen != null && !boss.Queen.IsDead && !boss.IsFinished;
        string next = alive ? $"{boss.Turn}/{boss.PatternStep}/{boss.CurrentPattern}/{boss.Center}/{boss.Danger.Count}/{boss.Walls.Count}/{boss.Guards.Count}" : "off";
        if (next == signature) return;
        signature = next;
        Redraw(alive);
    }

    private void Redraw(bool alive)
    {
        foreach (GameObject marker in markers) if (marker != null) Destroy(marker);
        markers.Clear();
        if (!alive) return;
        if (!boss.IsRecovery)
        {
            if (boss.SafeZones != null)
            {
                HashSet<Vector3Int> safe = boss.PatternStep < 2 ? boss.SafeZones.A : boss.SafeZones.B;
                for (int y = 1; y <= 13; y++)
                    for (int x = 1; x <= 13; x++)
                    {
                        var cell = new Vector3Int(x, y, 0) + boss.Origin;
                        if (!safe.Contains(cell) && !boss.ContainsQueenCell(cell)) Marker(cell, new Color(1f, .18f, .25f, .2f));
                    }
                foreach (Vector3Int cell in boss.SafeZones.A) Marker(cell, new Color(.1f, .65f, 1f, boss.PatternStep < 2 ? .85f : .25f));
                foreach (Vector3Int cell in boss.SafeZones.B) Marker(cell, new Color(1f, .7f, .12f, boss.PatternStep == 2 ? .85f : .35f));
            }
            else foreach (Vector3Int cell in boss.Danger) Marker(cell, new Color(1f, .18f, .25f, .55f));
        }
        foreach (Vector3Int cell in boss.Walls) Marker(cell, new Color(.48f, .53f, .6f, 1f));
        if (boss.CurrentPattern != QueenBossBehaviour.Pattern.Checkmate)
            foreach (MonsterMovement guard in boss.Guards)
                if (guard != null && !guard.IsDead && grid.IsWalkableCell(guard.GridPosition + Vector3Int.down))
                    Marker(guard.GridPosition + Vector3Int.down, new Color(1f, .5f, .15f, .4f));
    }

    private void Marker(Vector3Int cell, Color color)
    {
        if (pixel == null)
        {
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        }
        var go = new GameObject("QueenTelegraph " + cell);
        go.transform.position = grid.GetCellCenterWorld(cell);
        go.transform.localScale = Vector3.one * 0.94f;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = pixel;
        renderer.color = color;
        renderer.sortingOrder = 2;
        markers.Add(go);
    }

    private void OnGUI()
    {
        if (boss == null || boss.Queen == null || boss.Queen.IsDead || boss.IsFinished) return;
        if (style == null)
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter, fontSize = 22, fontStyle = FontStyle.Bold, richText = true,
                // 한글 글리프가 있는 OS 글꼴(비숍 불길 남은 턴 표시와 같은 방식)
                font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Arial" }, 22)
            };
        int hp = boss.Queen.GetComponent<CharacterHealth>().CurrentHealth;
        string text = $"여왕 {hp}/{boss.MaximumHealth} · {boss.Phase}페이즈 — <color=#ffd66e>{boss.PatternName}</color>\n{boss.TimingText}  ·  {boss.LastOutcome}";
        var rect = new Rect(0, 8, Screen.width, 60);
        style.normal.textColor = new Color(0f, 0f, 0f, .8f);
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", ""), style);
        style.normal.textColor = Color.white;
        GUI.Label(rect, text, style);
    }

    private void OnDestroy()
    {
        foreach (GameObject marker in markers) if (marker != null) Destroy(marker);
    }
}
