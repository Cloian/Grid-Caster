using System.Collections.Generic;
using UnityEngine;

// 불길은 플레이어의 이동 진입에만 피해를 주며 수명은 적 턴 시작 때만 감소한다.
public sealed class BishopFireTrail : MonoBehaviour
{
    private readonly Dictionary<Vector3Int, int> expiryTurns = new Dictionary<Vector3Int, int>();
    private readonly List<Vector3Int> expired = new List<Vector3Int>();
    private GridManager grid;
    private Move player;
    private Font font;
    private int turn;
    private int lifetime = 3;
    private int damage = 1;

    public Font LabelFont => font;
    public int Count => expiryTurns.Count;
    public bool HasFire(Vector3Int cell) => expiryTurns.ContainsKey(cell);
    public int RemainingTurns(Vector3Int cell) => expiryTurns.TryGetValue(cell, out int expiry) ? expiry - turn : 0;

    public void Initialize(GridManager map, Move target, int turns, int hitDamage)
    {
        if (player != null) player.PlayerMoved -= OnPlayerMoved;
        grid = map;
        player = target;
        lifetime = Mathf.Max(1, turns);
        damage = Mathf.Max(1, hitDamage);
        player.PlayerMoved += OnPlayerMoved;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Arial" }, 16);
    }

    public void AddFire(Vector3Int cell)
    {
        if (grid.IsWalkableCell(cell)) expiryTurns[cell] = turn + lifetime;
    }

    private void OnPlayerMoved()
    {
        if (player.CurrentHealth > 0 && HasFire(player.GridPosition)) player.TakeDamage(damage);
    }

    public void AdvanceTurn()
    {
        turn++;
        expired.Clear();
        foreach (var fire in expiryTurns) if (fire.Value <= turn) expired.Add(fire.Key);
        foreach (Vector3Int cell in expired) expiryTurns.Remove(cell);
    }

    public void Clear()
    {
        expiryTurns.Clear();
        turn = 0;
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        if (camera == null || grid == null) return;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            font = font, fontSize = 16, alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = new Color(1f, 0.4f, 0.12f);
        foreach (var fire in expiryTurns)
        {
            Vector3 point = camera.WorldToScreenPoint(grid.GetCellCenterWorld(fire.Key));
            if (point.z <= 0) continue;
            GUI.Label(new Rect(point.x - 40, Screen.height - point.y - 12, 80, 24), $"불길 {fire.Value - turn}", style);
        }
    }

    private void OnDestroy()
    {
        if (player != null) player.PlayerMoved -= OnPlayerMoved;
        if (font != null) Destroy(font);
    }
}
