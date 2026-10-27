using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - レバー。プレイヤーが攻撃すると、オンになる。もう一度攻撃すると、オフになる。
///
/// 使い方：
///   1. メニュー「Tools > KEY ESCAPE > ステージ部品 > レバーのプレハブを作る」で作ったプレハブを、シーンに置く。
///   2. Inspector の「Targets」に、レバーで動かしたい物（LeverBridge など、LeverReceiver を持つ物）を入れる。
///      いくつ入れてもよい。オンの時に SetLeverState(true)、オフの時に SetLeverState(false) が、全部に伝わる。
///      LeverReceiver 以外の物を動かしたい時は、「On Changed」（オン=true／オフ=false が渡る）に、動かしたい関数をつなぐ。
///   3. 状態（オン／オフ）は、やられてチェックポイントから再開した時も、覚えている（LeverState）。
///      最初から、またはステージを出て入り直した時は、Start On の状態に戻る。
///
/// 見た目：絵は、オフの時の向き。オンの時は、左右を反転する（Flip X When On）。
///   専用の絵（On Sprite / Off Sprite）を入れると、反転ではなく、絵を切り替える。
/// 効果音：Toggle Clip に入れると、切り替えるたびに鳴る（音量は、設定画面の SE の音量に従う）。
/// 当たり判定：Collider2D（Is Trigger）が必要。プレイヤーの攻撃の範囲に入っていれば、当たる（Attack.cs から呼ばれる）。
/// </summary>
public class Lever : MonoBehaviour
{
    [Header("つなげる物")]
    [Tooltip("レバーで動かしたい物（LeverBridge など）。いくつでも入れられる。")]
    public LeverReceiver[] targets;
    [Tooltip("オンになると true、オフになると false が渡る。LeverReceiver 以外の物を動かしたい時に使う。")]
    public UnityEvent<bool> onChanged;

    [Header("基本")]
    [Tooltip("レバーの名前（IDと呼ぶ）。状態を覚えるために使う。空なら、オブジェクトの名前と位置から自動で決まる。同じステージに同じ名前のレバーを置く時は、位置が違えば、そのままでよい。")]
    public string leverId = "";
    [Tooltip("ステージの最初の状態。ONなら、最初からオン。")]
    public bool startOn = false;
    [Tooltip("一度攻撃を受けたあと、次の攻撃を受け付けるまでの秒数（一振りで、二回切り替わらないため）。")]
    public float hitCooldown = 0.35f;

    [Header("カメラ（レバーを押した時、つないだ物を映す）")]
    [Tooltip("ONなら、レバーを押した時に、カメラが、つないだ物（橋など）の場所へ動く。動いている間は、プレイヤーの操作が止まる。")]
    public bool moveCamera = true;
    [Tooltip("ONなら、そのステージで最初に押した時だけ、カメラが動く。やられて再開した時は、また動く。OFFなら、押すたびに動く。")]
    public bool onlyFirstTime = true;
    [Tooltip("任意。カメラが映す場所を、自分で決める目印（空のオブジェクト）。空なら、つないだ物（橋など）の場所を映す。")]
    public Transform cameraPoint;
    [Tooltip("カメラが、その場所へ動く秒数。")]
    public float cameraMoveSeconds = 1.0f;
    [Tooltip("橋などの動きが終わってから、カメラがその場所に残る秒数。")]
    public float holdSeconds = 0.8f;
    [Tooltip("カメラが、プレイヤーへ戻る秒数。")]
    public float cameraReturnSeconds = 1.0f;
    [Tooltip("映している間の、画面の広さ。0なら、変えない。1.4なら、広く映す（長い橋全体を入れたい時）。0.8なら、寄る。")]
    public float zoomRatio = 0f;
    [Tooltip("ズームにかける秒数。")]
    public float zoomSeconds = 0.8f;

    [Header("調べる用")]
    [Tooltip("ONなら、攻撃を受けるたびに、何が起きたかを Console に出す（橋に知らせが届いているかを調べる時に使う）。")]
    public bool debugLog = false;

    [Header("見た目")]
    [Tooltip("レバーの絵（SpriteRenderer）。")]
    public SpriteRenderer visual;
    [Tooltip("任意。オフの時の絵。On Sprite と両方入れると、反転ではなく、絵を切り替える。")]
    public Sprite offSprite;
    [Tooltip("任意。オンの時の絵。")]
    public Sprite onSprite;
    [Tooltip("ONなら、オンの時に、絵の左右を反転する（絵が1枚だけの時用）。")]
    public bool flipXWhenOn = true;
    [Tooltip("切り替えた時に、ぷるっと大きくなる倍率。1なら、動かない。")]
    public float popScale = 1.15f;
    [Tooltip("ぷるっとする秒数。")]
    public float popSeconds = 0.18f;

    [Header("効果音（空なら鳴らさない）")]
    public AudioClip toggleClip;
    [Range(0f, 1f)] public float toggleVolume = 1f;

    [Header("目立たせる（まだ引いていない間、まわりがふわっと光る）")]
    [Tooltip("ONなら、まだ一度も引いていない間、レバーのまわりが、ゆっくり光る。引くと、光は消える（もう一度引いても、出ない）。")]
    public bool glowUntilPulled = true;
    [Tooltip("光の色（Aは、いちばん明るい時の濃さ）。")]
    public Color glowColor = new Color(1f, 0.82f, 0.25f, 0.65f);
    [Tooltip("光の大きさ。レバーの絵の、何倍の大きさか。")]
    public float glowScale = 1.7f;
    [Tooltip("光がふくらんだり、縮んだりする速さ。")]
    public float glowPulseSpeed = 2.2f;
    [Tooltip("引いたあと、光が消えるまでの秒数。")]
    public float glowFadeSeconds = 0.4f;

    private bool _on;
    private bool _busy;                 // カメラが動いている間
    private bool _cameraShown;          // このステージで、もうカメラを動かしたか
    private GameObject _lockedPlayer;
    private float _lastHit = -10f;
    private Vector3 _visualScale = Vector3.one;
    private Coroutine _pop;

    private SpriteRenderer _glow;
    private bool _pulled;               // 一度でも引いたか（引いたら、光は消える）
    private float _glowK = 1f;          // 光の強さ（1→0 へ消える）
    private static Sprite s_glowSprite;

    /// <summary>いまオンか。</summary>
    public bool IsOn { get { return _on; } }

    /// <summary>状態を覚えるための名前。</summary>
    public string Id
    {
        get
        {
            if (!string.IsNullOrEmpty(leverId))
                return leverId;
            Vector3 p = transform.position;
            return gameObject.name + "@" + Mathf.RoundToInt(p.x * 10f) + "," + Mathf.RoundToInt(p.y * 10f);
        }
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        if (visual == null)
            visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null)
            _visualScale = visual.transform.localScale;
        BuildGlow();
    }

    private void Start()
    {
        // シーンが始まった時：覚えていた状態があれば、それに。なければ、Start On に。演出なしで、すぐ。
        bool on = startOn;
        bool saved;
        bool hasSaved = LeverState.TryGet(Id, out saved);
        if (hasSaved)
            on = saved;
        _pulled = on || hasSaved;   // すでに引いた（またはオン）なら、光は出さない
        if (_pulled)
            _glowK = 0f;
        Apply(on, true);

        CheckTargets();
    }

    /// <summary>つなげ方の間違いを、再生した時に Console の警告で知らせる。</summary>
    private void CheckTargets()
    {
        bool hasTarget = false;
        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    Debug.LogWarning("[Lever] " + name + "：Targets の " + i + " 番目が空です。", this);
                    continue;
                }
                hasTarget = true;
                if (!targets[i].gameObject.scene.IsValid())
                    Debug.LogWarning("[Lever] " + name + "：Targets の " + i + " 番目（" + targets[i].name + "）が、プレハブそのもの（Project の中）です。Hierarchy にある、シーンに置いたものを入れ直してください。", this);
            }
        }

        // On Changed：行があっても、関数が選ばれていない（No Function）と、何も起きない
        int calls = onChanged != null ? onChanged.GetPersistentEventCount() : 0;
        int working = 0;
        for (int i = 0; i < calls; i++)
        {
            if (onChanged.GetPersistentTarget(i) == null || string.IsNullOrEmpty(onChanged.GetPersistentMethodName(i)))
                Debug.LogWarning("[Lever] " + name + "：On Changed の " + i + " 番目に、関数が選ばれていません（No Function）。このままでは何も起きません。橋などは、「Targets」に入れてください。", this);
            else
                working++;
        }

        if (!hasTarget && working == 0)
            Debug.LogWarning("[Lever] " + name + "：Targets が空です。動かしたい物（跳ね橋など）を、Inspector の Targets に入れてください。", this);
    }

    /// <summary>プレイヤーの攻撃が当たった時に、Attack.cs から呼ばれる。切り替えたら true。</summary>
    public bool Hit()
    {
        if (GamePause.IsPaused)
        {
            if (debugLog)
                Debug.Log("[Lever] " + name + "：ゲームが止まっているので、受け付けません。", this);
            return false;
        }
        if (_busy)
            return false;
        if (Time.time - _lastHit < hitCooldown)
        {
            if (debugLog)
                Debug.Log("[Lever] " + name + "：直前の攻撃から間もないので、受け付けません（Hit Cooldown）。", this);
            return false;
        }
        _lastHit = Time.time;
        SetOn(!_on, false);
        return true;
    }

    /// <summary>オン／オフを、コードや UnityEvent から決める。instant が true なら、演出なし。</summary>
    public void SetOn(bool on, bool instant)
    {
        if (!instant && on == _on)
            return;

        // カメラを動かす時は、橋などへの知らせを、カメラが着いてからにする
        bool useCamera = !instant && WantsCamera();
        Apply(on, instant, !useCamera);
        LeverState.Set(Id, on);
        _pulled = true;

        if (!instant)
        {
            if (toggleClip != null)
                AudioManager.PlaySE(toggleClip, toggleVolume);
            if (visual != null && popScale > 1.001f)
            {
                if (_pop != null)
                    StopCoroutine(_pop);
                _pop = StartCoroutine(Pop());
            }
        }

        if (useCamera)
        {
            _cameraShown = true;
            StartCoroutine(CameraSequence(on));
        }
    }

    private void Apply(bool on, bool instant, bool notify = true)
    {
        _on = on;
        UpdateVisual();

        if (notify)
            NotifyTargets(on, instant);
    }

    /// <summary>つなげた物に、オン／オフを知らせる。</summary>
    private void NotifyTargets(bool on, bool instant)
    {
        if (debugLog)
            Debug.Log("[Lever] " + name + "：" + (on ? "オン" : "オフ") + (instant ? "（演出なし）" : "") + " → Targets " + (targets != null ? targets.Length : 0) + " 個に知らせます。", this);

        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    targets[i].SetLeverState(on, instant);
            }
        }
        if (onChanged != null)
            onChanged.Invoke(on);
    }

    // ================= カメラ =================

    private bool WantsCamera()
    {
        if (!moveCamera)
            return false;
        if (onlyFirstTime && _cameraShown)
            return false;
        if (GamePause.IsPaused || LoadingScreen.IsLoading)
            return false;
        return FindObjectOfType<CameraFollow>() != null;
    }

    /// <summary>カメラが映す場所：目印があれば、それ。なければ、つないだ最初の物の場所。どちらも無ければ、レバー自身。</summary>
    private Vector3 FocusPoint()
    {
        if (cameraPoint != null)
            return cameraPoint.position;
        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    return targets[i].FocusPosition;
            }
        }
        return transform.position;
    }

    /// <summary>
    /// 押した→カメラが橋へ動く→橋が動く→少し見せる→カメラがプレイヤーへ戻る→操作が戻る。
    /// 動いている間は、プレイヤーの操作（移動・攻撃）だけ止める。敵や水は、そのまま動く。
    /// </summary>
    private IEnumerator CameraSequence(bool on)
    {
        _busy = true;

        CameraFollow cam = FindObjectOfType<CameraFollow>();
        GameObject playerObj = NewGameIntro.FindMainPlayerObject();
        PlayerMovement pm = playerObj != null ? playerObj.GetComponent<PlayerMovement>() : null;
        Attack atk = playerObj != null ? playerObj.GetComponent<Attack>() : null;

        // プレイヤーの操作を止める
        if (pm != null)
        {
            pm.SetInputLocked(true);
            _lockedPlayer = playerObj;
        }
        if (atk != null)
        {
            atk.canAttack = false;
        }

        Transform original = cam != null ? cam.Target : null;
        GameObject focus = new GameObject("LeverCameraPoint (auto)");
        focus.transform.position = FocusPoint();

        if (cam != null)
        {
            cam.MoveTo(focus.transform, Mathf.Max(0f, cameraMoveSeconds));
            if (zoomRatio > 0f)
                cam.ZoomTo(zoomRatio, Mathf.Max(0.05f, zoomSeconds));
        }
        yield return new WaitForSeconds(Mathf.Max(0f, cameraMoveSeconds));

        // カメラが着いた：橋などを動かして、終わるまで待つ
        NotifyTargets(on, false);
        float effect = 0f;
        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    effect = Mathf.Max(effect, targets[i].GetEffectSeconds(on));
            }
        }
        yield return new WaitForSeconds(effect + Mathf.Max(0f, holdSeconds));

        // プレイヤーへ戻る
        if (cam != null)
        {
            cam.MoveTo(original, Mathf.Max(0f, cameraReturnSeconds));
            if (zoomRatio > 0f)
                cam.ZoomTo(1f, Mathf.Max(0.05f, zoomSeconds));
        }
        yield return new WaitForSeconds(Mathf.Max(0f, cameraReturnSeconds));

        Destroy(focus);
        Unlock(atk);
        _busy = false;
    }

    private void Unlock(Attack atk)
    {
        if (_lockedPlayer != null)
        {
            PlayerMovement pm = _lockedPlayer.GetComponent<PlayerMovement>();
            if (pm != null)
                pm.SetInputLocked(false);
            _lockedPlayer = null;
        }
        if (atk != null)
            atk.canAttack = true;
    }

    private void OnDisable()
    {
        // カメラが動いている途中で、レバーが消えた時も、プレイヤーの操作を戻す
        if (_busy)
        {
            Attack atk = _lockedPlayer != null ? _lockedPlayer.GetComponent<Attack>() : null;
            Unlock(atk);
            _busy = false;
        }
    }

    private void UpdateVisual()
    {
        if (visual == null)
            return;
        if (onSprite != null && offSprite != null)
        {
            visual.sprite = _on ? onSprite : offSprite;
            visual.flipX = false;
        }
        else
        {
            visual.flipX = flipXWhenOn && _on;
        }
    }

    // ================= 光 =================

    private static Sprite GetGlowSprite()
    {
        if (s_glowSprite != null)
            return s_glowSprite;
        int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float c = (n - 1) * 0.5f;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = 1f - Mathf.SmoothStep(0.15f, 1f, d);
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        s_glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return s_glowSprite;
    }

    /// <summary>レバーの後ろに、光る丸を作る（引いていない間だけ見える）。</summary>
    private void BuildGlow()
    {
        if (!glowUntilPulled || visual == null)
            return;

        GameObject go = new GameObject("Glow (auto)");
        go.transform.SetParent(transform, false);
        _glow = go.AddComponent<SpriteRenderer>();
        _glow.sprite = GetGlowSprite();
        _glow.sortingLayerID = visual.sortingLayerID;
        _glow.sortingOrder = visual.sortingOrder - 1;

        // レバーの絵のまんなかに置き、絵の大きさに合わせる（ワールドの大きさで決める）
        Bounds b = visual.bounds;
        go.transform.position = b.center;
        float world = Mathf.Max(b.size.x, b.size.y) * Mathf.Max(0.5f, glowScale);
        Vector3 ls = transform.lossyScale;
        go.transform.localScale = new Vector3(world / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), world / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f);
        _glowBaseScale = go.transform.localScale;
        _glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
    }

    private Vector3 _glowBaseScale = Vector3.one;

    private void Update()
    {
        if (_glow == null)
            return;

        // 引いたら、光はすっと消える
        if (_pulled && _glowK > 0f)
            _glowK = Mathf.MoveTowards(_glowK, 0f, Time.unscaledDeltaTime / Mathf.Max(0.05f, glowFadeSeconds));

        if (_glowK <= 0f)
        {
            if (_glow.enabled)
                _glow.enabled = false;
            return;
        }

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowPulseSpeed);
        float alpha = glowColor.a * Mathf.Lerp(0.45f, 1f, wave) * _glowK;
        _glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
        _glow.transform.localScale = _glowBaseScale * Mathf.Lerp(0.94f, 1.06f, wave);
    }

    private IEnumerator Pop()
    {
        Transform t = visual.transform;
        float dur = Mathf.Max(0.01f, popSeconds);
        float e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(e / dur) * Mathf.PI);
            t.localScale = _visualScale * (1f + (popScale - 1f) * k);
            yield return null;
        }
        t.localScale = _visualScale;
        _pop = null;
    }

    // ================= シーン画面の目印 =================

    private void OnDrawGizmos()
    {
        if (targets == null)
            return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                Gizmos.DrawLine(transform.position, targets[i].transform.position);
        }
    }
}

/// <summary>
/// KEY ESCAPE - レバーの状態を、シーンを読み直しても覚えておく入れ物。
/// ・やられて、チェックポイントから再開した時（同じステージのシーンが読み直された時）は、覚えている。
/// ・チェックポイントを通らずに、最初からやり直した時は、消える（レバーも最初の状態に戻る）。
/// ・別のシーン（拠点・タイトル・別のステージ）に移ると、消える。
/// どこにも付けなくてよい。
/// </summary>
public static class LeverState
{
    private static readonly Dictionary<string, bool> s_states = new Dictionary<string, bool>();
    private static string s_scene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_states.Clear();
        s_scene = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        s_scene = SceneManager.GetActiveScene().name;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        // 別のシーンに移った、または、チェックポイントが無い（最初からやり直し）なら、忘れる。
        if (scene.name != s_scene || !CheckpointManager.HasCheckpoint)
            s_states.Clear();
        s_scene = scene.name;
    }

    public static bool TryGet(string id, out bool on)
    {
        return s_states.TryGetValue(id, out on);
    }

    public static void Set(string id, bool on)
    {
        s_states[id] = on;
    }
}
