using UnityEngine;

// PC 보드 배치. 가로 여유가 있으면 HUD를 보드 양옆에 두고 보드는 화면 높이에 맞춘다(측면 모드).
// 좁은 화면에서는 헤더를 보드 위, 행동 HUD를 보드 아래에 둔다(상하 모드).
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

    // 외벽 포함 15칸, 타일 원본 32px. 화면 배율이 정수일 때만 픽셀 크기가 고르게 보인다.
    public const int BoardTiles = 15;
    public const int TilePixels = 32;
    // 측면 HUD 열 폭과 화면 가장자리 여백(기준 해상도 단위)
    public const float SideColumnWidth = 340f;
    public const float ScreenMargin = 16f;

    public static float Scale(int width, int height)
        => Mathf.Max(0.01f, Mathf.Min(width / ReferenceResolution.x, height / ReferenceResolution.y));

    // 2배 이상 들어가면 정수로 내림해 픽셀이 고르게 보이게 하고, 그보다 작으면 남는 공간에 맞춘다.
    private static float Snap(float fit) => fit >= 2f ? Mathf.Floor(fit) : Mathf.Max(0.05f, fit);

    private static float SidePixelScale(int width, int height)
    {
        float scale = Scale(width, height);
        return Snap((height - 2f * ScreenMargin * scale) / (BoardTiles * TilePixels));
    }

    // 보드 양옆에 HUD 열 두 개가 들어가면 측면 모드.
    public static bool UsesSideHud(int width, int height)
    {
        float scale = Scale(width, height);
        float side = BoardTiles * TilePixels * SidePixelScale(width, height);
        return width - side >= 2f * (SideColumnWidth + PanelGap + ScreenMargin) * scale;
    }

    public static float PixelScale(int width, int height)
    {
        if (UsesSideHud(width, height)) return SidePixelScale(width, height);
        float scale = Scale(width, height);
        // 상하 모드: 위 외벽(룩이 서는 줄)은 가리지 않고, 아래 외벽 한 줄만 행동 HUD 뒤에 겹쳐도 된다.
        float fitHeight = (height - (HeaderSpace + ActionSpace) * scale) / ((BoardTiles - 1) * TilePixels);
        float fitWidth = (width - 48f * scale) / (BoardTiles * TilePixels);
        return Snap(Mathf.Min(fitHeight, fitWidth));
    }

    public static Rect BoardPixelRect(int width, int height)
    {
        float scale = Scale(width, height);
        float pixelScale = PixelScale(width, height);
        float side = BoardTiles * TilePixels * pixelScale;
        if (UsesSideHud(width, height))
            return new Rect(Mathf.Round((width - side) * 0.5f), Mathf.Round((height - side) * 0.5f), side, side);

        float wall = TilePixels * pixelScale;
        // 헤더 + 보드(아래 벽 제외) + 행동 HUD 묶음을 세로 가운데에 둔다.
        float block = HeaderSpace * scale + side - wall + ActionSpace * scale;
        float top = height - Mathf.Max(0f, (height - block) * 0.5f) - HeaderSpace * scale;
        // 맵 모서리를 정수 화면 픽셀에 맞춘다.
        return new Rect(Mathf.Round((width - side) * 0.5f), Mathf.Round(top - side), side, side);
    }
}
