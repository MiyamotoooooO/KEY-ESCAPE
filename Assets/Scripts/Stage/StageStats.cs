using System.Collections.Generic;
using UnityEngine;

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
/// どこにも付けなくてよい。
/// </summary>
public static class StageStats
{
    /// <summary>死んだ瞬間の写真1枚分。</summary>
    public class DeathPhoto
    {
        public Texture2D texture;
        /// <summary>プレイヤーが、写真のどこにいるか（0〜1。左下が0,0）。写真を切り抜く中心に使う。</summary>
        public Vector2 playerView = new Vector2(0.5f, 0.5f);
        public DeathCause cause;
    }

    const int   MaxPhotos   = 12;    // 持っておく写真の最大数（古いものから捨てる）
    const int   PhotoHeight = 360;   // 写真の高さ（ピクセル）。幅は画面の比率に合わせる
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
        for (int i = 0; i < _photos.Count; i++)
        {
            if (_photos[i] != null && _photos[i].texture != null)
                Object.Destroy(_photos[i].texture);
        }
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
            if (_photos[0].texture != null)
                Object.Destroy(_photos[0].texture);
            _photos.RemoveAt(0);
        }
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

    /// <summary>カメラに映っているものだけを、画像にする。</summary>
    private static DeathPhoto Capture(DeathCause cause)
    {
        Camera cam = null;
        CameraFollow follow = Object.FindObjectOfType<CameraFollow>();
        if (follow != null)
            cam = follow.GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return null;

        int h = PhotoHeight;
        int w = Mathf.Max(16, Mathf.RoundToInt(h * Mathf.Max(0.5f, cam.aspect)));

        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }

        var photo = new DeathPhoto();
        photo.texture = tex;
        photo.cause = cause;

        CharacterController2D player = Object.FindObjectOfType<CharacterController2D>();
        if (player != null)
        {
            Vector3 v = cam.WorldToViewportPoint(player.transform.position);
            photo.playerView = new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y));
        }
        return photo;
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
        private void Update()
        {
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
