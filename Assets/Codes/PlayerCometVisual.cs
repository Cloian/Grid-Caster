using UnityEngine;

// 플레이어 마법 투사체(혜성탄) 연출. 판정과 이동은 TurnProjectile이 맡고, 여기서는 그림만 바꾼다.
// 상하좌우는 오른쪽 그림, 대각선은 오른쪽 위 그림을 쓰고 90° 단위로만 돌려 픽셀이 깨지지 않게 한다.
public sealed class PlayerCometVisual : MonoBehaviour
{
    private const float FrameSeconds = 0.05f;
    private const int FramesPerDirection = 4;

    private static Sprite[] frames;

    private SpriteRenderer body;
    private int firstFrame;
    private float elapsed;

    // 그림의 꼬리 방향(로컬 좌표). 강화 꼬리를 혜성 꼬리 끝에 이어 붙일 때 쓴다.
    public Vector3 LocalBackward { get; private set; } = Vector3.left;
    public bool IsDiagonal { get; private set; }

    public static bool TryAttach(GameObject projectile, SpriteRenderer renderer, Animator animator,
        Vector3Int direction)
    {
        if (!LoadFrames()) return false;
        bool diagonal = direction.x != 0 && direction.y != 0;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - (diagonal ? 45f : 0f);
        projectile.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(angle / 90f) * 90f);
        // 기존 3프레임 Animator가 Sprite를 덮어쓰지 않게 끈다. TurnProjectile이 Animator 컴포넌트를 요구해 제거하지 않는다.
        animator.runtimeAnimatorController = null;
        animator.enabled = false;

        PlayerCometVisual visual = projectile.AddComponent<PlayerCometVisual>();
        visual.body = renderer;
        visual.IsDiagonal = diagonal;
        visual.firstFrame = diagonal ? FramesPerDirection : 0;
        visual.LocalBackward = diagonal ? new Vector3(-1f, -1f, 0f).normalized : Vector3.left;
        renderer.sprite = frames[visual.firstFrame];
        return true;
    }

    private static bool LoadFrames()
    {
        if (frames == null || frames.Length == 0)
        {
            frames = Resources.LoadAll<Sprite>("Projectiles/player_comet");
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        }
        return frames.Length >= FramesPerDirection * 2;
    }

    // 일시정지(timeScale 0) 중에는 투사체와 함께 멈추도록 스케일 시간을 쓴다.
    private void Update()
    {
        elapsed += Time.deltaTime;
        int frame = Mathf.FloorToInt(elapsed / FrameSeconds) % FramesPerDirection;
        body.sprite = frames[firstFrame + frame];
    }
}
