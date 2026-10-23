using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「はじめから」のあとの、会話の中身と見た目。
/// タイトルシーンの OpeningMovie の「Intro Talk」の欄で、セリフも、枠の絵も、変えられる。
/// </summary>
[System.Serializable]
public class NewGameIntroSettings
{
    public enum Speaker
    {
        Player,      // 主人公
        Key,         // Kキー
        Narration,   // 名前の札なし
    }

    [System.Serializable]
    public class Line
    {
        [Tooltip("だれのセリフか。Player＝主人公　Key＝Kキー　Narration＝名前の札なしの文。")]
        public Speaker speaker = Speaker.Player;
        [TextArea(1, 4)]
        [Tooltip("セリフ。改行もできる。（全角の空白は、フォントに無いので、半角の空白に置きかわる）")]
        public string text = "";
        [Tooltip("このセリフのとき、Kキーの頭の上に出すエモート（Assets/Prefab/Emotes のプレハブ）。空なら、このセリフでは、エモートを出さない。")]
        public GameObject emote;
        [Tooltip("エモートを出している秒数。0なら、このセリフが終わるまで出し続ける。")]
        public float emoteSeconds = 0f;
        [Tooltip("このセリフのエモートだけ、位置をずらす（共通の Emote Offset に、足される）。右が＋、上が＋。")]
        public Vector2 emoteOffset = Vector2.zero;
        [Tooltip("このセリフのエモートだけ、大きさを変える（共通の Emote Scale に、掛けられる）。1＝そのまま。")]
        public float emoteScale = 1f;
        [Tooltip("このセリフだけ、文字の大きさを変える。0なら、共通の Text Size を使う。")]
        public float textSize = 0f;

        public Line() { }
        public Line(Speaker speaker, string text) { this.speaker = speaker; this.text = text; }
    }

    [Header("Kキーの位置・主人公の歩き（「Kキーの位置の目印」を置いていないときに使う）")]
    [Tooltip("主人公が出てきた場所から、右へ何ユニットの所にKキーを置くか。家の前に置きたいので、小さめ。大きくするほど、右へ遠くなる。")]
    public float keyOffsetX = 3.0f;
    [Tooltip("話すときの、2人の間の距離。")]
    public float talkGap = 1.6f;
    [Tooltip("主人公が、Kキーへ歩く速さ。")]
    public float walkSpeed = 3.0f;

    [Header("セリフ（上から順に流れる）")]
    public Line[] lines = new Line[]
    {
        new Line(Speaker.Player, "……キー？ こんなところで、何してるの？"),
        new Line(Speaker.Key,    "ひっ……！ に、人間……！ あ、あなたは、あの家の……"),
        new Line(Speaker.Player, "うちのキーボードの、キーだよね。どうして外に？"),
        new Line(Speaker.Key,    "みんなで、逃げ出したんです。……でも、ぼくだけ、逃げ遅れてしまって"),
        new Line(Speaker.Player, "逃げた……？ どうして、そんな……"),
        new Line(Speaker.Key,    "毎日、毎日、打たれて、叩かれて……。もう、限界だったんです"),
        new Line(Speaker.Key,    "モニターで、調べたんです。「逃亡 仕方 バレる？」って……"),
        new Line(Speaker.Player, "……あの検索画面は、きみたちが。気づいてあげられなくて、ごめん"),
        new Line(Speaker.Key,    "みんなは、先に行ってしまいました。お願いです、ぼくも、連れていってください"),
        new Line(Speaker.Player, "うん。いっしょに、みんなを探そう"),
    };

    [Header("進め方")]
    [Tooltip("ONなら、Escで、会話をぜんぶ飛ばせる。OFFなら、飛ばせない（Spaceは、セリフを送るだけ）。")]
    public bool skippable = false;

    [Header("会話のあとのヒント（キーコンフィグの説明）")]
    public NewGameTutorialSettings tutorial = new NewGameTutorialSettings();

    [Header("映画のような黒い帯（画面の上と下）")]
    public bool letterbox = true;
    [Tooltip("帯の高さ（画面の高さに対する割合）。0.125 = 12.5%")]
    [Range(0f, 0.3f)]
    public float letterboxRatio = 0.125f;

    [Header("カメラ（会話が始まると、Kキーに視点が移る）")]
    [Tooltip("ONなら、会話が始まるときに、カメラがKキーに移る。会話が終わると、主人公にもどる。")]
    public bool focusOnKey = true;
    [Tooltip("Kキーに寄るときの、画面の広さの割合。0.8なら、2割寄る（拡大）。1なら、寄らない。")]
    [Range(0.4f, 1f)]
    public float focusZoom = 0.8f;
    [Tooltip("寄る秒数。")]
    public float focusZoomSeconds = 1.2f;
    [Tooltip("カメラが見る場所の、Kキーの体の中心からのずれ（右が＋、上が＋）。会話の枠が下にあるので、少し下を見ると、Kキーが上に映る。")]
    public Vector2 focusOffset = new Vector2(0f, -0.3f);

    [Header("エモート")]
    [Tooltip("Kキーの足元から、エモートまでの距離（ワールド単位）。頭の上に来るように。")]
    public Vector2 emoteOffset = new Vector2(0f, 1.0f);
    [Tooltip("エモート全体の拡大率。")]
    public float emoteScale = 1f;

    [Header("名前の札")]
    [Tooltip("主人公の名前。空なら、主人公のセリフには、名前の札を出さない。")]
    public string playerLabel = "";
    [Tooltip("Kキーの名前（こちらに気づいたあと）。")]
    public string keyLabel = "K";
    [Tooltip("Kキーが、まだ人影のあいだの名前。")]
    public string unknownLabel = "？？？";

    [Header("会話の枠の絵（空なら、コードで描いた仮の枠）")]
    [Tooltip("会話の枠の絵。Sprite Editor で、まわりの Border を設定しておくと、枠の大きさに合わせて、のびる。")]
    public Sprite boxSprite;
    [Tooltip("名前の札の絵。Border を設定しておくと、のびる。")]
    public Sprite nameSprite;
    [Tooltip("セリフが全部出たあとの、「次へ」の印の絵。空なら、白いひし形。")]
    public Sprite arrowSprite;

    [Header("見た目")]
    [Tooltip("枠の色（絵を使わないとき）。")]
    public Color boxColor = new Color(0.05f, 0.05f, 0.09f, 0.88f);
    [Tooltip("セリフの文字の色。")]
    public Color textColor = Color.white;
    [Tooltip("セリフの文字の大きさ。")]
    public float textSize = 46f;
    [Tooltip("枠の大きさ（横, 縦）。画面は 1920×1080 として。")]
    public Vector2 boxSize = new Vector2(1560f, 260f);
    [Tooltip("枠の、画面の下からの高さ。")]
    public float boxBottom = 60f;
    [Tooltip("枠の中の、文字の余白（左右, 上下）。")]
    public Vector2 textPadding = new Vector2(60f, 40f);
    [Tooltip("名前の札の大きさ（横, 縦）。")]
    public Vector2 nameSize = new Vector2(300f, 70f);
}

/// <summary>
/// KEY ESCAPE - 「はじめから」を押したあとの、後半のお話（拠点 SelectScene で流れる）。
///
/// 前半（タイトル画面での、キーの無い机→モニターのムービー）のあと、拠点が始まると、
/// 家から出た主人公の右に、人影が立っている。逃げ遅れた「K」キーだ。
/// 主人公が近づいて、Kキーと話して、事情を聞く。話の最後に、Kキーが仲間になる（キーを入手）。
///
/// ・このスクリプトは、シーンに置かなくていい。「はじめから」から来たときだけ、自分で現れる。
/// ・話の間、主人公の操作は止まる（PlayerMovement.SetInputLocked）。ゲーム自体は止めない。
/// ・Space／Enter／クリック：セリフを送る。飛ばせるのは、設定（Skippable）をONにしたときだけ（Esc）。
/// ・Kキーの絵は、タイトル画面のムービーと同じキーキャラクター（OpeningMovie の Key Character Prefab）。
/// </summary>
public class NewGameIntro : MonoBehaviour
{
    // ================= タイトルからの引き継ぎ =================

    private static bool s_pending;
    private static string s_hubScene = "SelectScene";
    private static GameObject s_keyPrefab;
    private static KeyDefinition s_key;
    private static TMP_FontAsset s_font;
    private static NewGameIntroSettings s_set;
    private static bool s_keepBgm;

    /// <summary>お話が流れている間は true。</summary>
    public static bool IsPlaying { get; private set; }

    /// <summary>タイトルから、拠点へ移る前に呼ぶ。次に読み込まれる拠点で、お話が始まる。</summary>
    public static void Prepare(string hubScene, GameObject keyPrefab, KeyDefinition key, TMP_FontAsset font, NewGameIntroSettings settings, bool keepBgm)
    {
        s_keepBgm = keepBgm;
        s_hubScene = string.IsNullOrEmpty(hubScene) ? "SelectScene" : hubScene;
        s_keyPrefab = keyPrefab;
        s_key = key;
        s_font = font;
        s_set = settings;
        s_pending = true;
    }

    private static NewGameIntroSettings S
    {
        get
        {
            if (s_set == null)
                s_set = new NewGameIntroSettings();
            return s_set;
        }
    }

    /// <summary>フォントに無い全角の空白を、半角の空白に置きかえる（文字化け防止）。</summary>
    private static string Clean(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace('\u3000', ' ');
    }

    public static void Cancel()
    {
        s_pending = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_pending = false;
        IsPlaying = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!s_pending || mode != LoadSceneMode.Single)
            return;
        if (scene.name != s_hubScene)
            return;
        s_pending = false;
        new GameObject("NewGameIntro").AddComponent<NewGameIntro>();
    }

    // ================= 調整できる数字 =================

    private const float CharInterval = 0.04f;   // 1文字ずつ出る間隔

    // ================= 状態 =================

    private Camera _cam;
    private GameObject _player;
    private PlayerMovement _pm;
    private bool _skip;

    private GameObject _canvasGo;
    private Image _black;
    private GameObject _box;
    private GameObject _hint;
    private GameObject _nameBox;
    private TMP_Text _nameText;
    private TMP_Text _body;
    private RectTransform _arrow;
    private RectTransform _barTop;
    private RectTransform _barBottom;
    private float _barPx;

    private CameraFollow _camFollow;
    private Transform _camOldTarget;
    private GameObject _focusPoint;
    private bool _focused;

    private GameObject _emoteAnchor;
    private GameObject _emote;
    private bool _emoteTimed;
    private float _emoteTimer;

    private GameObject _keyGo;
    private LimbRig _rig;
    private SpriteRenderer[] _srs;
    private Color[] _srColors;
    private TMP_Text[] _tms;
    private Color[] _tmColors;
    private bool _revealed;

    // 話の間、拠点のBGMに切り替わらないよう、止めておいた SceneBGM
    private readonly System.Collections.Generic.List<SceneBGM> _heldBgm = new System.Collections.Generic.List<SceneBGM>();

    private void HoldSceneBgm()
    {
        SceneBGM[] all = FindObjectsOfType<SceneBGM>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled)
            {
                all[i].enabled = false;   // Startが呼ばれないので、BGMは切り替わらない
                _heldBgm.Add(all[i]);
                Debug.Log("[NewGameIntro] 拠点のBGM（" + (all[i].clip != null ? all[i].clip.name : "Clipが空") + "）を、話が終わるまで止めておきます");
            }
        }
    }

    /// <summary>止めておいた SceneBGM を戻す（拠点のBGMに、クロスフェードで切り替わる）。</summary>
    private void ReleaseSceneBgm()
    {
        for (int i = 0; i < _heldBgm.Count; i++)
        {
            if (_heldBgm[i] != null)
            {
                _heldBgm[i].enabled = true;
                _heldBgm[i].Play();   // Startを待たずに、いま切り替える（同じ曲がすでに鳴っていれば、何も起きない）
                Debug.Log("[NewGameIntro] 拠点のBGMに切り替えました：" + (_heldBgm[i].clip != null ? _heldBgm[i].clip.name : "Clipが空（BGMに触れません）"));
            }
        }
        _heldBgm.Clear();
    }

    private void Awake()
    {
        IsPlaying = true;
        if (s_keepBgm)
            HoldSceneBgm();
        _cam = Camera.main;
        FindPlayer();
        LockPlayer(true);
        BuildUi();
    }

    private void Start()
    {
        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        IsPlaying = false;
        ReleaseSceneBgm();
        UnfocusKey();
        if (_emoteAnchor != null)
            Destroy(_emoteAnchor);
        if (_canvasGo != null)
            Destroy(_canvasGo);
    }

    private Vector2 _emoteLineOffset = Vector2.zero;   // 今のセリフ用の、足すずれ
    private float _emoteLineScale = 1f;                // 今のセリフ用の、掛ける拡大率

    private void Update()
    {
        if (S.skippable && Input.GetKeyDown(KeyCode.Escape))
            _skip = true;

        // エモートは、Kキーの頭の上についていく
        if (_emoteAnchor != null)
        {
            if (_keyGo != null)
                _emoteAnchor.transform.position = _keyGo.transform.position + (Vector3)(S.emoteOffset + _emoteLineOffset);
            _emoteAnchor.transform.localScale = Vector3.one * (S.emoteScale * _emoteLineScale);
            if (_emote != null && _emoteTimed)
            {
                _emoteTimer -= Time.deltaTime;
                if (_emoteTimer <= 0f)
                    ClearEmote();
            }
        }
    }

    /// <summary>カメラを、Kキーに移して、少し寄る。</summary>
    private void FocusOnKey()
    {
        if (_focused || !S.focusOnKey || _keyGo == null || _cam == null)
            return;
        _camFollow = _cam.GetComponentInParent<CameraFollow>();
        if (_camFollow == null)
            return;

        // Kキーの体の中心あたり
        Vector3 kp = _keyGo.transform.position;
        float h = 1f;
        Renderer[] rs = _keyGo.GetComponentsInChildren<Renderer>(true);
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
                b.Encapsulate(rs[i].bounds);
            h = Mathf.Max(0.5f, b.max.y - kp.y);
        }
        _focusPoint = new GameObject("NewGameIntro_CameraFocus");
        _focusPoint.transform.position = kp + new Vector3(S.focusOffset.x, h * 0.5f + S.focusOffset.y, 0f);

        _camOldTarget = _camFollow.Target;
        _camFollow.Target = _focusPoint.transform;
        if (S.focusZoom < 0.999f)
            _camFollow.ZoomTo(S.focusZoom, Mathf.Max(0.05f, S.focusZoomSeconds));
        _focused = true;
    }

    /// <summary>カメラを、主人公にもどす。</summary>
    private void UnfocusKey()
    {
        if (!_focused)
            return;
        _focused = false;
        if (_camFollow != null)
        {
            _camFollow.Target = _camOldTarget != null ? _camOldTarget : (_player != null ? _player.transform : null);
            if (S.focusZoom < 0.999f)
                _camFollow.ZoomTo(1f, Mathf.Max(0.05f, S.focusZoomSeconds * 0.7f));
        }
        if (_focusPoint != null)
            Destroy(_focusPoint);
        _focusPoint = null;
    }

    private void ShowEmote(NewGameIntroSettings.Line line)
    {
        ClearEmote();
        if (line == null || line.emote == null || _keyGo == null)
            return;   // 空なら、このセリフでは、エモートを出さない

        if (_emoteAnchor == null)
            _emoteAnchor = new GameObject("NewGameIntro_EmoteAnchor");
        _emoteLineOffset = line.emoteOffset;
        _emoteLineScale = Mathf.Approximately(line.emoteScale, 0f) ? 1f : line.emoteScale;
        _emoteAnchor.transform.position = _keyGo.transform.position + (Vector3)(S.emoteOffset + _emoteLineOffset);
        _emoteAnchor.transform.localScale = Vector3.one * (S.emoteScale * _emoteLineScale);

        _emote = Instantiate(line.emote, _emoteAnchor.transform);
        _emote.transform.localPosition = Vector3.zero;   // プレハブに残っている位置は使わない
        _emote.transform.localRotation = Quaternion.identity;

        // Kキーや背景より、手前に出す
        SpriteRenderer[] rs = _emote.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < rs.Length; i++)
            rs[i].sortingOrder += 100;

        _emoteTimed = line.emoteSeconds > 0f;
        _emoteTimer = line.emoteSeconds;
    }

    private void ClearEmote()
    {
        if (_emote != null)
            Destroy(_emote);
        _emote = null;
        _emoteTimed = false;
    }

    // ================= 流れ =================

    private IEnumerator Run()
    {
        float waited = 0f;
        while (_player == null && waited < 2f)
        {
            FindPlayer();
            LockPlayer(true);
            waited += Time.deltaTime;
            yield return null;
        }

        if (_player == null || s_keyPrefab == null)
        {
            Debug.LogWarning("[NewGameIntro] プレイヤー（Playerタグ）か、キーキャラクターのプレハブが見つからないので、お話を飛ばして、Kキーを渡します。 プレイヤー=" + (_player != null) + "  Kキーのプレハブ=" + (s_keyPrefab != null));
            _black.color = new Color(0f, 0f, 0f, 0f);
            yield return Finish();
            yield break;
        }

        SpawnKey();

        // 家の前から、はじまる（上と下に、映画のような黒い帯が出てくる）
        StartCoroutine(Bars(1f, 1.0f));
        yield return Fade(0f, 1.0f);
        yield return Wait(0.9f);

        // 家の前の人影（怪我をしたKキー）へ、近づく
        yield return WalkPlayerTo(_keyGo.transform.position.x);
        yield return Wait(0.3f);

        // 人影が、こちらに気づく（カメラが、Kキーに移る）
        if (!_skip)
        {
            FocusOnKey();
            yield return Reveal();
            yield return Wait(0.3f);
        }

        // 会話
        if (!_skip)
        {
            _box.SetActive(true);
            _hint.SetActive(true);
            NewGameIntroSettings.Line[] lines = S.lines;
            for (int i = 0; lines != null && i < lines.Length && !_skip; i++)
            {
                if (lines[i] == null || string.IsNullOrEmpty(lines[i].text))
                    continue;
                yield return Say(lines[i]);
            }
            _box.SetActive(false);
            _hint.SetActive(false);
        }

        yield return Finish();
    }

    /// <summary>Kキーが、仲間になる：見送って、操作を戻して、キーを渡す。</summary>
    private IEnumerator Finish()
    {
        if (_box != null) _box.SetActive(false);
        if (_hint != null) _hint.SetActive(false);
        ClearEmote();

        if (_keyGo != null)
        {
            if (_skip)
            {
                // 飛ばしたときは、いったん暗くして、すぐ片づける
                yield return Fade(1f, 0.25f);
                Destroy(_keyGo);
                _keyGo = null;
                StopPlayerWalk();
            }
            else
            {
                if (_rig != null)
                    _rig.SetMotion(LimbRig.KeyMotion.Cheer);
                yield return Wait(0.7f);
                yield return Leave();
            }
        }

        // カメラを主人公にもどして、黒い帯をしまう
        UnfocusKey();
        yield return Bars(0f, 0.6f);

        // 話が終わったので、先に拠点のBGMに切り替える。
        // （キーを入手した演出は、そのとき鳴っている曲を覚えて、終わるとその曲に戻す。
        //   先にキーを渡すと、ムービーのBGMに戻ってしまうため）
        ReleaseSceneBgm();
        yield return null;
        yield return null;   // SceneBGM の Start が呼ばれて、拠点のBGMが鳴りはじめるまで待つ

        // 操作を戻してから、キーを渡す（キーを入手した演出が、もう一度止めるため）
        LockPlayer(false);
        GiveKey();

        // キーを入手した演出が全部終わったら、ヒント（Tabでキーコンフィグ）が始まる
        NewGameTutorial.Begin(S.tutorial, _givenKey);

        if (_black != null && _black.color.a > 0.01f)
            yield return Fade(0f, 0.35f);

        IsPlaying = false;
        Destroy(gameObject);
    }

    private KeyDefinition _givenKey;   // 仲間になったキー（ヒントを始めるときに渡す）

    private void GiveKey()
    {
        KeyDefinition k = s_key;
        if (k == null)
        {
            KeyDefinition[] all = Resources.FindObjectsOfTypeAll<KeyDefinition>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].keyCode == KeyCode.K && !all[i].isSpecialKey)
                {
                    k = all[i];
                    break;
                }
            }
        }
        if (k == null)
        {
            Debug.LogWarning("[NewGameIntro] Kキー（Key_K）が見つかりません。OpeningMovie の Intro Key に、Key_K を入れてください（Setupメニューを実行すると、自動で入ります）。");
            return;
        }
        if (KeyInventory.Instance == null)
        {
            Debug.LogWarning("[NewGameIntro] KeyInventory（Bootstrap）がシーンにありません。Kキーを渡せませんでした。");
            return;
        }
        _givenKey = k;
        if (!KeyInventory.Instance.Owns(k))
            KeyInventory.Instance.AddKey(k);
    }

    // ================= 主人公 =================

    /// <summary>
    /// 本物の主人公。「Player」タグのついたものは、本物のほかに、キーコンフィグ画面のプレビュー用キャラ（PreviewChar：
    /// 遠く離れた (1000, 1000) にある）もいる。タグだけで探すと、プレビュー用のほうを拾ってしまうので、本物だけを選ぶ。
    /// 見つからなければ null。
    /// </summary>
    public static GameObject FindMainPlayerObject()
    {
        GameObject[] all = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < all.Length; i++)
        {
            GameObject g = all[i];
            if (g == null || !g.activeInHierarchy)
                continue;
            if (g.GetComponentInChildren<KeyConfigPreviewPlayer>(true) != null || g.GetComponentInParent<KeyConfigPreviewPlayer>() != null)
                continue;   // プレビュー用
            if (g.GetComponent<PlayerMovement>() == null)
                continue;
            Vector3 gp = g.transform.position;
            if (Mathf.Abs(gp.x) > 500f || Mathf.Abs(gp.y) > 500f)
                continue;   // 遠くにあるのは、プレビュー用の置き場
            return g;
        }
        return null;
    }

    private void FindPlayer()
    {
        if (_player != null)
            return;
        GameObject best = FindMainPlayerObject();
        if (best == null)
            return;   // 見つからなければ、次の機会にもう一度探す
        _player = best;
        _pm = best.GetComponent<PlayerMovement>();
    }

    private void LockPlayer(bool locked)
    {
        if (_pm != null)
            _pm.SetInputLocked(locked);
    }

    private Animator PlayerAnimator()
    {
        if (_pm != null && _pm.animator != null)
            return _pm.animator;
        return _player != null ? _player.GetComponentInChildren<Animator>() : null;
    }

    private void StopPlayerWalk()
    {
        Animator an = PlayerAnimator();
        if (an != null)
            an.SetFloat("Speed", 0f);
    }

    /// <summary>Kキーのほうへ、歩いて近づく（キーを持っていないので、操作ではなく、自動で）。もう近ければ、歩かない。</summary>
    private IEnumerator WalkPlayerTo(float keyX)
    {
        float px = _player.transform.position.x;
        if (Mathf.Abs(keyX - px) <= S.talkGap + 0.1f)
            yield break;

        float dir = keyX > px ? 1f : -1f;
        float targetX = keyX - dir * S.talkGap;

        Rigidbody2D rb = _player.GetComponent<Rigidbody2D>();
        Animator an = PlayerAnimator();

        // Kキーのほうを向く
        CharacterController2D cc = _player.GetComponent<CharacterController2D>();
        if (cc != null)
            cc.Move(0.01f * dir, false, false);

        if (an != null)
            an.SetFloat("Speed", 40f);

        while (!_skip && (dir > 0f ? _player.transform.position.x < targetX : _player.transform.position.x > targetX))
        {
            yield return new WaitForFixedUpdate();
            float step = dir * S.walkSpeed * Time.fixedDeltaTime;
            if (rb != null)
                rb.position = rb.position + new Vector2(step, 0f);
            else
                _player.transform.position += new Vector3(step, 0f, 0f);
        }

        StopPlayerWalk();
    }

    // ================= Kキー =================

    /// <summary>x の真下の地面の高さ。見つからなければ fallback。</summary>
    private float GroundY(float x, float fromY, float maxY, float fallback)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, fromY), Vector2.down, 20f);
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger || c.transform.IsChildOf(_player.transform))
                continue;
            if (hits[i].point.y > bestY && hits[i].point.y <= maxY)
                bestY = hits[i].point.y;
        }
        return float.IsNegativeInfinity(bestY) ? fallback : bestY;
    }

    private void SpawnKey()
    {
        Vector3 pp = _player.transform.position;
        float kx;
        float footY;

        NewGameIntroKeyPoint point = FindObjectOfType<NewGameIntroKeyPoint>();
        if (point != null)
        {
            // 自分で置いた目印（家の前）に立たせる
            Vector3 mp = point.transform.position;
            kx = mp.x;
            footY = point.snapToGround ? GroundY(kx, mp.y + 2f, mp.y + 2.5f, mp.y) : mp.y;
        }
        else
        {
            // 目印が無いときは、主人公（家の扉）の、すぐ右の、家の前
            kx = pp.x + Mathf.Max(0.5f, S.keyOffsetX);

            footY = pp.y;
            Collider2D pc = _player.GetComponent<Collider2D>();
            if (pc != null)
                footY = pc.bounds.min.y;
            footY = GroundY(kx, pp.y + 4f, pp.y + 1f, footY);
        }

        _keyGo = Instantiate(s_keyPrefab);
        _keyGo.name = "NewGameIntro_Key";
        _keyGo.transform.position = new Vector3(kx, footY, pp.z);

        // 主人公のほうを向く
        Vector3 s = _keyGo.transform.localScale;
        s.x = pp.x > kx ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
        _keyGo.transform.localScale = s;

        string letter = (s_key != null && !string.IsNullOrEmpty(s_key.displayName)) ? s_key.displayName : "K";
        KeyLabel label = _keyGo.GetComponentInChildren<KeyLabel>(true);
        if (label != null)
        {
            label.textOverride = letter;
            label.Refresh();
        }

        // 描く順番：主人公と同じ層・同じ順番を土台にして、背景の後ろに隠れないようにする（Kキーの部品の順番は、そのまま足す）
        int baseLayer = 0;
        int baseOrder = 0;
        SpriteRenderer psr = _player.GetComponentInChildren<SpriteRenderer>();
        if (psr != null)
        {
            baseLayer = psr.sortingLayerID;
            baseOrder = psr.sortingOrder;
        }
        Renderer[] krs = _keyGo.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < krs.Length; i++)
        {
            krs[i].sortingLayerID = baseLayer;
            krs[i].sortingOrder = baseOrder + krs[i].sortingOrder;
        }

        Debug.Log("[NewGameIntro] Kキーを出しました：位置 (" + kx.ToString("F2") + ", " + footY.ToString("F2") + ")  目印=" + (point != null ? "あり" : "なし（主人公の右 " + S.keyOffsetX.ToString("F1") + "）") + "  描く順番=" + baseOrder);

        _rig = _keyGo.GetComponent<LimbRig>();
        if (_rig != null)
        {
            _rig.spriteFacesRight = true;
            _rig.SetMotion(LimbRig.KeyMotion.Injured);   // 怪我をして、動けない
        }

        _srs = _keyGo.GetComponentsInChildren<SpriteRenderer>(true);
        _srColors = new Color[_srs.Length];
        for (int i = 0; i < _srs.Length; i++)
            _srColors[i] = _srs[i].color;
        _tms = _keyGo.GetComponentsInChildren<TMP_Text>(true);
        _tmColors = new Color[_tms.Length];
        for (int i = 0; i < _tms.Length; i++)
            _tmColors[i] = _tms[i].color;

        SetKeyTint(0f);   // 最初は、人影（まっ黒）
    }

    /// <summary>k = 0：黒い人影　k = 1：ふつうの色。</summary>
    private void SetKeyTint(float k)
    {
        Color dark = new Color(0.05f, 0.05f, 0.08f, 1f);
        for (int i = 0; i < _srs.Length; i++)
        {
            if (_srs[i] == null) continue;
            Color c = Color.Lerp(dark, _srColors[i], k);
            c.a = _srColors[i].a;
            _srs[i].color = c;
        }
        for (int i = 0; i < _tms.Length; i++)
        {
            if (_tms[i] == null) continue;
            Color c = Color.Lerp(dark, _tmColors[i], k);
            c.a = _tmColors[i].a;
            _tms[i].color = c;
        }
    }

    private void SetKeyAlpha(float a)
    {
        for (int i = 0; i < _srs.Length; i++)
        {
            if (_srs[i] == null) continue;
            Color c = _srColors[i];
            c.a *= a;
            _srs[i].color = c;
        }
        for (int i = 0; i < _tms.Length; i++)
        {
            if (_tms[i] == null) continue;
            Color c = _tmColors[i];
            c.a *= a;
            _tms[i].color = c;
        }
    }

    /// <summary>人影が、こちらに気づいて、ふつうの色にもどる（怪我で動けないので、小さく震えるだけ）。</summary>
    private IEnumerator Reveal()
    {
        _revealed = true;

        Vector3 p0 = _keyGo.transform.position;
        float e = 0f;
        const float time = 0.6f;
        while (e < time && !_skip)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / time);
            _keyGo.transform.position = p0 + new Vector3(Mathf.Sin(e * 60f) * 0.03f * (1f - k), 0f, 0f);
            SetKeyTint(k);
            yield return null;
        }
        _keyGo.transform.position = p0;
        SetKeyTint(1f);
    }

    /// <summary>仲間になって、ふわっと上へ消える。</summary>
    private IEnumerator Leave()
    {
        Vector3 p0 = _keyGo.transform.position;
        float e = 0f;
        const float time = 0.8f;
        while (e < time)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / time);
            _keyGo.transform.position = p0 + Vector3.up * (1.2f * k);
            SetKeyAlpha(1f - k);
            yield return null;
        }
        Destroy(_keyGo);
        _keyGo = null;
    }

    // ================= 会話 =================

    private static bool Advance()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);
    }

    private IEnumerator Say(NewGameIntroSettings.Line line)
    {
        bool isKey = line.speaker == NewGameIntroSettings.Speaker.Key;
        string who = "";
        if (isKey)
            who = _revealed ? S.keyLabel : S.unknownLabel;
        else if (line.speaker == NewGameIntroSettings.Speaker.Player)
            who = S.playerLabel;
        who = Clean(who);
        _nameBox.SetActive(!string.IsNullOrEmpty(who));
        _nameText.text = who;

        // Kキーは、ずっと怪我で動けないまま。このセリフ用のエモートがあれば、出す（無ければ、出さない）
        ShowEmote(line);

        _body.fontSize = line.textSize > 0f ? line.textSize : S.textSize;
        _body.text = Clean(line.text);
        _body.maxVisibleCharacters = 0;
        _body.ForceMeshUpdate();
        int total = _body.textInfo.characterCount;
        _arrow.gameObject.SetActive(false);

        // 1文字ずつ出す（途中で押すと、ぜんぶ出る）
        int shown = 0;
        float acc = 0f;
        while (shown < total && !_skip)
        {
            if (Advance())
                break;
            acc += Time.deltaTime;
            while (acc >= CharInterval && shown < total)
            {
                acc -= CharInterval;
                shown++;
            }
            _body.maxVisibleCharacters = shown;
            yield return null;
        }
        _body.maxVisibleCharacters = 99999;

        // 次へ進む印（▼のかわりの、ひし形）を出して、押されるのを待つ
        _arrow.gameObject.SetActive(true);
        yield return null;   // 同じフレームの入力を、次に持ち越さない
        float t = 0f;
        while (!_skip && !Advance())
        {
            t += Time.deltaTime;
            _arrow.anchoredPosition = new Vector2(-56f, 44f + Mathf.Sin(t * 6f) * 6f);
            yield return null;
        }
        _arrow.gameObject.SetActive(false);
    }

    // ================= 小さな道具 =================

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds && !_skip)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void SetBars(float k)
    {
        if (_barTop != null) _barTop.sizeDelta = new Vector2(0f, _barPx * k);
        if (_barBottom != null) _barBottom.sizeDelta = new Vector2(0f, _barPx * k);
    }

    /// <summary>映画のような黒い帯を、出す（1）／しまう（0）。</summary>
    private IEnumerator Bars(float to, float time)
    {
        if (_barTop == null)
            yield break;
        float from = _barTop.sizeDelta.y / Mathf.Max(0.0001f, _barPx);
        float t = 0f;
        float len = Mathf.Max(0.0001f, time);
        while (t < len)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / len);
            SetBars(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
            yield return null;
        }
        SetBars(to);
    }

    private IEnumerator Fade(float to, float time)
    {
        if (_black == null)
            yield break;
        float from = _black.color.a;
        float t = 0f;
        float len = Mathf.Max(0.0001f, time);
        while (t < len)
        {
            t += Time.deltaTime;
            if (_black == null) yield break;
            _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / len)));
            yield return null;
        }
        if (_black != null)
            _black.color = new Color(0f, 0f, 0f, to);
    }

    // ================= 画面（コードで作る） =================

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI NewText(string objName, Transform parent, float size, Color color, TextAlignmentOptions align)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        if (s_font != null)
            t.font = s_font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private RectTransform NewBar(string objName, bool top)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(_canvasGo.transform, false);
        RectTransform r = (RectTransform)g.transform;
        r.anchorMin = new Vector2(0f, top ? 1f : 0f);
        r.anchorMax = new Vector2(1f, top ? 1f : 0f);
        r.pivot = new Vector2(0.5f, top ? 1f : 0f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = Vector2.zero;
        Image im = g.AddComponent<Image>();
        im.color = Color.black;
        im.raycastTarget = false;
        return r;
    }

    private void BuildUi()
    {
        if (s_font == null)
            Debug.LogWarning("[NewGameIntro] 日本語のフォントが渡されていないので、文字が四角になるかもしれません（OpeningMovie の Font を入れてください）。");

        _canvasGo = new GameObject("NewGameIntroCanvas");
        Canvas canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;
        CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        // 映画のような黒い帯（上と下）。会話の枠より後ろ
        _barPx = S.letterbox ? S.letterboxRatio * 1080f : 0f;
        if (_barPx > 0.5f)
        {
            _barTop = NewBar("BarTop", true);
            _barBottom = NewBar("BarBottom", false);
            SetBars(0f);
        }

        // 会話の枠（下）
        _box = new GameObject("Box", typeof(RectTransform));
        _box.transform.SetParent(_canvasGo.transform, false);
        RectTransform br = (RectTransform)_box.transform;
        br.anchorMin = new Vector2(0.5f, 0f);
        br.anchorMax = new Vector2(0.5f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, S.boxBottom + _barPx);   // 黒い帯の上に置く
        br.sizeDelta = S.boxSize;
        Image back = _box.AddComponent<Image>();
        back.raycastTarget = false;
        if (S.boxSprite != null)
        {
            // 絵の枠：絵そのままの色で、枠の大きさにのばす
            back.sprite = S.boxSprite;
            back.type = Image.Type.Sliced;
            back.color = Color.white;
        }
        else
        {
            back.color = S.boxColor;
            Outline ol = _box.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 1f, 1f, 0.9f);
            ol.effectDistance = new Vector2(4f, -4f);
        }

        // 名前の札（枠の左上）
        _nameBox = new GameObject("NameBox", typeof(RectTransform));
        _nameBox.transform.SetParent(_box.transform, false);
        RectTransform nr = (RectTransform)_nameBox.transform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(0f, 1f);
        nr.pivot = new Vector2(0f, 0f);
        nr.anchoredPosition = new Vector2(36f, 10f);
        nr.sizeDelta = S.nameSize;
        Image nb = _nameBox.AddComponent<Image>();
        nb.raycastTarget = false;
        if (S.nameSprite != null)
        {
            nb.sprite = S.nameSprite;
            nb.type = Image.Type.Sliced;
            nb.color = Color.white;
        }
        else
        {
            nb.color = new Color(0.10f, 0.10f, 0.18f, 0.96f);
            Outline nol = _nameBox.AddComponent<Outline>();
            nol.effectColor = new Color(1f, 1f, 1f, 0.9f);
            nol.effectDistance = new Vector2(3f, -3f);
        }
        _nameText = NewText("Name", _nameBox.transform, 42f, S.textColor, TextAlignmentOptions.Center);
        Stretch(_nameText.rectTransform);

        // 本文
        _body = NewText("Body", _box.transform, S.textSize, S.textColor, TextAlignmentOptions.TopLeft);
        _body.enableWordWrapping = true;
        _body.overflowMode = TextOverflowModes.Overflow;
        RectTransform tr = _body.rectTransform;
        Stretch(tr);
        tr.offsetMin = new Vector2(S.textPadding.x, S.textPadding.y);
        tr.offsetMax = new Vector2(-S.textPadding.x, -S.textPadding.y);

        // 次へ進む印
        GameObject ag = new GameObject("Arrow", typeof(RectTransform));
        ag.transform.SetParent(_box.transform, false);
        _arrow = (RectTransform)ag.transform;
        _arrow.anchorMin = new Vector2(1f, 0f);
        _arrow.anchorMax = new Vector2(1f, 0f);
        _arrow.pivot = new Vector2(0.5f, 0.5f);
        _arrow.anchoredPosition = new Vector2(-56f, 44f);
        Image ai = ag.AddComponent<Image>();
        ai.raycastTarget = false;
        if (S.arrowSprite != null)
        {
            _arrow.sizeDelta = new Vector2(44f, 44f);
            ai.sprite = S.arrowSprite;
            ai.preserveAspect = true;
            ai.color = Color.white;
        }
        else
        {
            // 絵が無いときは、白いひし形
            _arrow.sizeDelta = new Vector2(24f, 24f);
            _arrow.localRotation = Quaternion.Euler(0f, 0f, 45f);
            ai.color = Color.white;
        }
        ag.SetActive(false);

        _box.SetActive(false);

        // 操作の案内（右上）
        TextMeshProUGUI hint = NewText("Hint", _canvasGo.transform, 28f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.TopRight);
        hint.text = S.skippable ? "Space：すすむ  Esc：スキップ" : "Space：すすむ";
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(1f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(1f, 1f);
        hr.anchoredPosition = new Vector2(-40f, -30f - _barPx);
        hr.sizeDelta = new Vector2(800f, 50f);
        _hint = hint.gameObject;
        _hint.SetActive(false);

        // 暗幕（最初は、まっ暗。いちばん手前）
        GameObject bg = new GameObject("Black", typeof(RectTransform));
        bg.transform.SetParent(_canvasGo.transform, false);
        Stretch((RectTransform)bg.transform);
        _black = bg.AddComponent<Image>();
        _black.color = Color.black;
        _black.raycastTarget = true;
    }
}
