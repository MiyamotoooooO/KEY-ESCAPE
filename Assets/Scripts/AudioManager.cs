using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - BGMと効果音を、設定画面の音量（GameSettings）に合わせて鳴らす。
/// シーンに置かなくても、ゲーム開始時に自動で作られ、シーンが変わっても残り続ける
/// （DontDestroyOnLoad）。
///
/// BGM：
///   AudioManager.PlayBGM(clip, fadeTime);   // BGMを切り替える（同じ曲がすでに鳴っているなら何もしない）
///   AudioManager.StopBGM(fadeTime);         // BGMを止める
///   AudioManager.PushBGM(clip, fadeTime);   // 今のBGMを覚えてから、別のBGMに切り替える（画面を開く時用）
///   AudioManager.PopBGM(fadeTime);          // 覚えておいたBGMを、続きから再開する（画面を閉じる時用）
///
/// 効果音：
///   AudioManager.PlaySE(clip);
///   AudioManager.PlaySEAtPoint(clip, position);
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("BGM自体の基本音量（0〜1）。設定画面の音量は、これに掛け合わされる。")]
    [Range(0f, 1f)] public float bgmBaseVolume = 1f;

    // BGMは2つのAudioSourceを使い、片方を消しながらもう片方を出して、曲を切り替える（クロスフェード）。
    private readonly AudioSource[] _bgm = new AudioSource[2];
    private readonly float[] _fade = new float[2];   // 各AudioSourceのフェード係数（0〜1）
    private readonly float[] _trackVolume = { 1f, 1f }; // 各AudioSourceで鳴らしている曲の音量（曲ごとの音量調整。0〜1）
    private int _active;                             // 今のBGMを鳴らしているほうの番号
    private Coroutine _crossfade;

    private AudioSource _seSource;

    private struct BgmState
    {
        public AudioClip clip;
        public float time;
        public bool loop;
        public float volume;
    }
    private readonly Stack<BgmState> _stack = new Stack<BgmState>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;
        var go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // すでにあるなら、重複したほうを消す。
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < _bgm.Length; i++)
        {
            _bgm[i] = gameObject.AddComponent<AudioSource>();
            _bgm[i].playOnAwake = false;
            _bgm[i].loop = true;
            _bgm[i].spatialBlend = 0f; // 2D（位置に関係なく同じ音量）
            _fade[i] = 0f;
        }

        _seSource = gameObject.AddComponent<AudioSource>();
        _seSource.playOnAwake = false;
        _seSource.loop = false;
        _seSource.spatialBlend = 0f;

        GameSettings.Changed += ApplyVolumes;
        ApplyVolumes();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            GameSettings.Changed -= ApplyVolumes;
            Instance = null;
        }
    }

    private void ApplyVolumes()
    {
        float v = bgmBaseVolume * GameSettings.BgmVolume;
        for (int i = 0; i < _bgm.Length; i++)
        {
            if (_bgm[i] != null)
                _bgm[i].volume = v * _trackVolume[i] * _fade[i];
        }
    }

    // ---- BGM ------------------------------------------------------------

    /// <summary>
    /// BGMを切り替える。同じ曲がすでに鳴っているなら、曲はそのまま（シーンをまたいでも途切れない）。
    /// volumeは、この曲だけの音量（0〜1）。設定画面のBGM音量に、さらに掛け合わされる。
    /// </summary>
    public static void PlayBGM(AudioClip clip, float fadeTime = 1f, bool loop = true, float volume = 1f)
    {
        if (Instance == null) return;
        Instance.SwitchTo(clip, fadeTime, loop, 0f, volume);
    }

    /// <summary>BGMを止める。</summary>
    public static void StopBGM(float fadeTime = 1f)
    {
        if (Instance == null) return;
        Instance.SwitchTo(null, fadeTime, true, 0f, 1f);
    }

    /// <summary>今のBGMと再生位置を覚えてから、別のBGMに切り替える。clipがnullなら、覚えるだけで曲は切り替えない。</summary>
    public static void PushBGM(AudioClip clip, float fadeTime = 0.5f, bool loop = true, float volume = 1f)
    {
        if (Instance == null) return;
        Instance.PushInternal(clip, fadeTime, loop, volume);
    }

    /// <summary>PushBGMで覚えたBGMを、覚えた位置から再開する。</summary>
    public static void PopBGM(float fadeTime = 0.5f)
    {
        if (Instance == null) return;
        Instance.PopInternal(fadeTime);
    }

    private void PushInternal(AudioClip clip, float fadeTime, bool loop, float volume)
    {
        AudioSource cur = _bgm[_active];
        _stack.Push(new BgmState
        {
            clip = cur.isPlaying ? cur.clip : null,
            time = cur.time,
            loop = cur.loop,
            volume = _trackVolume[_active]
        });

        if (clip != null)
            SwitchTo(clip, fadeTime, loop, 0f, volume);
    }

    private void PopInternal(float fadeTime)
    {
        if (_stack.Count == 0)
            return;
        BgmState s = _stack.Pop();
        SwitchTo(s.clip, fadeTime, s.loop, s.time, s.volume);
    }

    private void SwitchTo(AudioClip clip, float fadeTime, bool loop, float startTime, float volume)
    {
        AudioSource cur = _bgm[_active];
        volume = Mathf.Clamp01(volume);

        // 同じ曲がすでに鳴っているなら、曲はそのまま（音量だけ、頼まれた値にする）。
        // 無音を頼まれた時に、すでに無音でも何もしない。
        if (clip == null ? !cur.isPlaying : (cur.clip == clip && cur.isPlaying))
        {
            if (clip != null && !Mathf.Approximately(_trackVolume[_active], volume))
            {
                _trackVolume[_active] = volume;
                ApplyVolumes();
            }
            return;
        }

        if (_crossfade != null)
            StopCoroutine(_crossfade);

        int old = _active;
        int next = 1 - _active;

        // 前回のフェードアウトが残っていても、ここで打ち切って、次の曲に使い回す。
        _bgm[next].Stop();
        _bgm[next].clip = clip;
        _bgm[next].loop = loop;
        _trackVolume[next] = volume;
        _fade[next] = 0f;

        if (clip != null)
        {
            _bgm[next].Play();
            if (startTime > 0f)
                _bgm[next].time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.05f));
        }

        _active = next;
        _crossfade = StartCoroutine(Crossfade(old, next, fadeTime));
    }

    private IEnumerator Crossfade(int old, int next, float duration)
    {
        float oldStart = _fade[old];

        if (duration > 0f)
        {
            float t = 0f;
            while (t < duration)
            {
                // ゲームを止めている間（設定画面など）も進めるため、unscaledを使う。
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                _fade[next] = k;
                _fade[old] = oldStart * (1f - k);
                ApplyVolumes();
                yield return null;
            }
        }

        _fade[next] = 1f;
        _fade[old] = 0f;
        ApplyVolumes();
        _bgm[old].Stop();
        _crossfade = null;
    }

    // ---- 効果音 ------------------------------------------------------------

    /// <summary>効果音を鳴らす（位置に関係なく、設定のSE音量で）。</summary>
    public static void PlaySE(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || Instance == null)
            return;
        Instance._seSource.PlayOneShot(clip, volumeScale * GameSettings.SeVolume);
    }

    /// <summary>AudioSource.PlayClipAtPointの代わり。位置のある効果音を、設定のSE音量で鳴らす。</summary>
    public static void PlaySEAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null)
            return;
        AudioSource.PlayClipAtPoint(clip, position, volumeScale * GameSettings.SeVolume);
    }
}