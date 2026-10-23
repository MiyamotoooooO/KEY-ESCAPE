using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 「はじめから」のあと、Kキーとの会話が終わったタイミングで出る、キーコンフィグのヒント。
/// 設定は、OpeningMovie の Intro Talk の Tutorial に並ぶ。
/// </summary>
[System.Serializable]
public class NewGameTutorialSettings
{
    [Header("基本")]
    [Tooltip("OFFなら、会話のあとのヒントを出さない。")]
    public bool enabled = true;
    [Tooltip("ヒントの説明パネル（Assets/Prefab/GimmickIntroUI）。空なら、ヒントは出ない。メニュー Tools > KEY ESCAPE > オープニングムービー > タイトルシーンに追加する、で自動で入る。")]
    public GimmickIntroUI hintUiPrefab;
    [Tooltip("セリフが出てから、Space／Tabを受け付けるまでの秒数（うっかり飛ばさないため）。")]
    public float inputDelay = 0.5f;

    [Header("セリフ（Kキーが話す）")]
    [TextArea(2, 4)]
    [Tooltip("1：Tabを押すように言う。Tabを押さないと、次へ進めない。")]
    public string tabMessage = "さっそく、キーの設定をしてみよう。「Tab」を押してみて！";
    [TextArea(2, 4)]
    [Tooltip("2：キーコンフィグ画面が開いて、左の「持っているキーの一覧」に寄ったとき。Spaceで次へ。")]
    public string ownedMessage = "ここは、今あなたが持っているキーの一覧です。ほら、私が見えるでしょう？";
    [TextArea(2, 4)]
    [Tooltip("3：右の「現在の設定」に寄って、Kキーを右移動にドラッグするように言う。設定するまで、次へ進めない。")]
    public string dragMessage = "では、私をマウスでドラッグして、「右移動」に設定してください";
    [TextArea(2, 4)]
    [Tooltip("3の途中で、Tabやクリックで戻ろうとしたときの警告。")]
    public string warnMessage = "Kキーを右移動に設定してください";
    [TextArea(2, 4)]
    [Tooltip("4：右移動に設定できたとき。Tabを押すと、画面が閉じる。")]
    public string boundMessage = "マウスまで逃げ出さなくてよかったですね。じゃあ「Tab」で閉じて、実際に歩いてみましょう!!";
    [TextArea(2, 4)]
    [Tooltip("5：画面を閉じて、少し歩いたとき。Spaceで閉じる。")]
    public string walkedMessage = "うん、上手に歩けてる！";

    [Header("文字の見た目（ヒントの説明パネル）")]
    [Tooltip("セリフの文字の大きさ。0なら、パネル（GimmickIntroUI）に設定してある大きさのまま。")]
    public float textSize = 0f;
    [Tooltip("ONなら、下の Text Color をセリフの色にする。OFFなら、パネルのままの色。")]
    public bool overrideTextColor = false;
    public Color textColor = Color.white;
    [Tooltip("セリフが1秒間に出る文字数。0なら、パネルに設定してある速さのまま。")]
    public float charsPerSecond = 0f;
    [Tooltip("セリフの行の間隔（0がふつう。大きくすると、広がる）。")]
    public float lineSpacing = 0f;

    [Header("ズーム（キーコンフィグ画面）")]
    [Tooltip("左の「持っているキーの一覧」へ寄る倍率。1以下ならズームしない。")]
    public float ownedZoom = 1.3f;
    [Tooltip("右の「現在の設定」へ寄る倍率。Kキーのマスも画面に入るように、自動で控えめになる。")]
    public float settingsZoom = 1.25f;
    [Tooltip("寄る・戻る秒数。")]
    public float zoomSeconds = 0.6f;
    [Tooltip("ズームしたとき、Kキーのマスや設定の欄が画面の端から離れていてほしい距離（画面の単位）。")]
    public float zoomMargin = 60f;

    [Header("歩く")]
    [Tooltip("画面を閉じて操作が戻ってから、右へこれだけ歩くと、Kキーが話しかけてくる（ワールドの単位）。")]
    public float walkDistance = 4f;

    [Header("「Tabを押して」の案内")]
    [Tooltip("任意。Tabキーの絵。空なら、下の文字を出す。")]
    public Sprite tabPromptSprite;
    [Tooltip("Tabキーの絵が空のときに出す文字。")]
    public string tabPromptText = "Tab";
}

/// <summary>
/// KEY ESCAPE - 「はじめから」のあとのヒント。Kキーが仲間になった演出が全部終わると、始まる。
///
/// 流れ：
///   1. Kキーが「Tabを押して」と言う（ヒントの説明パネル）。Tabを押さないと、次へ進めない。
///   2. キーコンフィグ画面が開く。左の「持っているキーの一覧」に寄って、説明する。
///   3. 右の「現在の設定」に寄って、「Kキーを右移動にドラッグして」と言う。設定するまで、Tabやクリックでは閉じられない
///      （Tabを押すと、警告が出る）。
///   4. 設定できたら「Tabで閉じて、歩いてみよう」と言う。Tabを押すと閉じる。
///   5. 右へ少し歩くと、Kキーが「上手に歩けてる」と言う。
///
/// ・このスクリプトは、シーンに置かなくていい。NewGameIntro が、会話のあとに自分で出す。
/// ・ゲームを止めている間は、GamePause で止める。時間はすべて実時間で数える。
/// </summary>
public class NewGameTutorial : MonoBehaviour
{
    /// <summary>ヒントの途中（歩くのを待っている間も含む）は true。</summary>
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPlaying = false;
    }

    /// <summary>ヒントを始める。出せない条件（設定OFF・パネルの絵が空・Kキーが空）のときは、何もしない。</summary>
    public static void Begin(NewGameTutorialSettings settings, KeyDefinition key)
    {
        if (settings == null || !settings.enabled)
            return;
        if (settings.hintUiPrefab == null)
        {
            Debug.LogWarning("[NewGameTutorial] Hint Ui Prefab が空なので、ヒントを出しません。OpeningMovie の Intro Talk > Tutorial に、GimmickIntroUI を入れてください（Setupメニューを実行すると、自動で入ります）。");
            return;
        }
        if (key == null)
        {
            Debug.LogWarning("[NewGameTutorial] Kキーが決まっていないので、ヒントを出しません。");
            return;
        }
        if (IsPlaying)
            return;

        GameObject go = new GameObject("NewGameTutorial");
        NewGameTutorial t = go.AddComponent<NewGameTutorial>();
        t._s = settings;
        t._key = key;
        IsPlaying = true;
    }

    private NewGameTutorialSettings _s;
    private KeyDefinition _key;

    private PlayerMovement _player;
    private KeyConfigUI _config;
    private GimmickIntroUI _ui;
    private GameObject _tabPrompt;

    private bool _held;
    private bool _closeBlocked;
    private bool _aborted;

    // ズーム
    private RectTransform _host;
    private Vector3 _scale0;
    private Vector3 _pos0;
    private Vector2 _cMin;
    private Vector2 _cMax;
    private Vector3 _cCenter;
    private float _margin;
    private bool _coverX;
    private bool _coverY;
    private bool _zooming;
    private Coroutine _zoomCo;

    private void Start()
    {
        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        IsPlaying = false;
        RestoreZoomNow();
        if (_config != null)
        {
            _config.CloseBlocked -= OnCloseBlocked;
            _config.CloseLocked = false;
        }
        Hold(false);
        if (_ui != null)
            Destroy(_ui.gameObject);
    }

    private void OnCloseBlocked()
    {
        _closeBlocked = true;
    }

    // ================= ゲームを止める・操作を止める =================

    private void Hold(bool on)
    {
        if (on == _held)
            return;
        _held = on;
        if (on)
            GamePause.Acquire(this);
        else
            GamePause.Release(this);
        if (_player != null)
            _player.SetInputLocked(on);
    }

    // ================= 流れ =================

    private IEnumerator Run()
    {
        GameObject p = NewGameIntro.FindMainPlayerObject();
        if (p != null)
            _player = p.GetComponent<PlayerMovement>();

        // 「新しいキーを獲得した！」の演出などが、全部終わるのを待つ
        yield return WaitCelebrationEnd();

        if (KeyInventory.Instance == null || !KeyInventory.Instance.Owns(_key))
        {
            Debug.LogWarning("[NewGameTutorial] Kキーを持っていないので、ヒントを出しません。");
            Destroy(gameObject);
            yield break;
        }
        _config = FindObjectOfType<KeyConfigUI>(true);
        if (_config == null || _config.panelRoot == null)
        {
            Debug.LogWarning("[NewGameTutorial] キーコンフィグ画面（KeyConfigUI）がシーンに無いので、ヒントを出しません。");
            Destroy(gameObject);
            yield break;
        }
        _config.CloseBlocked += OnCloseBlocked;

        Hold(true);
        _ui = Instantiate(_s.hintUiPrefab);
        ApplyTextStyle();
        SoundEffects.Play(SEId.DialogueOpen);

        // 1. 「Tabを押して」
        yield return LineWaitTab(_s.tabMessage, false);

        // 2. キーコンフィグ画面を開いて、左の「持っているキーの一覧」に寄る
        yield return OpenConfig();
        RectTransform left = LeftListRect();
        if (_s.ownedZoom > 1f && left != null)
            StartZoom(new RectTransform[] { left }, new RectTransform[] { left }, _s.ownedZoom);
        yield return WaitZoom();
        yield return LineSpace(_s.ownedMessage);

        // 3・4. 右の「現在の設定」に寄って、Kキーを右移動へ。設定できたら、Tabで閉じる
        while (true)
        {
            if (_s.settingsZoom > 1f)
                StartZoom(RowRects(), RequiredForDrag(), _s.settingsZoom);
            else
                StartZoomBase();
            yield return WaitZoom();

            yield return WaitBound(_s.dragMessage);
            yield return WaitReal(0.6f);

            StartZoomBase();   // 設定できたので、元の大きさに戻しながら、次の言葉を出す
            _aborted = false;
            yield return LineWaitTab(_s.boundMessage, true);
            if (!_aborted)
                break;
        }
        yield return WaitZoom();

        // 画面を閉じて、操作を戻す
        _ui.Hide();
        SetTabPrompt(false);
        RestoreZoomNow();
        _config.CloseLocked = false;
        _config.CloseBlocked -= OnCloseBlocked;
        _config.Close();
        SoundEffects.Play(SEId.DialogueClose);
        Hold(false);

        // 5. 少し歩くのを待って、Kキーが話しかける
        if (_player != null)
        {
            float startX = _player.transform.position.x;
            while (_player != null && _player.transform.position.x - startX < _s.walkDistance)
                yield return null;
        }

        Hold(true);
        SoundEffects.Play(SEId.DialogueOpen);
        yield return LineSpace(_s.walkedMessage);
        SoundEffects.Play(SEId.DialogueClose);
        Hold(false);

        Destroy(gameObject);
    }

    /// <summary>ゲームが止まる演出（ポップアップ・解放演出）が始まって、全部終わるのを待つ。</summary>
    private IEnumerator WaitCelebrationEnd()
    {
        float t = 0f;
        while (!GamePause.IsPaused && t < 2f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        int stable = 0;
        while (stable < 6)
        {
            if (GamePause.IsPaused)
                stable = 0;
            else
                stable++;
            yield return null;
        }
    }

    /// <summary>設定した文字の大きさ・色・速さを、説明パネルに反映する（0・OFFの項目は、パネルのまま）。</summary>
    private void ApplyTextStyle()
    {
        if (_ui == null)
            return;
        if (_s.charsPerSecond > 0f)
            _ui.charsPerSecond = _s.charsPerSecond;
        TMP_Text tx = _ui.messageText;
        if (tx == null)
            return;
        if (_s.textSize > 0f)
        {
            tx.enableAutoSizing = false;
            tx.fontSize = _s.textSize;
        }
        if (_s.overrideTextColor)
            tx.color = _s.textColor;
        if (!Mathf.Approximately(_s.lineSpacing, 0f))
            tx.lineSpacing = _s.lineSpacing;
    }

    private IEnumerator WaitReal(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ================= キーコンフィグ画面 =================

    private IEnumerator OpenConfig()
    {
        _config.CloseLocked = true;
        _config.currentFilter = KeyConfigFilter.All;
        if (_config.IsInteractionLocked)
            _config.SetInteractionLocked(false);
        _config.Open();

        // 開始の演出（看板・線）が終わるのを待つ
        yield return null;
        float t = 0f;
        while (_config.introAnimator != null && _config.introAnimator.IsPlaying && t < 4f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return null;
        Canvas.ForceUpdateCanvases();
        PrepareZoomBase();
    }

    private RectTransform LeftListRect()
    {
        if (_config.keyGridContent == null)
            return null;
        ScrollRect sr = _config.keyGridContent.GetComponentInParent<ScrollRect>();
        if (sr != null)
            return sr.transform as RectTransform;
        return _config.keyGridContent.parent as RectTransform;
    }

    private RectTransform KeyTileRect()
    {
        KeyGridButtonUI tile = _config.FindKeyTile(_key);
        return tile != null ? tile.transform as RectTransform : null;
    }

    private RectTransform[] RowRects()
    {
        List<RectTransform> list = new List<RectTransform>();
        if (_config.rows != null)
        {
            for (int i = 0; i < _config.rows.Count; i++)
            {
                if (_config.rows[i] == null)
                    continue;
                RectTransform r = _config.rows[i].transform as RectTransform;
                if (r != null)
                    list.Add(r);
            }
        }
        return list.ToArray();
    }

    /// <summary>右へ寄ったときも、画面に残っていてほしいもの：Kキーのマスと、設定の欄。</summary>
    private RectTransform[] RequiredForDrag()
    {
        List<RectTransform> list = new List<RectTransform>(RowRects());
        RectTransform tile = KeyTileRect();
        if (tile != null)
            list.Add(tile);
        return list.ToArray();
    }

    private bool IsBound()
    {
        KeyBindingManager m = KeyBindingManager.Instance;
        return m != null && m.GetBinding(GameAction.MoveRight) == _key;
    }

    // ================= セリフ =================

    /// <summary>セリフを出して、Spaceで次へ（文字が出ている間のSpaceは、全文を出す）。</summary>
    private IEnumerator LineSpace(string message)
    {
        _ui.Show(message);
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            bool typing = _ui.IsTyping;
            _ui.SetPromptVisible(!typing && t >= _s.inputDelay);
            if (t >= _s.inputDelay && Input.GetKeyDown(KeyCode.Space))
            {
                if (typing)
                    _ui.CompleteText();
                else
                    break;
            }
            yield return null;
        }
        SoundEffects.Play(SEId.DialogueAdvance);
        _ui.Hide();
    }

    /// <summary>
    /// セリフを出して、Tabを押すまで待つ（文字が出ている間のTabは、全文を出す）。
    /// checkBound がtrueのとき、Kキーが右移動から外れたら、待つのをやめて _aborted にする。
    /// </summary>
    private IEnumerator LineWaitTab(string message, bool checkBound)
    {
        _ui.Show(message);
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            bool typing = _ui.IsTyping;
            SetTabPrompt(!typing && t >= _s.inputDelay);

            if (checkBound && !IsBound())
            {
                _aborted = true;
                break;
            }

            bool tab = Input.GetKeyDown(_config.toggleKey);
            bool space = Input.GetKeyDown(KeyCode.Space);
            if (typing && t >= _s.inputDelay && (tab || space))
            {
                _ui.CompleteText();
            }
            else if (!typing && t >= _s.inputDelay && tab)
            {
                break;
            }

            // 画面のボタンで閉じようとしても、閉じない（ここでは、何も起きなくていい）
            _closeBlocked = false;
            yield return null;
        }
        SetTabPrompt(false);
        if (!_aborted)
            SoundEffects.Play(SEId.DialogueAdvance);
        _ui.Hide();
    }

    /// <summary>「ドラッグして右移動に設定して」。設定するまで待つ。Tabやクリックで戻ろうとすると、警告を出す。</summary>
    private IEnumerator WaitBound(string message)
    {
        _ui.Show(message);
        SetTabPrompt(false);
        float warnUntil = -1f;
        while (!IsBound())
        {
            bool pressed = Input.GetKeyDown(_config.toggleKey) || _closeBlocked;
            _closeBlocked = false;
            if (pressed)
            {
                _ui.Show(_s.warnMessage);
                warnUntil = Time.unscaledTime + 3f;
            }
            else if (warnUntil > 0f && Time.unscaledTime >= warnUntil && !_ui.IsTyping)
            {
                warnUntil = -1f;
                _ui.Show(message);
            }
            yield return null;
        }
        _ui.Hide();
    }

    // ================= 「Tab」の案内 =================

    private void SetTabPrompt(bool on)
    {
        if (on && _tabPrompt == null)
            _tabPrompt = MakeTabPrompt();
        if (_tabPrompt != null && _tabPrompt.activeSelf != on)
            _tabPrompt.SetActive(on);
    }

    /// <summary>Spaceキーの案内（SpaceKey）と同じ場所に、Tabの案内を作る。</summary>
    private GameObject MakeTabPrompt()
    {
        if (_ui == null || _ui.spacePrompt == null)
            return null;
        RectTransform src = _ui.spacePrompt.GetComponent<RectTransform>();
        if (src == null || src.parent == null)
            return null;

        GameObject go = new GameObject("TabPrompt", typeof(RectTransform));
        go.transform.SetParent(src.parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = src.anchorMin;
        r.anchorMax = src.anchorMax;
        r.pivot = src.pivot;
        r.anchoredPosition = src.anchoredPosition;
        r.sizeDelta = src.sizeDelta;

        if (_s.tabPromptSprite != null)
        {
            Image im = go.AddComponent<Image>();
            im.sprite = _s.tabPromptSprite;
            im.preserveAspect = true;
            im.raycastTarget = false;
        }
        else
        {
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            if (_ui.messageText != null)
                t.font = _ui.messageText.font;
            t.text = _s.tabPromptText;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.95f, 0.95f, 0.9f, 1f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 20f;
            t.fontSizeMax = 64f;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
        }
        go.AddComponent<BlinkingPrompt>();
        return go;
    }

    // ================= ズーム =================

    private void PrepareZoomBase()
    {
        if (_host != null)
            return;
        _host = _config.panelRoot.GetComponent<RectTransform>();
        if (_host == null)
            return;
        _scale0 = _host.localScale;
        _pos0 = _host.position;

        Canvas cv = _host.GetComponentInParent<Canvas>();
        RectTransform cr = cv != null ? (RectTransform)cv.rootCanvas.transform : _host;
        Vector3[] cc = new Vector3[4];
        cr.GetWorldCorners(cc);
        _cMin = new Vector2(cc[0].x, cc[0].y);
        _cMax = new Vector2(cc[2].x, cc[2].y);
        _cCenter = new Vector3((_cMin.x + _cMax.x) * 0.5f, (_cMin.y + _cMax.y) * 0.5f, _host.position.z);
        _margin = Mathf.Max(0f, _s.zoomMargin) * Mathf.Abs(cr.lossyScale.x);

        // もともと、背景が画面を覆っていたか（見切れを防ぐ補正のため）
        Vector2 bmin, bmax;
        GetBounds(_host, out bmin, out bmax);
        _coverX = bmin.x <= _cMin.x + 1f && bmax.x >= _cMax.x - 1f;
        _coverY = bmin.y <= _cMin.y + 1f && bmax.y >= _cMax.y - 1f;
    }

    private static void GetBounds(RectTransform rt, out Vector2 min, out Vector2 max)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        min = new Vector2(Mathf.Min(c[0].x, c[2].x), Mathf.Min(c[0].y, c[2].y));
        max = new Vector2(Mathf.Max(c[0].x, c[2].x), Mathf.Max(c[0].y, c[2].y));
    }

    private static Vector3 UnionCenter(RectTransform[] rects)
    {
        Vector2 mn = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 mx = new Vector2(float.MinValue, float.MinValue);
        Vector3 z = Vector3.zero;
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i] == null)
                continue;
            Vector2 a, b;
            GetBounds(rects[i], out a, out b);
            mn = Vector2.Min(mn, a);
            mx = Vector2.Max(mx, b);
            z = rects[i].position;
        }
        return new Vector3((mn.x + mx.x) * 0.5f, (mn.y + mx.y) * 0.5f, z.z);
    }

    /// <summary>背景が画面から外れないように、はみ出した分だけ位置を戻す。</summary>
    private void ClampCover()
    {
        if (!_coverX && !_coverY)
            return;
        Vector2 bmin, bmax;
        GetBounds(_host, out bmin, out bmax);
        float dx = 0f;
        float dy = 0f;
        if (_coverX)
        {
            if (bmin.x > _cMin.x) dx = _cMin.x - bmin.x;
            else if (bmax.x < _cMax.x) dx = _cMax.x - bmax.x;
        }
        if (_coverY)
        {
            if (bmin.y > _cMin.y) dy = _cMin.y - bmin.y;
            else if (bmax.y < _cMax.y) dy = _cMax.y - bmax.y;
        }
        if (dx != 0f || dy != 0f)
            _host.position += new Vector3(dx, dy, 0f);
    }

    /// <summary>required の全部が、画面の中（端から margin 以上）に入っているか。</summary>
    private bool AllInside(RectTransform[] required)
    {
        if (required == null)
            return true;
        for (int i = 0; i < required.Length; i++)
        {
            if (required[i] == null)
                continue;
            Vector2 a, b;
            GetBounds(required[i], out a, out b);
            if (a.x < _cMin.x + _margin || a.y < _cMin.y + _margin || b.x > _cMax.x - _margin || b.y > _cMax.y - _margin)
                return false;
        }
        return true;
    }

    /// <summary>focus の真ん中を画面の中央へ寄せる、拡大した状態を計算する。required が入りきらないときは、倍率を下げる。</summary>
    private void ComputeZoomTarget(RectTransform[] focus, RectTransform[] required, float k, out Vector3 scale, out Vector3 pos)
    {
        Vector3 curS = _host.localScale;
        Vector3 curP = _host.position;

        // 元の状態にもどして計算する（終わったら、今の状態にもどす）
        _host.localScale = _scale0;
        _host.position = _pos0;
        scale = _scale0;
        pos = _pos0;

        if (k > 1.001f && focus != null && focus.Length > 0)
        {
            Vector3 f = UnionCenter(focus);
            Vector3 fLocal = _host.InverseTransformPoint(f);
            float kk = k;
            for (int i = 0; i < 16; i++)
            {
                _host.localScale = _scale0 * kk;
                _host.position = _pos0;
                Vector3 current = _host.TransformPoint(fLocal);
                Vector3 delta = _cCenter - current;
                delta.z = 0f;
                _host.position += delta;
                ClampCover();
                if (kk <= 1.02f || AllInside(required))
                    break;
                kk = Mathf.Max(1f, kk - 0.04f);
            }
            scale = _host.localScale;
            pos = _host.position;
        }

        _host.localScale = curS;
        _host.position = curP;
    }

    private void StartZoom(RectTransform[] focus, RectTransform[] required, float k)
    {
        PrepareZoomBase();
        if (_host == null)
            return;
        Vector3 s, p;
        ComputeZoomTarget(focus, required, k, out s, out p);
        if (_zoomCo != null)
            StopCoroutine(_zoomCo);
        _zoomCo = StartCoroutine(TweenZoom(s, p, _s.zoomSeconds));
    }

    private void StartZoomBase()
    {
        if (_host == null)
            return;
        if (_zoomCo != null)
            StopCoroutine(_zoomCo);
        _zoomCo = StartCoroutine(TweenZoom(_scale0, _pos0, _s.zoomSeconds));
    }

    private IEnumerator WaitZoom()
    {
        while (_zooming)
            yield return null;
    }

    private IEnumerator TweenZoom(Vector3 s1, Vector3 p1, float seconds)
    {
        _zooming = true;
        Vector3 s0 = _host.localScale;
        Vector3 p0 = _host.position;
        float dur = Mathf.Max(0.01f, seconds);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            _host.localScale = Vector3.Lerp(s0, s1, k);
            _host.position = Vector3.Lerp(p0, p1, k);
            yield return null;
        }
        _host.localScale = s1;
        _host.position = p1;
        _zooming = false;
    }

    /// <summary>拡大と位置を、すぐ元に戻す（画面を閉じる前・片づけのとき）。</summary>
    private void RestoreZoomNow()
    {
        if (_zoomCo != null)
            StopCoroutine(_zoomCo);
        _zoomCo = null;
        _zooming = false;
        if (_host != null)
        {
            _host.localScale = _scale0;
            _host.position = _pos0;
        }
    }
}
