using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - プレイヤーの被弾・死亡の演出をまとめて担当する。
///
///   PlayDamage()        被弾：画面の縁が赤くなり（うるさくない程度）、カメラが揺れ、効果音が鳴る。
///   PlayDeath()         死亡：スロー → 白フラッシュ → カメラがプレイヤーに寄る → 暗転。
///   ReloadWithFade()    落下死など：短く暗転してからシーンを読み直す。
///
/// シーンやプレハブには何も置かなくてよい。初めて呼ばれた時に、自分で画面用のキャンバスを作る。
/// 時間はすべて「実時間」で数えるので、スローやポーズ中でも演出がずれない。
/// 効果音は SoundEffects の PlayerDamage / PlayerDeath / DeathFlash に clip を入れると鳴る。
/// </summary>
public class HitFeedback : MonoBehaviour
{
    // ================= 調整用の数値（ここを変えると演出が変わる） =================

    // --- 被弾 ---
    const float DamageVignetteAlpha = 0.55f;  // 赤い縁の濃さ（0〜1）。大きいほど目立つ
    const float DamageVignetteHold  = 0.10f;  // 最も濃い状態を保つ秒数
    const float DamageVignetteFade  = 0.45f;  // 消えるまでの秒数
    const float DamageShakeTime     = 0.25f;  // 揺れの秒数
    const float DamageShakeScale    = 0.8f;   // 揺れの強さ（CameraFollow.shakeAmount の何倍か）

    // --- 死亡 ---
    const float DeathSlowScale      = 0.25f;  // スロー時の時間の速さ（0.25 = 1/4）
    const float DeathSlowTime       = 0.8f;   // スローの秒数（実時間）
    const float DeathSlowReturn     = 0.3f;   // スローの最後のこの秒数で、通常の速さへ戻していく
    const float DeathZoomRatio      = 0.6f;   // カメラが寄る量（0.6 = 画面の広さが6割になる）
    const float DeathZoomTime       = 0.8f;   // 寄るのにかける秒数
    const float DeathFlashAlpha     = 0.85f;  // 白フラッシュの最初の濃さ
    const float DeathFlashFade      = 0.35f;  // フラッシュが消える秒数
    const float DeathVignetteAlpha  = 0.65f;  // 死亡中に残る赤い縁の濃さ
    const float DeathFadeDelay      = 1.2f;   // 死亡してから暗転を始めるまでの秒数
    const float DeathFadeTime       = 0.6f;   // 暗転にかける秒数

    // --- シーン読み込み後 ---
    const float FadeInAfterLoad     = 0.5f;   // 暗転から明るく戻る秒数

    // 読み込み直後などの長いフレームで演出が飛ばないよう、1フレームの最大時間を決めておく
    const float MaxDt = 0.05f;

    // ==============================================================================

    private static HitFeedback _instance;
    private static bool _reloading;

    /// <summary>死亡演出の最中か。</summary>
    public static bool IsDying { get; private set; }

    private Image _vignette;
    private Image _flash;
    private Image _fade;

    private Coroutine _vignetteRoutine;
    private Coroutine _deathRoutine;

    private bool _slowActive;
    private float _baseFixedDelta = 0.02f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _reloading = false;
        IsDying = false;
    }

    private static HitFeedback Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("HitFeedback");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HitFeedback>();
            }
            return _instance;
        }
    }

    // ================= 外から呼ぶ =================

    /// <summary>被弾した時（死なない場合）の演出。</summary>
    public static void PlayDamage()
    {
        SoundEffects.Play(SEId.PlayerDamage);
        Instance.PulseVignette(DamageVignetteAlpha, DamageVignetteHold, DamageVignetteFade);

        CameraFollow cam = FindObjectOfType<CameraFollow>();
        if (cam != null)
            cam.ShakeCamera(DamageShakeTime, cam.shakeAmount * DamageShakeScale);
    }

    /// <summary>死亡した瞬間の演出。呼んだあとのシーン読み直しは、呼び出し側で行う。</summary>
    /// <param name="cause">死因（結果画面の一言コメントを選ぶのに使う）。</param>
    public static void PlayDeath(DeathCause cause = DeathCause.Enemy)
    {
        if (IsDying)
            return;
        IsDying = true;

        StageStats.RecordDeath(cause); // 結果画面用：死んだ回数と、その瞬間の写真

        SoundEffects.Play(SEId.PlayerDeath);
        SoundEffects.Play(SEId.DeathFlash);
        Instance.StartDeath();
    }

    /// <summary>落下死など：短く暗転してから、今のシーンを読み直す。</summary>
    public static void ReloadWithFade(float fadeTime = 0.35f, DeathCause cause = DeathCause.Fall)
    {
        if (IsDying || _reloading)
            return;
        _reloading = true;

        StageStats.RecordDeath(cause); // 結果画面用：死んだ回数と、その瞬間の写真
        HitFeedback fb = Instance;
        fb.StartCoroutine(fb.ReloadRoutine(fadeTime));
    }

    // ================= 初期化 =================

    private void Awake()
    {
        _baseFixedDelta = Time.fixedDeltaTime;
        BuildOverlay();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreTime();
        if (_instance == this)
            _instance = null;
    }

    private void BuildOverlay()
    {
        GameObject canvasObj = new GameObject("HitFeedbackCanvas");
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000; // 他のUIよりも手前

        _vignette = MakeImage("Vignette", canvasObj.transform, BuildVignetteSprite(), Color.white);
        _flash = MakeImage("Flash", canvasObj.transform, null, Color.white);
        _fade = MakeImage("Fade", canvasObj.transform, null, Color.black);

        SetImageAlpha(_vignette, 0f);
        SetImageAlpha(_flash, 0f);
        SetImageAlpha(_fade, 0f);
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false; // ボタンなどの操作をじゃましない
        return img;
    }

    /// <summary>中心が透明で、画面の縁に向かって赤くなる画像を、コードで作る。</summary>
    private static Sprite BuildVignetteSprite()
    {
        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float nx = (x + 0.5f) / N * 2f - 1f;
                float ny = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny) / 1.4142f; // 中心0、角1
                float a = Mathf.InverseLerp(0.45f, 1f, d);
                a = a * a * (3f - 2f * a); // なめらかに
                px[y * N + x] = new Color32(230, 20, 20, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void SetImageAlpha(Image img, float a)
    {
        if (img == null)
            return;
        Color c = img.color;
        c.a = a;
        img.color = c;
        img.enabled = a > 0.001f; // 見えない時は描かない
    }

    // ================= 被弾 =================

    private void PulseVignette(float peak, float hold, float fade)
    {
        if (IsDying)
            return; // 死亡演出中は、そちらが赤い縁を管理する
        if (_vignetteRoutine != null)
            StopCoroutine(_vignetteRoutine);
        _vignetteRoutine = StartCoroutine(VignetteRoutine(peak, hold, fade));
    }

    private IEnumerator VignetteRoutine(float peak, float hold, float fade)
    {
        SetImageAlpha(_vignette, peak);

        float t = 0f;
        while (t < hold)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxDt);
            yield return null;
        }

        t = 0f;
        while (t < fade)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxDt);
            float k = Mathf.Clamp01(t / fade);
            SetImageAlpha(_vignette, peak * (1f - k) * (1f - k));
            yield return null;
        }

        SetImageAlpha(_vignette, 0f);
        _vignetteRoutine = null;
    }

    // ================= 死亡 =================

    private void StartDeath()
    {
        if (_vignetteRoutine != null)
        {
            StopCoroutine(_vignetteRoutine);
            _vignetteRoutine = null;
        }
        if (_deathRoutine != null)
            StopCoroutine(_deathRoutine);

        CameraFollow cam = FindObjectOfType<CameraFollow>();
        _deathRoutine = StartCoroutine(DeathRoutine(cam));
    }

    private IEnumerator DeathRoutine(CameraFollow cam)
    {
        SetImageAlpha(_flash, DeathFlashAlpha);
        SetImageAlpha(_vignette, DeathVignetteAlpha);

        // スロー開始（ポーズ中や、すでに時間が止まっている時は、触らない）
        if (!GamePause.IsPaused && Time.timeScale > 0.99f)
        {
            Time.timeScale = DeathSlowScale;
            Time.fixedDeltaTime = _baseFixedDelta * DeathSlowScale;
            _slowActive = true;
        }

        if (cam != null)
        {
            cam.ShakeCamera(0.3f, cam.shakeAmount);
            cam.ZoomTo(DeathZoomRatio, DeathZoomTime);
        }

        float t = 0f;
        float total = DeathFadeDelay + DeathFadeTime;
        while (t < total)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxDt);

            // 白フラッシュ：一瞬で白くなり、すっと消える
            SetImageAlpha(_flash, DeathFlashAlpha * (1f - Mathf.Clamp01(t / DeathFlashFade)));

            // スロー：最後の少しで、通常の速さへ戻していく
            if (_slowActive && !GamePause.IsPaused && Time.timeScale > 0f)
            {
                float back = Mathf.InverseLerp(DeathSlowTime - DeathSlowReturn, DeathSlowTime, t);
                float scale = Mathf.Lerp(DeathSlowScale, 1f, back);
                Time.timeScale = scale;
                Time.fixedDeltaTime = _baseFixedDelta * scale;
                if (t >= DeathSlowTime)
                    RestoreTime();
            }

            // 暗転
            if (t > DeathFadeDelay)
                SetImageAlpha(_fade, Mathf.Clamp01((t - DeathFadeDelay) / DeathFadeTime));

            yield return null;
        }

        RestoreTime();
        SetImageAlpha(_flash, 0f);
        SetImageAlpha(_fade, 1f);
        _deathRoutine = null;
    }

    private void RestoreTime()
    {
        if (!_slowActive)
            return;
        _slowActive = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _baseFixedDelta;
    }

    // ================= 落下死 =================

    private IEnumerator ReloadRoutine(float fadeTime)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxDt);
            SetImageAlpha(_fade, Mathf.Clamp01(t / fadeTime));
            yield return null;
        }
        SetImageAlpha(_fade, 1f);
        LoadingScreen.Reload();
    }

    // ================= シーン読み込み後 =================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        // 前のシーンの演出を、すべて片づける
        StopAllCoroutines();
        _vignetteRoutine = null;
        _deathRoutine = null;
        RestoreTime();
        IsDying = false;
        _reloading = false;

        SetImageAlpha(_vignette, 0f);
        SetImageAlpha(_flash, 0f);

        // 暗転したまま読み込まれた時は、明るく戻す
        if (_fade != null && _fade.color.a > 0.01f)
            StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        float start = _fade.color.a;
        float t = 0f;
        while (t < FadeInAfterLoad)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxDt);
            SetImageAlpha(_fade, start * (1f - Mathf.Clamp01(t / FadeInAfterLoad)));
            yield return null;
        }
        SetImageAlpha(_fade, 0f);
    }
}
