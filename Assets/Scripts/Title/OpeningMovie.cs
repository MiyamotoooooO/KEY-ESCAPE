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
    [Tooltip("シーン1：キーボードの背景（キーボード背景）。")]
    public Sprite keyboardSprite;
    [Tooltip("シーン1：左上のモニター（モニター）。")]
    public Sprite monitorSprite;
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

    [Header("タイトル画面を走るキーたち・追いかけるプレイヤー")]
    [Tooltip("実際のタイトル画面の地面を、キーたちとプレイヤーが走る速さ。")]
    public float titleRunSpeed = 7f;
    [Tooltip("タイトル画面の岩（Rock）を、ぴょんと跳び越える。")]
    public bool hopOverRock = true;
    [Tooltip("キーたちを後ろから追いかけるプレイヤー（DrawCharacter）。空なら、プレイヤーは出ない。")]
    public GameObject playerPrefab;
    [Tooltip("プレイヤーの大きさ。")]
    public float playerScale = 0.9f;
    [Tooltip("いちばん後ろのキーと、プレイヤーの間の距離。大きいほど、離れる。")]
    public float playerGap = 4f;

    [Header("タイトル画面を、ときどき往復するキー")]
    [Tooltip("全員が画面の外に出たあと、ランダムなキーが、ときどき、右へ→左へと歩いて往復する。")]
    public bool idleKeysOnTitle = true;
    [Tooltip("往復が終わってから、次の往復が始まるまでの秒数。")]
    public float idleInterval = 6f;
    [Tooltip("往復するときの、歩く速さ。")]
    public float idleWalkSpeed = 3.6f;

    [Header("音（空なら、鳴らさない）")]
    public AudioClip bgm;
    [Range(0f, 1f)] public float bgmVolume = 0.8f;
    [Tooltip("決意したあと（みんなが跳ねて、「ここから出よう!!」の場面）から、切りかえて流すBGM。空なら、切りかえない。")]
    public AudioClip resolveBgm;
    [Range(0f, 1f)] public float resolveBgmVolume = 0.8f;
    [Tooltip("BGMを切りかえるときの、つなぎ（フェード）の秒数。")]
    public float resolveBgmFade = 1.0f;
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

    [Header("音量（効果音ごと。1 が、そのままの大きさ。0 で消える）")]
    [Range(0f, 2f)] public float clackVolume = 1f;
    [Range(0f, 2f)] public float slamVolume = 1f;
    [Range(0f, 2f)] public float hopVolume = 1f;
    [Range(0f, 2f)] public float collapseVolume = 1f;
    [Range(0f, 2f)] public float resolveVolume = 1f;
    [Range(0f, 2f)] public float doorVolume = 1f;
    [Range(0f, 2f)] public float landVolume = 1f;
    [Range(0f, 2f)] public float logoVolume = 1f;

    [Header("長さ")]
    [Tooltip("シーン1：カタカタ打つ秒数。")]
    public float typingTime = 6f;
    [Tooltip("スキップできるようになるまでの秒数（押した瞬間の連打で、すぐ飛ばないように）。")]
    public float skipGuardTime = 0.5f;

    [Header("「はじめから」のムービー")]
    [Tooltip("OFFにすると、「はじめから」を押しても、ムービーを流さない（確認用）。")]
    public bool playNewGameIntro = true;
    [Tooltip("ONなら、「はじめから」のムービーを、Spaceで飛ばせる。OFF（初期値）なら、飛ばせない。")]
    public bool introSkippable = false;
    [Tooltip("モニターの検索欄に出る言葉。（全角の空白は、フォントに無いので、半角の空白に置きかわる）")]
    public string introSearchText = "逃亡 仕方 バレる？";
    [Tooltip("モニターのアップの絵（正面から見た、モニター全体）。空なら、コードで描いた仮のモニター。\n絵を入れたときは、絵の中の「画面」の部分に、検索のページと言葉が重なって出る。")]
    public Sprite introMonitorSprite;
    [Tooltip("モニターの絵の、横の長さ（画面の横いっぱいが、約18）。縦は、絵の縦横比で決まる。")]
    public float introMonitorWidth = 15.6f;
    [Tooltip("モニターの絵の中の「画面」の位置と大きさ。絵全体を 0〜1 として、左下が原点（X, Y = 画面の左下、Width, Height = 画面の大きさ）。\n初期値は「モニター正面」の絵に合わせてある。")]
    public Rect introMonitorScreen = new Rect(0.055f, 0.266f, 0.890f, 0.619f);
    [Tooltip("OFFにすると、検索欄の言葉を出さない（絵に、はじめから言葉が描いてあるとき）。")]
    public bool introShowSearchText = true;

    [Header("「はじめから」前半の、「…？」「…！？」の吹き出し")]
    [Tooltip("位置のずれ（右が＋、上が＋）。")]
    public Vector2 introPopOffset = Vector2.zero;
    [Tooltip("文字の大きさの倍率。1＝そのまま。")]
    public float introPopScale = 1f;

    [Header("「はじめから」を押したあとのBGM")]
    [Tooltip("「はじめから」を押した瞬間に切り替わるBGM。空なら、BGMはそのまま。")]
    public AudioClip introBgm;
    [Range(0f, 1f)]
    public float introBgmVolume = 1f;
    [Tooltip("切り替えるときの、クロスフェードの秒数。")]
    public float introBgmFade = 0.8f;
    [Tooltip("ONなら、拠点でKキーと話している間も、このBGMを流す（話が終わると、拠点のBGMに切り替わる）。OFF（初期値）なら、拠点に着いた時点で、拠点のBGMに切り替わる。")]
    public bool introBgmKeepDuringTalk = false;

    [Header("「はじめから」のあとの会話（拠点で流れる）")]
    [Tooltip("Kキーとの会話のセリフ・名前・枠の絵・文字の見た目。")]
    public NewGameIntroSettings introTalk = new NewGameIntroSettings();
    [Tooltip("お話の最後に仲間になる、逃げ遅れたキー（Key_K）。空なら、読み込まれているものから探す。")]
    public KeyDefinition introKey;

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
            // ゲームから戻ってきたとき：いつものタイトル画面のまま（ときどき、キーが往復する）
            if (idleKeysOnTitle && keyCharacterPrefab != null)
            {
                BuildSprites();
                StartCoroutine(IdleKeys(3f));
            }
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
        if (_introMode && !introSkippable)
            return;   // 「はじめから」のムービーは、飛ばせない
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
        if (_introMode)
        {
            StartCoroutine(IntroSkipRoutine());
            return;
        }
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
        if (idleKeysOnTitle)
            StartCoroutine(IdleKeys(3f));
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
            AudioManager.PlaySE(clip, volume * seVolume * GainFor(clip));
    }

    /// <summary>効果音ごとの音量（上の「音量」の欄）。</summary>
    private float GainFor(AudioClip clip)
    {
        if (clip == slamSe) return slamVolume;
        if (clip == hopSe) return hopVolume;
        if (clip == collapseSe) return collapseVolume;
        if (clip == resolveSe) return resolveVolume;
        if (clip == doorSe) return doorVolume;
        if (clip == landSe) return landVolume;
        if (clip == logoSe) return logoVolume;
        if (clackClips != null)
        {
            for (int i = 0; i < clackClips.Length; i++)
            {
                if (clackClips[i] == clip)
                    return clackVolume;
            }
        }
        return 1f;
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
        t.text = string.IsNullOrEmpty(text) ? "" : text.Replace('\u3000', ' ');   // 全角の空白は、フォントに無いので、半角にする
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

    // ================= シーン1：机の上（ななめ上から見た景色）=================

    /// <summary>木目の机の絵（仮の絵）。細い線が、ゆるやかにうねる。</summary>
    private Sprite MakeWoodSprite()
    {
        const int w = 1024;
        const int h = 576;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[w * h];
        Color baseA = new Color(0.60f, 0.42f, 0.28f);
        Color baseB = new Color(0.52f, 0.35f, 0.23f);
        Color line = new Color(0.30f, 0.19f, 0.12f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float wy = y + 7f * Mathf.Sin(x * 0.011f + y * 0.004f) + 4f * Mathf.Sin(x * 0.027f + 1.3f) + 2f * Mathf.Sin(x * 0.061f);
                float band = 0.5f + 0.5f * Mathf.Sin(wy * 0.05f);
                Color c = Color.Lerp(baseB, baseA, band);
                float s = Mathf.Sin(wy * 0.27f + Mathf.Sin(x * 0.004f) * 2f);
                float ln = Mathf.Clamp01((s - 0.86f) / 0.14f);
                c = Color.Lerp(c, line, ln * 0.85f);
                px[y * w + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _made.Add(tex);
        Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
        _made.Add(sp);
        return sp;
    }

    /// <summary>コード（ケーブル）。曲線を、細い線のつなぎで描く。</summary>
    private void Cable(Transform parent, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float thick, Color col, int order)
    {
        const int n = 28;
        Vector2 prev = p0;
        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n;
            float u = 1f - t;
            Vector2 q = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            Vector2 mid = (prev + q) * 0.5f;
            float len = (q - prev).magnitude;
            float ang = Mathf.Atan2(q.y - prev.y, q.x - prev.x) * Mathf.Rad2Deg;
            SpriteRenderer sr = Spr(parent, "Cable", _sq, mid, new Vector2(len + thick * 0.6f, thick), col, order);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            prev = q;
        }
    }

    // 「キーボード背景」の絵は、約13度傾けた長方形が、絵の中にある（長方形の大きさは、絵全体との割合）
    private const float KeyboardArtTilt = 13f;
    private const float KeyboardArtRectW = 1712f / 1774f;
    private const float KeyboardArtRectH = 444f / 887f;

    /// <summary>
    /// 傾いて描かれた絵を、キーボードの面（Board）に、ぴったり合わせて置く。
    /// 絵の傾きをいったん戻して、横・縦をそれぞれ伸ばして、もう一度、Board の傾きにそろえる（ゆがまない）。
    /// center, size は、Board の座標で、傾きを戻した長方形の、中心と大きさ。
    /// </summary>
    private void AddBoardArt(Transform board, string objName, Sprite s, Vector2 center, Vector2 size, Color color, int order)
    {
        GameObject b = new GameObject(objName);
        b.transform.SetParent(board, false);
        b.transform.localPosition = new Vector3(center.x, center.y, 0f);

        GameObject c = new GameObject(objName + "Art");
        c.transform.SetParent(b.transform, false);
        c.transform.localRotation = Quaternion.Euler(0f, 0f, -KeyboardArtTilt);
        Vector2 bs = s.bounds.size;
        float k = size.x / (bs.x * KeyboardArtRectW);
        c.transform.localScale = new Vector3(k, k, 1f);
        b.transform.localScale = new Vector3(1f, size.y / (bs.y * KeyboardArtRectH * k), 1f);

        SpriteRenderer sr = c.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = color;
        sr.sortingOrder = order;
    }

    /// <summary>モニターの絵がないときの、仮のモニター（下のふちと、足の丸い台だけが見える）。</summary>
    private void AddMonitorShapes(Transform board)
    {
        // モニター（キーボードと同じ向き。キーボードの座標で置く）
        Spr(board, "MonShadow", _round, new Vector2(-4.9f, 8.4f), new Vector2(13.4f, 7.2f), new Color(0f, 0f, 0f, 0.25f), 1);
        Spr(board, "MonBase", _circle, new Vector2(-4.2f, 5.9f), new Vector2(3.0f, 3.0f), new Color(0.18f, 0.18f, 0.21f), 3);
        Spr(board, "MonBaseTop", _circle, new Vector2(-4.2f, 5.9f), new Vector2(2.1f, 2.1f), new Color(0.25f, 0.25f, 0.29f), 3);
        Spr(board, "Monitor", _round, new Vector2(-5.5f, 8.7f), new Vector2(13f, 7f), new Color(0.12f, 0.12f, 0.14f), 4);
        Spr(board, "MonEdge", _sq, new Vector2(-5.5f, 5.3f), new Vector2(12.4f, 0.16f), new Color(0.30f, 0.30f, 0.35f), 5);
        Spr(board, "Glare1", _sq, new Vector2(-8.4f, 8.2f), new Vector2(1.6f, 9f), new Color(1f, 1f, 1f, 0.07f), 5).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
        Spr(board, "Glare2", _sq, new Vector2(-6.0f, 8.2f), new Vector2(0.8f, 9f), new Color(1f, 1f, 1f, 0.05f), 5).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
    }

    /// <summary>机の上の小物：左上のモニター（下のふちと、足の丸い台だけが見える）、右下のマウス、コード。</summary>
    private void BuildDeskItems(Transform rt, Transform board)
    {
        // モニター（左上）
        if (monitorSprite != null)
        {
            // 傾いて描かれた絵なので、そのまま、同じ割合で大きさをそろえて置く（足の丸い台も、絵の中にある）
            const float mw = 12f;
            Vector2 ms = monitorSprite.bounds.size;
            float mh = mw * ms.y / ms.x;
            Spr(rt, "MonShadow", monitorSprite, new Vector2(-4.7f, 4.1f), new Vector2(mw, mh), new Color(0f, 0f, 0f, 0.25f), 1);
            Spr(rt, "Monitor", monitorSprite, new Vector2(-5.0f, 4.4f), new Vector2(mw, mh), Color.white, 4);
        }
        else
        {
            AddMonitorShapes(board);
        }

        // マウス（右下）と、コード
        // キーボードの右がわの外を回って、上へ伸びる（キーボードの下をくぐらない）
        Cable(rt, new Vector2(7.4f, -2.6f), new Vector2(8.9f, -2.4f), new Vector2(8.9f, 1.5f), new Vector2(8.0f, 6.0f), 0.07f, new Color(0.12f, 0.1f, 0.1f), 1);
        GameObject mouse = new GameObject("Mouse");
        mouse.transform.SetParent(rt, false);
        mouse.transform.localPosition = new Vector3(7.2f, -3.8f, 0f);
        mouse.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
        Transform mt = mouse.transform;
        Spr(mt, "MouseShadow", _round, new Vector2(0.15f, -0.15f), new Vector2(1.6f, 2.5f), new Color(0f, 0f, 0f, 0.25f), 1);
        Spr(mt, "MouseBody", _round, Vector2.zero, new Vector2(1.6f, 2.5f), new Color(0.16f, 0.16f, 0.19f), 2);
        Spr(mt, "MouseLine", _sq, new Vector2(0f, 0.55f), new Vector2(0.05f, 1.0f), new Color(0.35f, 0.35f, 0.4f), 3);
        Spr(mt, "MouseWheel", _round, new Vector2(0f, 0.6f), new Vector2(0.2f, 0.5f), new Color(0.55f, 0.55f, 0.6f), 3);
    }

    private IEnumerator Scene1Typing()
    {
        GameObject root = NewRoot("Scene1_Typing");
        Transform rt = root.transform;
        _cam.backgroundColor = new Color(0.30f, 0.20f, 0.13f);
        _camPos = Vector2.zero;
        _camSize = 5.4f;
        SetDim(0f);

        // 机（木目）
        bool customDesk = deskBackground != null;
        Spr(rt, "Desk", customDesk ? deskBackground : MakeWoodSprite(), Vector2.zero, new Vector2(40f, 22.5f), Color.white, 0);

        // ななめ上から見た机の上：キーボードが少し傾き、左上にモニター、右下にマウス。
        // キーも指も、キーボードといっしょに傾ける（「Board」の子にする）
        const float tilt = KeyboardArtTilt;
        GameObject boardGo = new GameObject("Board");
        boardGo.transform.SetParent(rt, false);
        boardGo.transform.localPosition = new Vector3(0.3f, -0.5f, 0f);
        boardGo.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        boardGo.transform.localScale = new Vector3(0.84f, 0.84f, 1f);
        Transform board = boardGo.transform;

        BuildDeskItems(rt, board);

        bool customKb = keyboardSprite != null;
        if (customKb && keyboardSprite.name == "キーボード背景")
        {
            AddBoardArt(board, "KbShadow", keyboardSprite, new Vector2(0.3f, 0.1f), new Vector2(17.8f, 7.4f), new Color(0f, 0f, 0f, 0.3f), 1);
            AddBoardArt(board, "Keyboard", keyboardSprite, new Vector2(0f, 0.45f), new Vector2(17.8f, 7.4f), Color.white, 2);
        }
        else
        {
            Spr(board, "KbShadow", _round, new Vector2(0.3f, 0.1f), new Vector2(17.8f, 7.4f), new Color(0f, 0f, 0f, 0.3f), 1);
            Spr(board, "Keyboard", Or(keyboardSprite, _round), new Vector2(0f, 0.45f), new Vector2(17.8f, 7.4f), customKb ? Color.white : new Color(0.17f, 0.17f, 0.20f), 2);
        }
        for (int i = 0; i < 3; i++)
            Spr(board, "Led", _circle, new Vector2(7.6f + i * 0.45f, 3.85f), new Vector2(0.22f, 0.22f), new Color(0.88f, 0.92f, 0.96f), 3);

        string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        float[] stagger = { 0f, 0.34f, 0.62f, 1.0f };
        List<KeyCap> caps = new List<KeyCap>();
        for (int r = 0; r < rows.Length; r++)
        {
            string row = rows[r];
            float y = 3.0f - r * 1.3f;
            float x0 = -(row.Length - 1) * 1.62f * 0.5f + stagger[r] - 0.5f;
            for (int c = 0; c < row.Length; c++)
                caps.Add(AddKeyCap(board, row[c].ToString(), new Vector2(x0 + c * 1.62f, y), 1.5f, 3));
        }
        caps.Add(AddKeyCap(board, "", new Vector2(0f, -2.2f), 6.0f, 3));

        // 指（キーボードの手前から、キーボードの上へ向かって出ている。先が指先）
        float[] fx = { -5.6f, -3.8f, -1.9f, 1.9f, 3.8f, 5.6f };
        const float restY = -1.7f;
        Transform[] fingers = new Transform[fx.Length];
        bool[] busy = new bool[fx.Length];
        for (int i = 0; i < fx.Length; i++)
        {
            GameObject f = MakeFinger(board, "Finger" + i, 1.7f, 6.5f, 12);
            f.transform.localPosition = new Vector3(fx[i], restY, 0f);
            fingers[i] = f.transform;
        }

        _fade.color = new Color(0f, 0f, 0f, 1f);
        StartCoroutine(FadeTo(_fade, 0f, 0.6f));
        StartCoroutine(Tween(typingTime + 1f, k => _camSize = Mathf.Lerp(5.4f, 4.9f, k)));
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
        GameObject big = MakeFinger(board, "BigFinger", 3.6f, 12f, 20);
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

    /// <summary>決意したあとのBGMに切りかえる（入っていなければ、何もしない）。</summary>
    private void SwitchToResolveBgm()
    {
        if (resolveBgm == null)
            return;
        if (_bgmPushed)
        {
            // いまのBGMは、すでに覚えてあるので、そのまま、次のBGMへ切りかえる
            AudioManager.PlayBGM(resolveBgm, resolveBgmFade, true, resolveBgmVolume);
        }
        else
        {
            // ムービーのBGMがなかったときは、いまのBGMを覚えてから、切りかえる
            AudioManager.PushBGM(resolveBgm, resolveBgmFade, true, resolveBgmVolume);
            _bgmPushed = true;
        }
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

        // みんなで、ぴょんと跳ねて、拳を上げる（BGMも、ここで切りかわる）
        SwitchToResolveBgm();
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
        int lightOrder = 7;   // 部屋を暗くする幕（6）より手前に出す（暗い部屋の中で、光が明るく見える）
        SpriteRenderer light = Spr(rt, "Light", _sq, new Vector2(doorX, doorCy), new Vector2(doorW, doorH), new Color(1f, 0.97f, 0.82f, 0f), lightOrder);
        SpriteRenderer glow = Spr(rt, "Glow", _glow, new Vector2(doorX - 1.2f, doorCy), new Vector2(13f, 15f), new Color(1f, 0.96f, 0.8f, 0f), lightOrder);

        // 部屋を暗くする幕。光がさす前は暗く、ドアが開くと、少し明るくなる
        SpriteRenderer shade = Spr(rt, "Shade", _sq, new Vector2(0f, 0.5f), new Vector2(32f, 18f), new Color(0.03f, 0.03f, 0.05f, 0f), 6);

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

        // 部屋が、すっと暗くなる
        yield return Tween(0.6f, k => shade.color = new Color(0.03f, 0.03f, 0.05f, 0.82f * Smooth(k)));
        yield return Wait(0.3f);

        // ドアが開いて、光があふれる。逆光で、キーたちが黒い影になる
        Se(doorSe);
        yield return Tween(1.1f, k =>
        {
            float e = Smooth(k);
            hinge.transform.localScale = new Vector3(Mathf.Lerp(1f, 0.12f, e), 1f, 1f);
            light.color = new Color(1f, 0.97f, 0.82f, e);
            glow.color = new Color(1f, 0.96f, 0.8f, 0.6f * e);
            shade.color = new Color(0.03f, 0.03f, 0.05f, Mathf.Lerp(0.82f, 0.4f, e));
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
            yield return RunTo(a, x0, titleRunSpeed);
            Se(hopSe);
            yield return JumpTo(a, x1, FloorY, hopH, Mathf.Max(0.65f, (x1 - x0) / titleRunSpeed));
            Landed(a);
        }
        yield return RunTo(a, exitX, titleRunSpeed);
        if (a.go != null)
            a.go.SetActive(false);
    }

    /// <summary>
    /// プレイヤーが、キーたちを、後ろから追いかける。
    /// 絵とアニメーションだけを借りて、動きは、このスクリプトで決める（プレイヤー本来のスクリプトは、動かさない）。
    /// </summary>
    private IEnumerator TitlePlayerChase(float startX, float exitX, bool hop, float rockL, float rockR, float hopH)
    {
        SpriteRenderer srcSr = playerPrefab.GetComponent<SpriteRenderer>();
        Animator srcAn = playerPrefab.GetComponent<Animator>();
        if (srcSr == null || srcSr.sprite == null)
            yield break;

        // 足元（当たりの下のふち）が、地面にそろう高さ
        CapsuleCollider2D cap = playerPrefab.GetComponent<CapsuleCollider2D>();
        float foot = cap != null ? (cap.offset.y - cap.size.y * 0.5f) : -1.0f;
        foot *= playerScale;

        GameObject go = new GameObject("TitlePlayer");
        go.transform.SetParent(_castRoot.transform, false);
        go.transform.localScale = new Vector3(playerScale, playerScale, 1f);
        go.transform.localPosition = new Vector3(startX, FloorY - foot, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = srcSr.sprite;
        sr.sharedMaterial = srcSr.sharedMaterial;
        sr.sortingOrder = srcSr.sortingOrder + 5;
        // アニメーションの状態に付いている足音などの処理が、AudioSource を探すので、音の出ない AudioSource を付けておく
        AudioSource mute = go.AddComponent<AudioSource>();
        mute.playOnAwake = false;
        mute.mute = true;

        Animator an = null;
        if (srcAn != null && srcAn.runtimeAnimatorController != null)
        {
            an = go.AddComponent<Animator>();
            an.runtimeAnimatorController = srcAn.runtimeAnimatorController;
            an.SetFloat("Speed", 40f);
        }

        Transform tr = go.transform;
        float groundY = tr.localPosition.y;
        float speed = titleRunSpeed;
        const float margin = 1.0f;
        bool hopped = !hop;
        while (tr.localPosition.x < exitX)
        {
            Vector3 p = tr.localPosition;
            if (!hopped && p.x >= rockL - margin)
            {
                // 岩を跳び越える
                hopped = true;
                float x0 = p.x;
                float x1 = rockR + margin;
                float time = Mathf.Max(0.65f, (x1 - x0) / speed);
                if (an != null) an.SetBool("IsJumping", true);
                float t = 0f;
                while (t < time)
                {
                    t += Dt();
                    float k = Mathf.Clamp01(t / time);
                    p.x = x0 + (x1 - x0) * k;
                    p.y = groundY + 4f * hopH * k * (1f - k);
                    tr.localPosition = p;
                    yield return null;
                }
                p.y = groundY;
                tr.localPosition = p;
                if (an != null) an.SetBool("IsJumping", false);
                continue;
            }
            p.x += speed * Dt();
            tr.localPosition = p;
            yield return null;
        }
        go.SetActive(false);
    }

    // ================= 「はじめから」のムービー（前半：キーの無い机 → モニター）=================

    private bool _introMode;

    /// <summary>「はじめから」のムービーを、流せるか。</summary>
    public bool CanPlayNewGameIntro
    {
        get { return playNewGameIntro && !_playing; }
    }

    /// <summary>
    /// 「はじめから」を押したあとのムービーを流す（TitleMenu から呼ぶ）。
    /// キーが1つも無い机 → 困った主人公がモニターを見る → 検索欄の言葉、の順に流れる。
    /// 終わると、画面は暗いままで onDone が呼ばれる（そのあと、拠点のシーンへ移る）。
    /// 飛ばせるかどうかは、introSkippable で決まる（初期値は、飛ばせない）。
    /// </summary>
    public void PlayNewGameIntro(System.Action onDone)
    {
        if (_playing)
        {
            if (onDone != null) onDone();
            return;
        }

        // タイトル画面で、ときどき往復していたキーなどを、止めて片づける
        StopAllCoroutines();
        _runCos.Clear();
        DestroyCast();
        _actors.Clear();
        _follow = false;
        _busy = 0;
        _shake = 0f;
        _revealing = false;
        _skipping = false;
        _onFinished = onDone;
        _introMode = true;

        if (_font == null)
            _font = font != null ? font : FindSceneFont();
        if (_sq == null)
            BuildSprites();

        if (_world != null && _cam == null)
        {
            Destroy(_world);
            _world = null;
        }
        if (_world == null)
        {
            BuildStudio();
        }
        else
        {
            ClearWorld(false);
            _cam.targetTexture = _rt;
            _cam.enabled = true;
            _camPos = Vector2.zero;
            _camSize = 5f;
            ApplyCamera(Vector2.zero);
        }

        _playing = true;
        _current = this;
        _startTime = Time.unscaledTime;
        LockNavigation();
        BuildOverlay();

        // 飛ばせないときは、「Spaceでスキップ」の表示も消す
        if (!introSkippable && _canvasGo != null)
        {
            Transform skipHint = _canvasGo.transform.Find("SkipHint");
            if (skipHint != null)
                skipHint.gameObject.SetActive(false);
        }

        // 「はじめから」を押した瞬間に、BGMを切り替える
        if (introBgm != null)
            AudioManager.PlayBGM(introBgm, introBgmFade, true, introBgmVolume);

        StartCoroutine(IntroRun());
    }

    /// <summary>
    /// 「はじめから」のムービーのあと、拠点のシーンを読み込めなかったとき：幕を外して、タイトル画面にもどす。
    /// </summary>
    public void AbortIntro()
    {
        _introMode = false;
        DestroyOverlay();
        HideMovieLayers();
        ClearWorld(false);
    }

    /// <summary>
    /// 「はじめから」のあとの、後半（拠点でKキーと話す）に、タイトル側から渡すものをそろえる。
    /// TitleMenu が、拠点のシーンを読み込む直前に呼ぶ。
    /// </summary>
    public void PrepareNewGameIntro(string hubScene)
    {
        TMP_FontAsset f = _font != null ? _font : (font != null ? font : FindSceneFont());
        NewGameIntro.Prepare(hubScene, keyCharacterPrefab, introKey, f, introTalk, introBgm != null && introBgmKeepDuringTalk);
    }

    private void FinishIntro()
    {
        _introMode = false;
        _playing = false;
        _skipping = false;
        if (_current == this)
            _current = null;
        // 幕は、暗いまま残す（次のシーンに切り替わるまで、画面がちらつかないように）
        Done();
    }

    private IEnumerator IntroSkipRoutine()
    {
        if (_overlayAlive && _fade != null)
            yield return FadeTo(_fade, 1f, 0.25f);
        ClearWorld(false);
        FinishIntro();
    }

    private IEnumerator IntroRun()
    {
        yield return IntroEmptyDesk();
        yield return IntroMonitor();
        ClearWorld(false);
        FinishIntro();
    }

    /// <summary>
    /// キーが1つも無いキーボード：1つ目のムービー（Scene1Typing）でキーが付いていた場所すべてに、からっぽの土台（へこみ＋スイッチの軸）を置く。
    /// 「キーが抜け落ちたあと」だと分かるように、ほこりも少し散らす。board の座標で置く（キーボードといっしょに傾く）。
    /// </summary>
    private void AddEmptyKeySockets(Transform board)
    {
        string[] rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        float[] stagger = { 0f, 0.34f, 0.62f, 1.0f };
        for (int r = 0; r < rows.Length; r++)
        {
            float y = 3.0f - r * 1.3f;
            float x0 = -(rows[r].Length - 1) * 1.62f * 0.5f + stagger[r] - 0.5f;
            for (int c = 0; c < rows[r].Length; c++)
                AddKeySocket(board, new Vector2(x0 + c * 1.62f, y), 1.5f, 1.2f, false);
        }
        AddKeySocket(board, new Vector2(0f, -2.2f), 6.0f, 1.2f, true);

        // ほこり・くず（毎回同じ形になるように、種を決めておく）
        System.Random rng = new System.Random(11);
        for (int i = 0; i < 14; i++)
        {
            float x = (float)(rng.NextDouble() * 16.0 - 8.0);
            float y = (float)(rng.NextDouble() * 6.5 - 2.9);
            float s = 0.04f + (float)rng.NextDouble() * 0.06f;
            float g = 0.38f + (float)rng.NextDouble() * 0.18f;
            SpriteRenderer d = Spr(board, "Dust", _sq, new Vector2(x, y), new Vector2(s, s), new Color(g, g, g), 7);
            d.transform.localRotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 90.0));
        }
    }

    /// <summary>キーが外れた、1つ分の土台：明るいふち・暗いへこみ・底・中央のスイッチの軸（十字）。wide が true なら、スペースキー（軸が3つ）。</summary>
    private void AddKeySocket(Transform board, Vector2 pos, float w, float h, bool wide)
    {
        Spr(board, "SocketRim", _round, pos + new Vector2(0f, -0.05f), new Vector2(w + 0.14f, h + 0.14f), new Color(0.33f, 0.33f, 0.36f), 3);
        Spr(board, "SocketWell", _round, pos, new Vector2(w + 0.08f, h + 0.06f), new Color(0.07f, 0.07f, 0.08f), 4);
        Spr(board, "SocketFloor", _round, pos + new Vector2(0f, -0.05f), new Vector2(w - 0.02f, h - 0.06f), new Color(0.13f, 0.13f, 0.14f), 5);

        float[] stems = wide ? new float[] { -2.3f, 0f, 2.3f } : new float[] { 0f };
        Color stemColor = new Color(0.31f, 0.31f, 0.34f);
        for (int i = 0; i < stems.Length; i++)
        {
            Vector2 p = pos + new Vector2(stems[i], -0.03f);
            Spr(board, "SocketStemH", _sq, p, new Vector2(0.5f, 0.13f), stemColor, 6);
            Spr(board, "SocketStemV", _sq, p, new Vector2(0.13f, 0.5f), stemColor, 6);
        }
    }

    /// <summary>前半その1：キーが1つも無い、机の上。主人公は、あっけにとられて、モニターのほうを見る。</summary>
    private IEnumerator IntroEmptyDesk()
    {
        GameObject root = NewRoot("Intro_Desk");
        Transform rt = root.transform;
        _cam.backgroundColor = new Color(0.30f, 0.20f, 0.13f);
        _camPos = Vector2.zero;
        _camSize = 5.4f;
        SetDim(0f);

        // 机・モニター・マウスは、いちばん最初のムービーと同じ。キーボードだけ、キーも指も無い
        bool customDesk = deskBackground != null;
        Spr(rt, "Desk", customDesk ? deskBackground : MakeWoodSprite(), Vector2.zero, new Vector2(40f, 22.5f), Color.white, 0);

        GameObject boardGo = new GameObject("Board");
        boardGo.transform.SetParent(rt, false);
        boardGo.transform.localPosition = new Vector3(0.3f, -0.5f, 0f);
        boardGo.transform.localRotation = Quaternion.Euler(0f, 0f, KeyboardArtTilt);
        boardGo.transform.localScale = new Vector3(0.84f, 0.84f, 1f);
        Transform board = boardGo.transform;

        BuildDeskItems(rt, board);

        bool customKb = keyboardSprite != null;
        if (customKb && keyboardSprite.name == "キーボード背景")
        {
            AddBoardArt(board, "KbShadow", keyboardSprite, new Vector2(0.3f, 0.1f), new Vector2(17.8f, 7.4f), new Color(0f, 0f, 0f, 0.3f), 1);
            AddBoardArt(board, "Keyboard", keyboardSprite, new Vector2(0f, 0.45f), new Vector2(17.8f, 7.4f), Color.white, 2);
        }
        else
        {
            Spr(board, "KbShadow", _round, new Vector2(0.3f, 0.1f), new Vector2(17.8f, 7.4f), new Color(0f, 0f, 0f, 0.3f), 1);
            Spr(board, "Keyboard", Or(keyboardSprite, _round), new Vector2(0f, 0.45f), new Vector2(17.8f, 7.4f), customKb ? Color.white : new Color(0.17f, 0.17f, 0.20f), 2);
        }
        for (int i = 0; i < 3; i++)
            Spr(board, "Led", _circle, new Vector2(7.6f + i * 0.45f, 3.85f), new Vector2(0.22f, 0.22f), new Color(0.88f, 0.92f, 0.96f), 3);

        // キーが抜け落ちたあと：キーが付いていた場所に、からっぽの土台（キーの穴）が並ぶ
        AddEmptyKeySockets(board);

        // 暗い画面から、はじまる。しばらく、音もなく、ゆっくり寄っていく
        StartCoroutine(FadeTo(_fade, 0f, 0.9f));
        StartCoroutine(Tween(5.0f, k => _camSize = Mathf.Lerp(5.4f, 5.1f, k)));
        yield return Wait(1.6f);

        StartCoroutine(PopText(rt, "…？", new Vector2(0.2f, 1.0f) + introPopOffset, 2.2f * introPopScale, new Color(1f, 0.95f, 0.78f), 1.2f, -4f));
        yield return Wait(1.6f);

        Shake(0.25f, 0.4f);
        StartCoroutine(PopText(rt, "…！？", new Vector2(-0.3f, 0.8f) + introPopOffset, 2.6f * introPopScale, new Color(1f, 0.9f, 0.7f), 1.1f, 5f));
        yield return Wait(1.9f);

        // 左上のモニターへ、目を向ける
        float s0 = _camSize;
        Vector2 p0 = _camPos;
        yield return Tween(1.3f, k =>
        {
            float e = Smooth(k);
            _camSize = Mathf.Lerp(s0, 3.6f, e);
            _camPos = Vector2.Lerp(p0, new Vector2(-4.8f, 3.6f), e);
        });
        yield return Wait(0.5f);

        yield return FadeTo(_fade, 1f, 0.45f);
        Destroy(root);
    }

    /// <summary>前半その2：モニターのアップ。検索欄に、「逃亡 仕方 バレる？」と残っている。</summary>
    private IEnumerator IntroMonitor()
    {
        GameObject root = NewRoot("Intro_Monitor");
        Transform rt = root.transform;
        _cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        _camPos = Vector2.zero;
        _camSize = 5.2f;
        SetDim(0f);

        // 暗い部屋・机・モニターの光
        Spr(rt, "Room", _sq, Vector2.zero, new Vector2(40f, 24f), new Color(0.06f, 0.06f, 0.09f), 0);
        Spr(rt, "DeskEdge", _sq, new Vector2(0f, -5.6f), new Vector2(40f, 3.0f), new Color(0.20f, 0.13f, 0.09f), 1);
        SpriteRenderer halo = Spr(rt, "Halo", _glow, new Vector2(0f, 0.4f), new Vector2(26f, 16f), new Color(0.62f, 0.80f, 1f, 0f), 1);

        bool customMon = introMonitorSprite != null;
        Color screenOn = new Color(0.94f, 0.96f, 0.99f, 1f);
        Color screenOff = customMon ? new Color(0.94f, 0.96f, 0.99f, 0f) : new Color(0.04f, 0.04f, 0.06f, 1f);
        Color artOff = new Color(0.45f, 0.45f, 0.5f);
        SpriteRenderer screen;
        SpriteRenderer monArt = null;

        // 画面の位置と大きさ（ワールド座標）
        Vector2 sc;
        float sw;
        float sh;

        if (customMon)
        {
            // 自分で用意した、モニターの絵（まっ正面から）。絵の中の画面の部分に、検索のページを重ねる
            Vector2 ms = introMonitorSprite.bounds.size;
            float mw = Mathf.Max(1f, introMonitorWidth);
            float mh = mw * ms.y / Mathf.Max(0.0001f, ms.x);
            Vector2 mc = new Vector2(0f, 0.4f);
            monArt = Spr(rt, "MonitorArt", introMonitorSprite, mc, new Vector2(mw, mh), artOff, 3);

            Rect r = introMonitorScreen;
            sw = mw * r.width;
            sh = mh * r.height;
            sc = new Vector2(mc.x + mw * (r.x + r.width * 0.5f - 0.5f), mc.y + mh * (r.y + r.height * 0.5f - 0.5f));
        }
        else
        {
            // 仮のモニター（まっ正面から。台つき）
            Spr(rt, "Neck", _sq, new Vector2(0f, -4.2f), new Vector2(1.6f, 1.4f), new Color(0.13f, 0.13f, 0.16f), 2);
            Spr(rt, "Base", _round, new Vector2(0f, -4.95f), new Vector2(6.0f, 0.7f), new Color(0.15f, 0.15f, 0.18f), 2);
            Spr(rt, "Bezel", _round, new Vector2(0f, 0.4f), new Vector2(15.6f, 8.9f), new Color(0.11f, 0.11f, 0.14f), 3);
            sw = 14.6f;
            sh = 7.9f;
            sc = new Vector2(0f, 0.45f);
        }
        screen = Spr(rt, "Screen", _sq, sc, new Vector2(sw, sh), screenOff, 4);

        // 画面の中身（ブラウザの検索ページ）。画面の大きさに合わせて、まとめて拡大・縮小する。画面がつくまで、隠しておく
        float s = Mathf.Min(sw / 14.6f, sh / 7.9f);
        float pw = sw / s;   // ページの横の長さ（ページの座標で）
        GameObject ui = new GameObject("Ui");
        ui.transform.SetParent(rt, false);
        ui.transform.localPosition = new Vector3(sc.x, sc.y - 0.45f * s, 0f);
        ui.transform.localScale = new Vector3(s, s, 1f);
        Transform u = ui.transform;

        Spr(u, "TabBar", _sq, new Vector2(0f, 4.0f), new Vector2(pw, 0.7f), new Color(0.80f, 0.83f, 0.88f), 5);
        Spr(u, "Tab", _round, new Vector2(-pw * 0.5f + 1.9f, 4.0f), new Vector2(3.8f, 0.6f), new Color(0.97f, 0.98f, 1f), 6);
        Txt(u, "検索", new Vector2(-pw * 0.5f + 1.9f, 4.0f), 0.34f, new Color(0.3f, 0.32f, 0.38f), 8, false);
        Spr(u, "ToolBar", _sq, new Vector2(0f, 3.25f), new Vector2(pw, 0.85f), new Color(0.93f, 0.95f, 0.98f), 5);
        Spr(u, "AddressBar", _round, new Vector2(0f, 3.25f), new Vector2(Mathf.Min(11.4f, pw - 3.2f), 0.55f), new Color(0.86f, 0.89f, 0.94f), 6);

        Txt(u, "検索", new Vector2(0f, 2.15f), 1.3f, new Color(0.28f, 0.42f, 0.78f), 8, false);

        Spr(u, "BoxLine", _round, new Vector2(0f, 0.7f), new Vector2(9.9f, 1.3f), new Color(0.70f, 0.74f, 0.82f), 6);
        Spr(u, "Box", _round, new Vector2(0f, 0.7f), new Vector2(9.6f, 1.1f), Color.white, 7);
        // 虫めがねのマーク
        Spr(u, "IconRing", _circle, new Vector2(-4.3f, 0.72f), new Vector2(0.4f, 0.4f), new Color(0.45f, 0.5f, 0.58f), 8);
        Spr(u, "IconHole", _circle, new Vector2(-4.3f, 0.72f), new Vector2(0.24f, 0.24f), Color.white, 9);
        SpriteRenderer handle = Spr(u, "IconHandle", _sq, new Vector2(-4.08f, 0.5f), new Vector2(0.26f, 0.07f), new Color(0.45f, 0.5f, 0.58f), 8);
        handle.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Spr(u, "Button", _round, new Vector2(0f, -0.55f), new Vector2(3.0f, 0.8f), new Color(0.92f, 0.94f, 0.97f), 7);
        Txt(u, "検索", new Vector2(0f, -0.55f), 0.4f, new Color(0.3f, 0.32f, 0.38f), 9, false);

        // 検索欄の言葉（左から並ぶ）と、点滅するカーソル
        SpriteRenderer cursor = null;
        if (introShowSearchText)
        {
            Color textCol = new Color(0.14f, 0.14f, 0.2f);
            TextMeshPro q = Txt(u, introSearchText, new Vector2(0.35f, 0.7f), 0.62f, textCol, 9, false);
            q.alignment = TextAlignmentOptions.Left;
            q.rectTransform.sizeDelta = new Vector2(8.2f, 1f);
            q.ForceMeshUpdate();
            Bounds qb = q.textBounds;
            Vector3 endWorld = q.transform.TransformPoint(new Vector3(qb.max.x, qb.center.y, 0f));
            Vector3 endLocal = u.InverseTransformPoint(endWorld);
            cursor = Spr(u, "Cursor", _sq, new Vector2(endLocal.x + 0.1f, 0.7f), new Vector2(0.05f, 0.68f), textCol, 9);
        }
        ui.SetActive(false);

        // 暗いなかから、はじまる
        yield return FadeTo(_fade, 0f, 0.7f);
        yield return Wait(0.7f);

        // モニターの画面が、ぱっとつく
        Shake(0.08f, 0.2f);
        yield return Tween(0.6f, k =>
        {
            float e = Smooth(k);
            screen.color = Color.Lerp(screenOff, screenOn, e);
            if (monArt != null) monArt.color = Color.Lerp(artOff, Color.white, e);
            halo.color = new Color(0.62f, 0.80f, 1f, 0.5f * e);
        });
        ui.SetActive(true);
        StartCoroutine(CursorBlink(cursor));

        // 検索欄を、じっと見つめる（ゆっくり寄る）
        float camEndSize = 4.0f * Mathf.Clamp(s, 0.7f, 1f);
        float camEndY = sc.y + 0.35f * s;
        yield return Tween(4.2f, k =>
        {
            float e = Smooth(k);
            _camSize = Mathf.Lerp(5.2f, camEndSize, e);
            _camPos = Vector2.Lerp(Vector2.zero, new Vector2(0f, camEndY), e);
        });
        yield return Wait(0.6f);

        yield return FadeTo(_fade, 1f, 0.8f);
        Destroy(root);
    }

    private IEnumerator CursorBlink(SpriteRenderer c)
    {
        while (c != null)
        {
            c.enabled = !c.enabled;
            yield return Wait(0.5f);
        }
    }

    // ================= タイトル画面で、ときどき往復するキー =================

    private float _gTop;
    private float _gLeft;
    private float _gRight;
    private bool _gHasRock;
    private float _gRockL;
    private float _gRockR;
    private float _gHopH = 1.6f;

    /// <summary>タイトル画面の、地面の高さ・画面のはし・岩の位置を調べる。</summary>
    private void ComputeTitleGeometry()
    {
        Camera tc = TitleCamera();
        float camX = tc != null ? tc.transform.position.x : 0f;
        float halfW = (tc != null && tc.orthographic) ? tc.orthographicSize * tc.aspect : 9f;
        _gLeft = camX - halfW;
        _gRight = camX + halfW;
        _gTop = TitleGroundTop();
        _gHasRock = false;
        _gHopH = 1.6f;
        if (hopOverRock)
        {
            Transform rock = FindTitleObject("Rock");
            SpriteRenderer rsr = rock != null ? rock.GetComponent<SpriteRenderer>() : null;
            if (rsr != null)
            {
                _gHasRock = true;
                _gRockL = rsr.bounds.min.x;
                _gRockR = rsr.bounds.max.x;
                float rockH = Mathf.Max(0.3f, rsr.bounds.max.y - _gTop);
                _gHopH = Mathf.Max(1.4f, (rockH + 0.4f) / 0.85f);
            }
        }
    }

    /// <summary>着地のほこり（音なし）。</summary>
    private void LandedQuiet(Actor a)
    {
        if (a.go == null) return;
        Vector3 p = a.go.transform.localPosition;
        StartCoroutine(Puff(a.go.transform.parent, new Vector2(p.x, FloorY + 0.15f), new Color(0.85f, 0.78f, 0.62f, 0.7f), 0.8f, 0.4f, 0.5f, 20));
    }

    /// <summary>画面を、右へ（または左へ）歩いて通りすぎる。岩は、ぴょんと跳び越える。</summary>
    private IEnumerator IdleWalk(Actor a, bool toRight, float endX)
    {
        if (_gHasRock)
        {
            float margin = 1.1f + 0.6f * (CastWidthOf(a) - 1f);
            float x0 = toRight ? _gRockL - margin : _gRockR + margin;
            float x1 = toRight ? _gRockR + margin : _gRockL - margin;
            yield return RunTo(a, x0, idleWalkSpeed);
            yield return JumpTo(a, x1, FloorY, _gHopH, Mathf.Max(0.65f, Mathf.Abs(x1 - x0) / idleWalkSpeed));
            LandedQuiet(a);
        }
        yield return RunTo(a, endX, idleWalkSpeed);
    }

    /// <summary>ランダムなキーが、左から右へ、そのあと、右から左へ、往復する。</summary>
    private IEnumerator IdleTrip(Actor a)
    {
        for (int leg = 0; leg < 2; leg++)
        {
            bool toRight = leg == 0;
            float startX = toRight ? _gLeft - 1.5f : _gRight + 1.5f;
            float endX = toRight ? _gRight + 2.5f : _gLeft - 2.5f;
            a.go.SetActive(true);
            a.go.transform.localEulerAngles = Vector3.zero;
            a.go.transform.localPosition = new Vector3(startX, FloorY, 0f);
            Tint(a, Color.white);
            Face(a, toRight);
            Stop(a);
            if (a.rig != null)
                a.rig.SetMotion(LimbRig.KeyMotion.None);
            yield return IdleWalk(a, toRight, endX);
            a.go.SetActive(false);
            if (leg == 0)
                yield return Wait(Random.Range(1.0f, 2.5f));
        }
    }

    /// <summary>一定の間をおいて、ランダムなキーが、往復する。</summary>
    private IEnumerator IdleKeys(float firstDelay)
    {
        ComputeTitleGeometry();
        if (_castRoot == null)
        {
            _castRoot = new GameObject("TitleCast");
            _castRoot.transform.position = new Vector3(0f, _gTop - FloorY, 0f);
        }
        if (_actors.Count == 0)
        {
            for (int i = 0; i < CastLetters.Length; i++)
            {
                Actor a = SpawnActor(_castRoot.transform, CastLetters[i], 0f, CastWidths[i], 10 + i * 5);
                a.go.SetActive(false);
            }
        }

        yield return Wait(firstDelay);
        while (true)
        {
            Actor pick = _actors[Random.Range(0, _actors.Count)];
            if (pick.go != null)
                yield return IdleTrip(pick);
            yield return Wait(Mathf.Max(0.5f, idleInterval));
        }
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

        // 走るキーたちを、後ろから、プレイヤーが追いかける
        if (playerPrefab != null)
        {
            float lastKeyX = startX - WalkOffsets[Mathf.Min(_actors.Count - 1, WalkOffsets.Length - 1)];
            StartCoroutine(Track(TitlePlayerChase(lastKeyX - playerGap, exitX, hasRock, rockL, rockR, hopH)));
        }
        yield return FadeTo(_fade, 0f, 0.5f);

        // 全員が、画面の右へ去るまで、歩く
        yield return WaitBusy();
        yield return Wait(0.5f);

        // 走り終わったので、ロゴ → メニューの順に出す（そのあと、ときどき、キーが往復する）
        DestroyOverlay();
        if (idleKeysOnTitle)
            StartCoroutine(IdleKeys(4f));
        yield return RevealUi();
    }
}
