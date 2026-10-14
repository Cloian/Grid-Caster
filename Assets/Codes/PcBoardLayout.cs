using UnityEngine;

// PC는 보드 위·아래 HUD 공간만 남기고 남은 화면을 전장에 사용한다.
public static class PcBoardLayout
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    public const float BoardSide = 850f;
    public const float HeaderSpace = 78f;
    public const float ActionSpace = 152f;
    public const float BoardCenterY = (ActionSpace - HeaderSpace) * 0.5f;
    public const float PanelGap = 18f;
    public const float PanelWidth = 330f;
    public const float PanelHeight = 820f;

    public static float Scale(int width, int height)
        => Mathf.Max(0.01f, Mathf.Min(width / ReferenceResolution.x, height / ReferenceResolution.y));

    public static Rect BoardPixelRect(int width, int height)
    {
        float scale = Scale(width, height);
        float side = Mathf.Max(1f, Mathf.Min(width - 48f * scale,
            height - (HeaderSpace + ActionSpace) * scale));
        return new Rect((width - side) * 0.5f,
            (height - side) * 0.5f + BoardCenterY * scale, side, side);
    }
}
