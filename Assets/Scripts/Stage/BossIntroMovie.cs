using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - ボス戦の、はじまりのムービー（Stage7 用）。
///
/// ステージに入ると、自動で始まる。
///   1) 上と下に黒い帯が出て、主人公と、仲間のKキーが、右へ歩く。道には、空っぽのキー（文字の無いキー）が、たくさん倒れている。
///   2) 倒れたキーの1つの前で止まり、カメラが寄る。倒れたキーが、ひとこと話す。
///   3) 画面が揺れて、Kキーが「な…なんだあれ!!」と声を上げる。
///   4) 一人称視点のような画面になり、巨大なAキーが、奥から飛びこんでくる。
///   5) 画面が白く光り、そのあいだに、主人公がボスの部屋へ移る。白が消えると、操作できる。
///
/// ・ムービーは、チェックポイントから再開したときは流れない（部屋の入口のチェックポイントを通っているので）。
/// ・ムービーが終わったときに呼ぶ処理は、On Movie End に入れる（ボス戦の開始など）。
/// ・Aキーの絵は、コードで描いている。
/// ・セリフを送る：Space / Enter / クリック。
/// ・作業中にムービーを飛ばしたいときは、Skip Movie をONにする（すぐ部屋の入口へ移る）。
/// ・場所の目印は、メニュー「Stage7を自動で組み立てる」が、置いて、ここに入れる。
/// </summary>
public class BossIntroMovie : MonoBehaviour
{
    /// <summary>ムービーが流れている間は true。</summary>
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPlaying = false;
    }

    // ================= 調整できる数字 =================

    [Header("はじめ方")]
    [Tooltip("ONなら、ステージに入ったときに、自動でムービーが始まる。")]
    public bool playOnStart = true;
    [Tooltip("ONなら、ムービーを流さず、すぐボスの部屋の入口へ移る（作業用）。")]
    public bool skipMovie = false;

    [Header("場所の目印")]
    [Tooltip("ボスの部屋で、主人公が立つ場所（足元のあたり）。")]
    public Transform arenaPlayerPoint;
    [Tooltip("ボスが現れる場所の目印（ボス戦をつくるときに使う）。ムービーでは使わない。")]
    public Transform bossSpawnPoint;
    [Tooltip("倒れているキーたちの親。この下のキーは、ムービーが始まると、倒れた姿になる。")]
    public Transform fallenKeysRoot;
    [Tooltip("ひとこと話す、倒れたキー。空なら、話す場面は飛ばす。")]
    public Transform speakerKey;
    [Tooltip("主人公が、倒れたキーの、どれくらい手前で止まるか。")]
    public float speakerStopDistance = 3.4f;
    [Tooltip("Speaker Key が空のときに、主人公が止まる x の位置。")]
    public float fallbackStopX = 45f;

    [Header("キーの見た目")]
    [Tooltip("手足の生えたキー（Assets/Prefab/Mob/Enemy/KeyNPC_Visual）。Kキーに使う。")]
    public GameObject keyPrefab;
    [Tooltip("キーの絵が右向きならON。（KeyNPC_Visual は左向きなので、OFFのままでよい）")]
    public bool keyPrefabFacesRight = false;
    [Tooltip("仲間のキーの文字。")]
    public string companionLabel = "K";
    [Tooltip("主人公の、どれくらい後ろを、Kキーが歩くか。")]
    public float companionGap = 1.8f;
    [Tooltip("歩く速さ。")]
    public float walkSpeed = 3.0f;
    [Tooltip("倒れているキーの色（暗くして、元気のない感じにする）。")]
    public Color fallenTint = new Color(0.62f, 0.62f, 0.68f, 1f);

    [Header("セリフ")]
    [Tooltip("倒れたキーのセリフ。")]
    public string fallenLine = "これ以上先に進んだらだめだ";
    [Tooltip("Kキーのセリフ。")]
    public string companionLine = "な…なんだあれ!!";
    [Tooltip("セリフの文字の大きさ。")]
    public float textSize = 46f;
    [Tooltip("日本語のフォント。空なら、シーンにある文字から借りる。")]
    public TMP_FontAsset font;

    [Header("Kキーのエモート（任意。空なら出さない）")]
    [Tooltip("「な…なんだあれ!!」のときに、Kキーの頭の上に出すエモート（Assets/Prefab/Emotes のプレハブ）。")]
    public GameObject surpriseEmote;
    public Vector2 emoteOffset = new Vector2(0f, 1.2f);
    public float emoteScale = 1f;
    public float emoteSeconds = 2.0f;

    [Header("映画のような黒い帯")]
    [Range(0f, 0.3f)] public float letterboxRatio = 0.125f;

    [Header("カメラ")]
    [Tooltip("倒れたキーに寄るときの、画面の広さの割合。0.7なら、3割寄る。")]
    [Range(0.4f, 1f)] public float focusZoom = 0.72f;
    public float focusSeconds = 1.3f;

    [Header("巨大なAキーの色")]
    public Color keyFaceColor = new Color(0.96f, 0.96f, 0.97f, 1f);
    public Color keyBaseColor = new Color(0.27f, 0.27f, 0.32f, 1f);
    public Color keyLetterColor = new Color(0.12f, 0.12f, 0.2f, 1f);
    [Tooltip("Aキーのまわりに出る、不気味な光の色。")]
    public Color keyAccentColor = new Color(0.9f, 0.15f, 0.22f, 1f);

    [Header("倒れたキーの吹き出し（主人公が近づくと出る）")]
    [Tooltip("OFFなら、吹き出しは出さない。")]
    public bool groanBubbles = true;
    [Tooltip("吹き出しを付けるキーの割合。0.6なら、およそ6割のキーに付く。")]
    [Range(0f, 1f)] public float bubbleRate = 0.6f;
    [Tooltip("吹き出しを付けるキーどうしの、最低のきょり。ところどころにだけ出すため。")]
    public float bubbleMinGap = 2.2f;
    [Tooltip("うめき声。ここから、キーごとに選ぶ。（全角の空白と、波線は使わない）")]
    public string[] groanLines = new string[] { "うう…", "いたい…", "だれか…", "もう…だめだ…", "にげて…", "さむい…", "たすけて…" };
    [Tooltip("主人公が、この距離まで近づくと、吹き出しが出る。")]
    public float bubbleShowDistance = 4.5f;
    [Tooltip("吹き出しを出している秒数（そのあと、すうっと消える）。")]
    public float bubbleSeconds = 2.8f;
    [Tooltip("キーの頭から、吹き出しまでの高さ。")]
    public float bubbleHeight = 0.55f;
    [Tooltip("吹き出しの文字の大きさ。")]
    public float bubbleTextSize = 3.4f;
    public Color bubbleColor = new Color(1f, 1f, 1f, 0.96f);
    public Color bubbleTextColor = new Color(0.12f, 0.12f, 0.2f, 1f);

    [Header("効果音（任意。空なら鳴らさない）")]
    [Tooltip("全部の効果音にかかる音量。設定画面のSEの音量も、かかる。")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("歩く足音（歩いている間、くりかえし鳴る）。")]
    public AudioClip footstepSfx;
    public float walkStepInterval = 0.36f;
    [Tooltip("吹き出しが出るときの音。")]
    public AudioClip bubbleSfx;
    [Tooltip("セリフの文字が出るときの音（数文字ごとに鳴る）。")]
    public AudioClip textBlipSfx;
    [Tooltip("文字を何文字出すごとに、上の音を鳴らすか。")]
    public int blipEveryChars = 2;
    [Tooltip("Space などで、セリフを送るときの音。")]
    public AudioClip advanceSfx;
    [Tooltip("ゴゴゴ…と、地面が揺れるときの音。")]
    public AudioClip rumbleClip;
    [Tooltip("Kキーが、びっくりして声を上げるときの音。")]
    public AudioClip surpriseSfx;
    [Tooltip("暗くなって、一人称の画面に切り替わるときの音。")]
    public AudioClip bossAppearSfx;
    [Tooltip("Aキーが、ぴょんとはねるときの音。")]
    public AudioClip hopClip;
    [Tooltip("Aキーが、着地するときの音。")]
    public AudioClip landSfx;
    [Tooltip("Aキーが、力をためてしゃがむときの音。")]
    public AudioClip chargeSfx;
    [Tooltip("Aキーが、画面にとびかかるときの音。")]
    public AudioClip leapClip;
    [Tooltip("画面が白く光るときの音。")]
    public AudioClip flashClip;
    [Tooltip("ボスの部屋に着いて、白が消えはじめるときの音。")]
    public AudioClip arrivalSfx;

    [Header("BGM（任意。空なら、その場面ではBGMに触れない）")]
    [Tooltip("ムービーの間のBGM。入れると、ステージのBGMは、ムービーのあとまで止めておく。")]
    public AudioClip movieBgm;
    public float movieBgmFade = 1.0f;
    public bool movieBgmLoop = true;
    [Tooltip("ONなら、Aキーが現れる場面で、BGMをすうっと止める（静けさから、どんと現れる）。")]
    public bool fadeOutBgmAtBossAppear = false;
    [Tooltip("ボスの部屋に着いたときから流すBGM。空なら、ステージのBGMにもどる。")]
    public AudioClip arenaBgm;
    public float arenaBgmFade = 0.8f;

    [Header("ムービーが終わったときに呼ぶ処理（ボス戦の開始など）")]
    public UnityEvent onMovieEnd;

    private const float CharInterval = 0.04f;   // 1文字ずつ出る間隔

    // ================= 状態 =================

    private GameObject _player;
    private PlayerMovement _pm;
    private Rigidbody2D _playerRb;
    private CameraFollow _camFollow;
    private Camera _cam;

    private GameObject _canvasGo;
    private Image _black;
    private Image _flash;
    private GameObject _box;
    private GameObject _hint;
    private GameObject _nameBox;
    private TMP_Text _nameText;
    private TMP_Text _body;
    private RectTransform _arrow;
    private RectTransform _barTop;
    private RectTransform _barBottom;
    private float _barPx;
    private AudioSource _audio;

    // 倒れたキーの吹き出し
    private class Bubble
    {
        public Transform key;
        public string line;
        public bool shown;
        public bool done;
        public float t;
        public GameObject go;
        public SpriteRenderer[] srs;
        public Color[] cols;
        public TextMeshPro tmp;
        public Vector3 basePos;
    }

    private readonly List<Bubble> _bubbles = new List<Bubble>();
    private int _bubbleLayer;
    private int _bubbleOrder;

    // ムービーの間、止めておいた、ステージのBGM
    private readonly List<SceneBGM> _heldBgm = new List<SceneBGM>();
    private bool _endBgmDone;

    // 一人称ふうの画面
    private RectTransform _fpRoot;
    private CanvasGroup _fpGroup;
    private RectTransform _keyRt;
    private RectTransform _shadowRt;
    private Image _shadowImg;
    private RectTransform _glowRt;
    private Image _glowImg;
    private CanvasGroup _linesGroup;
    private RectTransform _linesRt;
    private float _fpBaseY = 20f;

    private Sprite _roundSprite;
    private Sprite _radialSprite;
    private TMP_FontAsset _font;

    private GameObject _kRoot;
    private GameObject _kVisual;
    private LimbRig _kRig;
    private float _kFootY;
    private GameObject _emoteGo;
    private GameObject _focusPoint;
    private bool _running;

    // ================= 開始 =================

    private void Awake()
    {
        // ステージのBGMが勝手に鳴りはじめないよう、先に止めておく
        if (playOnStart && (movieBgm != null || arenaBgm != null))
            HoldSceneBgm();
    }

    private void Update()
    {
        if (_running && _bubbles.Count > 0 && _player != null)
            UpdateBubbles(Time.deltaTime);
    }

    private void Start()
    {
        if (!playOnStart)
            return;

        FindPlayer();
        _camFollow = FindObjectOfType<CameraFollow>();
        _cam = Camera.main;

        // チェックポイントから再開したときは、ムービーを流さない（主人公は、もうチェックポイントにいる）
        if (CheckpointManager.HasCheckpoint)
        {
            Debug.Log("[BossIntroMovie] チェックポイントから再開したので、ムービーは流しません。");
            PrepareFallenKeys(false);
            ApplyEndBgm();
            StartCoroutine(ResumeOnly());
            return;
        }

        LockPlayer(true);
        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        if (_running)
        {
            IsPlaying = false;
            _running = false;
        }
        ClearBubbles();
        if (_emoteGo != null) Destroy(_emoteGo);
        if (_focusPoint != null) Destroy(_focusPoint);
        if (_kRoot != null) Destroy(_kRoot);
        if (_canvasGo != null) Destroy(_canvasGo);
        if (_roundSprite != null)
        {
            if (_roundSprite.texture != null) Destroy(_roundSprite.texture);
            Destroy(_roundSprite);
        }
        if (_radialSprite != null)
        {
            if (_radialSprite.texture != null) Destroy(_radialSprite.texture);
            Destroy(_radialSprite);
        }
    }

    /// <summary>チェックポイントからの再開：ムービーなしで、そのまま始める。</summary>
    private IEnumerator ResumeOnly()
    {
        yield return null;
        LockPlayer(false);
        if (onMovieEnd != null)
            onMovieEnd.Invoke();
    }

    // ================= 流れ =================

    private IEnumerator Run()
    {
        _running = true;
        IsPlaying = true;

        // ステージの読みこみが終わるまで待つ
        float guard = 0f;
        while (LoadingScreen.IsLoading && guard < 10f)
        {
            guard += Time.unscaledDeltaTime;
            yield return null;
        }

        float waited = 0f;
        while (_player == null && waited < 2f)
        {
            FindPlayer();
            LockPlayer(true);
            waited += Time.deltaTime;
            yield return null;
        }

        if (_player == null)
        {
            Debug.LogWarning("[BossIntroMovie] プレイヤー（Playerタグ）が見つからないので、ムービーを飛ばします。");
            Finish();
            yield break;
        }

        if (skipMovie)
        {
            Debug.Log("[BossIntroMovie] Skip Movie がONなので、ムービーを流さず、部屋の入口へ移ります。");
            TeleportToArena();
            yield return null;
            Finish();
            yield break;
        }

        if (_camFollow == null)
            _camFollow = FindObjectOfType<CameraFollow>();
        if (_cam == null)
            _cam = Camera.main;

        _font = font != null ? font : FindSceneFont();
        BuildUi();
        PrepareFallenKeys(groanBubbles);
        SpawnCompanion();

        // ムービーのBGM（無ければ、止めておいたステージのBGMを、ここから流す）
        if (movieBgm != null)
            AudioManager.PlayBGM(movieBgm, movieBgmFade, movieBgmLoop);
        else
            ReleaseSceneBgm();

        // 黒い画面から、帯が出て、はじまる
        StartCoroutine(Bars(1f, 1.0f));
        yield return Fade(_black, 0f, 1.0f);
        yield return Wait(0.6f);

        // 1) 歩く
        float stopX = speakerKey != null ? speakerKey.position.x - speakerStopDistance : fallbackStopX;
        yield return WalkTogether(stopX);
        yield return Wait(0.5f);

        // 2) 倒れたキーに寄って、ひとこと
        if (speakerKey != null)
        {
            Vector3 mid = (_player.transform.position + speakerKey.position) * 0.5f;
            FocusOn(new Vector3(mid.x, speakerKey.position.y + 0.9f, 0f), focusZoom, focusSeconds);
            yield return Wait(focusSeconds + 0.2f);

            _box.SetActive(true);
            _hint.SetActive(true);
            yield return Say("", fallenLine, speakerKey);
            _box.SetActive(false);
            _hint.SetActive(false);
            yield return Wait(0.5f);
        }

        // 3) 揺れ。Kキーが声を上げる
        PlaySfx(rumbleClip);
        if (_camFollow != null)
            _camFollow.ShakeCamera(1.2f, 0.05f);
        yield return Wait(0.7f);

        if (_kRoot != null)
        {
            Vector3 kp = _kRoot.transform.position;
            Vector3 pp = _player.transform.position;
            FocusOn(new Vector3((kp.x + pp.x) * 0.5f + 0.6f, kp.y + 1.0f, 0f), focusZoom, 0.8f);
            StartCoroutine(Hop(_kRoot.transform, 0.55f, 0.35f));
            ShowEmote();
            PlaySfx(surpriseSfx);
        }
        yield return Wait(0.5f);

        _box.SetActive(true);
        _hint.SetActive(true);
        if (_camFollow != null)
            _camFollow.ShakeCamera(2.5f, 0.06f);
        yield return Say(companionLabel, companionLine, _kRoot != null ? _kRoot.transform : null);
        _box.SetActive(false);
        _hint.SetActive(false);

        // 揺れが大きくなる
        if (_camFollow != null)
            _camFollow.ShakeCamera(1.0f, 0.13f);
        yield return Wait(0.9f);

        // 4) 一人称ふうの画面：巨大なAキーが、飛びこんでくる
        yield return FirstPerson();

        // 5) 白い光のあいだに、ボスの部屋へ
        yield return FlashAndMove();

        Finish();
    }

    /// <summary>ムービーの後始末。操作を戻して、終わりの合図を出す。</summary>
    private void Finish()
    {
        ClearBubbles();
        ApplyEndBgm();
        ClearEmote();
        if (_kRoot != null)
        {
            Destroy(_kRoot);
            _kRoot = null;
        }
        UnfocusCamera();
        StopPlayerWalk();
        LockPlayer(false);

        if (_canvasGo != null)
        {
            Destroy(_canvasGo);
            _canvasGo = null;
        }

        _running = false;
        IsPlaying = false;

        if (onMovieEnd != null)
            onMovieEnd.Invoke();
    }

    // ================= 倒れているキー =================

    private void PrepareFallenKeys(bool withBubbles)
    {
        int baseLayer = 0;
        int baseOrder = 0;
        if (_player != null)
        {
            SpriteRenderer psr = _player.GetComponentInChildren<SpriteRenderer>();
            if (psr != null)
            {
                baseLayer = psr.sortingLayerID;
                baseOrder = psr.sortingOrder;
            }
        }

        if (fallenKeysRoot != null)
        {
            LimbRig[] rigs = fallenKeysRoot.GetComponentsInChildren<LimbRig>(true);
            for (int i = 0; i < rigs.Length; i++)
            {
                LimbRig rig = rigs[i];
                bool isSpeaker = speakerKey != null && (rig.transform == speakerKey || rig.transform.IsChildOf(speakerKey));
                rig.SetMotion(LimbRig.KeyMotion.Injured);
                if (!isSpeaker)
                    rig.hurtFlinchInterval = 9999f;   // ほかのキーは、ぴくりとも動かない

                // 文字の無い、空っぽのキーにする
                KeyLabel[] labels = rig.GetComponentsInChildren<KeyLabel>(true);
                for (int k = 0; k < labels.Length; k++)
                {
                    labels[k].textOverride = "";
                    labels[k].keyOverride = null;
                    labels[k].Refresh();
                }

                // 描く順番：主人公のすぐ後ろ。暗い色にする
                Renderer[] rs = rig.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rs.Length; r++)
                {
                    rs[r].sortingLayerID = baseLayer;
                    rs[r].sortingOrder = baseOrder - 5 + rs[r].sortingOrder;
                }
                SpriteRenderer[] srs = rig.GetComponentsInChildren<SpriteRenderer>(true);
                for (int s = 0; s < srs.Length; s++)
                {
                    Color c = srs[s].color;
                    srs[s].color = new Color(c.r * fallenTint.r, c.g * fallenTint.g, c.b * fallenTint.b, c.a);
                }
            }
        }

        if (withBubbles)
            RegisterBubbles(baseLayer, baseOrder);

        // ひとこと話すキーは、道の左（主人公のほう）を向く
        if (speakerKey != null)
        {
            Vector3 s = speakerKey.localScale;
            s.x = Mathf.Abs(s.x) * (keyPrefabFacesRight ? -1f : 1f);
            speakerKey.localScale = s;
        }
    }

    // ================= Kキー =================

    private void SpawnCompanion()
    {
        if (keyPrefab == null || _player == null)
        {
            Debug.LogWarning("[BossIntroMovie] Key Prefab（KeyNPC_Visual）が空なので、Kキーは出ません。");
            return;
        }

        Vector3 pp = _player.transform.position;
        float kx = pp.x - companionGap;
        _kFootY = GroundY(kx, pp.y + 2f, pp.y + 2.5f, FootY());

        // 親は反転しない。反転するのは見た目だけ（エモートが裏返らない）
        _kRoot = new GameObject("BossIntro_K");
        _kRoot.transform.position = new Vector3(kx, _kFootY, pp.z);
        _kVisual = Instantiate(keyPrefab, _kRoot.transform);
        _kVisual.transform.localPosition = Vector3.zero;
        _kVisual.transform.localRotation = Quaternion.identity;
        float face = keyPrefabFacesRight ? 1f : -1f;   // 右へ歩くので、左向きの絵は反転する
        Vector3 s = _kVisual.transform.localScale;
        _kVisual.transform.localScale = new Vector3(Mathf.Abs(s.x) * face, s.y, s.z);

        KeyLabel[] labels = _kVisual.GetComponentsInChildren<KeyLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].textOverride = companionLabel;
            labels[i].Refresh();
        }

        int baseLayer = 0;
        int baseOrder = 0;
        SpriteRenderer psr = _player.GetComponentInChildren<SpriteRenderer>();
        if (psr != null)
        {
            baseLayer = psr.sortingLayerID;
            baseOrder = psr.sortingOrder;
        }
        Renderer[] krs = _kVisual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < krs.Length; i++)
        {
            krs[i].sortingLayerID = baseLayer;
            krs[i].sortingOrder = baseOrder + krs[i].sortingOrder;
        }

        _kRig = _kVisual.GetComponentInChildren<LimbRig>(true);
        if (_kRig != null)
            _kRig.SetManualVelocity(0f);
    }

    private void ShowEmote()
    {
        ClearEmote();
        if (surpriseEmote == null || _kRoot == null)
            return;
        _emoteGo = new GameObject("BossIntro_Emote");
        _emoteGo.transform.SetParent(_kRoot.transform, false);
        _emoteGo.transform.localPosition = new Vector3(emoteOffset.x, emoteOffset.y, 0f);
        _emoteGo.transform.localScale = Vector3.one * emoteScale;
        GameObject e = Instantiate(surpriseEmote, _emoteGo.transform);
        e.transform.localPosition = Vector3.zero;
        e.transform.localRotation = Quaternion.identity;
        SpriteRenderer[] rs = e.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < rs.Length; i++)
            rs[i].sortingOrder += 100;
        if (emoteSeconds > 0f)
            Destroy(_emoteGo, emoteSeconds);
    }

    private void ClearEmote()
    {
        if (_emoteGo != null)
            Destroy(_emoteGo);
        _emoteGo = null;
    }

    private IEnumerator Hop(Transform t, float height, float seconds)
    {
        if (t == null)
            yield break;
        Vector3 p0 = t.position;
        float e = 0f;
        while (e < seconds)
        {
            e += Time.deltaTime;
            if (t == null)
                yield break;
            float u = Mathf.Clamp01(e / seconds);
            t.position = p0 + new Vector3(0f, height * 4f * u * (1f - u), 0f);
            yield return null;
        }
        if (t != null)
            t.position = p0;
    }

    // ================= 主人公 =================

    private void FindPlayer()
    {
        if (_player != null)
            return;
        GameObject p = NewGameIntro.FindMainPlayerObject();
        if (p == null)
            return;
        _player = p;
        _pm = p.GetComponent<PlayerMovement>();
        _playerRb = p.GetComponent<Rigidbody2D>();
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

    private float FootY()
    {
        Collider2D pc = _player.GetComponent<Collider2D>();
        return pc != null ? pc.bounds.min.y : _player.transform.position.y;
    }

    /// <summary>x の真下の地面の高さ。見つからなければ fallback。</summary>
    private float GroundY(float x, float fromY, float maxY, float fallback)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, fromY), Vector2.down, 30f);
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger || (_player != null && c.transform.IsChildOf(_player.transform)))
                continue;
            if (hits[i].point.y > bestY && hits[i].point.y <= maxY)
                bestY = hits[i].point.y;
        }
        return float.IsNegativeInfinity(bestY) ? fallback : bestY;
    }

    /// <summary>主人公が右へ歩き、Kキーが後ろからついてくる。</summary>
    private IEnumerator WalkTogether(float targetX)
    {
        Animator an = PlayerAnimator();

        // 右を向く
        CharacterController2D cc = _player.GetComponent<CharacterController2D>();
        if (cc != null)
            cc.Move(0.01f, false, false);

        if (an != null)
            an.SetFloat("Speed", 40f);

        float walkTimer = 0f;
        float stepTimer = 0f;
        while (PlayerX() < targetX && walkTimer < 60f)
        {
            yield return new WaitForFixedUpdate();
            walkTimer += Time.fixedDeltaTime;
            stepTimer += Time.fixedDeltaTime;
            if (stepTimer >= walkStepInterval)
            {
                stepTimer -= walkStepInterval;
                PlaySfx(footstepSfx);
            }
            float step = walkSpeed * Time.fixedDeltaTime;
            if (_playerRb != null)
                _playerRb.position = _playerRb.position + new Vector2(step, 0f);
            else
                _player.transform.position += new Vector3(step, 0f, 0f);
            UpdateCompanion(Time.fixedDeltaTime);
        }

        StopPlayerWalk();

        // Kキーが追いつくまで
        int guard = 0;
        while (_kRoot != null && Mathf.Abs((PlayerX() - companionGap) - _kRoot.transform.position.x) > 0.03f && guard < 200)
        {
            yield return new WaitForFixedUpdate();
            UpdateCompanion(Time.fixedDeltaTime);
            guard++;
        }
        if (_kRig != null)
            _kRig.SetManualVelocity(0f);
    }

    private float PlayerX()
    {
        return _playerRb != null ? _playerRb.position.x : _player.transform.position.x;
    }

    private void UpdateCompanion(float dt)
    {
        if (_kRoot == null)
            return;
        Vector3 kp = _kRoot.transform.position;
        float want = PlayerX() - companionGap;
        float dx = want - kp.x;
        float maxStep = walkSpeed * 1.2f * dt;
        float step = Mathf.Clamp(dx, -maxStep, maxStep);
        kp.x += step;
        kp.y = GroundY(kp.x, kp.y + 2f, kp.y + 2.5f, _kFootY);
        _kFootY = kp.y;
        _kRoot.transform.position = kp;
        if (_kRig != null)
            _kRig.SetManualVelocity(Mathf.Abs(step) / Mathf.Max(0.0001f, dt));
    }

    // ================= カメラ =================

    private void FocusOn(Vector3 point, float zoom, float seconds)
    {
        if (_camFollow == null)
            return;
        if (_focusPoint == null)
            _focusPoint = new GameObject("BossIntro_CameraFocus");
        _focusPoint.transform.position = point;
        _camFollow.MoveTo(_focusPoint.transform, Mathf.Max(0.05f, seconds));
        if (zoom < 0.999f)
            _camFollow.ZoomTo(zoom, Mathf.Max(0.05f, seconds));
    }

    private void UnfocusCamera()
    {
        if (_camFollow != null && _player != null)
        {
            _camFollow.MoveTo(_player.transform, 0f);
            _camFollow.ResetZoom();
        }
        if (_focusPoint != null)
            Destroy(_focusPoint);
        _focusPoint = null;
    }

    // ================= 一人称ふうの画面：巨大なAキー =================

    private static float EaseOut(float u) { return 1f - (1f - u) * (1f - u); }
    private static float EaseInOut(float u) { return u * u * (3f - 2f * u); }

    private const float LegLen = 320f;   // 体の下に垂れる脚の高さ（足の裏が、Aキーの原点）

    private class Limb
    {
        public RectTransform top;      // 付け根（腕：上腕　脚：もも）
        public RectTransform joint;    // ひじ・ひざから先
        public float side;             // 右 = +1　左 = -1
        public RectTransform[] fingers;
        public float[] fingerBase;
    }

    private readonly Limb[] _legs = new Limb[2];
    private readonly Limb[] _arms = new Limb[2];
    private RectTransform _bodyRt;

    private IEnumerator FirstPerson()
    {
        BuildFirstPersonUi();
        if (fadeOutBgmAtBossAppear && AudioManager.Instance != null)
            AudioManager.StopBGM(0.6f);
        PlaySfx(bossAppearSfx);

        // 暗い画面へ切り替わる
        yield return FadeGroup(_fpGroup, 1f, 0.45f);

        // 世界の側は、もう見えないので、カメラの寄りは、もどしておく
        UnfocusCamera();
        ClearBubbles();
        if (_kRoot != null)
            _kRoot.SetActive(false);

        // A) 遠くに、小さく立っている
        float t = 0f;
        while (t < 1.0f)
        {
            t += Time.deltaTime;
            float w = Mathf.Sin(t * 9f) * 0.004f;
            PoseKey(0.05f + w, 1f, 1f, 0f, 0f, 0f);
            SetLimbs(8f, 0f, 14f, 6f, 1f);
            yield return null;
        }

        // B) ぴょん、ぴょん、ぴょんと、はねながら近づく
        float[] sizes = { 0.06f, 0.09f, 0.14f, 0.19f };
        for (int i = 0; i < 3; i++)
        {
            PlaySfx(hopClip);
            float airTime = 0.42f;
            float e = 0f;
            while (e < airTime)
            {
                e += Time.deltaTime;
                float u = Mathf.Clamp01(e / airTime);
                float s = Mathf.Lerp(sizes[i], sizes[i + 1], u);
                float hop = (600f + LegLen) * s * 1.1f * 4f * u * (1f - u);
                float stretch = 1f + 0.12f * Mathf.Sin(u * Mathf.PI);
                PoseKey(s, 1f / stretch, stretch, 0f, hop, Mathf.Sin(u * Mathf.PI * 2f) * 4f);
                float sp = Mathf.Sin(u * Mathf.PI);
                // 空中：脚をたたんで、腕をばんざいのように振り上げる
                SetLimbs(8f + 26f * sp, 10f + 50f * sp, 14f + 80f * sp, 8f + 25f * sp, 1f + 0.4f * sp);
                yield return null;
            }
            // 着地：ぺしゃっとつぶれて、もどる
            PlaySfx(landSfx);
            float landTime = 0.13f;
            e = 0f;
            while (e < landTime)
            {
                e += Time.deltaTime;
                float u = Mathf.Clamp01(e / landTime);
                float sq = 1f - 0.22f * (1f - EaseOut(u));
                PoseKey(sizes[i + 1], 1f / sq, sq, 0f, 0f, 0f);
                SetLimbs(Mathf.Lerp(48f, 8f, EaseOut(u)), Mathf.Lerp(60f, 0f, EaseOut(u)), Mathf.Lerp(30f, 14f, u), 6f, 1f);
                yield return null;
            }
        }

        // C) 力をためる：ぐっとしゃがんで、腕をひきつける
        PlaySfx(chargeSfx);
        float crouch = 0.45f;
        t = 0f;
        while (t < crouch)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / crouch);
            float k = EaseInOut(u);
            float sq = 1f - 0.38f * k;
            float jitter = Mathf.Sin(t * 70f) * 0.004f * u;
            PoseKey(sizes[3], 1f / sq + jitter, sq, jitter * 600f, 0f, 0f);
            SetLimbs(Mathf.Lerp(8f, 55f, k), Mathf.Lerp(0f, 90f, k), Mathf.Lerp(14f, 38f, k), Mathf.Lerp(6f, -55f, k), 0.8f);
            yield return null;
        }

        // D) 画面に向かって、とびかかる（腕を大きく広げて、指をひらく）
        PlaySfx(leapClip);
        float leap = 0.8f;
        float startS = sizes[3];
        float endS = 3.8f;
        float lnRatio = Mathf.Log(endS / startS);
        float c0 = _fpBaseY + (LegLen + 300f) * startS;   // はじめの、体の中心の高さ
        t = 0f;
        bool flashSound = false;
        while (t < leap)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / leap);
            float e = Mathf.Pow(u, 1.5f);
            float s = startS * Mathf.Exp(lnRatio * e);
            // 体の中心が、画面の中心へ向かう（とちゅうで、ふわっと高く跳ぶ）
            float yc = Mathf.Lerp(c0, 0f, EaseInOut(u)) + 260f * Mathf.Sin(Mathf.PI * u) * (1f - u);
            float y = yc - (LegLen + 300f) * s;
            float rot = Mathf.Lerp(-14f, 0f, EaseOut(u));
            float stretch = 1f + 0.1f * Mathf.Sin(u * Mathf.PI);
            PoseKey(s, 1f / stretch, stretch, 0f, y - _fpBaseY, rot);

            float lk = EaseOut(Mathf.Clamp01(u * 1.6f));
            SetLimbs(Mathf.Lerp(55f, 18f, lk), Mathf.Lerp(90f, 25f, lk), Mathf.Lerp(38f, 155f, lk), Mathf.Lerp(-55f, -8f, lk), Mathf.Lerp(0.8f, 1.7f, lk));

            if (_linesGroup != null)
                _linesGroup.alpha = Mathf.Clamp01(u * 1.6f) * 0.8f;
            if (_linesRt != null)
                _linesRt.localScale = Vector3.one * (1f + u * 0.8f);
            if (_glowImg != null)
            {
                Color gc = keyAccentColor;
                gc.a = Mathf.Lerp(0.25f, 0.6f, u);
                _glowImg.color = gc;
                _glowRt.localScale = Vector3.one * Mathf.Lerp(1f, 2.4f, u);
            }

            // 最後のほうで、画面全体がぶるぶる揺れて、白く光りはじめる
            if (u > 0.78f)
            {
                float k = Mathf.Clamp01((u - 0.78f) / 0.22f);
                if (_fpRoot != null)
                    _fpRoot.anchoredPosition = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 26f * k;
                _flash.color = new Color(1f, 1f, 1f, k * k);
                if (!flashSound)
                {
                    flashSound = true;
                    PlaySfx(flashClip);
                }
            }
            yield return null;
        }
        _flash.color = Color.white;
        if (_fpRoot != null)
            _fpRoot.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Aキーの姿勢を決める。足の裏が原点。
    /// scale：全体の大きさ　sx, sy：つぶれ・のび　x, hop：地平線の位置からのずれ（ピクセル）
    /// </summary>
    private void PoseKey(float scale, float sx, float sy, float x, float hop, float rotZ)
    {
        if (_keyRt == null)
            return;
        _keyRt.localScale = new Vector3(scale * sx, scale * sy, 1f);
        _keyRt.anchoredPosition = new Vector2(x, _fpBaseY + hop);
        _keyRt.localRotation = Quaternion.Euler(0f, 0f, rotZ);

        // 影：地面に落ちる。高くはねるほど、小さく、うすくなる
        float h = Mathf.Clamp01(hop / Mathf.Max(1f, (600f + LegLen) * scale * 2.2f));
        if (_shadowRt != null)
        {
            _shadowRt.anchoredPosition = new Vector2(x, _fpBaseY - 4f);
            _shadowRt.localScale = new Vector3(scale * (1f - h * 0.4f), scale * (1f - h * 0.4f), 1f);
            Color c = _shadowImg.color;
            c.a = 0.6f * (1f - h * 0.7f) * Mathf.Clamp01(1.2f - scale * 0.3f);
            _shadowImg.color = c;
        }
        if (_glowRt != null)
        {
            _glowRt.anchoredPosition = new Vector2(x, _fpBaseY + hop + (LegLen + 300f) * scale);
            if (_glowImg != null && scale < 0.25f)
            {
                Color gc = keyAccentColor;
                gc.a = 0.25f;
                _glowImg.color = gc;
                _glowRt.localScale = Vector3.one * (scale * 3.5f);
            }
        }
    }

    /// <summary>
    /// 手足の形を決める（角度は、度）。
    /// legOut：もものひらき　legBend：ひざの曲がり　armOut：腕のひらき（上げる）　armBend：ひじの曲がり（マイナスは内側）　handOpen：指のひらき（1が基本）
    /// </summary>
    private void SetLimbs(float legOut, float legBend, float armOut, float armBend, float handOpen)
    {
        for (int i = 0; i < 2; i++)
        {
            Limb lg = _legs[i];
            if (lg != null && lg.top != null)
            {
                lg.top.localRotation = Quaternion.Euler(0f, 0f, lg.side * legOut);
                lg.joint.localRotation = Quaternion.Euler(0f, 0f, -lg.side * legBend);
            }
            Limb am = _arms[i];
            if (am != null && am.top != null)
            {
                am.top.localRotation = Quaternion.Euler(0f, 0f, am.side * armOut);
                am.joint.localRotation = Quaternion.Euler(0f, 0f, am.side * armBend);
                for (int f = 0; f < am.fingers.Length; f++)
                    am.fingers[f].localRotation = Quaternion.Euler(0f, 0f, am.fingerBase[f] * handOpen);
            }
        }
    }

    /// <summary>手足の1つのパーツ（角の丸い四角）。anchor：親の中の基準点（0〜1）　pivot：回す中心</summary>
    private RectTransform Seg(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        Color fill, Color edge, bool sliced)
    {
        Image im = NewImage(name, parent, fill, RoundSprite(), sliced);
        RectTransform r = im.rectTransform;
        r.anchorMin = anchor;
        r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        Outline o = im.gameObject.AddComponent<Outline>();
        o.effectColor = edge;
        o.effectDistance = new Vector2(5f, -5f);
        return r;
    }

    private Limb BuildLeg(float side, Color fill, Color edge)
    {
        Limb l = new Limb();
        l.side = side;
        Vector2 bottom = new Vector2(0.5f, 0f);
        Vector2 topPivot = new Vector2(0.5f, 1f);
        l.top = Seg("Thigh", _keyRt, bottom, topPivot, new Vector2(side * 150f, LegLen + 14f), new Vector2(78f, 160f), fill, edge, true);
        l.joint = Seg("Shin", l.top, bottom, topPivot, Vector2.zero, new Vector2(66f, 150f), fill, edge, true);
        Seg("Foot", l.joint, bottom, new Vector2(0.5f, 0.5f), new Vector2(side * 30f, 8f), new Vector2(160f, 62f), fill, edge, true);
        l.fingers = new RectTransform[0];
        l.fingerBase = new float[0];
        return l;
    }

    private Limb BuildArm(float side, Color fill, Color edge)
    {
        Limb l = new Limb();
        l.side = side;
        Vector2 bottom = new Vector2(0.5f, 0f);
        Vector2 topPivot = new Vector2(0.5f, 1f);
        l.top = Seg("Upper", _keyRt, bottom, topPivot, new Vector2(side * 318f, LegLen + 540f), new Vector2(74f, 190f), fill, edge, true);
        l.joint = Seg("Fore", l.top, bottom, topPivot, Vector2.zero, new Vector2(62f, 180f), fill, edge, true);
        RectTransform hand = Seg("Hand", l.joint, bottom, topPivot, Vector2.zero, new Vector2(124f, 104f), fill, edge, true);
        float[] xs = { -42f, -14f, 14f, 42f };
        float[] angles = { -26f, -9f, 9f, 26f };
        l.fingers = new RectTransform[4];
        l.fingerBase = new float[4];
        for (int i = 0; i < 4; i++)
        {
            l.fingers[i] = Seg("Finger" + i, hand, bottom, topPivot, new Vector2(xs[i], 8f), new Vector2(28f, 92f), fill, edge, false);
            l.fingerBase[i] = angles[i];
        }
        return l;
    }

    private void BuildFirstPersonUi()
    {
        // 帯や会話の枠より後ろに置く（映画の帯は、そのまま見える）
        GameObject fp = new GameObject("FirstPerson", typeof(RectTransform));
        fp.transform.SetParent(_canvasGo.transform, false);
        fp.transform.SetAsFirstSibling();
        _fpRoot = (RectTransform)fp.transform;
        Stretch(_fpRoot);
        _fpGroup = fp.AddComponent<CanvasGroup>();
        _fpGroup.alpha = 0f;
        _fpGroup.blocksRaycasts = false;

        // 暗い空間
        Image bg = NewImage("Bg", _fpRoot, new Color(0.03f, 0.03f, 0.06f, 1f), null, false);
        Stretch(bg.rectTransform);

        // 床（地平線から下）
        float horizon = _fpBaseY;
        Image floor = NewImage("Floor", _fpRoot, new Color(0.07f, 0.07f, 0.1f, 1f), null, false);
        RectTransform fr = floor.rectTransform;
        fr.anchorMin = new Vector2(0f, 0.5f);
        fr.anchorMax = new Vector2(1f, 0.5f);
        fr.pivot = new Vector2(0.5f, 1f);
        fr.anchoredPosition = new Vector2(0f, horizon);
        fr.sizeDelta = new Vector2(0f, 900f);

        // 道の線（奥に向かってすぼまる）
        GameObject road = new GameObject("Road", typeof(RectTransform));
        road.transform.SetParent(_fpRoot, false);
        RectTransform rr = (RectTransform)road.transform;
        rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
        rr.anchoredPosition = new Vector2(0f, horizon);
        float[] roadAngles = { -78f, -56f, -38f, -22f, -9f, 9f, 22f, 38f, 56f, 78f };
        for (int i = 0; i < roadAngles.Length; i++)
        {
            Image ln = NewImage("RoadLine" + i, rr, new Color(0.2f, 0.2f, 0.28f, 0.8f), null, false);
            RectTransform lr = ln.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 1f);
            lr.anchoredPosition = Vector2.zero;
            lr.sizeDelta = new Vector2(3f, 1500f);
            lr.localRotation = Quaternion.Euler(0f, 0f, roadAngles[i]);
        }

        // 不気味な光
        _glowImg = NewImage("Glow", _fpRoot, new Color(keyAccentColor.r, keyAccentColor.g, keyAccentColor.b, 0.25f), RadialSprite(), false);
        _glowRt = _glowImg.rectTransform;
        _glowRt.anchorMin = _glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        _glowRt.pivot = new Vector2(0.5f, 0.5f);
        _glowRt.sizeDelta = new Vector2(1400f, 1400f);

        // 集中線
        GameObject lines = new GameObject("SpeedLines", typeof(RectTransform));
        lines.transform.SetParent(_fpRoot, false);
        _linesRt = (RectTransform)lines.transform;
        _linesRt.anchorMin = _linesRt.anchorMax = new Vector2(0.5f, 0.5f);
        _linesRt.anchoredPosition = new Vector2(0f, horizon);
        _linesGroup = lines.AddComponent<CanvasGroup>();
        _linesGroup.alpha = 0f;
        System.Random rnd = new System.Random(11);
        for (int i = 0; i < 26; i++)
        {
            float a = (float)rnd.NextDouble() * 360f;
            float w = 3f + (float)rnd.NextDouble() * 8f;
            float len = 800f + (float)rnd.NextDouble() * 700f;
            Image ln = NewImage("Line" + i, _linesRt, new Color(1f, 1f, 1f, 0.35f + (float)rnd.NextDouble() * 0.4f), null, false);
            RectTransform lr = ln.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            // 中心から少しはなれた所から、外へのびる
            lr.pivot = new Vector2(0.5f, -0.12f - (float)rnd.NextDouble() * 0.2f);
            lr.anchoredPosition = Vector2.zero;
            lr.sizeDelta = new Vector2(w, len);
            lr.localRotation = Quaternion.Euler(0f, 0f, a);
        }

        // 影
        _shadowImg = NewImage("Shadow", _fpRoot, new Color(0f, 0f, 0f, 0.6f), RadialSprite(), false);
        _shadowRt = _shadowImg.rectTransform;
        _shadowRt.anchorMin = _shadowRt.anchorMax = new Vector2(0.5f, 0.5f);
        _shadowRt.pivot = new Vector2(0.5f, 0.5f);
        _shadowRt.sizeDelta = new Vector2(760f, 170f);

        // 巨大なAキー（足の裏が原点）
        GameObject key = new GameObject("BossKeyA", typeof(RectTransform));
        key.transform.SetParent(_fpRoot, false);
        _keyRt = (RectTransform)key.transform;
        _keyRt.anchorMin = _keyRt.anchorMax = new Vector2(0.5f, 0.5f);
        _keyRt.pivot = new Vector2(0.5f, 0f);
        _keyRt.sizeDelta = new Vector2(600f, 600f + LegLen);

        // 手足：黒くて太く、ふちが赤い（体より先に作るので、体の後ろにまわる）
        Color limbFill = new Color(0.06f, 0.06f, 0.08f, 1f);
        Color limbEdge = new Color(keyAccentColor.r, keyAccentColor.g, keyAccentColor.b, 0.9f);
        _legs[0] = BuildLeg(-1f, limbFill, limbEdge);
        _legs[1] = BuildLeg(1f, limbFill, limbEdge);
        _arms[0] = BuildArm(-1f, limbFill, limbEdge);
        _arms[1] = BuildArm(1f, limbFill, limbEdge);

        // 体（キー）
        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(_keyRt, false);
        _bodyRt = (RectTransform)body.transform;
        _bodyRt.anchorMin = _bodyRt.anchorMax = new Vector2(0.5f, 0f);
        _bodyRt.pivot = new Vector2(0.5f, 0f);
        _bodyRt.anchoredPosition = new Vector2(0f, LegLen);
        _bodyRt.sizeDelta = new Vector2(600f, 600f);

        Image baseImg = NewImage("Base", _bodyRt, keyBaseColor, RoundSprite(), true);
        Stretch(baseImg.rectTransform);
        Outline ol = baseImg.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(keyAccentColor.r, keyAccentColor.g, keyAccentColor.b, 0.95f);
        ol.effectDistance = new Vector2(10f, -10f);

        Image face = NewImage("Face", _bodyRt, keyFaceColor, RoundSprite(), true);
        RectTransform fcr = face.rectTransform;
        fcr.anchorMin = fcr.anchorMax = new Vector2(0.5f, 0.5f);
        fcr.sizeDelta = new Vector2(530f, 510f);
        fcr.anchoredPosition = new Vector2(0f, 32f);

        Color dishColor = new Color(keyFaceColor.r * 0.9f, keyFaceColor.g * 0.9f, keyFaceColor.b * 0.92f, 1f);
        Image dish = NewImage("Dish", _bodyRt, dishColor, RoundSprite(), true);
        RectTransform dr = dish.rectTransform;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
        dr.sizeDelta = new Vector2(440f, 420f);
        dr.anchoredPosition = new Vector2(0f, 38f);

        TextMeshProUGUI letter = NewText("Letter", _bodyRt, 380f, keyLetterColor, TextAlignmentOptions.Center);
        letter.text = "A";
        letter.fontStyle = FontStyles.Bold;
        letter.enableWordWrapping = false;
        letter.overflowMode = TextOverflowModes.Overflow;
        RectTransform lrt = letter.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(500f, 480f);
        lrt.anchoredPosition = new Vector2(0f, 36f);

        PoseKey(0.05f, 1f, 1f, 0f, 0f, 0f);
        SetLimbs(8f, 0f, 14f, 6f, 1f);
    }

    // ================= 倒れたキーの吹き出し =================

    /// <summary>吹き出しを付けるキーを、決める（ところどころ。同じ文が続かない）。</summary>
    private void RegisterBubbles(int layer, int order)
    {
        _bubbles.Clear();
        _bubbleLayer = layer;
        _bubbleOrder = order + 200;
        if (fallenKeysRoot == null || groanLines == null || groanLines.Length == 0)
            return;

        LimbRig[] rigs = fallenKeysRoot.GetComponentsInChildren<LimbRig>(true);
        System.Array.Sort(rigs, (r1, r2) => r1.transform.position.x.CompareTo(r2.transform.position.x));

        System.Random rnd = new System.Random(21);
        float lastX = -9999f;
        int lineIdx = rnd.Next(groanLines.Length);
        for (int i = 0; i < rigs.Length; i++)
        {
            Transform tr = rigs[i].transform;
            if (speakerKey != null && (tr == speakerKey || tr.IsChildOf(speakerKey)))
                continue;
            if (tr.position.x - lastX < bubbleMinGap)
                continue;
            if (rnd.NextDouble() > bubbleRate)
                continue;
            lastX = tr.position.x;
            lineIdx = (lineIdx + 1 + rnd.Next(Mathf.Max(1, groanLines.Length - 1))) % groanLines.Length;

            Bubble b = new Bubble();
            b.key = tr;
            b.line = groanLines[lineIdx];
            _bubbles.Add(b);
        }
    }

    private void UpdateBubbles(float dt)
    {
        float px = PlayerX();
        for (int i = 0; i < _bubbles.Count; i++)
        {
            Bubble b = _bubbles[i];
            if (b.done || b.key == null)
                continue;

            if (!b.shown)
            {
                float dx = b.key.position.x - px;
                if (dx < bubbleShowDistance && dx > -1.5f)
                    ShowBubble(b);
                continue;
            }

            b.t += dt;
            float u = Mathf.Clamp01(b.t / 0.25f);
            const float c1 = 1.70158f;
            float pop = 1f + (c1 + 1f) * Mathf.Pow(u - 1f, 3f) + c1 * Mathf.Pow(u - 1f, 2f);   // ぽんと出る
            float fade = b.t > bubbleSeconds ? 1f - Mathf.Clamp01((b.t - bubbleSeconds) / 0.5f) : 1f;
            if (b.go != null)
            {
                b.go.transform.localScale = Vector3.one * Mathf.Max(0.01f, pop);
                b.go.transform.position = b.basePos + new Vector3(0f, Mathf.Sin(b.t * 3f) * 0.04f, 0f);
                SetBubbleAlpha(b, Mathf.Clamp01(u * 2f) * fade);
            }
            if (b.t > bubbleSeconds + 0.5f)
            {
                if (b.go != null)
                    Destroy(b.go);
                b.go = null;
                b.done = true;
            }
        }
    }

    private SpriteRenderer MakeBubblePart(string name, Transform parent, Vector2 pos, Color color, int order, Vector2 size, bool sliced)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = RoundSprite();
        sr.color = color;
        sr.sortingLayerID = _bubbleLayer;
        sr.sortingOrder = order;
        if (sliced)
        {
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
        }
        return sr;
    }

    private void ShowBubble(Bubble b)
    {
        b.shown = true;
        PlaySfx(bubbleSfx);

        // キーの頭の上
        float top = b.key.position.y + 0.8f;
        float cx = b.key.position.x;
        Renderer[] rs = b.key.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds bb = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
                bb.Encapsulate(rs[i].bounds);
            top = bb.max.y;
            cx = bb.center.x;
        }
        b.basePos = new Vector3(cx, top + bubbleHeight, b.key.position.z);

        GameObject go = new GameObject("GroanBubble");
        go.transform.position = b.basePos;
        b.go = go;

        // 文字
        TMP_FontAsset f = _font != null ? _font : FindSceneFont();
        GameObject tg = new GameObject("Text");
        tg.transform.SetParent(go.transform, false);
        TextMeshPro tmp = tg.AddComponent<TextMeshPro>();
        if (f != null)
            tmp.font = f;
        tmp.fontSize = bubbleTextSize;
        tmp.color = bubbleTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.text = Clean(b.line);
        tmp.sortingLayerID = _bubbleLayer;
        tmp.sortingOrder = _bubbleOrder + 2;
        tmp.ForceMeshUpdate();
        Vector2 size = tmp.GetPreferredValues(tmp.text);
        tmp.rectTransform.sizeDelta = size;
        b.tmp = tmp;

        // ふきだしの形（ふちの黒 → 白い中 → しっぽ）
        float w = Mathf.Max(size.x + 0.5f, 0.9f);
        float h = Mathf.Max(size.y + 0.34f, 0.55f);
        Color edge = new Color(0.1f, 0.1f, 0.15f, 1f);
        SpriteRenderer outline = MakeBubblePart("Outline", go.transform, Vector2.zero, edge, _bubbleOrder - 2, new Vector2(w + 0.1f, h + 0.1f), true);
        SpriteRenderer fill = MakeBubblePart("Fill", go.transform, Vector2.zero, bubbleColor, _bubbleOrder, new Vector2(w, h), true);

        float tailY = -h * 0.5f + 0.02f;
        SpriteRenderer tailOut = MakeBubblePart("TailOutline", go.transform, new Vector2(0f, tailY), edge, _bubbleOrder - 2, Vector2.one, false);
        tailOut.transform.localScale = Vector3.one * (0.27f / 0.64f);
        tailOut.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        SpriteRenderer tailFill = MakeBubblePart("TailFill", go.transform, new Vector2(0f, tailY + 0.01f), bubbleColor, _bubbleOrder, Vector2.one, false);
        tailFill.transform.localScale = Vector3.one * (0.21f / 0.64f);
        tailFill.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        b.srs = new SpriteRenderer[] { outline, fill, tailOut, tailFill };
        b.cols = new Color[] { outline.color, fill.color, tailOut.color, tailFill.color };

        go.transform.localScale = Vector3.one * 0.01f;
        SetBubbleAlpha(b, 0f);
    }

    private void SetBubbleAlpha(Bubble b, float a)
    {
        if (b.srs != null)
        {
            for (int i = 0; i < b.srs.Length; i++)
            {
                if (b.srs[i] == null) continue;
                Color c = b.cols[i];
                c.a *= a;
                b.srs[i].color = c;
            }
        }
        if (b.tmp != null)
        {
            Color tc = bubbleTextColor;
            tc.a *= a;
            b.tmp.color = tc;
        }
    }

    private void ClearBubbles()
    {
        for (int i = 0; i < _bubbles.Count; i++)
        {
            if (_bubbles[i] != null && _bubbles[i].go != null)
                Destroy(_bubbles[i].go);
        }
        _bubbles.Clear();
    }

    // ================= BGM =================

    /// <summary>ステージのBGMを、ムービーが終わるまで止めておく（勝手に鳴りはじめないように）。</summary>
    private void HoldSceneBgm()
    {
        SceneBGM[] all = FindObjectsOfType<SceneBGM>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled)
            {
                all[i].enabled = false;   // Start が呼ばれないので、鳴りはじめない
                _heldBgm.Add(all[i]);
            }
        }
    }

    /// <summary>止めておいたステージのBGMを、ここから流す。</summary>
    private void ReleaseSceneBgm()
    {
        for (int i = 0; i < _heldBgm.Count; i++)
        {
            if (_heldBgm[i] != null)
            {
                _heldBgm[i].enabled = true;
                _heldBgm[i].Play();
            }
        }
        _heldBgm.Clear();
    }

    /// <summary>ムービーが終わるときのBGM：ボスの部屋のBGMがあればそれ、なければステージのBGMにもどす。</summary>
    private void ApplyEndBgm()
    {
        if (_endBgmDone)
            return;
        _endBgmDone = true;

        if (arenaBgm != null)
        {
            AudioManager.PlayBGM(arenaBgm, arenaBgmFade, true);
            _heldBgm.Clear();   // ステージのBGMは、もどさない
            return;
        }
        ReleaseSceneBgm();
    }

    // ================= 白い光と、部屋への移動 =================

    private IEnumerator FlashAndMove()
    {
        // 画面は、真っ白。そのあいだに、全部入れかえる
        if (_fpRoot != null)
            Destroy(_fpRoot.gameObject);
        _fpRoot = null;
        if (_barTop != null) SetBars(0f);
        if (_box != null) _box.SetActive(false);
        if (_hint != null) _hint.SetActive(false);
        if (_black != null)
            _black.color = new Color(0f, 0f, 0f, 0f);

        TeleportToArena();
        if (_kRoot != null)
        {
            Destroy(_kRoot);
            _kRoot = null;
        }
        ApplyEndBgm();   // ボスの部屋のBGMへ
        yield return Wait(0.6f);

        // 白が、すうっと消える
        PlaySfx(arrivalSfx);
        yield return Fade(_flash, 0f, 1.1f);
    }

    /// <summary>主人公を、ボスの部屋の入口へ移して、カメラも、すぐ同じ場所へ。</summary>
    private void TeleportToArena()
    {
        if (_player == null)
            return;
        if (arenaPlayerPoint == null)
        {
            Debug.LogWarning("[BossIntroMovie] Arena Player Point が空なので、主人公を移せません。");
            return;
        }

        Vector3 ap = arenaPlayerPoint.position;
        float offset = _player.transform.position.y - FootY();   // 足元から、体の中心までの高さ
        float gy = GroundY(ap.x, ap.y + 3f, ap.y + 3.5f, ap.y);
        Vector3 np = new Vector3(ap.x, gy + offset + 0.03f, _player.transform.position.z);

        _player.transform.position = np;
        if (_playerRb != null)
        {
            _playerRb.position = np;
            _playerRb.velocity = Vector2.zero;
        }
        StopPlayerWalk();

        UnfocusCamera();
        if (_camFollow != null)
        {
            Vector3 c = _camFollow.transform.position;
            _camFollow.transform.position = new Vector3(np.x, np.y, c.z);
        }
        Debug.Log("[BossIntroMovie] 主人公をボスの部屋の入口へ移しました： (" + np.x.ToString("F2") + ", " + np.y.ToString("F2") + ")");
    }

    // ================= 会話 =================

    private static bool Advance()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);
    }

    private static string Clean(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace('　', ' ');
    }

    /// <summary>1つのセリフ。who が空なら、名前の札は出さない。wobble を渡すと、話している間、少し震える。</summary>
    private IEnumerator Say(string who, string text, Transform wobble)
    {
        who = Clean(who);
        _nameBox.SetActive(!string.IsNullOrEmpty(who));
        _nameText.text = who;

        _body.fontSize = textSize;
        _body.text = Clean(text);
        _body.maxVisibleCharacters = 0;
        _body.ForceMeshUpdate();
        int total = _body.textInfo.characterCount;
        _arrow.gameObject.SetActive(false);

        Vector3 w0 = wobble != null ? wobble.position : Vector3.zero;

        int shown = 0;
        float acc = 0f;
        while (shown < total)
        {
            if (Advance())
                break;
            acc += Time.deltaTime;
            while (acc >= CharInterval && shown < total)
            {
                acc -= CharInterval;
                shown++;
                if (blipEveryChars > 0 && shown % blipEveryChars == 0)
                    PlaySfx(textBlipSfx);
            }
            _body.maxVisibleCharacters = shown;
            if (wobble != null)
                wobble.position = w0 + new Vector3(Mathf.Sin(Time.time * 55f) * 0.015f, 0f, 0f);
            yield return null;
        }
        _body.maxVisibleCharacters = 99999;
        if (wobble != null)
            wobble.position = w0;

        _arrow.gameObject.SetActive(true);
        yield return null;   // 同じフレームの入力を、次に持ち越さない
        float t = 0f;
        while (!Advance())
        {
            t += Time.deltaTime;
            _arrow.anchoredPosition = new Vector2(-56f, 44f + Mathf.Sin(t * 6f) * 6f);
            yield return null;
        }
        PlaySfx(advanceSfx);
        _arrow.gameObject.SetActive(false);
    }

    // ================= 小さな道具 =================

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null)
            return;
        // 設定画面のSEの音量に、したがう
        if (AudioManager.Instance != null)
        {
            AudioManager.PlaySE(clip, sfxVolume);
            return;
        }
        if (_audio != null)
            _audio.PlayOneShot(clip, sfxVolume);
    }

    private void SetBars(float k)
    {
        if (_barTop != null) _barTop.sizeDelta = new Vector2(0f, _barPx * k);
        if (_barBottom != null) _barBottom.sizeDelta = new Vector2(0f, _barPx * k);
    }

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
            SetBars(Mathf.Lerp(from, to, EaseInOut(k)));
            yield return null;
        }
        SetBars(to);
    }

    /// <summary>黒や白の幕の、アルファを to にする。</summary>
    private IEnumerator Fade(Image img, float to, float time)
    {
        if (img == null)
            yield break;
        float from = img.color.a;
        Color c = img.color;
        float t = 0f;
        float len = Mathf.Max(0.0001f, time);
        while (t < len)
        {
            t += Time.deltaTime;
            if (img == null) yield break;
            img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, Mathf.Clamp01(t / len)));
            yield return null;
        }
        if (img != null)
            img.color = new Color(c.r, c.g, c.b, to);
    }

    private IEnumerator FadeGroup(CanvasGroup g, float to, float time)
    {
        if (g == null)
            yield break;
        float from = g.alpha;
        float t = 0f;
        float len = Mathf.Max(0.0001f, time);
        while (t < len)
        {
            t += Time.deltaTime;
            if (g == null) yield break;
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / len));
            yield return null;
        }
        if (g != null)
            g.alpha = to;
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

    // ================= 画面（コードで作る） =================

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private Image NewImage(string objName, Transform parent, Color color, Sprite sprite, bool sliced)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        Image im = g.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        if (sprite != null)
        {
            im.sprite = sprite;
            im.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        }
        return im;
    }

    private TextMeshProUGUI NewText(string objName, Transform parent, float size, Color color, TextAlignmentOptions align)
    {
        GameObject g = new GameObject(objName, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        if (_font != null)
            t.font = _font;
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

    /// <summary>角の丸い四角の絵（まわりの20ピクセルを、9分割で伸ばす）。</summary>
    private Sprite RoundSprite()
    {
        if (_roundSprite != null)
            return _roundSprite;
        const int n = 64;
        const float radius = 22f;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[n * n];
        float c = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - c) - (c - radius);
                float dy = Mathf.Abs(y + 0.5f - c) - (c - radius);
                float ox = Mathf.Max(dx, 0f);
                float oy = Mathf.Max(dy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _roundSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
        return _roundSprite;
    }

    /// <summary>まん中が濃く、まわりへ薄れる、まるい光の絵。</summary>
    private Sprite RadialSprite()
    {
        if (_radialSprite != null)
            return _radialSprite;
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[n * n];
        float c = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - c) / c;
                float dy = (y + 0.5f - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a;
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _radialSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        return _radialSprite;
    }

    private void BuildUi()
    {
        if (_font == null)
            Debug.LogWarning("[BossIntroMovie] 日本語のフォントが見つからないので、文字が四角になるかもしれません（Font に入れてください）。");

        _canvasGo = new GameObject("BossIntroCanvas");
        Canvas canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;
        CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        _audio = _canvasGo.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;

        // 映画のような黒い帯（上と下）
        _barPx = letterboxRatio * 1080f;
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
        br.anchoredPosition = new Vector2(0f, 60f + _barPx);
        br.sizeDelta = new Vector2(1560f, 260f);
        Image back = _box.AddComponent<Image>();
        back.raycastTarget = false;
        back.color = new Color(0.05f, 0.05f, 0.09f, 0.88f);
        Outline ol = _box.AddComponent<Outline>();
        ol.effectColor = new Color(1f, 1f, 1f, 0.9f);
        ol.effectDistance = new Vector2(4f, -4f);

        // 名前の札（枠の左上）
        _nameBox = new GameObject("NameBox", typeof(RectTransform));
        _nameBox.transform.SetParent(_box.transform, false);
        RectTransform nr = (RectTransform)_nameBox.transform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(0f, 1f);
        nr.pivot = new Vector2(0f, 0f);
        nr.anchoredPosition = new Vector2(36f, 10f);
        nr.sizeDelta = new Vector2(300f, 70f);
        Image nb = _nameBox.AddComponent<Image>();
        nb.raycastTarget = false;
        nb.color = new Color(0.10f, 0.10f, 0.18f, 0.96f);
        Outline nol = _nameBox.AddComponent<Outline>();
        nol.effectColor = new Color(1f, 1f, 1f, 0.9f);
        nol.effectDistance = new Vector2(3f, -3f);
        _nameText = NewText("Name", _nameBox.transform, 42f, Color.white, TextAlignmentOptions.Center);
        Stretch(_nameText.rectTransform);

        // 本文
        _body = NewText("Body", _box.transform, textSize, Color.white, TextAlignmentOptions.TopLeft);
        _body.enableWordWrapping = true;
        _body.overflowMode = TextOverflowModes.Overflow;
        RectTransform tr = _body.rectTransform;
        Stretch(tr);
        tr.offsetMin = new Vector2(60f, 40f);
        tr.offsetMax = new Vector2(-60f, -40f);

        // 次へ進む印（白いひし形）
        GameObject ag = new GameObject("Arrow", typeof(RectTransform));
        ag.transform.SetParent(_box.transform, false);
        _arrow = (RectTransform)ag.transform;
        _arrow.anchorMin = new Vector2(1f, 0f);
        _arrow.anchorMax = new Vector2(1f, 0f);
        _arrow.pivot = new Vector2(0.5f, 0.5f);
        _arrow.anchoredPosition = new Vector2(-56f, 44f);
        _arrow.sizeDelta = new Vector2(24f, 24f);
        _arrow.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image ai = ag.AddComponent<Image>();
        ai.raycastTarget = false;
        ai.color = Color.white;
        ag.SetActive(false);

        _box.SetActive(false);

        // 操作の案内（右上）
        TextMeshProUGUI hint = NewText("Hint", _canvasGo.transform, 28f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.TopRight);
        hint.text = "Space：すすむ";
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(1f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(1f, 1f);
        hr.anchoredPosition = new Vector2(-40f, -30f - _barPx);
        hr.sizeDelta = new Vector2(800f, 50f);
        _hint = hint.gameObject;
        _hint.SetActive(false);

        // 暗幕（最初は、まっ暗）
        _black = NewImage("Black", _canvasGo.transform, Color.black, null, false);
        Stretch(_black.rectTransform);
        _black.raycastTarget = true;

        // 白い閃光（いちばん手前）
        _flash = NewImage("Flash", _canvasGo.transform, new Color(1f, 1f, 1f, 0f), null, false);
        Stretch(_flash.rectTransform);
    }
}
