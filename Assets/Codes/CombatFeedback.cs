using System.Collections.Generic;
using UnityEngine;

// 판정과 점유는 원본에 남긴다. 반동/변위는 애니메이션을 복사한 비전투 Sprite로 표현한다.
[DefaultExecutionOrder(90)]
public sealed partial class CombatFeedback : MonoBehaviour
{
    public const float RecoilSeconds = 0.09f;
    public const float SparkSeconds = 0.08f;
    public const float DeathEchoSeconds = 0.14f;
    public const float KnockbackSeconds = 0.11f;
    private const float PixelsPerUnit = 32f;
    private const float RecoilPixels = 3f;
    private static CombatFeedback instance;

    private sealed class ActorVisual
    {
        public CharacterHealth Health;
        public SpriteRenderer Source;
        public SpriteRenderer Copy;
        public bool WasEnabled;
        public Vector3 RecoilDirection;
        public float RecoilElapsed = RecoilSeconds;
        public Vector3 TravelOffset;
        public float TravelElapsed = KnockbackSeconds;
        public bool PendingFlash;
    }

    private sealed class DeathEcho
    {
        public SpriteRenderer Copy;
        public Vector3 Position;
        public Vector3 Scale;
        public Color Color;
        public Vector3 RecoilDirection;
        public Vector3 TravelOffset;
        public float Elapsed;
        public float TravelElapsed;
        public float Lifetime = DeathEchoSeconds;
        public Animator Animator;
    }

    private sealed class Spark
    {
        public GameObject Root;
        public SpriteRenderer[] Rays;
        public float Elapsed;
        public Color Tint;
    }

    private readonly Dictionary<SpriteRenderer, ActorVisual> actors = new Dictionary<SpriteRenderer, ActorVisual>();
    private readonly List<SpriteRenderer> expiredActors = new List<SpriteRenderer>();
    private readonly List<DeathEcho> echoes = new List<DeathEcho>();
    private readonly List<Spark> sparks = new List<Spark>();
    private Sprite pixelSprite;
    private Texture2D pixelTexture;

    public static int DeathEchoCount => instance != null ? instance.echoes.Count : 0;
    public static int SparkCount => instance != null ? instance.sparks.Count : 0;
    public static bool HasPendingDisplacement
    {
        get
        {
            if (instance == null) return false;
            foreach (ActorVisual actor in instance.actors.Values)
                if (actor.Source != null && actor.TravelElapsed < KnockbackSeconds) return true;
            foreach (DeathEcho echo in instance.echoes)
                if (echo.TravelOffset != Vector3.zero && echo.TravelElapsed < KnockbackSeconds) return true;
            return false;
        }
    }

    private static CombatFeedback GetOrCreate()
    {
        if (instance == null) instance = new GameObject("CombatFeedback").AddComponent<CombatFeedback>();
        return instance;
    }

    private void Awake() { instance = this; }

    public static void PlayHit(CharacterHealth health, Vector3Int incomingDirection, bool lethal)
    {
        if (attackActive && lethal && health.GetComponent<MonsterMovement>() != null)
            attackKills.Add(health.GetInstanceID());
        SpriteRenderer source = health.GetComponent<SpriteRenderer>();
        if (source == null || source.sprite == null) return;
        CombatFeedback feedback = GetOrCreate();
        Vector3 direction = incomingDirection == Vector3Int.zero ? Vector3.up : ((Vector3)incomingDirection).normalized;
        feedback.SpawnSpark(source, lethal);
        if (DamageVisualDelay <= 0f) CameraController.PlayImpactShake(direction);

        if (lethal)
        {
            Vector3 travel = Vector3.zero;
            if (feedback.actors.TryGetValue(source, out ActorVisual previous))
            {
                travel = TravelOffset(previous);
                feedback.RemoveActor(previous, false);
                feedback.actors.Remove(source);
            }
            // 사망 모션이 있는 플레이어는 원본이 그대로 남아 모션을 재생한다.
            bool animatedDeath = ActorMotion.HasDeath(health);
            if (animatedDeath && health.GetComponent<MonsterMovement>() == null)
            {
                source.enabled = true;
                return;
            }
            SpriteRenderer copy = feedback.CreateCopy(source, "CombatDeathEcho");
            var echo = new DeathEcho {
                Copy = copy, Position = source.transform.position, Scale = source.transform.lossyScale,
                Color = source.color, RecoilDirection = direction, TravelOffset = travel,
                Elapsed = -DamageVisualDelay
            };
            if (animatedDeath)
            {
                echo.Lifetime = Mathf.Max(DeathEchoSeconds, ActorMotion.PlayDeathOn(health, copy.gameObject));
                echo.Animator = copy.GetComponent<Animator>();
                if (health.TryGetComponent(out DamageFlash flash)) copy.color = echo.Color = flash.BaseColor(source);
                if (echo.Animator != null) echo.Animator.enabled = echo.Elapsed >= 0f;
            }
            feedback.echoes.Add(echo);
            copy.transform.position += travel + (DamageVisualDelay > 0f ? Vector3.zero
                : PixelOffset(direction * RecoilPixels / PixelsPerUnit));
            source.enabled = false;
            return;
        }

        ActorVisual actor = feedback.GetActor(health, source);
        actor.RecoilDirection = direction;
        actor.RecoilElapsed = -DamageVisualDelay;
        actor.PendingFlash = DamageVisualDelay > 0f;
        feedback.UpdateActorPose(actor);
    }

    public static void AnimateKnockback(MonsterMovement monster, Vector3 from, Vector3 to)
    {
        SpriteRenderer source = monster.GetComponent<SpriteRenderer>();
        if (source == null || source.sprite == null) return;
        CombatFeedback feedback = GetOrCreate();
        ActorVisual actor = feedback.GetActor(monster.GetComponent<CharacterHealth>(), source);
        // 같은 행동의 두 번째 밀기는 현재 보이는 위치에서 계속한다.
        actor.TravelOffset = from - to + TravelOffset(actor);
        actor.TravelElapsed = 0f;
        feedback.UpdateActorPose(actor);
    }

    public static Vector3 VisualPosition(Component actor)
    {
        SpriteRenderer source = actor.GetComponent<SpriteRenderer>();
        if (instance != null && source != null && instance.actors.TryGetValue(source, out ActorVisual visual))
            return visual.Copy.transform.position;
        return actor.transform.position;
    }

    public static void ResetActor(Component actor)
    {
        if (instance == null) return;
        SpriteRenderer source = actor.GetComponent<SpriteRenderer>();
        if (source != null && instance.actors.TryGetValue(source, out ActorVisual visual))
        {
            instance.RemoveActor(visual, true);
            instance.actors.Remove(source);
        }
    }

    public static void ClearAll()
    {
        ResetAttackOutcome();
        if (instance == null) return;
        instance.ClearFragments();
        foreach (ActorVisual actor in instance.actors.Values) instance.RemoveActor(actor, true);
        instance.actors.Clear();
        foreach (DeathEcho echo in instance.echoes) HideAndDestroy(echo.Copy.gameObject);
        foreach (Spark spark in instance.sparks) HideAndDestroy(spark.Root);
        instance.echoes.Clear();
        instance.sparks.Clear();
    }

    private ActorVisual GetActor(CharacterHealth health, SpriteRenderer source)
    {
        if (actors.TryGetValue(source, out ActorVisual actor)) return actor;
        actor = new ActorVisual { Health = health, Source = source, WasEnabled = source.enabled,
            Copy = CreateCopy(source, "CombatActorVisual") };
        actors.Add(source, actor);
        source.enabled = false;
        return actor;
    }

    private SpriteRenderer CreateCopy(SpriteRenderer source, string name)
    {
        GameObject visual = new GameObject(name);
        visual.layer = source.gameObject.layer;
        visual.transform.SetParent(transform, false);
        SpriteRenderer copy = visual.AddComponent<SpriteRenderer>();
        CopyAppearance(source, copy);
        visual.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        visual.transform.localScale = source.transform.lossyScale;
        return copy;
    }

    private static void CopyAppearance(SpriteRenderer source, SpriteRenderer copy)
    {
        copy.sprite = source.sprite;
        copy.color = source.color;
        copy.flipX = source.flipX;
        copy.flipY = source.flipY;
        copy.sharedMaterial = source.sharedMaterial;
        copy.sortingLayerID = source.sortingLayerID;
        copy.sortingOrder = source.sortingOrder;
        copy.drawMode = source.drawMode;
        copy.size = source.size;
        copy.maskInteraction = source.maskInteraction;
    }

    private static Vector3 PixelOffset(Vector3 offset)
        => new Vector3(Mathf.Round(offset.x * PixelsPerUnit) / PixelsPerUnit,
            Mathf.Round(offset.y * PixelsPerUnit) / PixelsPerUnit, 0f);

    private static Vector3 TravelOffset(ActorVisual actor)
    {
        float remaining = 1f - Mathf.Clamp01(actor.TravelElapsed / KnockbackSeconds);
        return actor.TravelOffset * remaining * remaining * remaining;
    }

    private void UpdateActorPose(ActorVisual actor)
    {
        CopyAppearance(actor.Source, actor.Copy);
        float remaining = 1f - Mathf.Clamp01(actor.RecoilElapsed / RecoilSeconds);
        Vector3 recoil = actor.RecoilElapsed < 0f ? Vector3.zero
            : actor.RecoilDirection * (RecoilPixels / PixelsPerUnit * remaining * remaining);
        actor.Copy.transform.SetPositionAndRotation(actor.Source.transform.position
            + PixelOffset(TravelOffset(actor) + recoil), actor.Source.transform.rotation);
        actor.Copy.transform.localScale = actor.Source.transform.lossyScale;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        UpdateFragments(dt);
        outcomeRemaining = Mathf.Max(0f, outcomeRemaining - dt);
        outcomeDelay = Mathf.Max(0f, outcomeDelay - dt);
        expiredActors.Clear();
        foreach (var pair in actors)
        {
            ActorVisual actor = pair.Value;
            if (actor.Source == null || actor.Health == null || actor.Health.IsDead || !actor.Source.gameObject.activeInHierarchy)
            {
                RemoveActor(actor, false);
                expiredActors.Add(pair.Key);
                continue;
            }
            actor.RecoilElapsed += dt;
            if (actor.PendingFlash && actor.RecoilElapsed >= 0f)
            {
                actor.PendingFlash = false;
                actor.Health.GetComponent<DamageFlash>()?.Play();
            }
            actor.TravelElapsed += dt;
            if (actor.RecoilElapsed >= RecoilSeconds && actor.TravelElapsed >= KnockbackSeconds)
            {
                RemoveActor(actor, true);
                expiredActors.Add(pair.Key);
            }
            else UpdateActorPose(actor);
        }
        foreach (SpriteRenderer source in expiredActors) actors.Remove(source);

        for (int i = echoes.Count - 1; i >= 0; i--)
        {
            DeathEcho echo = echoes[i];
            echo.Elapsed += dt;
            echo.TravelElapsed += dt;
            if (echo.Elapsed >= echo.Lifetime)
            {
                HideAndDestroy(echo.Copy.gameObject);
                echoes.RemoveAt(i);
                continue;
            }
            float travel = Mathf.Pow(1f - Mathf.Clamp01(echo.TravelElapsed / KnockbackSeconds), 3f);
            if (echo.Elapsed < 0f)
            {
                echo.Copy.transform.position = echo.Position + PixelOffset(echo.TravelOffset * travel);
                continue;
            }
            float recoil = Mathf.Pow(1f - Mathf.Clamp01(echo.Elapsed / RecoilSeconds), 2f);
            echo.Copy.transform.position = echo.Position + PixelOffset(echo.TravelOffset * travel
                + echo.RecoilDirection * (RecoilPixels / PixelsPerUnit * recoil));
            if (echo.Animator != null)
            {
                // 사망 모션 자체가 소멸까지 그리므로 축소·페이드는 하지 않는다.
                echo.Animator.enabled = true;
                continue;
            }
            float progress = echo.Elapsed / DeathEchoSeconds;
            echo.Copy.transform.localScale = echo.Scale * Mathf.Lerp(1f, 0.35f, progress);
            Color color = echo.Elapsed < CoreGatherSeconds ? Color.white : echo.Color;
            color.a *= 1f - progress;
            echo.Copy.color = color;
        }

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark spark = sparks[i];
            spark.Elapsed += dt;
            if (spark.Elapsed >= SparkSeconds)
            {
                HideAndDestroy(spark.Root);
                sparks.RemoveAt(i);
                continue;
            }
            if (spark.Elapsed < 0f) continue;
            spark.Root.SetActive(true);
            float progress = spark.Elapsed / SparkSeconds;
            for (int ray = 0; ray < spark.Rays.Length; ray++)
            {
                SpriteRenderer renderer = spark.Rays[ray];
                Color tint = spark.Tint;
                tint.a *= 1f - progress;
                renderer.color = tint;
                Vector3 direction = Quaternion.Euler(0f, 0f, ray * 90f) * Vector3.right;
                renderer.transform.localPosition = PixelOffset(direction * Mathf.Lerp(2f, 5f, progress) / PixelsPerUnit);
            }
        }
    }

    private void SpawnSpark(SpriteRenderer source, bool lethal)
    {
        EnsurePixelSprite();
        GameObject root = new GameObject("CombatHitSpark");
        root.transform.SetParent(transform, false);
        root.transform.position = DamageVisualDelay <= 0f && actors.TryGetValue(source, out ActorVisual actor)
            ? actor.Copy.bounds.center : source.bounds.center;
        var rays = new SpriteRenderer[4];
        for (int i = 0; i < rays.Length; i++)
        {
            GameObject ray = new GameObject("Ray");
            ray.layer = source.gameObject.layer;
            ray.transform.SetParent(root.transform, false);
            ray.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
            float rayPixels = lethal ? 6f + (attackActive ? Mathf.Min(2, attackKills.Count - 1) : 0f)
                : powerAttack && DamageVisualDelay <= 0f ? 5f : 3f;
            ray.transform.localScale = new Vector3(rayPixels, 1f, 1f);
            ray.transform.localPosition = ray.transform.localRotation * Vector3.right * (2f / PixelsPerUnit);
            SpriteRenderer renderer = ray.AddComponent<SpriteRenderer>();
            renderer.sprite = pixelSprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + 2;
            rays[i] = renderer;
        }
        Color tint = lethal ? new Color(1f, 0.8f, 0.38f) : Color.white;
        foreach (SpriteRenderer ray in rays) ray.color = tint;
        root.SetActive(DamageVisualDelay <= 0f);
        sparks.Add(new Spark { Root = root, Rays = rays, Elapsed = -DamageVisualDelay, Tint = tint });
    }

    private void EnsurePixelSprite()
    {
        if (pixelSprite == null)
        {
            pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            pixelTexture.SetPixel(0, 0, Color.white);
            pixelTexture.Apply();
            pixelSprite = Sprite.Create(pixelTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f),
                PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }

    private void RemoveActor(ActorVisual actor, bool restore)
    {
        if (restore && actor.Source != null && actor.Health != null && !actor.Health.IsDead)
            actor.Source.enabled = actor.WasEnabled;
        if (actor.Copy != null) HideAndDestroy(actor.Copy.gameObject);
    }

    private static void HideAndDestroy(GameObject visual)
    {
        if (visual == null) return;
        visual.SetActive(false);
        Destroy(visual);
    }

    private void OnDestroy()
    {
        foreach (ActorVisual actor in actors.Values)
            if (actor.Source != null && actor.Health != null && !actor.Health.IsDead) actor.Source.enabled = actor.WasEnabled;
        if (pixelSprite != null) Destroy(pixelSprite);
        if (pixelTexture != null) Destroy(pixelTexture);
        if (instance == this) instance = null;
    }
}
