using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>死因。結果画面の一言コメントを選ぶのに使う。</summary>
public enum DeathCause
{
    Fall,   // 落下
    Enemy,  // 敵にやられた
    Other,  // その他
}

/// <summary>
/// KEY ESCAPE - ステージ挑戦中の記録（クリアタイム、死亡回数、死んだ瞬間の写真）。結果画面で使う。
///
///   ・ステージに入ると、記録を始める（StageRunから）。やられて同じステージが読み直されても、続けて数える。
///   ・タイムは、ポーズ中（会話・設定・キーコンフィグなど）と、やられている最中は数えない。
///   ・ゴールすると、止める（StageGoalから）。
///   ・拠点に戻ると、記録を消す（StageRunから）。
///   ・写真は、死んだ瞬間のゲーム画面だけ（HPゲージやフラッシュは写らない）を、メモリに持つ。最大 MaxPhotos 枚。
///     プレイヤーのまわりだけを切り抜いて撮る。1回の死につき、死んだ瞬間から約 ClipSeconds 秒のあいだ、
///     FrameInterval 秒ごとに撮り続けて、「倒れるまでの動き」（連続写真）にする。結果画面は、これを繰り返し再生する。
/// どこにも付けなくてよい。
/// </summary>
public static class StageStats
{
    /// <summary>死んだ瞬間の写真1枚分。</summary>
    public class DeathPhoto
    {
        /// <summary>死んだ瞬間の1枚目（frames[0]と同じもの）。</summary>
        public Texture2D texture;
        /// <summary>死んだ瞬間から、倒れるまでの連続写真（古い順）。1枚だけのこともある。</summary>
        public List<Texture2D> frames = new List<Texture2D>();
        /// <summary>連続写真の、1枚と1枚の間の秒数。</summary>
        public float frameInterval = FrameInterval;
        /// <summary>プレイヤーが、写真のどこにいるか（0〜1。左下が0,0）。写真を切り抜く中心に使う。</summary>
        public Vector2 playerView = new Vector2(0.5f, 0.5f);
        public DeathCause cause;
    }

    const int   MaxPhotos   = 8;     // 持っておく死亡の最大数（古いものから捨てる）。1回の死が、連続写真の数だけ画像を持つ
    const int   OutWidth    = 336;   // 写真1枚の大きさ（ピクセル）。結果画面の枠の縦横比に近くしておく
    const int   OutHeight   = 270;
    const float ViewScale   = 0.5f;  // 死んだ瞬間のカメラの高さの、何割を写すか（小さいほど、プレイヤーに寄る。以前は0.7）
    const float FrameInterval = 0.12f; // 連続写真の間隔（実時間の秒）
    const float ClipSeconds = 1.4f;  // 死んでから、連続写真を撮り続ける秒数（実時間）
    const int   MaxFrames   = 12;    // 連続写真の、最大の枚数
    const float MaxStep     = 0.1f;  // 1フレームで数える最大の秒数（読み込み直後の長いフレーム対策）

    /// <summary>いま記録しているステージのID。空なら、記録していない。</summary>
    public static string StageId { get; private set; } = "";
    /// <summary>記録しているか（ステージに入って、まだ拠点に戻っていない）。</summary>
    public static bool IsTracking { get { return !string.IsNullOrEmpty(StageId); } }
    /// <summary>ステージに入ってからの秒数（ポーズ中は数えない）。</summary>
    public static float ElapsedTime { get; private set; }
    /// <summary>死んだ回数（敵にやられた・落下）。</summary>
    public static int DeathCount { get; private set; }

    private static readonly List<DeathPhoto> _photos = new List<DeathPhoto>();
    private static bool _running;
    private static Ticker _ticker;

    /// <summary>死んだ瞬間の写真（古い順）。</summary>
    public static IReadOnlyList<DeathPhoto> Photos { get { return _photos; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        StageId = "";
        ElapsedTime = 0f;
        DeathCount = 0;
        _photos.Clear();
        _running = false;
        _ticker = null;
    }

    // ================= StageRun / StageGoal から呼ぶ =================

    /// <summary>新しく挑戦を始める（前の記録は消す）。</summary>
    public static void Begin(string stageId)
    {
        Clear();
        StageId = stageId ?? "";
        _running = IsTracking;
        EnsureTicker();
    }

    /// <summary>このステージの記録が無ければ、始める（ゲームを閉じて、続きから始めた時のため）。あれば、続ける。</summary>
    public static void EnsureRun(string stageId)
    {
        if (StageId != stageId)
            Begin(stageId);
    }

    /// <summary>数えるのを止める（ゴールした時）。</summary>
    public static void Stop()
    {
        _running = false;
    }

    /// <summary>記録をすべて消す（拠点に戻った時）。</summary>
    public static void Clear()
    {
        if (_ticker != null)
            _ticker.StopClip();
        for (int i = 0; i < _photos.Count; i++)
            DestroyPhoto(_photos[i]);
        _photos.Clear();
        StageId = "";
        ElapsedTime = 0f;
        DeathCount = 0;
        _running = false;
    }

    // ================= 死亡 =================

    /// <summary>死んだ時に呼ぶ。回数を数えて、その瞬間のゲーム画面を撮る。死亡の演出が始まる前に呼ぶこと。</summary>
    public static void RecordDeath(DeathCause cause)
    {
        if (!IsTracking || !_running)
            return;

        DeathCount++;

        DeathPhoto photo = null;
        try
        {
            photo = Capture(cause);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[StageStats] 死んだ瞬間の写真を撮れませんでした。 " + e.Message);
        }
        if (photo == null)
            return;

        _photos.Add(photo);
        while (_photos.Count > MaxPhotos)
        {
            DestroyPhoto(_photos[0]);
            _photos.RemoveAt(0);
        }

        // このあとの動き（倒れるまで）も、続けて撮る。
        EnsureTicker();
        if (_ticker != null)
            _ticker.StartClip(photo);
    }

    private static void DestroyPhoto(DeathPhoto p)
    {
        if (p == null)
            return;
        if (p.frames != null)
        {
            for (int i = 0; i < p.frames.Count; i++)
            {
                if (p.frames[i] != null)
                    Object.Destroy(p.frames[i]);
            }
            p.frames.Clear();
        }
        if (p.texture != null)
            Object.Destroy(p.texture);
        p.texture = null;
    }

    /// <summary>結果画面に出す写真を、最大max枚選ぶ。多い時はランダムで選んで、古い順に並べる。</summary>
    public static List<DeathPhoto> PickPhotos(int max)
    {
        var result = new List<DeathPhoto>();
        if (max <= 0 || _photos.Count == 0)
            return result;

        if (_photos.Count <= max)
        {
            result.AddRange(_photos);
            return result;
        }

        var indices = new List<int>();
        for (int i = 0; i < _photos.Count; i++)
            indices.Add(i);
        var chosen = new List<int>();
        for (int n = 0; n < max; n++)
        {
            int pick = Random.Range(0, indices.Count);
            chosen.Add(indices[pick]);
            indices.RemoveAt(pick);
        }
        chosen.Sort();
        for (int i = 0; i < chosen.Count; i++)
            result.Add(_photos[chosen[i]]);
        return result;
    }

    /// <summary>ゲームのカメラを探す。</summary>
    private static Camera FindCamera()
    {
        Camera cam = null;
        CameraFollow follow = Object.FindObjectOfType<CameraFollow>();
        if (follow != null)
            cam = follow.GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
        return cam;
    }

    /// <summary>プレイヤーの、真ん中あたりの位置（当たり判定があれば、その中心）。</summary>
    private static Vector3 PlayerCenter(Transform player)
    {
        Collider2D col = player.GetComponent<Collider2D>();
        return col != null ? col.bounds.center : player.position;
    }

    /// <summary>死んだ瞬間の1枚目を撮る。プレイヤーのまわりだけを切り抜く。</summary>
    private static DeathPhoto Capture(DeathCause cause)
    {
        Camera cam = FindCamera();
        if (cam == null)
            return null;

        CharacterController2D playerCtl = Object.FindObjectOfType<CharacterController2D>();
        Transform player = playerCtl != null ? playerCtl.transform : null;

        // 写す高さ（ワールドの長さ）を、ここで決める。連続写真のあいだは、カメラが寄っても、同じ大きさで写す。
        float worldH = cam.orthographic ? cam.orthographicSize * 2f * ViewScale : 0f;

        Vector2 view;
        bool inView;
        Texture2D tex = CaptureFrame(cam, player, worldH, out view, out inView);
        if (tex == null)
            return null;

        DeathPhoto photo = new DeathPhoto();
        photo.texture = tex;
        photo.frames.Add(tex);
        photo.cause = cause;
        photo.playerView = view;
        return photo;
    }

    /// <summary>
    /// カメラに映っているものだけを、プレイヤーを真ん中にして切り抜き、1枚の画像にする。
    /// worldHは、写すワールドの高さ（0なら、画面の ViewScale 割）。viewは、プレイヤーが画像のどこにいるか（0〜1）。
    /// inViewは、プレイヤーがまだ画面に映っているか。
    /// </summary>
    private static Texture2D CaptureFrame(Camera cam, Transform player, float worldH, out Vector2 view, out bool inView)
    {
        view = new Vector2(0.5f, 0.5f);
        inView = true;

        float aspect = Mathf.Max(0.5f, cam.aspect);
        float outAspect = OutWidth / (float)OutHeight;

        // 切り抜く範囲（画面全体を1としたときの、縦と横の割合）
        float fh = ViewScale;
        if (cam.orthographic && worldH > 0.0001f)
            fh = Mathf.Clamp(worldH / (cam.orthographicSize * 2f), 0.15f, 1f);
        float fw = fh * outAspect / aspect;
        if (fw > 1f)
        {
            fw = 1f;
            fh = Mathf.Min(1f, fw * aspect / outAspect);
        }

        // 中心：プレイヤー（見つからなければ、画面の真ん中）
        Vector2 center = new Vector2(0.5f, 0.5f);
        if (player != null)
        {
            Vector3 v = cam.WorldToViewportPoint(PlayerCenter(player));
            center = new Vector2(v.x, v.y);
            inView = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        }
        float x0 = Mathf.Clamp(center.x - fw * 0.5f, 0f, 1f - fw);
        float y0 = Mathf.Clamp(center.y - fh * 0.5f, 0f, 1f - fh);
        view = new Vector2(Mathf.Clamp01((center.x - x0) / fw), Mathf.Clamp01((center.y - y0) / fh));

        // 画面を、切り抜く大きさがちょうどよい細かさになるように描いて、そこから切り抜く。
        int rtH = Mathf.Clamp(Mathf.RoundToInt(OutHeight / fh), OutHeight, 1080);
        int rtW = Mathf.Max(16, Mathf.RoundToInt(rtH * aspect));

        RenderTexture full = RenderTexture.GetTemporary(rtW, rtH, 24, RenderTextureFormat.ARGB32);
        RenderTexture crop = RenderTexture.GetTemporary(OutWidth, OutHeight, 0, RenderTextureFormat.ARGB32);
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;

        Texture2D tex = new Texture2D(OutWidth, OutHeight, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        try
        {
            cam.targetTexture = full;
            cam.Render();
            cam.targetTexture = prevTarget;
            Graphics.Blit(full, crop, new Vector2(fw, fh), new Vector2(x0, y0));
            RenderTexture.active = crop;
            tex.ReadPixels(new Rect(0, 0, OutWidth, OutHeight), 0, 0);
            tex.Apply();
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(full);
            RenderTexture.ReleaseTemporary(crop);
        }
        return tex;
    }

    // ================= 時間を数える =================

    private static void EnsureTicker()
    {
        if (_ticker != null)
            return;
        GameObject go = new GameObject("StageStatsTicker");
        go.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(go);
        _ticker = go.AddComponent<Ticker>();
    }

    private class Ticker : MonoBehaviour
    {
        // 倒れるまでの連続写真を撮っている最中の、死亡1回分
        private DeathPhoto _clip;
        private Transform _player;
        private float _worldH;
        private float _acc;
        private float _elapsed;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // シーンが読み直されたら、撮るのをやめる。
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _clip = null;
            _player = null;
        }

        public void StopClip()
        {
            _clip = null;
        }

        /// <summary>死んだ瞬間の1枚目を撮ったあと、続きの写真を撮り始める。</summary>
        public void StartClip(DeathPhoto photo)
        {
            _clip = photo;
            _acc = 0f;
            _elapsed = 0f;

            Camera cam = FindCamera();
            CharacterController2D ctl = Object.FindObjectOfType<CharacterController2D>();
            _player = ctl != null ? ctl.transform : null;
            _worldH = (cam != null && cam.orthographic) ? cam.orthographicSize * 2f * ViewScale : 0f;
            if (cam == null || _player == null)
                _clip = null;
        }

        private void RecordClip()
        {
            if (_clip == null)
                return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            _acc += dt;
            _elapsed += dt;
            if (_elapsed > ClipSeconds || _clip.frames.Count >= MaxFrames || _player == null)
            {
                _clip = null;
                return;
            }
            if (_acc < FrameInterval)
                return;
            _acc -= FrameInterval;

            Camera cam = FindCamera();
            if (cam == null)
            {
                _clip = null;
                return;
            }
            Vector2 view;
            bool inView;
            Texture2D tex = null;
            try
            {
                tex = CaptureFrame(cam, _player, _worldH, out view, out inView);
            }
            catch (System.Exception)
            {
                _clip = null;
                return;
            }
            if (tex == null)
            {
                _clip = null;
                return;
            }
            // 画面の外へ落ちていったら、そこで終わり（何も無い画面を、撮り続けない）。
            if (!inView && _clip.frames.Count >= 3)
            {
                Object.Destroy(tex);
                _clip = null;
                return;
            }
            _clip.frames.Add(tex);
        }

        private void Update()
        {
            RecordClip();

            if (!_running)
                return;
            // ポーズ中（時間が止まっている間）と、やられている最中は、数えない。
            // スロー演出中は、実際の時間で数える。
            if (GamePause.IsPaused || Time.timeScale <= 0.0001f || HitFeedback.IsDying)
                return;
            ElapsedTime += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        }
    }
}
