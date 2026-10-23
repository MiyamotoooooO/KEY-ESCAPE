using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - ギミック紹介（GimmickIntroZone）の、説明パネルとSpaceキーの案内を出し入れする部品。
/// プレハブ（Assets/Prefab/GimmickIntroUI.prefab）のルートに付ける。
/// ゲームを止めている間も動くよう、実時間で動く。
///
/// ・説明の文字は1文字ずつ出る。出ている間、キーのキャラクター（Key Character Prefab）が話す動きをして、
///   数文字ごとに「しゃべる効果音」が鳴る。出終わると、キャラクターは待機に戻る。
/// ・キャラクターは、結果画面と同じ「小さな撮影場所」で撮った映像を、Character View（RawImage）に映す。
///   Key Character Prefab か Character View が空なら、キャラクターは出さない。
/// </summary>
public class GimmickIntroUI : MonoBehaviour
{
    [Tooltip("説明パネル全体。最初は非表示になる。")]
    public GameObject panel;
    [Tooltip("説明の文字。")]
    public TMP_Text messageText;
    [Tooltip("Spaceキーの案内（BlinkingPrompt を付けると点滅する）。最初は非表示になる。")]
    public GameObject spacePrompt;
    [Tooltip("任意。パネルをふわっと出すための CanvasGroup。")]
    public CanvasGroup fadeGroup;
    [Tooltip("パネルが出るまでの秒数。")]
    public float fadeInTime = 0.2f;

    [Header("文字の出方としゃべる音")]
    [Tooltip("1秒間に出る文字数。0以下なら、全文をすぐ出す。")]
    public float charsPerSecond = 30f;
    [Tooltip("何文字ごとに、しゃべる音を鳴らすか。")]
    public int talkSoundEvery = 2;
    [Tooltip("しゃべる音。空なら、SoundEffects の Dialogue Talk を鳴らす。")]
    public AudioClip talkClip;
    [Range(0f, 1f)] public float talkVolume = 1f;

    [Header("キーのキャラクター（説明の横）")]
    [Tooltip("手足の生えたキー（Assets/Prefab/Mob/Enemy/KeyNPC_Visual）。空ならキャラクターを出さない。")]
    public GameObject keyCharacterPrefab;
    [Tooltip("キャラクターの映像を映す RawImage。説明の文字の横に置く。")]
    public RawImage characterView;
    [Tooltip("キートップに出す文字（Key Definition が空のときに使う）。")]
    public string keyLabelText = "K";
    [Tooltip("任意。入れると、このキーの文字を出す（Key Label Text より優先）。")]
    public KeyDefinition keyDefinition;
    [Tooltip("ONなら右（説明の文字のほう）を向く。もとの絵は左向き。")]
    public bool keyFacesRight = true;
    [Tooltip("映像に映る高さ（ワールドの単位）。大きいほど、キャラクターが小さく映る。")]
    public float keyViewHeight = 2.2f;
    [Tooltip("キャラクターの拡大率。")]
    public float keyScale = 1f;

    [Header("キーの動き（ランダムに切り替える）")]
    [Tooltip("ONなら、下の動きの中からランダムに選んで、切り替えながら動く。OFFなら、文字が出ている間だけ「話す」動きをする。")]
    public bool randomMotion = true;
    [Tooltip("ランダムに選ぶ動きの一覧。出したくない動きは、一覧から外す。")]
    public LimbRig.KeyMotion[] motions = new LimbRig.KeyMotion[]
    {
        LimbRig.KeyMotion.Talk,
        LimbRig.KeyMotion.Cheer,
        LimbRig.KeyMotion.Sway,
        LimbRig.KeyMotion.Hop,
        LimbRig.KeyMotion.Wave,
        LimbRig.KeyMotion.Disco,
    };
    [Tooltip("動きを切り替える間隔の、いちばん短い秒数。")]
    public float motionIntervalMin = 2f;
    [Tooltip("動きを切り替える間隔の、いちばん長い秒数。")]
    public float motionIntervalMax = 4f;
    [Tooltip("ONなら、文字が出終わってSpaceキーを待つ間も、踊り続ける。OFFなら、待機に戻る。")]
    public bool danceWhileWaiting = true;

    private const float StageY = -7000f;

    private bool _showing;
    private float _time;

    private bool _typing;
    private float _shown;
    private int _total;
    private int _lastCount;
    private int _soundCounter;

    private ResultCharacterStage _stage;
    private LimbRig _rig;
    private bool _characterTried;
    private LimbRig.KeyMotion _currentMotion = LimbRig.KeyMotion.None;
    private float _motionTimer;

    /// <summary>文字が1文字ずつ出ている最中か。</summary>
    public bool IsTyping { get { return _typing; } }

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (spacePrompt != null) spacePrompt.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_stage != null)
            _stage.Dispose();
        _stage = null;
    }

    /// <summary>説明を出す。Spaceキーの案内は、SetPromptVisible で別に出す。</summary>
    public void Show(string message)
    {
        if (spacePrompt != null)
            spacePrompt.SetActive(false);
        if (_keyPrompt != null)
            _keyPrompt.SetActive(false);
        if (panel != null)
            panel.SetActive(true);
        _time = 0f;
        _showing = true;
        if (fadeGroup != null)
            fadeGroup.alpha = fadeInTime > 0f ? 0f : 1f;

        SetupCharacter();

        if (messageText != null)
        {
            messageText.text = message;
            messageText.ForceMeshUpdate();
            _total = messageText.textInfo.characterCount;
            _shown = 0f;
            _lastCount = 0;
            _soundCounter = 0;
            if (charsPerSecond > 0f && _total > 0)
            {
                messageText.maxVisibleCharacters = 0;
                _typing = true;
            }
            else
            {
                messageText.maxVisibleCharacters = int.MaxValue;
                _typing = false;
            }
        }
        else
        {
            _typing = false;
        }

        _currentMotion = LimbRig.KeyMotion.None;
        _motionTimer = 0f;
        if (_rig != null)
            UpdateMotion(0f, true);
    }

    /// <summary>1文字ずつ出している途中でも、全文をすぐ出す。</summary>
    public void CompleteText()
    {
        if (!_typing)
            return;
        FinishTyping();
    }

    public void SetPromptVisible(bool visible)
    {
        if (spacePrompt != null && spacePrompt.activeSelf != visible)
            spacePrompt.SetActive(visible);
    }

    private GameObject _keyPrompt;
    private TMP_Text _keyPromptText;

    /// <summary>
    /// 「このキーを押して」の案内を出す・隠す（Spaceキーの絵の代わりに、同じ場所に、キーの名前を出す）。
    /// label は、押してほしいキーの名前（例：F）。
    /// </summary>
    public void SetKeyPromptVisible(bool visible, string label)
    {
        if (visible && _keyPrompt == null)
            CreateKeyPrompt();
        if (_keyPrompt == null)
            return;
        if (visible && _keyPromptText != null && _keyPromptText.text != label)
            _keyPromptText.text = label;
        if (_keyPrompt.activeSelf != visible)
            _keyPrompt.SetActive(visible);
    }

    private void CreateKeyPrompt()
    {
        if (spacePrompt == null)
            return;
        RectTransform src = spacePrompt.GetComponent<RectTransform>();
        if (src == null || src.parent == null)
            return;

        GameObject go = new GameObject("KeyPrompt", typeof(RectTransform));
        go.transform.SetParent(src.parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = src.anchorMin;
        r.anchorMax = src.anchorMax;
        r.pivot = src.pivot;
        r.anchoredPosition = src.anchoredPosition;
        r.sizeDelta = src.sizeDelta;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (messageText != null)
            t.font = messageText.font;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.95f, 0.95f, 0.9f, 1f);
        t.enableAutoSizing = true;
        t.fontSizeMin = 20f;
        t.fontSizeMax = 72f;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        go.AddComponent<BlinkingPrompt>();

        _keyPrompt = go;
        _keyPromptText = t;
    }

    public void Hide()
    {
        _showing = false;
        _typing = false;
        if (_rig != null)
            _rig.SetMotion(LimbRig.KeyMotion.None);
        if (spacePrompt != null) spacePrompt.SetActive(false);
        if (_keyPrompt != null) _keyPrompt.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (!_showing)
            return;

        float dt = Time.unscaledDeltaTime;

        if (fadeGroup != null && fadeInTime > 0f)
        {
            _time += dt;
            fadeGroup.alpha = Mathf.Clamp01(_time / fadeInTime);
        }

        if (_typing && messageText != null)
        {
            _shown += charsPerSecond * dt;
            int count = Mathf.Min(_total, Mathf.FloorToInt(_shown));
            if (count > _lastCount)
            {
                CountTalkSound(_lastCount, count);
                _lastCount = count;
                messageText.maxVisibleCharacters = count;
            }
            if (count >= _total)
                FinishTyping();
        }

        if (_rig != null)
            UpdateMotion(dt, false);
    }

    /// <summary>
    /// キーの動きを決める。ランダムONなら、一定の間隔で、一覧の中から前と違う動きを選ぶ。
    /// OFF（または一覧が空）なら、文字が出ている間だけ「話す」。
    /// 文字が出終わったあと、踊り続けない設定なら、待機に戻る。
    /// </summary>
    private void UpdateMotion(float dt, bool force)
    {
        bool active = _typing || danceWhileWaiting;
        bool random = randomMotion && motions != null && motions.Length > 0;

        if (!random)
        {
            LimbRig.KeyMotion m = _typing ? LimbRig.KeyMotion.Talk : LimbRig.KeyMotion.None;
            if (force || m != _currentMotion)
            {
                _currentMotion = m;
                _rig.SetMotion(m);
            }
            return;
        }

        if (!active)
        {
            if (_currentMotion != LimbRig.KeyMotion.None)
            {
                _currentMotion = LimbRig.KeyMotion.None;
                _rig.SetMotion(LimbRig.KeyMotion.None);
            }
            return;
        }

        _motionTimer -= dt;
        if (force || _currentMotion == LimbRig.KeyMotion.None || _motionTimer <= 0f)
        {
            _currentMotion = PickMotion(_currentMotion);
            _rig.SetMotion(_currentMotion);
            float lo = Mathf.Max(0.3f, motionIntervalMin);
            float hi = Mathf.Max(lo, motionIntervalMax);
            _motionTimer = Random.Range(lo, hi);
        }
    }

    /// <summary>一覧から、前と違う動きをランダムに選ぶ（一覧が1つだけなら、それ）。</summary>
    private LimbRig.KeyMotion PickMotion(LimbRig.KeyMotion previous)
    {
        int n = motions.Length;
        for (int tries = 0; tries < 8; tries++)
        {
            LimbRig.KeyMotion m = motions[Random.Range(0, n)];
            if (m == LimbRig.KeyMotion.None)
                continue;
            if (m != previous || n == 1)
                return m;
        }
        return motions[0] == LimbRig.KeyMotion.None ? LimbRig.KeyMotion.Talk : motions[0];
    }

    private void FinishTyping()
    {
        _typing = false;
        if (messageText != null)
            messageText.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>新しく出た文字（空白以外）を数えて、talkSoundEvery 文字ごとに音を鳴らす。</summary>
    private void CountTalkSound(int from, int to)
    {
        TMP_TextInfo info = messageText.textInfo;
        for (int i = from; i < to && i < info.characterCount; i++)
        {
            if (!info.characterInfo[i].isVisible)
                continue;
            _soundCounter++;
            if (_soundCounter >= Mathf.Max(1, talkSoundEvery))
            {
                _soundCounter = 0;
                PlayTalk();
            }
        }
    }

    private void PlayTalk()
    {
        if (talkClip != null)
            AudioManager.PlaySE(talkClip, talkVolume);
        else
            SoundEffects.Play(SEId.DialogueTalk, 0.03f);
    }

    // ---- キーのキャラクター ------------------------------------------------

    private void SetupCharacter()
    {
        if (_characterTried)
            return;
        _characterTried = true;

        if (keyCharacterPrefab == null || characterView == null)
        {
            if (characterView != null)
                characterView.gameObject.SetActive(false);
            return;
        }

        Rect r = characterView.rectTransform.rect;
        float aspect = (r.width > 1f && r.height > 1f) ? r.width / r.height : 0.8f;

        _stage = new ResultCharacterStage("GimmickIntroKeyStage", new Vector3(0f, StageY, 0f), 1, keyViewHeight, aspect, 384);

        GameObject go = Instantiate(keyCharacterPrefab);
        go.name = "GimmickIntroKey";
        _stage.Place(go, 0, false);

        // もとの絵は左向き。右（説明の文字のほう）を向かせるときは、左右を反転する。
        float face = keyFacesRight ? -1f : 1f;
        Vector3 s = go.transform.localScale;
        go.transform.localScale = new Vector3(s.x * face * keyScale, s.y * keyScale, s.z * keyScale);

        // キートップの文字
        KeyLabel[] labels = go.GetComponentsInChildren<KeyLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (keyDefinition != null)
            {
                labels[i].keyOverride = keyDefinition;
                labels[i].textOverride = "";
            }
            else
            {
                labels[i].textOverride = keyLabelText;
            }
            labels[i].Refresh();
        }

        _rig = go.GetComponentInChildren<LimbRig>(true);
        if (_rig != null)
            _rig.SetManualVelocity(0f);

        characterView.texture = _stage.Texture;
        characterView.uvRect = _stage.SlotUv(0);
        characterView.gameObject.SetActive(true);
    }
}
