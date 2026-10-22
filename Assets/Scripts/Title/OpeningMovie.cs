using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - オープニングムービー（ストーリー）。ゲームを起動した最初の1回だけ、タイトル画面で流れる。
///
/// ストーリー：人間に酷使されたキーボードたちが、家から飛び出す。
///   1. 人の目線で、指がカタカタとキーを押す
///   2. キーたちが、指を必死によける
///   3. 限界に達する
///   4. みんなで、家を飛び出す決意をする
///   5. 家を飛び出す
///   6. 新たな世界へ（ステージを駆け抜ける）
///   7. 走り抜けるキーたち → そのままタイトル画面になる（ロゴが左上に出て、メニューが並ぶ）
///
/// ・新しいシーンは作らない。タイトルシーンに置いた、このスクリプトが、すべてコードで作って動かす。
///   動く場所は、画面の外の離れた所に作り、専用のカメラで撮った映像を、全画面で重ねて見せる。
/// ・絵は、指定がなければ、図形と色で作った仮の絵になる。完成した絵ができたら、下の「絵」の欄に入れれば差し替わる
///   （指定した大きさに、引き伸ばして出す）。
/// ・キーキャラクターは、手足つきのキー（KeyNPC_Visual）を使う。
/// ・Space キーで、いつでもスキップできる。
/// ・音は、欄に入れたものだけ鳴る（空なら、無音）。
///
/// ・ムービーの最後は、実際のタイトル画面（地面・草・岩・木・ロゴ）に切り替わる。キーたちが、その地面を歩いて通りすぎる。
///   そのあと、ロゴ → はじめから・つづきから・設定・終わる の順に出てくる。Spaceでスキップすると、すぐこの状態になる。
/// ・ゲームから、タイトルに戻ってきたときは、ムービーは流さず、いつものタイトル画面のまま。
///
/// 使い方：TitleMenu.openingMovie に、このスクリプトを割り当てる
///         （メニュー Tools > KEY ESCAPE > オープニングムービー > タイトルシーンに追加する で、自動で行える）。
/// </summary>
public class OpeningMovie : MonoBehaviour
{
    // ================= 設定 =================

    [Header("キーキャラクター")]
    [Tooltip("手足つきのキーのプレハブ（KeyNPC_Visual）。メニューから追加すると、自動で入る。")]
    public GameObject keyCharacterPrefab;
    [Tooltip("キャラクターの大きさ。大きいほど、画面に大きく映る。")]
    public float charScale = 2.2f;

    [Header("文字")]
    [Tooltip("セリフなどのフォント（日本語が出るもの）。空なら、シーンの中の文字から借りる。")]
    public TMP_FontAsset font;
    [Tooltip("セリフ・擬音の大きさの倍率。")]
    public float textScale = 1f;

    [Header("絵（空なら、仮の絵。指定した大きさに引き伸ばして出す）")]
    [Tooltip("指の絵（指先が上向き。YubiImage）。")]
    public Sprite fingerSprite;
    [Tooltip("シーン1：机の背景。")]
    public Sprite deskBackground;
    [Tooltip("シーン1：キーボード本体。")]
    public Sprite keyboardSprite;
    [Tooltip("シーン2〜5：キーボードの上・部屋の背景。")]
    public Sprite roomBackground;
    [Tooltip("シーン5：開くドアの板（内側。DoorUtigawaImage）。向かって右に開く。")]
    public Sprite doorSprite;
    [Tooltip("シーン5：ドアの枠（外側。DoorImage）。穴の大きさの割合は、DoorImage に合わせてある。")]
    public Sprite doorFrameSprite;
    [Tooltip("シーン2〜5：部屋の地面（moveBG）。キーたちが立つ、木の板。空なら、ゲームの地面の線。")]
    public Sprite roomGroundSprite;
    [Tooltip("シーン6：外の空の背景。")]
    public Sprite skyBackground;
    [Tooltip("シーン6：地面（ゲームで使っている Ground。細い線の絵を、足場の長さに伸ばして出す）。")]
    public Sprite groundSprite;
    [Tooltip("シーン6：地面の草（ゲームで使っている Grass）。空なら、図形の草。")]
    public Sprite grassSprite;
    [Tooltip("シーン1：キーボードのキー1つぶんの絵（キー素材）。空なら、図形のキー。")]
    public Sprite keycapSprite;

    [Header("タイトル画面（ムービーのあと、実際のタイトル画面で、ロゴとメニューが出てくる）")]
    [Tooltip("タイトルのロゴ（シーンの中の dasda_0）。空なら、自動で探す。キーたちが歩き終わったら、ぽんと出る。")]
    public GameObject titleLogo;
    [Tooltip("メニューのボタン（はじめから・つづきから・設定・終わる）。ロゴのあと、順番に出る。空なら、TitleMenuから自動で入る。")]
    public Button[] titleButtons;

    [Header("タイトル画面を歩くキーたち")]
    [Tooltip("実際のタイトル画面の地面を、キーたちが歩く速さ。")]
    public float titleWalkSpeed = 4.4f;
    [Tooltip("タイトル画面の岩（Rock）を、ぴょんと跳び越える。")]
    public bool hopOverRock = true;

    [Header("音（空なら、鳴らさない）")]
    public AudioClip bgm;
    [Range(0f, 1f)] public float bgmVolume = 0.8f;
    [Tooltip("キーを押すカタカタ音（複数入れると、ランダムに鳴る）。")]
    public AudioClip[] clackClips;
    [Tooltip("指が叩きつけられる音。")]
    public AudioClip slamSe;
    [Tooltip("キーがぴょんと跳ぶ音。")]
    public AudioClip hopSe;
    [Tooltip("Spaceが倒れる音。")]
    public AudioClip collapseSe;
    [Tooltip("決意の瞬間の音。")]
    public AudioClip resolveSe;
    [Tooltip("ドアが開く音。")]
    public AudioClip doorSe;
    [Tooltip("着地の音。")]
    public AudioClip landSe;
    [Tooltip("ロゴ・メニューが出る音。")]
    public AudioClip logoSe;
    [Range(0f, 1f)] public float seVolume = 1f;

    [Header("長さ")]
    [Tooltip("シーン1：カタカタ打つ秒数。")]
    public float typingTime = 6f;
    [Tooltip("スキップできるようになるまでの秒数（押した瞬間の連打で、すぐ飛ばないように）。")]
    public float skipGuardTime = 0.5f;

    // ================= 状態 =================

    private static OpeningMovie _current;

    /// <summary>ムービーが流れている間は true。</summary>
    public static bool IsPlaying
    {
        get { return _current != null && _current._playing; }
    }

    private bool _playing;
    private System.Action _onFinished;
    private float _startTime;
    private bool _prevNav = true;
    private bool _navSaved;
    private bool _bgmPushed;

    private GameObject _world;
    private Camera _cam;
    private RenderTexture _rt;
    private GameObject _canvasGo;
    private Image _dim;
    private Image _flash;
    private Image _fade;

    private Vector2 _camPos;
    private float _camSize = 5f;
    private float _shake;
    private float _shakeDecay;

    private Sprite _sq;
    private Sprite _round;
    private Sprite _circle;
    private Sprite _capsule;
    private readonly List<Object> _made = new List<Object>();
    private TMP_FontAsset _font;

    private const float WorldX = -12000f;
    private const float FloorY = -2.0f;

    // ================= 開始・終了 =================

    // ゲームを起動してから、ムービーを流したか（ゲームからタイトルに戻ってきたときは、流さない）
    private static bool s_played;

    private bool _begun;
    private bool _overlayAlive;
    private bool _revealing;
    private bool _skipping;

    private RawImage _movieRaw;
    private Image _blackBg;
    private readonly Dictionary<Transform, Vector3> _uiBaseScale = new Dictionary<Transform, Vector3>();

    // タイトル画面で、キーたちが歩いてくるときの、並び方（先頭からの距離）。タイトル画面に地面が見つからないときの、地面の高さ
    private static readonly float[] WalkOffsets = { 0f, 1.7f, 3.4f, 5.8f, 7.8f, 9.4f };
    private const float FallbackGroundTop = -4.3f;

    // タイトル画面を歩くキーたちを、のせておく入れ物
    private GameObject _castRoot;
    private readonly List<Coroutine> _runCos = new List<Coroutine>();

    /// <summary>
    /// タイトル画面を始める（TitleMenu.Start から呼ぶ）。
    /// ゲームを起動して最初の1回なら、ムービーを流して、そのままタイトル画面になる。
    /// 2回目以降（ゲームから戻ってきたとき）は、ムービーなしで、すぐタイトル画面になる。
    /// ぜんぶ終わったら、onDone を呼ぶ。
    /// </summary>
    public void BeginTitle(bool playMovie, System.Action onDone)
    {
        if (_begun)
            return;
        _begun = true;
        _onFinished = onDone;

        bool movie = playMovie && !s_played;
        if (!movie)
        {
            // ゲームから戻ってきたとき：いつものタイトル画面のまま
            Done();
            return;
        }

        if (keyCharacterPrefab == null)
        {
            Debug.LogWarning("[OpeningMovie] キーキャラクターのプレハブが入っていないので、ムービーを飛ばします。");
            Done();
            return;
        }

        FindTitleParts();
        _font = font != null ? font : FindSceneFont();
        BuildSprites();
        BuildStudio();

        s_played = true;
        _playing = true;
        _current = this;
        _startTime = Time.unscaledTime;
        LockNavigation();
        BuildOverlay();
        HideTitleUi();

        // BGMが入っていれば、それを流す（いまのBGMは覚えておいて、終わったら戻す）。空なら、いまのBGMのまま
        if (bgm != null)
        {
            AudioManager.PushBGM(bgm, 0.6f, false, bgmVolume);
            _bgmPushed = true;
        }

        StartCoroutine(Run());
    }

    private void Done()
    {
        System.Action cb = _onFinished;
        _onFinished = null;
        if (cb != null)
            cb();
    }

    /// <summary>ムービーとタイトル画面の登場が、ぜんぶ終わった。</summary>
    private void EndMovie()
    {
        _revealing = false;
        _playing = false;
        _skipping = false;
        if (_current == this)
            _current = null;
        if (_bgmPushed)
        {
            AudioManager.PopBGM(1.2f);
            _bgmPushed = false;
        }
        Done();
    }

    private void LockNavigation()
    {
        if (EventSystem.current == null)
            return;
        _prevNav = EventSystem.current.sendNavigationEvents;
        _navSaved = true;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.sendNavigationEvents = false;
    }

    private void ReleaseNavigation()
    {
        if (_navSaved && EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = _prevNav;
        _navSaved = false;
    }

    private void Update()
    {
        if (!_playing || _skipping)
            return;
        if (Time.unscaledTime - _startTime < skipGuardTime)
            return;
        if (Input.GetKeyDown(KeyCode.Space))
            Skip();
    }

    private void Skip()
    {
        if (_skipping)
            return;
        _skipping = true;
        StopAllCoroutines();
        _busy = 0;
        StartCoroutine(SkipRoutine());
    }

    /// <summary>スキップ：いったん暗くして、完成したタイトル画面にする。</summary>
    private IEnumerator SkipRoutine()
    {
        if (_overlayAlive && _fade != null)
            yield return FadeTo(_fade, 1f, 0.25f);

        EnterTitleState();

        if (_overlayAlive)
            yield return FadeTo(_fade, 0f, 0.3f);
        DestroyOverlay();
        EndMovie();
    }

    /// <summary>ムービー用の全画面の幕を外す。</summary>
    private void DestroyOverlay()
    {
        ReleaseNavigation();
        if (_canvasGo != null)
            Destroy(_canvasGo);
        _canvasGo = null;
        _dim = null;
        _flash = null;
        _fade = null;
        _blackBg = null;
        _movieRaw = null;
        _overlayAlive = false;
    }

    private void OnDestroy()
    {
        ReleaseNavigation();
        if (_current == this)
            _current = null;
        if (_bgmPushed)
        {
            AudioManager.PopBGM(0.3f);
            _bgmPushed = false;
        }

        DestroyCast();
        if (_cam != null)
            _cam.targetTexture = null;
        if (_world != null)
            Destroy(_world);
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
        for (int i = 0; i < _made.Count; i++)
            if (_made[i] != null) Destroy(_made[i]);
        _made.Clear();
    }

    private static TMP_FontAsset FindSceneFont()
    {
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
                return texts[i].font;
        }
        return null;
    }

    // ================= タイトル画面（背景・ロゴ・メニュー）=================

    private static CanvasGroup Group(GameObject go)
    {
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    private void SetUiVisible(GameObject go, float alpha, bool interact)
    {
        if (go == null)
            return;
        CanvasGroup cg = Group(go);
        cg.alpha = alpha;
        cg.interactable = interact;
        cg.blocksRaycasts = interact;
    }

    private Vector3 BaseScale(Transform tr)
    {
        Vector3 s;
        if (!_uiBaseScale.TryGetValue(tr, out s))
        {
            s = tr.localScale;
            _uiBaseScale[tr] = s;
        }
        return s;
    }

    /// <summary>ロゴとメニューのボタンを、見えなくして、押せなくする（ムービーの間）。</summary>
    private void HideTitleUi()
    {
        if (titleLogo != null)
        {
            BaseScale(titleLogo.transform);
            SetLogoAlpha(0f);
        }
        if (titleButtons != null)
        {
            for (int i = 0; i < titleButtons.Length; i++)
            {
                if (titleButtons[i] == null) continue;
                BaseScale(titleButtons[i].transform);
                SetUiVisible(titleButtons[i].gameObject, 0f, false);
            }
        }
    }

    /// <summary>ロゴとメニューを、すぐ全部出す。</summary>
    private void RevealUiInstant()
    {
        if (titleLogo != null)
        {
            titleLogo.transform.localScale = BaseScale(titleLogo.transform);
            SetLogoAlpha(1f);
        }
        if (titleButtons != null)
        {
            for (int i = 0; i < titleButtons.Length; i++)
            {
                if (titleButtons[i] == null) continue;
                titleButtons[i].transform.localScale = BaseScale(titleButtons[i].transform);
                SetUiVisible(titleButtons[i].gameObject, 1f, true);
            }
        }
    }

    private IEnumerator PopUi(GameObject go, float time, float fromScale)
    {
        if (go == null)
            yield break;
        Transform tr = go.transform;
        Vector3 baseScale = BaseScale(tr);
        yield return Tween(time, k =>
        {
            SetAlphaAny(go, Mathf.Clamp01(k * 2f));
            float s = Mathf.Lerp(fromScale, 1f, EaseOutBack(k));
            tr.localScale = baseScale * s;
        });
        SetAlphaAny(go, 1f);
        tr.localScale = baseScale;
    }

    /// <summary>ロゴがぽんと出て、そのあと、メニューが順番に出る。</summary>
    private IEnumerator RevealUi()
    {
        _revealing = true;
        Se(logoSe);
        yield return PopUi(titleLogo, 0.5f, 1.35f);
        yield return Wait(0.3f);
        if (titleButtons != null)
        {
            for (int i = 0; i < titleButtons.Length; i++)
            {
                if (titleButtons[i] == null) continue;
                StartCoroutine(PopUi(titleButtons[i].gameObject, 0.3f, 0.85f));
                Se(logoSe, 0.4f);
                yield return Wait(0.14f);
            }
        }
        yield return Wait(0.4f);
        RevealUiInstant();
        EndMovie();
    }

    /// <summary>ロゴの透明度を変える（ロゴが、シーンの中の絵でも、UIの絵でも使える）。</summary>
    private void SetLogoAlpha(float a)
    {
        if (titleLogo == null)
            return;
        SpriteRenderer[] srs = titleLogo.GetComponentsInChildren<SpriteRenderer>(true);
        if (srs.Length > 0)
        {
            for (int i = 0; i < srs.Length; i++)
            {
                Color c = srs[i].color;
                c.a = a;
                srs[i].color = c;
            }
            return;
        }
        SetUiVisible(titleLogo, a, a >= 1f);
    }

    private void SetAlphaAny(GameObject go, float a)
    {
        if (go == titleLogo)
        {
            SetLogoAlpha(a);
            return;
        }
        Group(go).alpha = a;
    }

    /// <summary>ロゴとメニューのボタンを探す（入っていなければ）。</summary>
    private void FindTitleParts()
    {
        if (titleLogo == null)
        {
            GameObject g = GameObject.Find("dasda_0");
            if (g != null)
                titleLogo = g;
        }
        if (titleLogo == null)
            Debug.LogWarning("[OpeningMovie] ロゴ（dasda_0）が見つかりません。OpeningMovie の Title Logo に入れてください。");

        if (titleButtons == null || titleButtons.Length == 0)
        {
            TitleMenu menu = FindObjectOfType<TitleMenu>(true);
            if (menu != null)
            {
                List<Button> list = new List<Button>();
                if (menu.newGameButton != null) list.Add(menu.newGameButton);
                if (menu.continueButton != null) list.Add(menu.continueButton);
                if (menu.settingsButton != null) list.Add(menu.settingsButton);
                if (menu.quitButton != null) list.Add(menu.quitButton);
                titleButtons = list.ToArray();
            }
        }
    }

    /// <summary>タイトルシーンにある、ふつうのカメラ（ムービー用のカメラは、除く）。</summary>
    private Camera TitleCamera()
    {
        Camera c = Camera.main;
        if (c != null && c != _cam)
            return c;
        Camera[] all = Camera.allCameras;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i] != _cam)
                return all[i];
        }
        return null;
    }

    /// <summary>タイトルシーンの中から、名前でオブジェクトを探す（ムービー用のものは、除く）。</summary>
    private Transform FindTitleObject(string objName)
    {
        Transform[] all = FindObjectsOfType<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != objName)
                continue;
            if (_world != null && t.IsChildOf(_world.transform))
                continue;
            if (_castRoot != null && t.IsChildOf(_castRoot.transform))
                continue;
            return t;
        }
        return null;
    }

    /// <summary>タイトル画面の地面の、いちばん上の高さ。</summary>
    private float TitleGroundTop()
    {
        Transform g = FindTitleObject("Ground");
        if (g != null)
        {
            SpriteRenderer sr = g.GetComponent<SpriteRenderer>();
            if (sr != null)
                return sr.bounds.max.y;
            Collider2D col = g.GetComponent<Collider2D>();
            if (col != null)
                return col.bounds.max.y;
        }
        return FallbackGroundTop;
    }

    private void DestroyCast()
    {
        if (_castRoot != null)
            Destroy(_castRoot);
        _castRoot = null;
    }

    /// <summary>ムービーの映像と、黒い背景を、見えなくする（後ろの、実際のタイトル画面が見える）。</summary>
    private void HideMovieLayers()
    {
        if (_movieRaw != null)
            _movieRaw.gameObject.SetActive(false);
        if (_blackBg != null)
            _blackBg.gameObject.SetActive(false);
        if (_cam != null)
            _cam.enabled = false;
    }

    /// <summary>いまの場面を全部片づける（カメラ以外）。</summary>
    private void ClearWorld(bool keepActors)
    {
        if (_world != null)
        {
            for (int i = _world.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = _world.transform.GetChild(i);
                if (_cam != null && c == _cam.transform)
                    continue;
                Destroy(c.gameObject);
            }
        }
        if (_cam != null)
        {
            Transform sky = _cam.transform.Find("Sky");
            if (sky != null)
                Destroy(sky.gameObject);
        }
        if (!keepActors)
            _actors.Clear();
        _keyWorld = null;
        _prevRoot = null;
        _worldRoot = null;
    }

    /// <summary>
    /// 完成したタイトル画面にする：キーたちを片づけて、ロゴ・メニューを、すぐ全部出す。
    /// （ムービーをスキップしたとき、使う）
    /// </summary>
    private void EnterTitleState()
    {
        _follow = false;
        _busy = 0;
        ClearWorld(false);
        DestroyCast();
        HideMovieLayers();
        SetDim(0f);
        RevealUiInstant();
    }


    // ================= 土台（撮影場所・画面・仮の絵）=================

    private Texture2D MakeTexture(int w, int h, float radius)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[w * h];
        float hw = w * 0.5f;
        float hh = h * 0.5f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - hw) - (hw - radius);
                float qy = Mathf.Abs(y + 0.5f - hh) - (hh - radius);
                float ox = Mathf.Max(qx, 0f);
                float oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float a = Mathf.Clamp01(0.5f - d);
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _made.Add(tex);
        return tex;
    }

    private Sprite MakeSprite(int w, int h, float radius, float ppu)
    {
        Texture2D tex = MakeTexture(w, h, radius);
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), ppu);
        _made.Add(s);
        return s;
    }

    // 絵の、まわりの透明な余白の割合（左・上・右・下）。余白を切って使うと、指定した大きさどおりに絵が出る。
    // 名前が合う絵（いま使っているもの）にだけ使う。別の絵に差し替えたときは、余白は切らず、そのまま使う。
    private static readonly Dictionary<string, Vector4> TrimTable = new Dictionary<string, Vector4>
    {
        { "キー素材", new Vector4(0.1655f, 0.1716f, 0.1708f, 0.1480f) },
        { "YubiImage", new Vector4(0.1411f, 0.0674f, 0.1484f, 0.0590f) },
        { "DoorImage", new Vector4(0.0791f, 0.0579f, 0.0781f, 0.0508f) },
        { "moveBG", new Vector4(0.0138f, 0.4392f, 0.0134f, 0.1644f) },
        { "DoorUtigawaImage", new Vector4(0.1475f, 0.0697f, 0.1436f, 0.0723f) },
    };

    private Sprite _glow;     // なめらかに消えていく光の円
    private Sprite _bubble;   // ふきだし（角が丸く、大きさを変えても角がつぶれない）
    private Sprite _roomGround;
    private Sprite _keycap;
    private Sprite _finger;
    private Sprite _doorFrame;
    private Sprite _doorLeaf;

    private Sprite Trim(Sprite s)
    {
        if (s == null)
            return null;
        Vector4 m;
        if (!TrimTable.TryGetValue(s.name, out m))
            return s;
        Rect r = s.rect;
        Rect cut = new Rect(r.x + r.width * m.x, r.y + r.height * m.w, r.width * (1f - m.x - m.z), r.height * (1f - m.y - m.w));
        Sprite t = Sprite.Create(s.texture, cut, new Vector2(0.5f, 0.5f), s.pixelsPerUnit);
        _made.Add(t);
        return t;
    }

    /// <summary>まん中が明るく、ふちに向かって、なめらかに消えていく円（1×1）。つなぎ目が出ない。</summary>
    private Sprite MakeGlowSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _made.Add(tex);
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _made.Add(s);
        return s;
    }

    /// <summary>ふきだし用の、角の丸い四角（四隅は伸ばさず、まん中だけ伸ばす）。</summary>
    private Sprite MakeBubbleSprite()
    {
        Texture2D tex = MakeTexture(128, 128, 44f);
        Sprite s = Sprite.Create(tex, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 128f, 0, SpriteMeshType.FullRect, new Vector4(48f, 48f, 48f, 48f));
        _made.Add(s);
        return s;
    }

    /// <summary>角をつぶさずに大きさを変えられる絵（_bubble など）を置く。</summary>
    private SpriteRenderer SlicedSpr(Transform parent, string objName, Sprite s, Vector2 pos, Vector2 size, Color color, int order)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void BuildSprites()
    {
        _sq = MakeSprite(4, 4, 0f, 4f);            // 1×1 の四角
        _round = MakeSprite(96, 96, 20f, 96f);     // 1×1 の角丸
        _circle = MakeSprite(96, 96, 48f, 96f);    // 1×1 の円
        _capsule = MakeSprite(64, 192, 32f, 64f);  // 1×3 のカプセル（指など）

        _glow = MakeGlowSprite(512);
        _bubble = MakeBubbleSprite();
        _roomGround = Trim(roomGroundSprite);
        _keycap = Trim(keycapSprite);
        _finger = Trim(fingerSprite);
        _doorFrame = Trim(doorFrameSprite);
        _doorLeaf = Trim(doorSprite);
    }

    private void BuildStudio()
    {
        _world = new GameObject("OpeningMovieWorld");
        _world.transform.position = new Vector3(WorldX, 0f, 0f);

        _rt = new RenderTexture(1920, 1080, 16, RenderTextureFormat.ARGB32);
        _rt.name = "OpeningMovieRT";
        _rt.filterMode = FilterMode.Bilinear;
        _rt.wrapMode = TextureWrapMode.Clamp;
        _rt.Create();

        GameObject camObj = new GameObject("Camera");
        camObj.transform.SetParent(_world.transform, false);
        _cam = camObj.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = 5f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.black;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 50f;
        _cam.allowHDR = false;
        _cam.allowMSAA = false;
        _cam.depth = -90f;
        _cam.targetTexture = _rt;
        _camPos = Vector2.zero;
        _camSize = 5f;
        ApplyCamera(Vector2.zero);
    }

    private void BuildOverlay()
    {
        _canvasGo = new GameObject("OpeningMovieCanvas");
        Canvas canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        // 後ろのタイトル画面を、すべて隠して、クリックも通さない
        _blackBg = NewImage("Black", _canvasGo.transform, Color.black, true);

        GameObject movie = new GameObject("Movie", typeof(RectTransform));
        movie.transform.SetParent(_canvasGo.transform, false);
        Stretch((RectTransform)movie.transform);
        RawImage raw = movie.AddComponent<RawImage>();
        raw.texture = _rt;
        raw.raycastTarget = false;
        AspectRatioFitter fit = movie.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = 16f / 9f;
        _movieRaw = raw;
        _overlayAlive = true;

        _dim = NewImage("Dim", _canvasGo.transform, new Color(0f, 0f, 0f, 0f), false);
        _flash = NewImage("Flash", _canvasGo.transform, new Color(1f, 1f, 1f, 0f), false);
        _fade = NewImage("Fade", _canvasGo.transform, new Color(0f, 0f, 0f, 1f), false);

        // 「Spaceでスキップ」
        GameObject hint = new GameObject("SkipHint", typeof(RectTransform));
        hint.transform.SetParent(_canvasGo.transform, false);
        RectTransform hr = (RectTransform)hint.transform;
        hr.anchorMin = new Vector2(1f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(1f, 0f);
        hr.anchoredPosition = new Vector2(-40f, 28f);
        hr.sizeDelta = new Vector2(600f, 60f);
        TextMeshProUGUI ht = hint.AddComponent<TextMeshProUGUI>();
        if (_font != null) ht.font = _font;
        ht.text = "Space：スキップ";
        ht.fontSize = 30f;
        ht.alignment = TextAlignmentOptions.BottomRight;
        ht.color = new Color(1f, 1f, 1f, 0.75f);
        ht.raycastTarget = false;
    }

    private static Image NewImage(string name, Transform parent, Color c, bool raycast)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        Stretch((RectTransform)g.transform);
        Image im = g.AddComponent<Image>();
        im.color = c;
        im.raycastTarget = raycast;
        return im;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    // ================= カメラ・画面効果 =================

    private void LateUpdate()
    {
        if (_cam == null)
            return;

        float dt = Time.unscaledDeltaTime;

        if (_follow)
            UpdateFollow(dt);

        Vector2 off = Vector2.zero;
        if (_shake > 0.0001f)
        {
            off = Random.insideUnitCircle * _shake;
            _shake = Mathf.Max(0f, _shake - _shakeDecay * dt);
        }
        ApplyCamera(off);
    }

    private void ApplyCamera(Vector2 off)
    {
        _cam.transform.localPosition = new Vector3(_camPos.x + off.x, _camPos.y + off.y, -10f);
        _cam.orthographicSize = _camSize;
    }

    /// <summary>画面を揺らす（amp = 揺れの大きさ、time = 収まるまでの秒数）。</summary>
    private void Shake(float amp, float time)
    {
        _shake = Mathf.Max(_shake, amp);
        _shakeDecay = _shake / Mathf.Max(0.05f, time);
    }

    private void SetDim(float a)
    {
        if (_dim != null) _dim.color = new Color(0f, 0f, 0f, a);
    }

    private IEnumerator DimTo(float a, float time)
    {
        float from = _dim != null ? _dim.color.a : 0f;
        yield return Tween(time, k => SetDim(Mathf.Lerp(from, a, k)));
    }

    private IEnumerator FadeTo(Image img, float a, float time)
    {
        if (img == null)
            yield break;
        float from = img.color.a;
        Color c = img.color;
        yield return Tween(time, k =>
        {
            if (img == null) return;
            img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, a, k));
        });
    }

    /// <summary>画面を一瞬、白く光らせる。</summary>
    private IEnumerator FlashRoutine(float peak, float inTime, float outTime)
    {
        if (_flash == null)
            yield break;
        _flash.color = new Color(1f, 1f, 1f, 0f);
        yield return FadeTo(_flash, peak, inTime);
        yield return FadeTo(_flash, 0f, outTime);
    }

    // ================= 小さな道具 =================

    private static float Dt()
    {
        return Time.unscaledDeltaTime;
    }

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Dt();
            yield return null;
        }
    }

    private IEnumerator Tween(float time, System.Action<float> step)
    {
        float t = 0f;
        float len = Mathf.Max(0.0001f, time);
        while (t < len)
        {
            t += Dt();
            step(Mathf.Clamp01(t / len));
            yield return null;
        }
        step(1f);
    }

    private static float Smooth(float k)
    {
        return k * k * (3f - 2f * k);
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private int _busy;

    /// <summary>動きを並べて動かして、あとで WaitBusy で、全部終わるのを待てるようにする。</summary>
    private IEnumerator Track(IEnumerator e)
    {
        _busy++;
        while (e.MoveNext())
            yield return e.Current;
        _busy--;
    }

    private IEnumerator WaitBusy()
    {
        while (_busy > 0)
            yield return null;
    }

    private void Se(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
            AudioManager.PlaySE(clip, volume * seVolume);
    }

    private void Clack()
    {
        if (clackClips == null || clackClips.Length == 0)
            return;
        AudioClip c = clackClips[Random.Range(0, clackClips.Length)];
        Se(c, 0.8f);
    }

    private static Sprite Or(Sprite custom, Sprite fallback)
    {
        return custom != null ? custom : fallback;
    }

    // ================= 絵・文字の作り方 =================

    private GameObject NewRoot(string rootName)
    {
        GameObject g = new GameObject(rootName);
        g.transform.SetParent(_world.transform, false);
        return g;
    }

    /// <summary>スプライトを1枚置く（size = 画面での大きさ）。</summary>
    private SpriteRenderer Spr(Transform parent, string objName, Sprite s, Vector2 pos, Vector2 size, Color color, int order)
    {
        GameObject g = new GameObject(objName);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = color;
        sr.sortingOrder = order;
        Vector2 ss = s.bounds.size;
        g.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, ss.x), size.y / Mathf.Max(0.0001f, ss.y), 1f);
        return sr;
    }

    /// <summary>文字を置く（height = 文字の高さの目安）。</summary>
    private TextMeshPro Txt(Transform parent, string text, Vector2 pos, float height, Color color, int order, bool outline)
    {
        GameObject g = new GameObject("Text");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        TextMeshPro t = g.AddComponent<TextMeshPro>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = height * 10f * textScale;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = new Vector2(30f, 6f);
        if (outline)
        {
            t.outlineColor = new Color32(0, 0, 0, 255);
            t.outlineWidth = 0.22f;
        }
        MeshRenderer mr = g.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = order;
        return t;
    }

    /// <summary>指（指先が原点。体は、下にのびる。180度回すと、下向きの指になる）。</summary>
    private GameObject MakeFinger(Transform parent, string objName, float width, float length, int order)
    {
        GameObject root = new GameObject(objName);
        root.transform.SetParent(parent, false);

        if (_finger != null)
        {
            // 絵の指は、少し細いので、すこし太くして出す
            Spr(root.transform, "Finger", _finger, new Vector2(0f, -length * 0.5f), new Vector2(width * 1.2f, length), Color.white, order);
            return root;
        }

        Color outlineColor = new Color(0.45f, 0.30f, 0.24f);
        Color skin = new Color(0.94f, 0.77f, 0.63f);
        Color nail = new Color(0.98f, 0.88f, 0.84f);
        Spr(root.transform, "Outline", _capsule, new Vector2(0f, -length * 0.5f + 0.02f), new Vector2(width + 0.16f, length + 0.12f), outlineColor, order);
        Spr(root.transform, "Skin", _capsule, new Vector2(0f, -length * 0.5f), new Vector2(width, length), skin, order + 1);
        Spr(root.transform, "Nail", _capsule, new Vector2(0f, -width * 0.52f), new Vector2(width * 0.58f, width * 0.62f), nail, order + 2);
        return root;
    }

    // ================= キャラクター =================

    private class Actor
    {
        public GameObject go;
        public LimbRig rig;
        public string letter;
        public float height;
        public float baseX;
        public bool frozen;
        public SpriteRenderer[] sprites;
    }

    private readonly List<Actor> _actors = new List<Actor>();

    private Actor SpawnActor(Transform parent, string letter, float x, float widthFactor, int order)
    {
        GameObject go = Instantiate(keyCharacterPrefab, parent);
        go.name = "Key_" + letter;
        go.transform.localPosition = new Vector3(x, FloorY, 0f);
        go.transform.localScale = go.transform.localScale * charScale;

        // Spaceのように、横に広いキーにする（体を横に伸ばして、手足を外へずらす。文字は伸ばさない）
        if (widthFactor > 1.01f)
        {
            Transform vis = go.transform.Find("Visual");
            if (vis != null)
            {
                string[] limbs = { "LegL", "LegR", "ArmL", "ArmR" };
                for (int i = 0; i < limbs.Length; i++)
                {
                    Transform c = vis.Find(limbs[i]);
                    if (c == null) continue;
                    Vector3 p = c.localPosition;
                    p.x *= widthFactor;
                    c.localPosition = p;
                }
                Transform body = vis.Find("Body");
                if (body != null)
                {
                    Vector3 s = body.localScale;
                    s.x *= widthFactor;
                    body.localScale = s;
                    Transform lab = body.Find("Label");
                    if (lab != null)
                    {
                        Vector3 ls = lab.localScale;
                        ls.x /= widthFactor;
                        lab.localScale = ls;
                        TextMeshPro tmp = lab.GetComponent<TextMeshPro>();
                        if (tmp != null)
                        {
                            Vector2 sd = tmp.rectTransform.sizeDelta;
                            tmp.rectTransform.sizeDelta = new Vector2(sd.x * widthFactor, sd.y);
                        }
                    }
                }
            }
        }

        KeyLabel label = go.GetComponentInChildren<KeyLabel>(true);
        if (label != null)
        {
            label.textOverride = letter;
            label.Refresh();
        }

        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
            rs[i].sortingOrder += order;

        Actor a = new Actor();
        a.go = go;
        a.rig = go.GetComponent<LimbRig>();
        a.letter = letter;
        a.baseX = x;
        a.sprites = go.GetComponentsInChildren<SpriteRenderer>(true);
        if (a.rig != null)
            a.rig.spriteFacesRight = true;

        // 高さ（足元から頭の上まで）
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        for (int i = 0; i < a.sprites.Length; i++)
        {
            if (first) { b = a.sprites[i].bounds; first = false; }
            else b.Encapsulate(a.sprites[i].bounds);
        }
        a.height = Mathf.Max(0.5f, b.max.y - go.transform.position.y);

        _actors.Add(a);
        return a;
    }

    private static void Face(Actor a, bool right)
    {
        Vector3 s = a.go.transform.localScale;
        s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
        a.go.transform.localScale = s;
    }

    private static void Tint(Actor a, Color c)
    {
        for (int i = 0; i < a.sprites.Length; i++)
            if (a.sprites[i] != null) a.sprites[i].color = c;
    }

    private static void Stop(Actor a)
    {
        if (a.rig != null)
            a.rig.SetManualVelocity(0f, 0f);
    }

    /// <summary>ぴょんと跳んで、x の位置へ移動する（h = 跳ぶ高さ）。</summary>
    private IEnumerator HopTo(Actor a, float x, float h, float time)
    {
        yield return JumpTo(a, x, a.go.transform.localPosition.y, h, time);
    }

    /// <summary>(x, y) へ、放物線を描いて跳ぶ。</summary>
    private IEnumerator JumpTo(Actor a, float x, float y, float h, float time)
    {
        Transform tr = a.go.transform;
        Vector3 p0 = tr.localPosition;
        float dx = x - p0.x;
        float dy = y - p0.y;
        if (Mathf.Abs(dx) > 0.01f) Face(a, dx > 0f);
        float t = 0f;
        float len = Mathf.Max(0.05f, time);
        while (t < len)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / len);
            Vector3 p = p0;
            p.x = p0.x + dx * k;
            p.y = p0.y + dy * k + 4f * h * k * (1f - k);
            tr.localPosition = p;
            float vy = dy / len + 4f * h * (1f - 2f * k) / len;
            if (a.rig != null) a.rig.SetManualVelocity(dx / len, vy);
            yield return null;
        }
        tr.localPosition = new Vector3(x, y, p0.z);
        Stop(a);
    }

    /// <summary>走って、x の位置まで移動する。</summary>
    private IEnumerator RunTo(Actor a, float x, float speed)
    {
        Transform tr = a.go.transform;
        float dir = x >= tr.localPosition.x ? 1f : -1f;
        Face(a, dir > 0f);
        while (Mathf.Abs(x - tr.localPosition.x) > 0.05f)
        {
            Vector3 p = tr.localPosition;
            p.x = Mathf.MoveTowards(p.x, x, speed * Dt());
            tr.localPosition = p;
            if (a.rig != null) a.rig.SetManualVelocity(dir * speed);
            yield return null;
        }
        Stop(a);
    }

    // ================= ふきだし・擬音・小さな演出 =================

    /// <summary>キャラクターの頭の上に、セリフのふきだしを出す。</summary>
    private IEnumerator ShowBubble(Actor a, string text, float life)
    {
        GameObject root = new GameObject("Bubble");
        root.transform.SetParent(a.go.transform.parent, false);

        TextMeshPro t = Txt(root.transform, text, Vector2.zero, 0.7f, new Color(0.1f, 0.1f, 0.12f), 61, false);
        t.ForceMeshUpdate();
        Bounds tb = t.textBounds;
        t.transform.localPosition = new Vector3(-tb.center.x, -tb.center.y, 0f);
        float w = tb.size.x + 1.5f;
        float h = tb.size.y + 0.8f;
        SlicedSpr(root.transform, "Line", _bubble, Vector2.zero, new Vector2(w + 0.2f, h + 0.2f), new Color(0.08f, 0.08f, 0.1f), 59);
        SlicedSpr(root.transform, "Back", _bubble, Vector2.zero, new Vector2(w, h), Color.white, 60);
        SpriteRenderer tail = Spr(root.transform, "Tail", _sq, new Vector2(0f, -h * 0.5f - 0.02f), new Vector2(0.36f, 0.36f), Color.white, 60);
        tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        float popTime = 0.18f;
        float outTime = 0.15f;
        float t0 = 0f;
        while (t0 < life)
        {
            t0 += Dt();
            if (a.go == null) break;
            Vector3 p = a.go.transform.localPosition;
            root.transform.localPosition = new Vector3(p.x, p.y + a.height + 0.9f + h * 0.5f, 0f);
            float s;
            if (t0 < popTime) s = EaseOutBack(t0 / popTime);
            else if (t0 > life - outTime) s = Mathf.Clamp01((life - t0) / outTime);
            else s = 1f;
            root.transform.localScale = Vector3.one * s;
            yield return null;
        }
        Destroy(root);
    }

    /// <summary>擬音（「カタ」「ドン!」など）を、ぽんと出して、すぐ消す。</summary>
    private IEnumerator PopText(Transform parent, string text, Vector2 pos, float height, Color color, float life, float angle)
    {
        TextMeshPro t = Txt(parent, text, pos, height, color, 70, true);
        t.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        float popTime = 0.12f;
        float outTime = 0.14f;
        float t0 = 0f;
        while (t0 < life)
        {
            t0 += Dt();
            if (t == null) yield break;
            float s;
            if (t0 < popTime) s = EaseOutBack(t0 / popTime);
            else if (t0 > life - outTime) s = Mathf.Clamp01((life - t0) / outTime);
            else s = 1f;
            t.transform.localScale = Vector3.one * s;
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    /// <summary>丸い小さなもの（湯気・砂ぼこり）が、ふわっと上がって消える。</summary>
    private IEnumerator Puff(Transform parent, Vector2 pos, Color color, float size, float rise, float life, int order)
    {
        SpriteRenderer sr = Spr(parent, "Puff", _circle, pos, new Vector2(size, size), color, order);
        float t = 0f;
        while (t < life)
        {
            t += Dt();
            if (sr == null) yield break;
            float k = t / life;
            sr.transform.localPosition = new Vector3(pos.x, pos.y + rise * k, 0f);
            float s = size * (1f + 1.2f * k);
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    /// <summary>きらっとする星（ひし形）。</summary>
    private IEnumerator Sparkle(Transform parent, Vector2 pos, float size, float life, Color color, int order)
    {
        SpriteRenderer sr = Spr(parent, "Sparkle", _sq, pos, new Vector2(size, size), color, order);
        float spin = Random.Range(-180f, 180f);
        float t = 0f;
        while (t < life)
        {
            t += Dt();
            if (sr == null) yield break;
            float k = t / life;
            float s = size * Mathf.Sin(k * Mathf.PI);
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, 45f + spin * k);
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    // ================= 流れ =================

    private IEnumerator Run()
    {
        yield return Scene1Typing();
        yield return Scene2Dodge();
        yield return Scene3Limit();
        yield return Scene4Resolve();
        yield return Scene5Door();
        yield return Scene6NewWorld();
        yield return Scene7Title();
    }

    // ================= シーン1：カタカタ（人の目線）=================

    private class KeyCap
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector3 baseScale;
        public Vector3 basePos;
        public Color baseColor;
        public Transform label;
        public Vector3 labelScale;
    }

    private static readonly string[] Clacks = { "カタ", "カタカタ", "カタッ", "タタッ", "カチャ", "カタン" };

    private KeyCap AddKeyCap(Transform parent, string letter, Vector2 pos, float width, int order)
    {
        bool custom = _keycap != null;
        float height = custom ? 1.15f : 1.2f;
        if (custom && width < 2f)
            width *= 0.9f;   // 絵のキーは、少し縦長なので、横をせまくする
        Color baseColor = custom ? Color.white : new Color(0.84f, 0.86f, 0.9f);
        SpriteRenderer sr = Spr(parent, "Key", Or(_keycap, _round), pos, new Vector2(width, height), baseColor, order);
        KeyCap cap = new KeyCap();
        cap.tr = sr.transform;
        cap.sr = sr;
        cap.baseScale = sr.transform.localScale;
        cap.basePos = sr.transform.localPosition;
        cap.baseColor = baseColor;
        if (!string.IsNullOrEmpty(letter))
        {
            // キーキャラクターの絵は、文字を少し上（キーの上面）に出す
            Vector2 lp = custom ? pos + new Vector2(-width * 0.015f, height * 0.12f) : pos;
            TextMeshPro t = Txt(parent, letter, lp, custom ? 0.58f : 0.8f, new Color(0.15f, 0.15f, 0.2f), order + 1, false);
            cap.label = t.transform;
            cap.labelScale = t.transform.localScale;
        }
        return cap;
    }

    private static void PressVisual(KeyCap cap, bool down)
    {
        if (down)
        {
            cap.tr.localScale = cap.baseScale * 0.88f;
            cap.tr.localPosition = cap.basePos + new Vector3(0f, -0.07f, 0f);
            cap.sr.color = new Color(cap.baseColor.r * 0.62f, cap.baseColor.g * 0.62f, cap.baseColor.b * 0.62f, cap.baseColor.a);
            if (cap.label != null) cap.label.localScale = cap.labelScale * 0.9f;
        }
        else
        {
            cap.tr.localScale = cap.baseScale;
            cap.tr.localPosition = cap.basePos;
            cap.sr.color = cap.baseColor;
            if (cap.label != null) cap.label.localScale = cap.labelScale;
        }
    }

    private IEnumerator MoveTransform(Transform t, Vector3 from, Vector3 to, float time)
    {
        yield return Tween(time, k =>
        {
            if (t != null) t.localPosition = Vector3.Lerp(from, to, Smooth(k));
        });
    }

    private IEnumerator Scene1Typing()
    {
        GameObject root = NewRoot("Scene1_Typing");
        Transform rt = root.transform;
        _cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
        _camPos = Vector2.zero;
        _camSize = 5.2f;
        SetDim(0f);

        bool customDesk = deskBackground != null;
        Spr(rt, "Desk", Or(deskBackground, _sq), Vector2.zero, new Vector2(40f, 22f), customDesk ? Color.white : new Color(0.09f, 0.08f, 0.1f), 0);
        bool customKb = keyboardSprite != null;
        Spr(rt, "Keyboard", Or(keyboardSprite, _round), new Vector2(0f, 0.45f), new Vector2(17.8f, 7.4f), customKb ? Color.white : new Color(0.15f, 0.15f, 0.18f), 2);

        string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        float[] stagger = { 0f, 0.34f, 0.62f, 1.0f };
        List<KeyCap> caps = new List<KeyCap>();
        for (int r = 0; r < rows.Length; r++)
        {
            string row = rows[r];
            float y = 3.0f - r * 1.3f;
            float x0 = -(row.Length - 1) * 1.62f * 0.5f + stagger[r] - 0.5f;
            for (int c = 0; c < row.Length; c++)
                caps.Add(AddKeyCap(rt, row[c].ToString(), new Vector2(x0 + c * 1.62f, y), 1.5f, 3));
        }
        caps.Add(AddKeyCap(rt, "", new Vector2(0f, -2.2f), 6.0f, 3));

        // 指（下の端から、上に向かって出ている。先が指先）
        float[] fx = { -5.6f, -3.8f, -1.9f, 1.9f, 3.8f, 5.6f };
        const float restY = -1.7f;
        Transform[] fingers = new Transform[fx.Length];
        bool[] busy = new bool[fx.Length];
        for (int i = 0; i < fx.Length; i++)
        {
            GameObject f = MakeFinger(rt, "Finger" + i, 1.45f, 5.5f, 12);
            f.transform.localPosition = new Vector3(fx[i], restY, 0f);
            fingers[i] = f.transform;
        }

        _fade.color = new Color(0f, 0f, 0f, 1f);
        StartCoroutine(FadeTo(_fade, 0f, 0.6f));
        StartCoroutine(Tween(typingTime + 1f, k => _camSize = Mathf.Lerp(5.2f, 4.6f, k)));
        yield return Wait(0.7f);

        // だんだん速く、カタカタ打つ
        float elapsed = 0f;
        while (elapsed < typingTime)
        {
            float k = elapsed / typingTime;
            float interval = Mathf.Lerp(0.26f, 0.07f, k * k);

            int fi = Random.Range(0, fingers.Length);
            for (int tries = 0; tries < fingers.Length && busy[fi]; tries++)
                fi = (fi + 1) % fingers.Length;
            if (!busy[fi])
            {
                List<KeyCap> near = new List<KeyCap>();
                for (int i = 0; i < caps.Count; i++)
                    if (Mathf.Abs(caps[i].basePos.x - fx[fi]) < 2.0f) near.Add(caps[i]);
                if (near.Count > 0)
                    StartCoroutine(PressRoutine(rt, fingers, busy, fi, new Vector3(fx[fi], restY, 0f), near[Random.Range(0, near.Count)]));
            }

            float w = interval * Random.Range(0.8f, 1.2f);
            yield return Wait(w);
            elapsed += w;
        }
        yield return Wait(0.3f);

        // 最後に、大きな指がドンと押しつぶす
        GameObject big = MakeFinger(rt, "BigFinger", 3.4f, 11f, 20);
        big.transform.localPosition = new Vector3(0f, -7.5f, 0f);
        yield return MoveTransform(big.transform, new Vector3(0f, -7.5f, 0f), new Vector3(0f, 0.2f, 0f), 0.14f);
        Se(slamSe);
        Shake(0.6f, 0.5f);
        for (int i = 0; i < caps.Count; i++)
            PressVisual(caps[i], true);
        StartCoroutine(PopText(rt, "ドン!", new Vector2(0f, 3.4f), 2.2f, new Color(1f, 0.95f, 0.6f), 0.7f, -6f));
        yield return Wait(0.8f);

        yield return FadeTo(_fade, 1f, 0.25f);
        Destroy(root);
    }

    private IEnumerator PressRoutine(Transform parent, Transform[] fingers, bool[] busy, int fi, Vector3 rest, KeyCap cap)
    {
        busy[fi] = true;
        Vector3 target = new Vector3(cap.basePos.x, cap.basePos.y - 0.15f, 0f);
        yield return MoveTransform(fingers[fi], rest, target, 0.06f);
        PressVisual(cap, true);
        Clack();
        Shake(0.05f, 0.12f);
        StartCoroutine(PopText(parent, Clacks[Random.Range(0, Clacks.Length)],
            new Vector2(Random.Range(-6.5f, 6.5f), Random.Range(3.0f, 4.1f)), 0.8f, Color.white, 0.5f, Random.Range(-14f, 14f)));
        yield return Wait(0.05f);
        PressVisual(cap, false);
        yield return MoveTransform(fingers[fi], target, rest, 0.09f);
        busy[fi] = false;
    }

    // ================= シーン2〜4で使う、キーボードの上の世界 =================

    private GameObject _keyWorld;

    /// <summary>
    /// 部屋の背景の絵を使うときの、キーが立つ地面。
    /// 背景を少し暗くした床と、ゲームの地面の線（Ground）を、キーの足元（FloorY）に置く。
    /// </summary>
    private void AddRoomGround(Transform rt)
    {
        if (_roomGround != null)
        {
            // 木の板の絵（moveBG）。板の上の面の、すこし下が、キーの足元（FloorY）。下は、画面の外まで伸ばす
            const float plankH = 3.0f;
            Spr(rt, "Floor", _roomGround, new Vector2(0f, FloorY + 0.12f * plankH - plankH * 0.5f), new Vector2(30f, plankH), Color.white, 2);
            return;
        }
        Spr(rt, "FloorFill", _sq, new Vector2(0f, FloorY - 3f), new Vector2(40f, 6f), new Color(0f, 0f, 0f, 0.07f), 1);
        if (groundSprite != null)
            Spr(rt, "FloorLine", groundSprite, new Vector2(0f, FloorY - 0.09f), new Vector2(40f, 0.18f), Color.white, 2);
        else
            Spr(rt, "FloorLine", _sq, new Vector2(0f, FloorY - 0.05f), new Vector2(40f, 0.1f), new Color(0.2f, 0.2f, 0.2f), 2);
    }

    private void BuildKeyWorld()
    {
        _keyWorld = NewRoot("KeyWorld");
        Transform rt = _keyWorld.transform;
        _cam.backgroundColor = new Color(0.13f, 0.12f, 0.16f);
        _camPos = new Vector2(0f, 0.5f);
        _camSize = 5f;

        bool custom = roomBackground != null;
        Spr(rt, "Room", Or(roomBackground, _sq), new Vector2(0f, 0.5f), new Vector2(26f, 14f), custom ? Color.white : new Color(0.13f, 0.12f, 0.16f), 0);
        if (!custom)
        {
            // キーボードの表面（キーとキーのすき間の線）
            Spr(rt, "Floor", _sq, new Vector2(0f, -4.0f), new Vector2(40f, 4f), new Color(0.22f, 0.22f, 0.27f), 1);
            Spr(rt, "FloorLine", _sq, new Vector2(0f, FloorY), new Vector2(40f, 0.12f), new Color(0.45f, 0.45f, 0.52f), 2);
            for (int i = -12; i <= 12; i++)
                Spr(rt, "Gap", _sq, new Vector2(i * 1.6f + 0.8f, -3.2f), new Vector2(0.1f, 2.2f), new Color(0.1f, 0.1f, 0.13f), 2);
        }
        else
        {
            AddRoomGround(rt);
        }

        _actors.Clear();
        SpawnCast(rt, new float[] { -6.6f, -4.7f, -2.8f, 0.3f, 3.5f, 5.8f });
    }

    private static readonly string[] CastLetters = { "W", "S", "D", "Space", "Ctrl", "F" };
    private static readonly float[] CastWidths = { 1f, 1f, 1f, 2.2f, 1f, 1f };

    /// <summary>
    /// 6人のキーを出す。1人ずつ、描く順番の「帯」を分ける（文字や腕が、となりのキーの手前に出てしまわないように）。
    /// xs は、6人の、はじめの横の位置。
    /// </summary>
    private void SpawnCast(Transform rt, float[] xs)
    {
        for (int i = 0; i < CastLetters.Length; i++)
        {
            float x = xs[i];
            Actor a = SpawnActor(rt, CastLetters[i], x, CastWidths[i], 10 + i * 5);
            Face(a, true);
        }
    }

    // ================= シーン2：指をよける =================

    /// <summary>指が、x のところへ叩きつけられる（影で、先に知らせる）。</summary>
    private IEnumerator Slam(float x, float warn)
    {
        Transform rt = _keyWorld.transform;
        SpriteRenderer shadow = Spr(rt, "Shadow", _circle, new Vector2(x, FloorY - 0.05f), new Vector2(0.5f, 0.18f), new Color(0f, 0f, 0f, 0f), 8);
        GameObject f = MakeFinger(rt, "SlamFinger", 2.4f, 9f, 50);
        f.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        Vector3 top = new Vector3(x, 9.5f, 0f);
        Vector3 hit = new Vector3(x, FloorY + 0.05f, 0f);
        f.transform.localPosition = top;

        yield return Tween(warn, k =>
        {
            if (shadow == null) return;
            float w = Mathf.Lerp(0.6f, 3.2f, k);
            shadow.transform.localScale = new Vector3(w, w * 0.34f, 1f);
            shadow.color = new Color(0f, 0f, 0f, 0.5f * k);
        });
        yield return MoveTransform(f.transform, top, hit, 0.09f);
        Se(slamSe);
        Shake(0.42f, 0.3f);
        yield return Wait(0.22f);
        yield return MoveTransform(f.transform, hit, top, 0.2f);
        Destroy(f);
        if (shadow != null) Destroy(shadow.gameObject);
    }

    /// <summary>セリフを言いながら、叩きつけられる指をよける。終わったら、元の場所へ戻る。</summary>
    private IEnumerator DodgeRound(Actor a, string line, float dx)
    {
        float x = a.go.transform.localPosition.x;
        StartCoroutine(Slam(x, 0.7f));
        StartCoroutine(ShowBubble(a, line, 1.9f));
        yield return Wait(0.35f);
        Se(hopSe);
        yield return HopTo(a, x + dx, 1.2f, 0.35f);
        yield return Wait(0.6f);
        yield return HopTo(a, a.baseX, 0.5f, 0.3f);
    }

    private IEnumerator Scene2Dodge()
    {
        BuildKeyWorld();
        _fade.color = new Color(0f, 0f, 0f, 1f);
        StartCoroutine(FadeTo(_fade, 0f, 0.35f));
        yield return Wait(0.8f);

        yield return DodgeRound(_actors[0], "またジャンプか…", 1.4f);
        yield return DodgeRound(_actors[1], "ずっと移動…", -1.4f);
        yield return DodgeRound(_actors[4], "攻撃ばかり…", 1.4f);
        yield return DodgeRound(_actors[5], "休ませてくれ…", -1.4f);

        // 最後は、指が次々と降ってきて、みんな逃げ回る
        float[] xs = { -4.6f, 0.4f, 4.8f };
        for (int i = 0; i < xs.Length; i++)
        {
            StartCoroutine(Slam(xs[i], 0.45f));
            for (int j = 0; j < _actors.Count; j++)
            {
                Actor a = _actors[j];
                if (Mathf.Abs(a.go.transform.localPosition.x - xs[i]) < 2.4f)
                {
                    float dir = a.go.transform.localPosition.x >= xs[i] ? 1f : -1f;
                    float nx = Mathf.Clamp(a.go.transform.localPosition.x + dir * 1.5f, -7.8f, 7.8f);
                    StartCoroutine(Track(HopTo(a, nx, 1.3f, 0.35f)));
                }
            }
            yield return Wait(0.6f);
        }
        yield return WaitBusy();
        for (int j = 0; j < _actors.Count; j++)
            StartCoroutine(Track(HopTo(_actors[j], _actors[j].baseX, 0.4f, 0.3f)));
        yield return WaitBusy();
        yield return Wait(0.5f);
    }

    // ================= シーン3：限界 =================

    private bool _droop;

    /// <summary>疲れ果てて、うなだれて、ぷるぷる震える。</summary>
    private IEnumerator Droop(Actor a, float tilt, float seed)
    {
        Vector3 basePos = a.go.transform.localPosition;
        while (_droop)
        {
            if (!a.frozen && a.go != null)
            {
                float t = Time.unscaledTime;
                float z = tilt + Mathf.Sin(t * 3.1f + seed) * 1.6f;
                a.go.transform.localEulerAngles = new Vector3(0f, 0f, z);
                float jx = Mathf.Sin(t * 37f + seed) * 0.025f;
                a.go.transform.localPosition = new Vector3(basePos.x + jx, basePos.y, basePos.z);
            }
            yield return null;
        }
        if (a.go != null && !a.frozen)
            a.go.transform.localPosition = basePos;
    }

    private IEnumerator SteamLoop()
    {
        while (_droop)
        {
            Actor a = _actors[Random.Range(0, _actors.Count)];
            if (!a.frozen && a.go != null)
            {
                Vector3 p = a.go.transform.localPosition;
                float ox = Random.Range(-0.5f, 0.5f);
                StartCoroutine(Puff(_keyWorld.transform, new Vector2(p.x + ox, p.y + a.height * 0.95f),
                    new Color(1f, 1f, 1f, 0.55f), Random.Range(0.3f, 0.5f), Random.Range(0.9f, 1.4f), 1.0f, 40));
            }
            yield return Wait(0.12f);
        }
    }

    private IEnumerator Scene3Limit()
    {
        _droop = true;
        float[] tilts = { 7f, -6f, 8f, 0f, -7f, 6f };
        for (int i = 0; i < _actors.Count; i++)
            StartCoroutine(Droop(_actors[i], tilts[i % tilts.Length], i * 1.7f));
        StartCoroutine(SteamLoop());
        StartCoroutine(DimTo(0.5f, 1.0f));
        StartCoroutine(Tween(5f, k => _camSize = Mathf.Lerp(5f, 4.5f, k)));

        yield return Wait(0.9f);
        StartCoroutine(ShowBubble(_actors[0], "もうムリ…", 1.8f));
        yield return Wait(1.1f);
        StartCoroutine(ShowBubble(_actors[1], "こんな生活…", 1.8f));
        yield return Wait(1.3f);

        // Spaceが、力尽きて倒れる
        Actor sp = _actors[3];
        sp.frozen = true;
        Vector3 spPos = sp.go.transform.localPosition;
        yield return Tween(0.45f, k =>
        {
            float e = k * k;
            sp.go.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(0f, -82f, e));
            sp.go.transform.localPosition = new Vector3(spPos.x, spPos.y - 0.12f * e, spPos.z);
        });
        Se(collapseSe);
        Shake(0.3f, 0.35f);
        StartCoroutine(Puff(_keyWorld.transform, new Vector2(spPos.x + 1.5f, FloorY + 0.1f), new Color(0.8f, 0.8f, 0.85f, 0.5f), 0.9f, 0.5f, 0.7f, 40));
        yield return Wait(0.8f);
        StartCoroutine(ShowBubble(_actors[5], "ずっと…？", 2.0f));
        yield return Wait(2.2f);
    }

    // ================= シーン4：決意 =================

    private IEnumerator SpeedLines(Transform parent, Vector2 center, float duration)
    {
        const int count = 30;
        SpriteRenderer[] lines = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
            lines[i] = Spr(parent, "Line", _sq, center, new Vector2(0.07f, 2f), new Color(1f, 1f, 1f, 0.55f), 5);

        float t = 0f;
        float next = 0f;
        while (t < duration)
        {
            t += Dt();
            if (t >= next)
            {
                next = t + 0.07f;
                for (int i = 0; i < count; i++)
                {
                    if (lines[i] == null) continue;
                    float ang = Random.Range(0f, 360f);
                    float r = Random.Range(5.5f, 10f);
                    float len = Random.Range(2f, 5f);
                    float rad = ang * Mathf.Deg2Rad;
                    lines[i].transform.localPosition = new Vector3(center.x + Mathf.Cos(rad) * r, center.y + Mathf.Sin(rad) * r * 0.6f, 0f);
                    lines[i].transform.localRotation = Quaternion.Euler(0f, 0f, ang + 90f);
                    lines[i].transform.localScale = new Vector3(0.07f, len, 1f);
                }
            }
            yield return null;
        }
        for (int i = 0; i < count; i++)
            if (lines[i] != null) Destroy(lines[i].gameObject);
    }

    private IEnumerator Scene4Resolve()
    {
        // うなだれをやめて、まっすぐ立つ（Spaceは、起き上がる）
        _droop = false;
        yield return null;
        float[] z0 = new float[_actors.Count];
        for (int i = 0; i < _actors.Count; i++)
            z0[i] = Mathf.DeltaAngle(0f, _actors[i].go.transform.localEulerAngles.z);
        Vector3 spPos = _actors[3].go.transform.localPosition;
        float spY = Mathf.Max(spPos.y, FloorY);
        StartCoroutine(DimTo(0.1f, 1.0f));
        yield return Tween(0.6f, k =>
        {
            float e = Smooth(k);
            for (int i = 0; i < _actors.Count; i++)
                _actors[i].go.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(z0[i], 0f, e));
            _actors[3].go.transform.localPosition = new Vector3(spPos.x, Mathf.Lerp(spPos.y, spY, e), spPos.z);
        });
        for (int i = 0; i < _actors.Count; i++)
            _actors[i].frozen = false;
        yield return Wait(0.3f);

        // みんなで、ぴょんと跳ねて、拳を上げる
        Se(resolveSe);
        for (int i = 0; i < _actors.Count; i++)
            StartCoroutine(Track(HopTo(_actors[i], _actors[i].baseX, 1.0f, 0.4f)));
        yield return WaitBusy();
        for (int i = 0; i < _actors.Count; i++)
            if (_actors[i].rig != null) _actors[i].rig.SetMotion(LimbRig.KeyMotion.Cheer);

        Transform rt = _keyWorld.transform;
        StartCoroutine(SpeedLines(rt, new Vector2(0f, 0.8f), 2.8f));
        StartCoroutine(Tween(2.8f, k => _camSize = Mathf.Lerp(4.8f, 3.9f, Smooth(k))));
        Shake(0.2f, 0.4f);

        TextMeshPro big = Txt(rt, "ここから出よう!!", new Vector2(0f, 3.2f), 1.6f, new Color(1f, 0.93f, 0.45f), 80, true);
        big.outlineWidth = 0.3f;
        yield return Tween(0.3f, k => { if (big != null) big.transform.localScale = Vector3.one * EaseOutBack(k); });
        Shake(0.3f, 0.4f);
        yield return Wait(2.2f);

        StartCoroutine(FlashRoutine(1f, 0.2f, 0.5f));
        yield return Wait(0.25f);
        for (int i = 0; i < _actors.Count; i++)
            if (_actors[i].rig != null) _actors[i].rig.SetMotion(LimbRig.KeyMotion.None);
        SetDim(0f);
    }

    // ================= 追いかけるカメラ =================

    private bool _follow;
    private float _followDX = 1.5f;
    private float _followY = 0.8f;

    private void UpdateFollow(float dt)
    {
        float sum = 0f;
        int n = 0;
        for (int i = 0; i < _actors.Count; i++)
        {
            Actor a = _actors[i];
            if (a.go != null && a.go.activeSelf)
            {
                sum += a.go.transform.localPosition.x;
                n++;
            }
        }
        if (n == 0)
            return;

        float k = 1f - Mathf.Exp(-4f * dt);
        float target = sum / n + _followDX * (_camSize / 5f);
        _camPos.x = Mathf.Lerp(_camPos.x, target, k);
        float ty = _followY + (_camSize - 5f) * 0.6f;
        _camPos.y = Mathf.Lerp(_camPos.y, ty, k);
    }

    private void ReparentActors(Transform parent)
    {
        for (int i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].go == null) continue;
            _actors[i].go.transform.SetParent(parent, false);
            _actors[i].go.transform.localEulerAngles = Vector3.zero;
        }
    }

    // ================= シーン5：家を飛び出す =================

    private IEnumerator EnterDoor(Transform parent, Actor a, float doorX, float delay)
    {
        yield return Wait(delay);
        yield return RunTo(a, doorX - 0.3f, 7.5f);
        Vector3 p = a.go.transform.localPosition;
        a.go.SetActive(false);
        StartCoroutine(Sparkle(parent, new Vector2(doorX - 0.3f, p.y + a.height * 0.5f), 0.9f, 0.4f, new Color(1f, 0.98f, 0.8f), 40));
    }

    private IEnumerator Scene5Door()
    {
        GameObject root = NewRoot("Scene5_Door");
        Transform rt = root.transform;
        ReparentActors(rt);
        if (_keyWorld != null)
        {
            Destroy(_keyWorld);
            _keyWorld = null;
        }
        _follow = false;
        _cam.backgroundColor = new Color(0.1f, 0.08f, 0.08f);
        _camPos = new Vector2(0f, 0.5f);
        _camSize = 5f;
        SetDim(0f);

        bool custom = roomBackground != null;
        Spr(rt, "Wall", Or(roomBackground, _sq), new Vector2(0f, 0.5f), new Vector2(26f, 14f), custom ? Color.white : new Color(0.17f, 0.14f, 0.13f), 0);
        if (!custom)
        {
            Spr(rt, "Floor", _sq, new Vector2(0f, -4.0f), new Vector2(40f, 4f), new Color(0.24f, 0.19f, 0.16f), 1);
            Spr(rt, "FloorLine", _sq, new Vector2(0f, FloorY), new Vector2(40f, 0.1f), new Color(0.4f, 0.32f, 0.27f), 2);
        }
        else
        {
            AddRoomGround(rt);
        }

        // ドア（右側）。ドアの向こうは、まぶしい光
        bool customFrame = _doorFrame != null;
        float doorX = customFrame ? 6.4f : 7.0f;
        float doorW = customFrame ? 3.0f : 2.6f;
        // 枠の絵（DoorImage）は、余白を切ると 863×1369。ドアが入る穴は、幅 601・高さ 1247
        float doorH = customFrame ? doorW * (1247f / 601f) : 6.2f;
        float doorCy = FloorY + doorH * 0.5f;
        if (customFrame)
        {
            float sc = doorW / 601f;
            Spr(rt, "Frame", _doorFrame, new Vector2(doorX, FloorY + 1369f * sc * 0.5f), new Vector2(863f * sc, 1369f * sc), Color.white, 3);
        }
        else
        {
            Spr(rt, "Frame", _sq, new Vector2(doorX, doorCy + 0.1f), new Vector2(doorW + 0.4f, doorH + 0.2f), new Color(0.08f, 0.06f, 0.05f), 2);
        }
        int lightOrder = customFrame ? 1 : 3;   // 枠の絵のときは、光は、地面の板より後ろ
        SpriteRenderer light = Spr(rt, "Light", _sq, new Vector2(doorX, doorCy), new Vector2(doorW, doorH), new Color(1f, 0.97f, 0.82f, 0f), lightOrder);
        SpriteRenderer glow = Spr(rt, "Glow", _glow, new Vector2(doorX - 1.2f, doorCy), new Vector2(13f, 15f), new Color(1f, 0.96f, 0.8f, 0f), lightOrder);

        GameObject hinge = new GameObject("Hinge");
        hinge.transform.SetParent(rt, false);
        hinge.transform.localPosition = new Vector3(doorX - doorW * 0.5f, doorCy, 0f);
        bool customDoor = _doorLeaf != null;
        Spr(hinge.transform, "Door", Or(_doorLeaf, _sq), new Vector2(doorW * 0.5f, 0f), new Vector2(doorW, doorH), customDoor ? Color.white : new Color(0.4f, 0.26f, 0.17f), 4);
        if (!customDoor)
            Spr(hinge.transform, "Knob", _circle, new Vector2(doorW - 0.35f, -0.2f), new Vector2(0.3f, 0.3f), new Color(0.85f, 0.75f, 0.35f), 5);

        // キーたちが、ドアの前に並ぶ（右を向いて）
        float[] xs = { -7.8f, -5.6f, -3.4f, -0.8f, 1.9f, 3.9f };
        for (int i = 0; i < _actors.Count; i++)
        {
            Actor a = _actors[i];
            a.go.SetActive(true);
            a.go.transform.localPosition = new Vector3(xs[i % xs.Length], FloorY, 0f);
            a.baseX = xs[i % xs.Length];
            Face(a, true);
            Stop(a);
            if (a.rig != null) a.rig.SetMotion(LimbRig.KeyMotion.None);
            Tint(a, Color.white);
        }

        yield return Wait(0.6f);

        // ドアが開いて、光があふれる。逆光で、キーたちが黒い影になる
        Se(doorSe);
        yield return Tween(1.1f, k =>
        {
            float e = Smooth(k);
            hinge.transform.localScale = new Vector3(Mathf.Lerp(1f, 0.12f, e), 1f, 1f);
            light.color = new Color(1f, 0.97f, 0.82f, e);
            glow.color = new Color(1f, 0.96f, 0.8f, 0.6f * e);
            Color c = Color.Lerp(Color.white, new Color(0.12f, 0.1f, 0.1f), e);
            for (int i = 0; i < _actors.Count; i++) Tint(_actors[i], c);
        });
        Shake(0.12f, 0.3f);
        yield return Wait(0.4f);

        // 光に向かって、みんなで走り出す
        for (int i = 0; i < _actors.Count; i++)
            StartCoroutine(Track(EnterDoor(rt, _actors[i], doorX, (_actors.Count - 1 - i) * 0.06f)));
        yield return WaitBusy();

        StartCoroutine(FlashRoutine(1f, 0.25f, 0.7f));
        yield return Wait(0.3f);
        _prevRoot = root;
    }

    private GameObject _prevRoot;
    private GameObject _worldRoot;

    // ================= 道の景色（シーン6・タイトル画面で共通）=================

    /// <summary>空・丘・雲・道・草を作る。道は、途中に2つの谷があって、そのあとは長く平らに続く（タイトル画面の背景になる）。</summary>
    private void BuildRoad(Transform rt)
    {
        // 背景色は、実際のタイトル画面のカメラと同じ色（切り替えたときに、つながって見える）
        Camera titleCam = TitleCamera();
        _cam.backgroundColor = titleCam != null ? titleCam.backgroundColor : new Color(0.867f, 0.867f, 0.867f);
        if (skyBackground != null)
        {
            // 空の絵は、カメラにくっつけて、いつも画面いっぱいに出す
            SpriteRenderer sky = Spr(_cam.transform, "Sky", skyBackground, Vector2.zero, new Vector2(70f, 40f), Color.white, -5);
            sky.transform.localPosition = new Vector3(0f, 0f, 20f);
        }

        // 足場（途中に、2つの谷がある）
        float[,] plats = { { -30f, -1f }, { 2f, 9f }, { 12f, 70f } };
        bool customGround = groundSprite != null;
        for (int i = 0; i < 3; i++)
        {
            float x0 = plats[i, 0];
            float x1 = plats[i, 1];
            float cx = (x0 + x1) * 0.5f;
            float w = x1 - x0;
            if (customGround)
            {
                // ゲームの地面の絵（細い線）。ゲームと同じ太さで、足場の長さに伸ばす。線の上のふちが、キーの足元
                Spr(rt, "Ground", groundSprite, new Vector2(cx, FloorY - 0.09f), new Vector2(w, 0.18f), Color.white, 5);
            }
            else
            {
                Spr(rt, "Ground", _sq, new Vector2(cx, FloorY - 4f), new Vector2(w, 8f), new Color(0.3f, 0.32f, 0.37f), 5);
                Spr(rt, "GroundTop", _sq, new Vector2(cx, FloorY - 0.12f), new Vector2(w, 0.24f), new Color(0.55f, 0.58f, 0.64f), 6);
            }
            // 草
            for (float gx = x0 + 0.8f; gx < x1 - 0.6f; gx += Random.Range(1.6f, 2.8f))
            {
                if (gx < -40f || gx > 80f) continue;
                if (grassSprite != null)
                {
                    // ゲームの草の絵（根もとが、足元）
                    Spr(rt, "Grass", grassSprite, new Vector2(gx, FloorY - 0.02f), new Vector2(0.91f * 1.2f, 0.79f * 1.2f), Color.white, 7);
                    continue;
                }
                Spr(rt, "Grass", _sq, new Vector2(gx, FloorY + 0.22f), new Vector2(0.1f, 0.5f), new Color(0.35f, 0.62f, 0.3f), 7).transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                Spr(rt, "Grass", _sq, new Vector2(gx + 0.12f, FloorY + 0.2f), new Vector2(0.1f, 0.42f), new Color(0.3f, 0.55f, 0.27f), 7).transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            }
        }
    }

    // ================= シーン6：新たな世界へ =================

    private IEnumerator RunPath(Actor a)
    {
        yield return RunTo(a, -2.6f, 7f);
        Se(hopSe);
        yield return JumpTo(a, 3.2f, FloorY, 1.9f, 0.85f);
        Landed(a);
        yield return RunTo(a, 8.2f, 7f);
        Se(hopSe);
        yield return JumpTo(a, 13.2f, FloorY, 1.9f, 0.85f);
        Landed(a);
        yield return RunTo(a, 60f, 7f);
    }

    private void Landed(Actor a)
    {
        if (a.go == null) return;
        Se(landSe, 0.6f);
        Vector3 p = a.go.transform.localPosition;
        StartCoroutine(Puff(a.go.transform.parent, new Vector2(p.x, FloorY + 0.15f), new Color(0.85f, 0.78f, 0.62f, 0.7f), 0.8f, 0.4f, 0.5f, 20));
    }

    private IEnumerator Scene6NewWorld()
    {
        GameObject root = NewRoot("Scene6_World");
        _worldRoot = root;
        Transform rt = root.transform;
        ReparentActors(rt);
        if (_prevRoot != null)
        {
            Destroy(_prevRoot);
            _prevRoot = null;
        }
        SetDim(0f);

        BuildRoad(rt);

        // キーたちが、左から走ってくる
        for (int i = 0; i < _actors.Count; i++)
        {
            Actor a = _actors[i];
            a.go.SetActive(true);
            Tint(a, Color.white);
            a.go.transform.localPosition = new Vector3(-10f - 1.55f * i, FloorY, 0f);
            Face(a, true);
            Stop(a);
        }
        _camPos = new Vector2(-8.5f, 0.8f);
        _camSize = 5f;
        _follow = true;
        _followDX = 1.5f;
        _followY = 0.8f;

        _runCos.Clear();
        for (int i = 0; i < _actors.Count; i++)
            _runCos.Add(StartCoroutine(RunPath(_actors[i])));

        yield return Wait(3.5f);
    }

    // ================= シーン7：実際のタイトル画面を、歩いて通りすぎる =================

    private float MinActorX()
    {
        float m = 1000f;
        for (int i = 0; i < _actors.Count; i++)
        {
            Actor a = _actors[i];
            if (a.go != null && a.go.activeSelf)
                m = Mathf.Min(m, a.go.transform.localPosition.x);
        }
        return m;
    }

    private static float CastWidthOf(Actor a)
    {
        int idx = System.Array.IndexOf(CastLetters, a.letter);
        return (idx >= 0 && idx < CastWidths.Length) ? CastWidths[idx] : 1f;
    }

    /// <summary>キーが、タイトル画面の地面を歩いて、右へ去っていく（岩があれば、ぴょんと跳び越える）。</summary>
    private IEnumerator TitleWalk(Actor a, float exitX, bool hop, float rockL, float rockR, float hopH)
    {
        if (hop)
        {
            float margin = 1.1f + 0.6f * (CastWidthOf(a) - 1f);
            float x0 = rockL - margin;
            float x1 = rockR + margin;
            yield return RunTo(a, x0, titleWalkSpeed);
            Se(hopSe);
            yield return JumpTo(a, x1, FloorY, hopH, Mathf.Max(0.65f, (x1 - x0) / titleWalkSpeed));
            Landed(a);
        }
        yield return RunTo(a, exitX, titleWalkSpeed);
        if (a.go != null)
            a.go.SetActive(false);
    }

    private IEnumerator Scene7Title()
    {
        // 全員が、2つ目の谷を越えて、平らな道を走るところまで見届ける
        float t = 0f;
        while (MinActorX() < 15f && t < 9f)
        {
            t += Dt();
            yield return null;
        }

        // いったん暗くして、実際のタイトル画面に切り替える
        _follow = false;
        yield return FadeTo(_fade, 1f, 0.35f);

        for (int i = 0; i < _runCos.Count; i++)
        {
            if (_runCos[i] != null)
                StopCoroutine(_runCos[i]);
        }
        _runCos.Clear();

        // 実際のタイトル画面の、地面の高さ・画面のはし・岩を調べる
        Camera tc = TitleCamera();
        float camX = tc != null ? tc.transform.position.x : 0f;
        float halfW = (tc != null && tc.orthographic) ? tc.orthographicSize * tc.aspect : 9f;
        float leftX = camX - halfW;
        float rightX = camX + halfW;
        float groundTop = TitleGroundTop();

        bool hasRock = false;
        float rockL = 0f;
        float rockR = 0f;
        float hopH = 1.6f;
        if (hopOverRock)
        {
            Transform rock = FindTitleObject("Rock");
            SpriteRenderer rsr = rock != null ? rock.GetComponent<SpriteRenderer>() : null;
            if (rsr != null)
            {
                hasRock = true;
                rockL = rsr.bounds.min.x;
                rockR = rsr.bounds.max.x;
                float rockH = Mathf.Max(0.3f, rsr.bounds.max.y - groundTop);
                hopH = Mathf.Max(1.4f, (rockH + 0.4f) / 0.85f);
            }
        }

        // キーたちを、ムービーの世界から、タイトルシーンの地面の上に移す（画面の左の外に、並べる）
        _castRoot = new GameObject("TitleCast");
        _castRoot.transform.position = new Vector3(0f, groundTop - FloorY, 0f);
        ReparentActors(_castRoot.transform);
        ClearWorld(true);
        SetDim(0f);
        if (_flash != null)
            _flash.color = new Color(1f, 1f, 1f, 0f);
        HideMovieLayers();

        float startX = leftX - 1.5f;
        for (int i = 0; i < _actors.Count; i++)
        {
            Actor a = _actors[i];
            a.go.SetActive(true);
            Tint(a, Color.white);
            float off = WalkOffsets[Mathf.Min(i, WalkOffsets.Length - 1)];
            a.go.transform.localPosition = new Vector3(startX - off, FloorY, 0f);
            a.go.transform.localEulerAngles = Vector3.zero;
            Face(a, true);
            Stop(a);
            if (a.rig != null)
                a.rig.SetMotion(LimbRig.KeyMotion.None);
        }

        // 歩き出す（暗い幕は、歩きながら、ゆっくり明るくなる）
        float exitX = rightX + 2.5f;
        _busy = 0;
        for (int i = 0; i < _actors.Count; i++)
            StartCoroutine(Track(TitleWalk(_actors[i], exitX, hasRock, rockL, rockR, hopH)));
        yield return FadeTo(_fade, 0f, 0.5f);

        // 全員が、画面の右へ去るまで、歩く
        yield return WaitBusy();
        yield return Wait(0.5f);

        // 歩き終わったので、ロゴ → メニューの順に出す
        DestroyCast();
        DestroyOverlay();
        yield return RevealUi();
    }
}
