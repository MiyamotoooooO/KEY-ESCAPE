using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - キーを獲得したあとの、キーコンフィグ画面での演出。
///
/// 流れ：
///   1. 「新しいキーを獲得した！」のポップアップを閉じると（KeyAcquiredPopup が、ここに引き継ぐ）、
///      ゲームを止めたまま、キーコンフィグ画面が開く（開始の演出も、いつもどおり再生される）。
///   2. 左のキー一覧で、新しいキーのマスだけ「？」と鍵の表示のまま。タブは「すべて」に切り替わり、そのマスが見える位置までスクロールする。
///   3. 画面の左の外から、キーキャラクター（手足つき）が走ってきて、そのマスの位置で止まる。
///   4. 手足が薄くなって消え、キーの本体だけが残る。
///   5. マスの表示が切り替わって、跳ねる（解放完了）。右の情報パネルに、そのキーの説明が出る。
///   6. 少し待ってから、キーコンフィグ画面が自動で閉じて、ゲームが再開する。
///
/// キャラクターは、結果画面と同じ「小さな撮影場所」で撮った映像を、RawImageにして動かしている。
/// 時間はすべて実時間（unscaled）で数える。
///
/// KeyAcquiredPopup と同じオブジェクトに付ける（メニュー Tools > KEY ESCAPE > キー解放演出 > シーンに追加する で、自動で付く）。
/// キーキャラクターのプレハブが空のとき、シーンにキーコンフィグ画面が無いときは、演出なし（ポップアップだけで終わる）。
/// </summary>
public class KeyConfigUnlockSequence : MonoBehaviour
{
    [Header("キャラクター")]
    [Tooltip("手足の生えたキー（Assets/Prefab/Mob/Enemy/KeyNPC_Visual）。空なら、演出なし。")]
    public GameObject keyCharacterPrefab;
    [Tooltip("キャラクターの拡大率。")]
    public float characterScale = 1f;
    [Tooltip("映像に映る高さ（ワールドの単位）。大きいほど、キャラクターが小さく映る。")]
    public float viewHeight = 2.2f;

    [Header("走る")]
    [Tooltip("キャラクターの大きさ（映像の縦横）。マスの高さに対する倍率。")]
    public float runnerSizeRatio = 1.8f;
    [Tooltip("キャラクターを置く縦の位置のずれ（画面の単位）。マスより上・下にずれて見えるときに調整する。")]
    public float runnerYOffset = 0f;
    [Tooltip("走る速さ（画面の単位／秒）。")]
    public float runSpeed = 900f;
    [Tooltip("走るアニメーションの速さ（LimbRigに渡す速度）。")]
    public float runAnimSpeed = 5f;

    [Header("ズーム（置き換わる少し前に、左の欄へ寄る）")]
    [Tooltip("拡大の倍率。1.2なら、画面全体が1.2倍になる。1以下なら、ズームしない。")]
    public float zoomRatio = 1.2f;
    [Tooltip("寄るのにかける秒数。手足が消え始めるのと同時に始まる。")]
    public float zoomInTime = 0.6f;
    [Tooltip("元の大きさに戻るのにかける秒数。解放後の待ち時間が終わってから戻る。")]
    public float zoomOutTime = 0.4f;
    [Tooltip("ズームが元の大きさに戻ってから、画面を閉じるまでの秒数。画面全体を見渡せる時間。ズームしない設定（Zoom Ratioが1以下）のときは、使わない。")]
    public float afterZoomOutWait = 1.2f;
    [Tooltip("新しいキーのマスを、画面の中央へどれだけ寄せるか（0〜1）。大きいほど、左の欄がこちらへ寄ってくる。")]
    [Range(0f, 1f)] public float zoomCenterPull = 0.3f;
    [Tooltip("ONなら、ズームしても、背景が画面から外れない（左端などが見切れない）ように、位置を自動で補正する。もともと画面を覆っていない方向は、補正しない。")]
    public bool zoomKeepCovering = true;
    [Tooltip("任意。画面を覆っている背景のオブジェクト。空なら、Panel Root を使う。Panel Root より背景の絵のほうが大きくはみ出しているときは、背景のImageを入れる。")]
    public RectTransform zoomCoverRect;

    [Header("止まってから")]
    [Tooltip("止まってから、手足が消え始めるまでの秒数。")]
    public float stopPause = 0.3f;
    [Tooltip("手足が消えるのにかける秒数。")]
    public float limbFadeTime = 0.7f;
    [Tooltip("キャラクターからマスの表示に切り替わる（キャラクターが消える）秒数。")]
    public float swapFadeTime = 0.25f;
    [Tooltip("解放したとき、マスが跳ねる大きさ（1で跳ねない）。")]
    public float tilePunch = 1.3f;
    [Tooltip("解放してから、キーコンフィグ画面を閉じるまでの秒数。")]
    public float afterRevealWait = 1.4f;

    [Header("スキップ（Spaceキー）")]
    [Tooltip("任意。「SPACEでスキップ」の案内（BlinkingPrompt を付けると点滅する）。演出が始まったときに出て、終わるかスキップすると消える。空なら案内は出ない。")]
    public GameObject skipPrompt;
    [Tooltip("演出が始まってから、スキップを受け付けない秒数（ポップアップを閉じたSpaceが、そのままスキップにならないように）。")]
    public float skipGuardTime = 0.3f;

    private const float StageY = -8000f;
    private const float IntroWaitMax = 4f;

    private bool _running;
    private KeyConfigUI _config;
    private KeyConfigFilter _prevFilter;
    private float _startTime;
    private PlayerMovement _player;
    private ResultCharacterStage _stage;
    private GameObject _runner;
    private ScrollRect _scroll;
    private bool _scrollWasEnabled;

    // ズーム
    private RectTransform _zoomHost;
    private Vector3 _zoomScale0;
    private Vector3 _zoomPos0;
    private Vector3 _zoomFocusWorld;
    private Vector3 _zoomFocusLocal;
    private Vector3 _zoomCenterWorld;
    private bool _zoomed;
    private bool _zooming;
    private Vector2 _coverMin;   // 画面（Canvas）の、ワールドでの左下
    private Vector2 _coverMax;   // 画面（Canvas）の、ワールドでの右上
    private bool _coverX;        // もともと、横方向に画面を覆っていたか
    private bool _coverY;        // もともと、縦方向に画面を覆っていたか
    private Coroutine _zoomCoroutine;

    /// <summary>演出の最中か。</summary>
    public bool IsRunning { get { return _running; } }

    /// <summary>
    /// 演出を始める。始められたらtrue。始めるとき、先にゲームを止める権利を取り、プレイヤーの操作もロックする
    /// （呼んだ側が、自分の分を手放しても、間でゲームが動かない）。演出が終わったら、ここで解除する。
    /// 始められないとき（すでに演出中、キーコンフィグ画面やキャラクターが無い）は、何もせずfalseを返す。
    /// </summary>
    public bool TryPlay(KeyDefinition key)
    {
        if (_running || key == null || keyCharacterPrefab == null || !isActiveAndEnabled)
            return false;

        KeyConfigUI config = FindObjectOfType<KeyConfigUI>(true);
        if (config == null || config.panelRoot == null || config.keyGridContent == null)
            return false;

        _running = true;
        GamePause.Acquire(this);
        _player = FindObjectOfType<PlayerMovement>();
        if (_player != null)
            _player.SetInputLocked(true);

        _startTime = Time.unscaledTime;
        SetSkipPrompt(true);
        StartCoroutine(Run(config, key));
        return true;
    }

    private void Update()
    {
        if (!_running || Time.unscaledTime - _startTime < skipGuardTime)
            return;
        if (Input.GetKeyDown(KeyCode.Space))
            Skip();
    }

    /// <summary>演出を打ち切って、キーコンフィグ画面を閉じ、ゲームを再開する（キーはすでに所持済みなので、解放は取り消されない）。</summary>
    private void Skip()
    {
        if (!_running)
            return;
        StopAllCoroutines();
        _zoomCoroutine = null;
        Cleanup();
    }

    private void SetSkipPrompt(bool visible)
    {
        if (skipPrompt != null && skipPrompt.activeSelf != visible)
            skipPrompt.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (_running)
        {
            if (_player != null)
                _player.SetInputLocked(false);
            GamePause.Release(this);
        }
        if (_stage != null)
            _stage.Dispose();
        _stage = null;
    }

    private IEnumerator Run(KeyConfigUI config, KeyDefinition key)
    {
        KeyConfigFilter prevFilter = config.currentFilter;
        _config = config;
        _prevFilter = prevFilter;
        try
        {
            // 新しいキーのマスだけ「？」のままにして、すべてのキーが見えるタブで、画面を開く。
            config.currentFilter = KeyConfigFilter.All;
            config.SetHiddenOwnedKey(key);
            config.Open();

            // 開始の演出（看板・線）が終わるのを待つ。
            yield return null;
            float t = 0f;
            while (config.introAnimator != null && config.introAnimator.IsPlaying && t < IntroWaitMax)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;

            KeyGridButtonUI tile = config.FindKeyTile(key);
            if (tile != null)
                yield return RunCharacter(config, key, tile);
            else
                config.RevealKey(key);

            // 解放したキーの説明を出して、少し見せる。
            yield return WaitReal(afterRevealWait);

            // ズームを元の大きさに戻す。
            if (_zoomed)
            {
                yield return AnimateZoom(1f, 0f, zoomOutTime);
                // 元の大きさに戻ったあと、画面全体を見渡せる時間。
                yield return WaitReal(afterZoomOutWait);
            }
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// 演出の後始末（通常どおり終わったときも、Spaceでスキップしたときも、同じ）。
    /// ズームを戻し、キャラクターと撮影場所を消し、マスの表示とタブを戻して、キーコンフィグ画面を閉じ、
    /// ゲームの停止と操作のロックを解除する。2回呼ばれても、2回目は何もしない。
    /// </summary>
    private void Cleanup()
    {
        if (!_running)
            return;

        if (_zoomCoroutine != null)
            StopCoroutine(_zoomCoroutine);
        _zoomCoroutine = null;
        RestoreZoom();

        if (_runner != null)
            Destroy(_runner);
        _runner = null;
        if (_stage != null)
            _stage.Dispose();
        _stage = null;
        if (_scroll != null)
            _scroll.enabled = _scrollWasEnabled;
        _scroll = null;

        SetSkipPrompt(false);

        if (_config != null)
        {
            _config.SetHiddenOwnedKey(null);
            _config.SetFilter(_prevFilter);
            _config.Close();
        }
        _config = null;

        if (_player != null)
            _player.SetInputLocked(false);
        GamePause.Release(this);
        _running = false;
    }

    // ---- キャラクターが走ってきて、置かれる -----------------------------------

    private IEnumerator RunCharacter(KeyConfigUI config, KeyDefinition key, KeyGridButtonUI tile)
    {
        RectTransform host = config.panelRoot.GetComponent<RectTransform>();
        RectTransform tileRt = (RectTransform)tile.transform;

        PrepareScroll(config, tileRt);
        yield return null;
        Canvas.ForceUpdateCanvases();

        float tileH;
        Vector2 target = TileCenter(host, tileRt, out tileH);
        float size = Mathf.Max(60f, tileH * runnerSizeRatio);

        // 小さな撮影場所と、キャラクター
        _stage = new ResultCharacterStage("KeyUnlockStage", new Vector3(0f, StageY, 0f), 1, viewHeight, 1f, 384);
        GameObject go = Instantiate(keyCharacterPrefab);
        go.name = "KeyUnlockKey";
        _stage.Place(go, 0, false);

        // もとの絵は左向き。右へ走るので、左右を反転する。
        Vector3 s = go.transform.localScale;
        go.transform.localScale = new Vector3(-s.x * characterScale, s.y * characterScale, s.z * characterScale);

        KeyLabel[] labels = go.GetComponentsInChildren<KeyLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].keyOverride = key;
            labels[i].Refresh();
        }
        LimbRig rig = go.GetComponentInChildren<LimbRig>(true);

        // 映像を、キーコンフィグ画面の一番手前に置く。
        GameObject view = new GameObject("KeyUnlockRunner", typeof(RectTransform));
        view.transform.SetParent(host, false);
        view.transform.SetAsLastSibling();
        RectTransform vr = (RectTransform)view.transform;
        vr.anchorMin = new Vector2(0.5f, 0.5f);
        vr.anchorMax = new Vector2(0.5f, 0.5f);
        vr.pivot = new Vector2(0.5f, 0.5f);
        vr.sizeDelta = new Vector2(size, size);
        RawImage raw = view.AddComponent<RawImage>();
        raw.texture = _stage.Texture;
        raw.uvRect = _stage.SlotUv(0);
        raw.raycastTarget = false;
        _runner = view;

        // 画面の左の外から、走り出す。
        float x = host.rect.xMin - size * 0.6f;
        vr.localPosition = new Vector3(x, target.y + runnerYOffset, 0f);
        if (rig != null)
            rig.SetManualVelocity(runAnimSpeed);

        while (true)
        {
            target = TileCenter(host, tileRt, out tileH);
            float dist = target.x - x;
            if (dist <= 2f)
                break;
            float slow = Mathf.Clamp(dist / 300f, 0.35f, 1f); // 着く手前で、少しゆっくりになる
            x = Mathf.MoveTowards(x, target.x, runSpeed * slow * Time.unscaledDeltaTime);
            vr.localPosition = new Vector3(x, target.y + runnerYOffset, 0f);
            yield return null;
        }
        vr.localPosition = new Vector3(target.x, target.y + runnerYOffset, 0f);

        // 止まる（マスの位置に置かれる）
        if (rig != null)
            rig.SetManualVelocity(0f);
        SoundEffects.Play(SEId.KeyUnlockPlace);
        yield return WaitReal(stopPause);

        // 手足が薄くなり始めるのと同時に、左の欄へ寄る
        if (zoomRatio > 1f)
        {
            BeginZoom(host, tileRt);
            _zoomCoroutine = StartCoroutine(AnimateZoom(0f, 1f, zoomInTime));
        }

        // 手足が薄くなって、キーの本体だけになる
        if (rig != null)
        {
            rig.FadeOutLimbs(limbFadeTime);
            float wait = 0f;
            while (!rig.LimbsGone && wait < limbFadeTime + 1f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            yield return WaitReal(limbFadeTime);
        }
        yield return WaitReal(0.15f);
        while (_zooming)
            yield return null; // ズームが終わってから、置き換える

        // 解放完了：マスの表示に切り替えて、跳ねる。キャラクターは消える。
        SoundEffects.Play(SEId.KeyUnlockComplete);
        config.RevealKey(key);
        config.SelectKey(key);
        StartCoroutine(PunchTile(tileRt));
        StartCoroutine(FlashTile(tileRt));

        float f = 0f;
        float dur = Mathf.Max(0.01f, swapFadeTime);
        while (f < dur)
        {
            f += Time.unscaledDeltaTime;
            raw.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(f / dur));
            yield return null;
        }
        raw.color = new Color(1f, 1f, 1f, 0f);
    }

    // ---- ズーム -----------------------------------------------------------

    /// <summary>画面全体（host）を、マスを中心に拡大する準備。元の大きさと位置を覚える。</summary>
    private void BeginZoom(RectTransform host, RectTransform tileRt)
    {
        _zoomHost = host;
        _zoomScale0 = host.localScale;
        _zoomPos0 = host.position;
        _zoomFocusWorld = tileRt.TransformPoint(tileRt.rect.center);
        _zoomFocusLocal = host.InverseTransformPoint(_zoomFocusWorld);

        Canvas canvas = host.GetComponentInParent<Canvas>();
        RectTransform cr = canvas != null ? (RectTransform)canvas.rootCanvas.transform : host;
        _zoomCenterWorld = cr.TransformPoint(cr.rect.center);

        // 画面（Canvas）の範囲と、もともと背景が画面を覆っていたかを覚える（見切れを防ぐ補正のため）。
        Vector3[] cc = new Vector3[4];
        cr.GetWorldCorners(cc);
        _coverMin = new Vector2(cc[0].x, cc[0].y);
        _coverMax = new Vector2(cc[2].x, cc[2].y);
        Vector2 bgMin, bgMax;
        GetCoverBounds(out bgMin, out bgMax);
        const float eps = 1f;
        _coverX = bgMin.x <= _coverMin.x + eps && bgMax.x >= _coverMax.x - eps;
        _coverY = bgMin.y <= _coverMin.y + eps && bgMax.y >= _coverMax.y - eps;

        _zoomed = true;
    }

    /// <summary>背景（Cover Rect。空ならhost）の、ワールドでの範囲。</summary>
    private void GetCoverBounds(out Vector2 min, out Vector2 max)
    {
        RectTransform bg = zoomCoverRect != null ? zoomCoverRect : _zoomHost;
        Vector3[] c = new Vector3[4];
        bg.GetWorldCorners(c);
        min = new Vector2(Mathf.Min(c[0].x, c[2].x), Mathf.Min(c[0].y, c[2].y));
        max = new Vector2(Mathf.Max(c[0].x, c[2].x), Mathf.Max(c[0].y, c[2].y));
    }

    /// <summary>ズームの進み具合（0=元の大きさ、1=いちばん寄った状態）を反映する。マスは、画面の中央へ少し寄っていく。</summary>
    private void ApplyZoom(float p)
    {
        if (_zoomHost == null)
            return;
        float k = Mathf.Lerp(1f, zoomRatio, p);
        _zoomHost.localScale = _zoomScale0 * k;

        Vector3 desired = Vector3.Lerp(_zoomFocusWorld, _zoomCenterWorld, zoomCenterPull * p);
        Vector3 current = _zoomHost.TransformPoint(_zoomFocusLocal);
        _zoomHost.position += desired - current;

        // 背景が画面から外れないように、はみ出した分だけ位置を戻す（左端などが見切れないように）。
        if (zoomKeepCovering && (_coverX || _coverY))
        {
            Vector2 bgMin, bgMax;
            GetCoverBounds(out bgMin, out bgMax);
            float dx = 0f;
            float dy = 0f;
            if (_coverX)
            {
                if (bgMin.x > _coverMin.x) dx = _coverMin.x - bgMin.x;
                else if (bgMax.x < _coverMax.x) dx = _coverMax.x - bgMax.x;
            }
            if (_coverY)
            {
                if (bgMin.y > _coverMin.y) dy = _coverMin.y - bgMin.y;
                else if (bgMax.y < _coverMax.y) dy = _coverMax.y - bgMax.y;
            }
            if (dx != 0f || dy != 0f)
                _zoomHost.position += new Vector3(dx, dy, 0f);
        }
    }

    private IEnumerator AnimateZoom(float from, float to, float duration)
    {
        _zooming = true;
        float dur = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            ApplyZoom(Mathf.Lerp(from, to, k));
            yield return null;
        }
        ApplyZoom(to);
        _zooming = false;
    }

    /// <summary>拡大と位置を、必ず元に戻す（画面を閉じる前に呼ぶ）。</summary>
    private void RestoreZoom()
    {
        if (_zoomHost != null && _zoomed)
        {
            _zoomHost.localScale = _zoomScale0;
            _zoomHost.position = _zoomPos0;
        }
        _zoomHost = null;
        _zoomed = false;
        _zooming = false;
    }

    /// <summary>マスの中心（画面の座標・hostの中での位置）と、高さ。</summary>
    private static Vector2 TileCenter(RectTransform host, RectTransform tileRt, out float height)
    {
        Vector3[] c = new Vector3[4];
        tileRt.GetWorldCorners(c);
        Vector2 a = host.InverseTransformPoint(c[0]);
        Vector2 b = host.InverseTransformPoint(c[2]);
        height = Mathf.Abs(b.y - a.y);
        return (a + b) * 0.5f;
    }

    /// <summary>マスが、一覧の見える範囲の真ん中に来るようにスクロールして、そのあとは動かないようにする。</summary>
    private void PrepareScroll(KeyConfigUI config, RectTransform tileRt)
    {
        _scroll = config.keyGridContent.GetComponentInParent<ScrollRect>();
        if (_scroll == null || _scroll.content == null)
        {
            _scroll = null;
            return;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform view = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;

        Vector3 tileLocal = view.InverseTransformPoint(tileRt.TransformPoint(tileRt.rect.center));
        Vector2 delta = (Vector2)view.rect.center - (Vector2)tileLocal;
        if (!_scroll.horizontal) delta.x = 0f;
        if (!_scroll.vertical) delta.y = 0f;

        _scroll.StopMovement();
        _scroll.content.anchoredPosition += delta;
        if (_scroll.vertical)
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition);
        if (_scroll.horizontal)
            _scroll.horizontalNormalizedPosition = Mathf.Clamp01(_scroll.horizontalNormalizedPosition);

        // 演出の間は、スクロールで位置がずれないようにする。
        _scrollWasEnabled = _scroll.enabled;
        _scroll.enabled = false;
    }

    // ---- マスの演出 -----------------------------------------------------

    private IEnumerator PunchTile(RectTransform rt)
    {
        if (rt == null || tilePunch <= 1f)
            yield break;
        Vector3 baseScale = rt.localScale;
        const float dur = 0.3f;
        float t = 0f;
        while (t < dur && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            rt.localScale = baseScale * (1f + (tilePunch - 1f) * k);
            yield return null;
        }
        if (rt != null)
            rt.localScale = baseScale;
    }

    private IEnumerator FlashTile(RectTransform rt)
    {
        if (rt == null)
            yield break;
        GameObject flash = new GameObject("UnlockFlash", typeof(RectTransform), typeof(LayoutElement));
        flash.transform.SetParent(rt, false);
        flash.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform fr = (RectTransform)flash.transform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = Vector2.zero;
        fr.offsetMax = Vector2.zero;
        Image img = flash.AddComponent<Image>();
        img.raycastTarget = false;

        const float dur = 0.4f;
        float t = 0f;
        while (t < dur && img != null)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(1f, 1f, 1f, 0.85f * (1f - Mathf.Clamp01(t / dur)));
            yield return null;
        }
        if (flash != null)
            Destroy(flash);
    }

    private static IEnumerator WaitReal(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
