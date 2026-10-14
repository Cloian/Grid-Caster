using System;
using UnityEngine;

public enum CombatSound { MagicLaunch, SoftHit, HardHit, PlayerHurt, KnightLand, FireTrail, BlockedMove }

// 소리는 판정 뒤에만 호출한다. 재생 시간/음성 제한은 턴의 완료 조건이 아니다.
[DisallowMultipleComponent]
public sealed class GameAudio : MonoBehaviour
{
    public const string SfxVolumeKey = "GridCaster.Audio.SfxVolume";
    public const string MusicVolumeKey = "GridCaster.Audio.MusicVolume";
    public const float DefaultSfxVolume = 0.6f;
    public const float DefaultMusicVolume = 0.5f;
    private static GameAudio instance;
    private static readonly string[] ClipNames = {
        "01_magic_soft_laser", "02_magic_hit_soft", "03_magic_hit_hard",
        "04_player_hurt", "05_knight_land", "06_fire_trail"
    };
    private static readonly float[] Gains = { 0.72f, 0.78f, 0.6f, 0.9f, 0.5f, 0.45f, 1f };
    private static readonly float[] MinimumIntervals = { 0f, 0.035f, 0.035f, 0.08f, 0.12f, 0.15f, 0.08f };
    private readonly AudioClip[] clips = new AudioClip[6];
    private readonly float[] lastPlayed = new float[7];
    private readonly float[] lastImportant = new float[7];
    private readonly int[] playedCounts = new int[7];
    private readonly Voice[] voices = new Voice[4];
    private AudioSource[] musicSources;
    public float SfxVolume { get; private set; }
    public float MusicVolume { get; private set; }

    private sealed class Voice
    {
        public AudioSource Source;
        public CombatSound Kind;
        public float Gain;
        public float Started;
        public float Duck = 1f;
    }

    public static GameAudio GetOrCreate()
    {
        if (instance == null)
            instance = new GameObject("GameAudio").AddComponent<GameAudio>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i] = Resources.Load<AudioClip>("CombatSFX/" + ClipNames[i]);
            if (clips[i] == null) Debug.LogError("전투 효과음 누락: " + ClipNames[i], this);
        }
        for (int i = 0; i < lastPlayed.Length; i++)
            lastPlayed[i] = lastImportant[i] = float.NegativeInfinity;
        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = i == 3 ? 32 : 64;
            voices[i] = new Voice { Source = source };
        }
        musicSources = Array.FindAll(FindObjectsByType<AudioSource>(),
            source => source.loop && source.gameObject.scene == gameObject.scene);
        ApplyVolumes();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    public void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        ApplyVolumes();
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        ApplyVolumes();
    }

    public void SaveSettings() => PlayerPrefs.Save();
    private void OnApplicationPause(bool paused) { if (paused) SaveSettings(); }
    private void OnApplicationQuit() => SaveSettings();

    private void ApplyVolumes()
    {
        foreach (Voice voice in voices)
            if (voice != null) voice.Source.volume = SfxVolume * voice.Gain * voice.Duck;
        if (musicSources != null)
            foreach (AudioSource source in musicSources)
                if (source != null) source.volume = MusicVolume;
    }

    public static bool Play(CombatSound kind) => GetOrCreate().PlayClip(kind, null, 1f);
    public static bool PlayBlockedMove(AudioClip clip, float gain) =>
        GetOrCreate().PlayClip(CombatSound.BlockedMove, clip, gain);

    public static bool PlayImpact(CombatSound kind, float pitch, float gain = 1f, bool important = false) =>
        GetOrCreate().PlayClip(kind, null, gain, pitch, important);

    private bool PlayClip(CombatSound kind, AudioClip overrideClip, float gain, float pitch = 1f, bool important = false)
    {
        int index = (int)kind;
        AudioClip clip = overrideClip != null ? overrideClip : index < clips.Length ? clips[index] : null;
        float now = Time.unscaledTime;
        if (clip == null || SfxVolume <= 0f) return false;
        bool impact = kind == CombatSound.SoftHit || kind == CombatSound.HardHit;
        if (impact)
            foreach (Voice voice in voices)
                if (voice.Kind == CombatSound.MagicLaunch && voice.Source.isPlaying)
                {
                    voice.Duck = 0.25f;
                    voice.Source.volume = SfxVolume * voice.Gain * voice.Duck;
                }
        if (now - (important ? lastImportant[index] : lastPlayed[index]) < MinimumIntervals[index]) return false;

        // 피격용 한 음성을 따로 확보한다. 일반 효과는 최대 세 음성, 동시 환경음은 한 종류당 한 번.
        Voice chosen = kind == CombatSound.PlayerHurt ? voices[3] : null;
        bool environment = IsEnvironment(kind);
        if (environment)
            for (int i = 0; i < 3; i++)
                if (voices[i].Kind == kind && voices[i].Source.isPlaying) return false;
        if (chosen == null)
            for (int i = 0; i < 3; i++)
                if (!voices[i].Source.isPlaying) { chosen = voices[i]; break; }
        // 적중/발사는 오래된 환경음보다 우선한다. 중요한 전투 소리를 환경음으로 끊지 않는다.
        if (chosen == null && !environment)
            for (int i = 0; i < 3; i++)
                if (IsEnvironment(voices[i].Kind) && (chosen == null || voices[i].Started < chosen.Started))
                    chosen = voices[i];
        // 발사 꼬리가 세 음성을 채워도 실제 명중은 들리게 한다. 피격용 음성은 별도로 유지한다.
        if (chosen == null && impact)
            for (int i = 0; i < 3; i++)
                if (voices[i].Kind == CombatSound.MagicLaunch
                    && (chosen == null || voices[i].Started < chosen.Started)) chosen = voices[i];
        // 처치는 이미 재생 중인 일반 명중보다 우선한다. 피격 전용 음성과 전체 음성 상한은 유지한다.
        if (chosen == null && impact && important)
            for (int i = 0; i < 3; i++)
                if (chosen == null || voices[i].Started < chosen.Started) chosen = voices[i];
        if (chosen == null) return false;
        chosen.Source.Stop();
        chosen.Kind = kind;
        chosen.Gain = Gains[index] * Mathf.Clamp01(gain);
        chosen.Started = now;
        chosen.Duck = 1f;
        chosen.Source.clip = clip;
        chosen.Source.pitch = pitch;
        chosen.Source.volume = SfxVolume * chosen.Gain;
        chosen.Source.Play();
        lastPlayed[index] = now;
        if (important) lastImportant[index] = now;
        playedCounts[index]++;
        return true;
    }

    private static bool IsEnvironment(CombatSound kind) =>
        kind == CombatSound.KnightLand || kind == CombatSound.FireTrail || kind == CombatSound.BlockedMove;
}
