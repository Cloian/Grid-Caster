using System.Collections.Generic;
using UnityEngine;

// 유물 제단 칸 위에 떠 있는 보석. 보석 색 = 등급(일반 은백·희귀 파랑·전설 금), 위험 제단 = 금 간 보석 + 핏빛 고리.
// 위험 제단은 사슬로 잠긴 채 시작하고, 아래 점이 남은 처치 수를 보여 준다. 다 채우면 사슬이 끊어지는 연출 뒤 대기 모션.
// 판정은 RunProgressionSystem이 맡고 여기는 그림만 그린다.
public sealed class RelicAltarVisual : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    private const int FrameWidth = 32;
    private const int FrameHeight = 48;
    private const float Pivot = 16f / FrameHeight;      // 아래 32px이 그 칸, 칸 중심이 기준점
    private const float IdleSeconds = 0.13f;
    private const float UnlockSeconds = 0.1f;
    private const int SortingOrder = 4;                  // 바닥 위, 캐릭터(10) 아래
    private static readonly Color PipDone = new Color32(240, 96, 80, 255);
    private static readonly Color PipLeft = new Color32(40, 34, 46, 255);

    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
    private static Sprite pipSprite;

    private readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
    private SpriteRenderer body;
    private Sprite[] idle, locked, unlock;
    private Sprite[] playing;
    private bool playingOnce;
    private float elapsed;
    private int frame;

    public bool IsLockedVisual => playing == locked;

    public static RelicAltarVisual Create(GridManager grid, RelicOffer offer)
    {
        var root = new GameObject(offer.IsRisky ? "RiskyRelicAltar" : "SafeRelicAltar");
        root.transform.position = grid.GetCellCenterWorld(offer.Cell);
        var visual = root.AddComponent<RelicAltarVisual>();
        visual.body = root.AddComponent<SpriteRenderer>();
        visual.body.sortingOrder = SortingOrder;
        string rarity = offer.Relic.Rarity switch
        {
            RelicRarity.Legendary => "legendary",
            RelicRarity.Rare => "rare",
            _ => "common"
        };
        string kind = offer.IsRisky ? "risky" : "safe";
        visual.idle = Load($"relic_{rarity}_{kind}_idle");
        if (offer.IsRisky)
        {
            visual.locked = Load($"relic_{rarity}_risky_locked");
            visual.unlock = Load($"relic_{rarity}_risky_unlock");
        }
        visual.Play(offer.IsLocked && visual.locked != null ? visual.locked : visual.idle, false);
        visual.SetLockProgress(offer.KillsRemaining, offer.KillsRequired, false);
        return visual;
    }

    public void SetLockProgress(int remaining, int required, bool justUnlocked)
    {
        if (justUnlocked && unlock != null) Play(unlock, true);
        int count = remaining > 0 ? required : 0;
        if (pipSprite == null) pipSprite = MakePip();
        while (pips.Count < count)
        {
            var child = new GameObject("LockPip");
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = pipSprite;
            renderer.sortingOrder = SortingOrder + 1;
            pips.Add(renderer);
        }
        // 2px 점을 3px 간격으로(많으면 2px 간격) 보석과 바닥 고리 사이에 놓는다. 채운 점 = 처치한 수.
        float spacing = count > 9 ? 2f : 3f;
        float size = count > 9 ? 0.5f : 1f;
        for (int i = 0; i < pips.Count; i++)
        {
            bool visible = i < count;
            pips[i].gameObject.SetActive(visible);
            if (!visible) continue;
            pips[i].transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * spacing / PixelsPerUnit, -2f / PixelsPerUnit, 0f);
            pips[i].transform.localScale = Vector3.one * size;
            pips[i].color = i < required - remaining ? PipDone : PipLeft;
        }
    }

    private void Play(Sprite[] frames, bool once)
    {
        if (frames == null || frames.Length == 0) return;
        playing = frames;
        playingOnce = once;
        frame = 0;
        elapsed = 0f;
        body.sprite = frames[0];
    }

    private void Update()
    {
        if (playing == null) return;
        elapsed += Time.deltaTime;
        float step = playingOnce ? UnlockSeconds : IdleSeconds;
        while (elapsed >= step)
        {
            elapsed -= step;
            frame++;
            if (frame >= playing.Length)
            {
                if (playingOnce) { Play(idle, false); return; }
                frame = 0;
            }
            body.sprite = playing[frame];
        }
    }

    private static Sprite[] Load(string name)
    {
        if (cache.TryGetValue(name, out Sprite[] frames) && frames != null && frames.Length > 0 && frames[0] != null)
            return frames;
        Texture2D texture = Resources.Load<Texture2D>("Relics/" + name);
        if (texture == null) return null;
        int count = texture.width / FrameWidth;
        frames = new Sprite[count];
        for (int i = 0; i < count; i++)
            frames[i] = Sprite.Create(texture, new Rect(i * FrameWidth, 0, FrameWidth, FrameHeight),
                new Vector2(0.5f, Pivot), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        cache[name] = frames;
        return frames;
    }

    private static Sprite MakePip()
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color32 white = new Color32(255, 255, 255, 255);
        texture.SetPixels32(new[] { white, white, white, white });
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), PixelsPerUnit);
    }
}
