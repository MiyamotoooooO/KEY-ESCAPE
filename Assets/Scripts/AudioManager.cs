using UnityEngine;

/// <summary>
/// KEY ESCAPE - BGMと効果音を、設定画面の音量（GameSettings）に合わせて鳴らす。
/// シーンに置かなくても、ゲーム開始時に自動で作られ、シーンが変わっても残り続ける
/// （DontDestroyOnLoad）。
///
/// 使い方：
///   AudioManager.PlayBGM(clip);                       // BGMをループ再生（同じ曲なら何もしない）
///   AudioManager.StopBGM();
///   AudioManager.PlaySE(clip);                        // 効果音を鳴らす（設定のSE音量が掛かる）
///   AudioManager.PlaySE(clip, volumeScale: 0.6f);     // その効果音だけ音量を下げたい時
///   AudioManager.PlaySEAtPoint(clip, position);       // AudioSource.PlayClipAtPointの代わり
///
/// 設定画面でBGM音量を変えると、再生中のBGMにもすぐ反映される。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("BGM自体の基本音量（0〜1）。設定画面の音量は、これに掛け合わされる。")]
    [Range(0f, 1f)] public float bgmBaseVolume = 1f;

    private AudioSource _bgmSource;
    private AudioSource _seSource;

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

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f; // 2D（位置に関係なく同じ音量）

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
        if (_bgmSource != null)
            _bgmSource.volume = bgmBaseVolume * GameSettings.BgmVolume;
    }

    // ---- BGM ------------------------------------------------------------

    public static void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (Instance != null)
            Instance.PlayBGMInternal(clip, loop);
    }

    public static void StopBGM()
    {
        if (Instance != null)
            Instance._bgmSource.Stop();
    }

    private void PlayBGMInternal(AudioClip clip, bool loop)
    {
        if (clip == null)
            return;
        // 同じ曲がすでに鳴っているなら、頭から再生し直さない
        // （シーンが変わっても、同じBGMなら続けて鳴らせる）。
        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
            return;

        _bgmSource.clip = clip;
        _bgmSource.loop = loop;
        ApplyVolumes();
        _bgmSource.Play();
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