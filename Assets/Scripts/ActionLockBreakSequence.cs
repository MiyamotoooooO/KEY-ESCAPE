using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - アクション解放の演出。ステージクリアで新しく使えるようになったアクションの「ロック」を、
/// キーキャラクターが壊しに来る。
///
/// 流れ：
///   1. 結果画面が終わると（StageGoal が、ここに引き継ぐ）、キーコンフィグ画面が開く。
///      解放されたアクションの行は、まだ「ロック中」の見た目のまま。
///   2. 所持しているキーの中からランダムに選んだキーキャラクターが、画面の上から飛び降りてきて、ロックの上に着地する（着地の音・画面が揺れる）。
///   3. ロックの上で何度か跳ねて踏みつける（そのたびに、ヒビが増えて、ロックが揺れる。叩く音）。
///   4. 最後の一撃でロックが砕け散る（砕ける音）。行が解放の表示に変わり、光って、右下のプレビューで動きが流れる。
///   5. キーキャラクターが喜んで、消える。
///   6. 解放されたアクションが複数あるときは、2〜5を1つずつ順番に繰り返す（そのつど、別のキーがランダムに来る）。
///   7. 全部終わったら、キーコンフィグ画面が自動で閉じる。
///
/// Spaceキーで、演出を飛ばせる（飛ばしても解放は取り消されない。画面を閉じるだけ）。
/// キャラクターは、結果画面と同じ「小さな撮影場所」で撮った映像を、RawImageにして動かしている。時間はすべて実時間で数える。
///
/// シーンに無いときは、StageGoal が自動で作る。キャラクターのプレハブや「SPACEでスキップ」の案内が空のときは、
/// KeyConfigUnlockSequence に設定してあるものを使う。メニュー Tools > KEY ESCAPE > ロック解除演出 > シーンに追加する で、手動で付けることもできる。
/// </summary>
public class ActionLockBreakSequence : MonoBehaviour
{
    [Header("キャラクター")]
    [Tooltip("手足の生えたキー（Assets/Prefab/Mob/Enemy/KeyNPC_Visual）。空なら、KeyConfigUnlockSequence のものを使う。")]
    public GameObject keyCharacterPrefab;
    [Tooltip("キャラクターの拡大率。")]
    public float characterScale = 1f;
    [Tooltip("映像に映る高さ（ワールドの単位）。大きいほど、キャラクターが小さく映る。")]
    public float viewHeight = 2.2f;
    [Tooltip("キャラクターの大きさ（映像の縦横）。行の高さに対する倍率。")]
    public float characterSizeRatio = 2.4f;
    [Tooltip("キャラクターの大きさの下限（画面の単位）。行が低くても、これより小さくならない。")]
    public float minCharacterSize = 170f;
    [Tooltip("着地する位置の上下のずれ（画面の単位）。プラスで、ロックの上にもっと浮く。マイナスで、ロックにめり込む。")]
    public float landingOffsetY = 0f;

    [Header("飛び降りる")]
    [Tooltip("演出が始まってから（画面が開いて）、キャラクターが降り始めるまでの秒数。")]
    public float startDelay = 0.3f;
    [Tooltip("画面の上から、ロックの上に着地するまでの秒数。")]
    public float fallTime = 0.5f;
    [Tooltip("着地したときの、画面の揺れの大きさ（画面の単位）。0で揺れない。")]
    public float landShake = 14f;

    [Header("ズーム（降り始めてから、元に戻るまで、ロックの行へ寄る）")]
    [Tooltip("拡大の倍率。1.2なら、画面全体が1.2倍になる。1以下なら、ズームしない。")]
    public float zoomRatio = 1.2f;
    [Tooltip("寄るのにかける秒数。キャラクターが降り始めるのと同時に始まる。")]
    public float zoomInTime = 0.6f;
    [Tooltip("元の大きさに戻るのにかける秒数。喜んだあと、キャラクターが消え始めるのと同時に始まる。")]
    public float zoomOutTime = 0.4f;
    [Tooltip("ズームが元の大きさに戻ってから、次へ進むまでの秒数。画面全体を見渡せる時間。ズームしない設定（Zoom Ratioが1以下）のときは、使わない。")]
    public float afterZoomOutWait = 0.8f;
    [Tooltip("ロックの行を、画面の中央へどれだけ寄せるか（0〜1）。大きいほど、その行がこちらへ寄ってくる。")]
    [Range(0f, 1f)] public float zoomCenterPull = 0.3f;
    [Tooltip("ONなら、ズームしても、背景が画面から外れない（端が見切れない）ように、位置を自動で補正する。もともと画面を覆っていない方向は、補正しない。")]
    public bool zoomKeepCovering = true;
    [Tooltip("任意。画面を覆っている背景のオブジェクト。空なら、Panel Root を使う。Panel Root より背景の絵のほうが大きくはみ出しているときは、背景のImageを入れる。")]
    public RectTransform zoomCoverRect;

    [Header("ロックを壊す")]
    [Tooltip("ロックを踏む回数。最後の1回で砕ける。")]
    public int hitCount = 3;
    [Tooltip("踏みつけの跳ねる高さ。キャラクターの大きさに対する割合。")]
    public float stompHeight = 0.3f;
    [Tooltip("1回の踏みつけ（跳ねて、降りてくる）にかかる秒数。")]
    public float stompTime = 0.32f;
    [Tooltip("踏んでから、次に跳ねるまでの間の秒数。")]
    public float hitInterval = 0.12f;
    [Tooltip("踏んだときの、ロックの揺れの大きさ（画面の単位）。最後の一撃ほど大きくなる。")]
    public float lockShake = 10f;
    [Tooltip("踏んだときの、画面全体の揺れの大きさ（画面の単位）。最後の一撃ほど大きくなる。")]
    public float hitScreenShake = 8f;
    [Tooltip("揺れが収まるまでの秒数。")]
    public float shakeTime = 0.28f;

    [Header("砕け散る")]
    [Tooltip("砕けて飛び散る破片の数。")]
    public int shardCount = 24;
    [Tooltip("破片の色。空（アルファ0）なら、ロックの色を使う。")]
    public Color shardColor = new Color(0f, 0f, 0f, 0f);
    [Tooltip("破片が飛び散る勢い（画面の単位／秒）。")]
    public float shardSpeed = 700f;
    [Tooltip("破片が消えるまでの秒数。")]
    public float shardLife = 1f;
    [Tooltip("解放したとき、行が跳ねる大きさ（1で跳ねない）。")]
    public float rowPunch = 1.06f;
    [Tooltip("解放したあと、右下のプレビューで、そのアクションの動きを流す。")]
    public bool playPreview = true;

    [Header("壊したあと")]
    [Tooltip("壊してから、キャラクターが喜んでいる秒数。")]
    public float celebrateTime = 1.1f;
    [Tooltip("喜ぶ動き。キー達が踊るときと同じ設定（ランダムに切り替わる）。")]
    public KeyMotionSettings celebrateMotion = new KeyMotionSettings();
    [Tooltip("キャラクターが消えるのにかける秒数。")]
    public float leaveFadeTime = 0.3f;
    [Tooltip("1つ壊してから、次のアクションのキャラクターが降り始めるまでの秒数。")]
    public float betweenActions = 0.35f;
    [Tooltip("全部壊してから、キーコンフィグ画面を閉じるまでの秒数。")]
    public float endWait = 0.9f;

    [Header("スキップ（Spaceキー）")]
    [Tooltip("任意。「SPACEでスキップ」の案内。空なら、KeyConfigUnlockSequence のものを使う（それも空なら、案内は出ない）。")]
    public GameObject skipPrompt;
    [Tooltip("演出が始まってから、スキップを受け付けない秒数（結果画面を閉じたキーが、そのままスキップにならないように）。")]
    public float skipGuardTime = 0.3f;

    private const float StageY = -10000f;
    private const float IntroWaitMax = 4f;
    private const float FootPivot = 0.08f; // 撮影場所の映像で、足元がある高さ（下から）

    private bool _running;
    private KeyConfigUI _config;
    private GameObject _prefab;
    private GameObject _skipPromptObj;
    private float _startTime;
    private bool _lockedPlayer;
    private PlayerMovement _player;
    private ResultCharacterStage _stage;
    private GameObject _runner;
    private RectTransform _shardRoot;
    private KeyDefinition _lastKey;
    private readonly List<GameObject> _temp = new List<GameObject>();

    // 揺れ
    private RectTransform _hostRt;
    private Vector3 _hostPos0;
    private RectTransform _lockRt;
    private Vector3 _lockPos0;
    private float _hostShake;
    private float _lockShakeNow;
    private Vector3 _hostShakeOffset; // いま画面に足している揺れ（ズームと両立するため、足した分を覚えて引く）

    // ズーム
    private RectTransform _zoomHost;
    private Vector3 _zoomScale0;
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
    /// 演出を始める。始められたらtrue。始めるとき、ゲームを止める権利を取る（終わったら、ここで手放す）。
    /// 始められないとき（すでに演出中、解放するアクションが無い、キーコンフィグ画面やキャラクターが無い）は、何もせずfalseを返す。
    /// </summary>
    public bool TryPlay(IList<GameAction> actions)
    {
        if (_running || actions == null || actions.Count == 0 || !isActiveAndEnabled)
            return false;

        KeyConfigUI config = FindObjectOfType<KeyConfigUI>(true);
        if (config == null || config.panelRoot == null)
            return false;

        KeyConfigUnlockSequence other = FindObjectOfType<KeyConfigUnlockSequence>(true);
        GameObject prefab = keyCharacterPrefab != null ? keyCharacterPrefab : (other != null ? other.keyCharacterPrefab : null);
        if (prefab == null)
        {
            Debug.LogWarning("[ActionLockBreakSequence] キーキャラクターのプレハブが空のため、ロックを壊す演出をスキップします（Key Character Prefab を設定してください）。");
            return false;
        }
        _prefab = prefab;
        _skipPromptObj = skipPrompt != null ? skipPrompt : (other != null ? other.skipPrompt : null);

        // 解放するアクションの行を、演出が終わるまで、ロック中の見た目のままにしておく。
        List<GameAction> list = new List<GameAction>(actions);

        _running = true;
        _config = config;
        GamePause.Acquire(this);

        // ステージクリアの演出が、すでにプレイヤーの操作をロックしている。そのときは、触らない。
        _lockedPlayer = false;
        if (!StageClearDirector.IsPlaying)
        {
            _player = FindObjectOfType<PlayerMovement>();
            if (_player != null)
            {
                _player.SetInputLocked(true);
                _lockedPlayer = true;
            }
        }

        _startTime = Time.unscaledTime;
        SetSkipPrompt(true);
        StartCoroutine(Run(config, list));
        return true;
    }

    private void Update()
    {
        if (!_running || Time.unscaledTime - _startTime < skipGuardTime)
            return;
        if (Input.GetKeyDown(KeyCode.Space))
            Skip();
    }

    private void LateUpdate()
    {
        if (!_running)
            return;

        float dt = Time.unscaledDeltaTime;
        float decay = Mathf.Pow(0.001f, dt / Mathf.Max(0.05f, shakeTime));

        if (_hostRt != null)
        {
            // 揺れは、いまの位置に足す（ズームで動いた位置を、上書きしない）。足した分は覚えておいて、次に引く。
            Vector3 off = _hostShake > 0.05f ? (Vector3)(Random.insideUnitCircle * _hostShake) : Vector3.zero;
            _hostRt.localPosition += off - _hostShakeOffset;
            _hostShakeOffset = off;
            _hostShake = _hostShake > 0.05f ? _hostShake * decay : 0f;
        }
        if (_lockRt != null)
        {
            _lockRt.localPosition = _lockPos0 + (_lockShakeNow > 0.05f ? (Vector3)(Random.insideUnitCircle * _lockShakeNow) : Vector3.zero);
            _lockShakeNow = _lockShakeNow > 0.05f ? _lockShakeNow * decay : 0f;
        }
    }

    /// <summary>演出を打ち切って、キーコンフィグ画面を閉じる（解放はもう済んでいるので、取り消されない）。</summary>
    private void Skip()
    {
        if (!_running)
            return;
        StopAllCoroutines();
        Cleanup();
    }

    private void OnDestroy()
    {
        if (_running)
            Cleanup();
    }

    private void SetSkipPrompt(bool visible)
    {
        if (_skipPromptObj != null && _skipPromptObj.activeSelf != visible)
            _skipPromptObj.SetActive(visible);
    }

    // ---- 全体の流れ -----------------------------------------------------

    private IEnumerator Run(KeyConfigUI config, List<GameAction> actions)
    {
        try
        {
            // 解放するアクションの行を、ロック中の見た目のまま、画面を開く。
            config.SetForcedLockedActions(actions);
            config.SetInteractionLocked(true); // 演出の間は、画面を操作できない
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

            _hostRt = config.panelRoot.GetComponent<RectTransform>();
            if (_hostRt != null)
                _hostPos0 = _hostRt.localPosition;

            yield return WaitReal(startDelay);

            for (int i = 0; i < actions.Count; i++)
            {
                ActionBindingRow row = config.FindRow(actions[i]);
                if (row != null && row.gameObject.activeInHierarchy && _hostRt != null)
                    yield return BreakOne(config, row, actions[i]);
                else
                    config.ReleaseForcedLock(actions[i]);

                if (i < actions.Count - 1)
                    yield return WaitReal(betweenActions);
            }

            yield return WaitReal(endWait);
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// 演出の後始末（通常どおり終わったときも、Spaceでスキップしたときも、同じ）。
    /// 揺れを戻し、キャラクター・破片・撮影場所を消し、ロック表示の固定を外して、キーコンフィグ画面を閉じ、
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

        if (_hostRt != null)
            _hostRt.localPosition = _hostPos0;
        _hostRt = null;
        _hostShakeOffset = Vector3.zero;
        if (_lockRt != null)
            _lockRt.localPosition = _lockPos0;
        _lockRt = null;
        _hostShake = 0f;
        _lockShakeNow = 0f;

        if (_runner != null)
            Destroy(_runner);
        _runner = null;
        if (_shardRoot != null)
            Destroy(_shardRoot.gameObject);
        _shardRoot = null;
        for (int i = 0; i < _temp.Count; i++)
        {
            if (_temp[i] != null)
                Destroy(_temp[i]);
        }
        _temp.Clear();
        if (_stage != null)
            _stage.Dispose();
        _stage = null;

        SetSkipPrompt(false);

        if (_config != null)
        {
            _config.SetInteractionLocked(false);
            _config.ClearForcedLockedActions();
            _config.Close();
        }
        _config = null;

        if (_lockedPlayer && _player != null)
            _player.SetInputLocked(false);
        _lockedPlayer = false;
        GamePause.Release(this);
        _running = false;
    }

    // ---- 1つのロックを壊す -------------------------------------------------

    private IEnumerator BreakOne(KeyConfigUI config, ActionBindingRow row, GameAction action)
    {
        RectTransform host = _hostRt;
        RectTransform rowRt = (RectTransform)row.transform;
        RectTransform lockRt = row.lockedIndicator != null ? row.lockedIndicator.transform as RectTransform : null;
        bool hasLockObject = lockRt != null;
        if (lockRt == null)
            lockRt = rowRt;

        Canvas.ForceUpdateCanvases();
        Vector2 lockCenter, rowCenter;
        float lockW, lockH, lockTop, rowW, rowH, rowTop;
        RectInHost(host, lockRt, out lockCenter, out lockW, out lockH, out lockTop);
        RectInHost(host, rowRt, out rowCenter, out rowW, out rowH, out rowTop);

        float size = Mathf.Max(minCharacterSize, rowH * characterSizeRatio);
        Vector2 feetTarget = new Vector2(lockCenter.x, lockTop + landingOffsetY);

        // 揺らすのは、このロックだけ（元の位置を覚えておく）
        _lockRt = lockRt;
        _lockPos0 = lockRt.localPosition;

        // 小さな撮影場所と、キャラクター（所持キーからランダム）
        KeyDefinition key = PickKey();
        _lastKey = key;
        _stage = new ResultCharacterStage("ActionLockBreakStage", new Vector3(0f, StageY, 0f), 1, viewHeight, 1f, 384);
        GameObject go = Instantiate(_prefab);
        go.name = "ActionLockBreaker";
        _stage.Place(go, 0, false);
        Vector3 s = go.transform.localScale;
        go.transform.localScale = new Vector3(s.x * characterScale, s.y * characterScale, s.z * characterScale);

        KeyLabel[] labels = go.GetComponentsInChildren<KeyLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (key != null)
                labels[i].keyOverride = key;
            labels[i].Refresh();
        }
        LimbRig rig = go.GetComponentInChildren<LimbRig>(true);

        // 映像を、キーコンフィグ画面の一番手前に置く（足元を軸にする）
        GameObject view = new GameObject("ActionLockBreakerView", typeof(RectTransform));
        view.transform.SetParent(host, false);
        view.transform.SetAsLastSibling();
        RectTransform vr = (RectTransform)view.transform;
        vr.anchorMin = new Vector2(0.5f, 0.5f);
        vr.anchorMax = new Vector2(0.5f, 0.5f);
        vr.pivot = new Vector2(0.5f, FootPivot);
        vr.sizeDelta = new Vector2(size, size);
        RawImage raw = view.AddComponent<RawImage>();
        raw.texture = _stage.Texture;
        raw.uvRect = _stage.SlotUv(0);
        raw.raycastTarget = false;
        _runner = view;

        // ロックの行へ寄り始める（降り始めるのと同時）
        if (zoomRatio > 1f)
        {
            BeginZoom(host, lockRt);
            _zoomCoroutine = StartCoroutine(AnimateZoom(0f, 1f, zoomInTime));
        }

        // --- 画面の上から降りてくる（両腕を上げて） ---
        float startY = host.rect.yMax + 10f;
        vr.localPosition = new Vector3(feetTarget.x, startY, 0f);
        if (rig != null)
        {
            rig.SetManualVelocity(0f);
            rig.SetCheer(true);
        }

        float t = 0f;
        float dur = Mathf.Max(0.05f, fallTime);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            vr.localPosition = new Vector3(feetTarget.x, Mathf.Lerp(startY, feetTarget.y, k * k), 0f);
            yield return null;
        }
        vr.localPosition = new Vector3(feetTarget.x, feetTarget.y, 0f);

        // --- 着地 ---
        if (rig != null)
            rig.SetCheer(false);
        SoundEffects.Play(SEId.ActionLockLand);
        _hostShake = Mathf.Max(_hostShake, landShake);
        yield return Squash(vr, 1.28f, 0.72f, 0.07f, 0.14f);

        // --- 踏みつける ---
        int hits = Mathf.Max(1, hitCount);
        List<GameObject> cracks = new List<GameObject>();
        for (int i = 0; i < hits; i++)
        {
            bool last = (i == hits - 1);

            // ため（少し縮む）
            yield return Squash(vr, 1.08f, 0.86f, 0.08f, 0.0f);

            // 跳ねて、降りてくる
            if (rig != null)
                rig.SetCheer(true);
            float air = Mathf.Max(0.1f, stompTime);
            float h = size * Mathf.Max(0.05f, stompHeight) * (last ? 1.25f : 1f);
            vr.localScale = Vector3.one;
            t = 0f;
            while (t < air)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / air);
                vr.localPosition = new Vector3(feetTarget.x, feetTarget.y + h * 4f * u * (1f - u), 0f);
                yield return null;
            }
            vr.localPosition = new Vector3(feetTarget.x, feetTarget.y, 0f);
            if (rig != null)
                rig.SetCheer(false);

            // 踏んだ！
            float power = (i + 1f) / hits; // 最後の一撃ほど、強い
            _hostShake = Mathf.Max(_hostShake, hitScreenShake * power);
            _lockShakeNow = Mathf.Max(_lockShakeNow, lockShake * (0.6f + 0.6f * power));
            StartCoroutine(FlashOn(lockRt, last ? 0.95f : 0.7f, 0.22f));

            if (!last)
            {
                SoundEffects.Play(SEId.ActionLockHit);
                AddCracks(lockRt, lockW, lockH, 2 + i, cracks);
                yield return Squash(vr, 1.2f, 0.8f, 0.05f, 0.1f);
                yield return WaitReal(hitInterval);
            }
            else
            {
                // 最後の一撃：ロックが砕ける
                SoundEffects.Play(SEId.ActionLockBreak);
                SpawnShards(host, lockRt, lockCenter, lockW, lockH);
                for (int c = 0; c < cracks.Count; c++)
                {
                    if (cracks[c] != null)
                        Destroy(cracks[c]);
                }
                cracks.Clear();

                // ロックの揺れを止めて、元の位置に戻す
                _lockShakeNow = 0f;
                lockRt.localPosition = _lockPos0;
                _lockRt = null;

                // 行をロック中の見た目から、解放済みの見た目にする
                config.ReleaseForcedLock(action);
                StartCoroutine(FlashOn(rowRt, 0.7f, 0.35f));
                StartCoroutine(Punch(rowRt));
                if (playPreview)
                    config.PlayActionPreview(action);

                yield return Squash(vr, 1.25f, 0.75f, 0.06f, 0.14f);
            }
        }

        // ロックのオブジェクトが無い（行そのものを使った）ときは、揺れを止めて戻す
        if (!hasLockObject && _lockRt != null)
        {
            lockRt.localPosition = _lockPos0;
            _lockRt = null;
        }

        // --- 喜ぶ（ランダムな踊り） ---
        KeyMotionRandomizer.Begin(rig, celebrateMotion);
        yield return WaitReal(celebrateTime);

        // --- 元の大きさに戻り始めながら、キャラクターが消える ---
        if (_zoomed)
            _zoomCoroutine = StartCoroutine(AnimateZoom(1f, 0f, zoomOutTime));

        float f = 0f;
        float fd = Mathf.Max(0.01f, leaveFadeTime);
        while (f < fd)
        {
            f += Time.unscaledDeltaTime;
            raw.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(f / fd));
            yield return null;
        }
        Destroy(view);
        _runner = null;
        _stage.Dispose();
        _stage = null;

        // ズームが戻りきるのを待って、画面全体を見渡す時間をとる
        if (_zoomed)
        {
            while (_zooming)
                yield return null;
            _zoomCoroutine = null;
            RestoreZoom();
            yield return WaitReal(afterZoomOutWait);
        }
    }

    /// <summary>所持しているキーから、ランダムに1つ選ぶ（直前と同じキーは、なるべく避ける）。所持が無ければnull。</summary>
    private KeyDefinition PickKey()
    {
        KeyInventory inv = KeyInventory.Instance;
        if (inv == null || inv.OwnedKeys == null || inv.OwnedKeys.Count == 0)
            return null;
        int n = inv.OwnedKeys.Count;
        if (n == 1)
            return inv.OwnedKeys[0];
        for (int tries = 0; tries < 8; tries++)
        {
            KeyDefinition k = inv.OwnedKeys[Random.Range(0, n)];
            if (k != null && k != _lastKey)
                return k;
        }
        return inv.OwnedKeys[Random.Range(0, n)];
    }

    // ---- ズーム -----------------------------------------------------------

    /// <summary>画面全体（host）を、対象（ロックの行）を中心に拡大する準備。元の大きさを覚える。</summary>
    private void BeginZoom(RectTransform host, RectTransform targetRt)
    {
        _zoomHost = host;
        _zoomScale0 = host.localScale;
        _zoomFocusWorld = targetRt.TransformPoint(targetRt.rect.center);
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

    /// <summary>ズームの進み具合（0=元の大きさ、1=いちばん寄った状態）を反映する。ロックの行は、画面の中央へ少し寄っていく。</summary>
    private void ApplyZoom(float p)
    {
        if (_zoomHost == null)
            return;
        float k = Mathf.Lerp(1f, zoomRatio, p);
        _zoomHost.localScale = _zoomScale0 * k;

        Vector3 desired = Vector3.Lerp(_zoomFocusWorld, _zoomCenterWorld, zoomCenterPull * p);
        Vector3 current = _zoomHost.TransformPoint(_zoomFocusLocal);
        _zoomHost.position += desired - current;

        // 背景が画面から外れないように、はみ出した分だけ位置を戻す（端が見切れないように）。
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

    /// <summary>拡大と位置を、必ず元に戻す（次のアクションへ進む前と、画面を閉じる前に呼ぶ）。</summary>
    private void RestoreZoom()
    {
        if (_zoomHost != null && _zoomed)
        {
            _zoomHost.localScale = _zoomScale0;
            _zoomHost.localPosition = _hostPos0;
            _hostShakeOffset = Vector3.zero;
        }
        _zoomHost = null;
        _zoomed = false;
        _zooming = false;
    }

    // ---- 演出の部品 -----------------------------------------------------

    /// <summary>足元を軸に、いったん潰れて（またはのびて）、元の大きさに戻る。</summary>
    private static IEnumerator Squash(RectTransform rt, float sx, float sy, float inTime, float outTime)
    {
        if (rt == null)
            yield break;
        Vector3 to = new Vector3(sx, sy, 1f);
        float t = 0f;
        float d = Mathf.Max(0.001f, inTime);
        while (t < d && rt != null)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(Vector3.one, to, Mathf.Clamp01(t / d));
            yield return null;
        }
        if (rt == null)
            yield break;
        rt.localScale = to;
        if (outTime <= 0f)
            yield break;
        t = 0f;
        d = outTime;
        while (t < d && rt != null)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(to, Vector3.one, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d)));
            yield return null;
        }
        if (rt != null)
            rt.localScale = Vector3.one;
    }

    /// <summary>対象いっぱいに、白い板を重ねて、すぐ消す（光る）。</summary>
    private IEnumerator FlashOn(RectTransform target, float alpha, float duration)
    {
        if (target == null)
            yield break;
        GameObject flash = new GameObject("LockBreakFlash", typeof(RectTransform), typeof(LayoutElement));
        flash.transform.SetParent(target, false);
        flash.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform fr = (RectTransform)flash.transform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = Vector2.zero;
        fr.offsetMax = Vector2.zero;
        Image img = flash.AddComponent<Image>();
        img.raycastTarget = false;
        _temp.Add(flash);

        float t = 0f;
        float d = Mathf.Max(0.01f, duration);
        while (t < d && img != null)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(1f, 1f, 1f, alpha * (1f - Mathf.Clamp01(t / d)));
            yield return null;
        }
        if (flash != null)
            Destroy(flash);
    }

    private IEnumerator Punch(RectTransform rt)
    {
        if (rt == null || rowPunch <= 1f)
            yield break;
        Vector3 baseScale = rt.localScale;
        const float dur = 0.3f;
        float t = 0f;
        while (t < dur && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            rt.localScale = baseScale * (1f + (rowPunch - 1f) * k);
            yield return null;
        }
        if (rt != null)
            rt.localScale = baseScale;
    }

    /// <summary>ロックの上に、ヒビ（暗い細い線）を足す。ロックの子になるので、ロックと一緒に動いて、一緒に消える。</summary>
    private void AddCracks(RectTransform lockRt, float w, float h, int count, List<GameObject> list)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject c = new GameObject("LockCrack", typeof(RectTransform), typeof(LayoutElement));
            c.transform.SetParent(lockRt, false);
            c.GetComponent<LayoutElement>().ignoreLayout = true;
            RectTransform cr = (RectTransform)c.transform;
            cr.anchorMin = new Vector2(0.5f, 0.5f);
            cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            float len = Mathf.Max(24f, Mathf.Max(w, h) * Random.Range(0.35f, 0.7f));
            cr.sizeDelta = new Vector2(len, Random.Range(3f, 5f));
            cr.anchoredPosition = new Vector2(Random.Range(-0.3f, 0.3f) * w, Random.Range(-0.3f, 0.3f) * h);
            cr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
            Image img = c.AddComponent<Image>();
            img.color = new Color(0.08f, 0.06f, 0.05f, 0.92f);
            img.raycastTarget = false;
            list.Add(c);
            _temp.Add(c);
        }
    }

    /// <summary>ロックの破片を飛び散らせる。</summary>
    private void SpawnShards(RectTransform host, RectTransform lockRt, Vector2 center, float w, float h)
    {
        if (_shardRoot == null)
        {
            GameObject root = new GameObject("LockShards", typeof(RectTransform), typeof(LayoutElement));
            root.transform.SetParent(host, false);
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            _shardRoot = (RectTransform)root.transform;
            _shardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _shardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _shardRoot.sizeDelta = Vector2.zero;
            _shardRoot.localPosition = Vector3.zero;
        }
        _shardRoot.SetAsLastSibling();

        // 破片の色：指定があればそれ、無ければロックの色
        Color baseColor = shardColor;
        if (baseColor.a <= 0f)
        {
            baseColor = new Color(0.95f, 0.8f, 0.25f, 1f);
            Graphic g = lockRt != null ? lockRt.GetComponentInChildren<Graphic>(true) : null;
            if (g != null)
            {
                baseColor = g.color;
                baseColor.a = 1f;
            }
        }

        int n = Mathf.Max(1, shardCount);
        for (int i = 0; i < n; i++)
        {
            GameObject sh = new GameObject("Shard", typeof(RectTransform));
            sh.transform.SetParent(_shardRoot, false);
            RectTransform sr = (RectTransform)sh.transform;
            sr.anchorMin = new Vector2(0.5f, 0.5f);
            sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            float sz = Random.Range(10f, 28f);
            sr.sizeDelta = new Vector2(sz, sz * Random.Range(0.4f, 1f));
            sr.localPosition = new Vector3(center.x + Random.Range(-0.5f, 0.5f) * w, center.y + Random.Range(-0.5f, 0.5f) * h, 0f);
            sr.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Image img = sh.AddComponent<Image>();
            img.raycastTarget = false;
            float shade = Random.Range(0.65f, 1.15f);
            Color col = (Random.value < 0.2f) ? Color.white : new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 1f);
            img.color = col;

            Vector2 v = Random.insideUnitCircle * shardSpeed;
            v.y = Mathf.Abs(v.y) * 0.8f + shardSpeed * 0.35f;
            StartCoroutine(MoveShard(sr, img, v, Random.Range(-720f, 720f)));
        }
    }

    private IEnumerator MoveShard(RectTransform rt, Image img, Vector2 v, float spin)
    {
        const float gravity = -2200f;
        float life = Mathf.Max(0.2f, shardLife);
        float t = 0f;
        Color c = img.color;
        while (t < life && rt != null)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            v.y += gravity * dt;
            rt.localPosition += new Vector3(v.x * dt, v.y * dt, 0f);
            rt.Rotate(0f, 0f, spin * dt);
            float fade = Mathf.Clamp01((life - t) / (life * 0.4f));
            img.color = new Color(c.r, c.g, c.b, fade);
            yield return null;
        }
        if (rt != null)
            Destroy(rt.gameObject);
    }

    /// <summary>対象の、host内での中心・幅・高さ・上端の高さ。</summary>
    private static void RectInHost(RectTransform host, RectTransform rt, out Vector2 center, out float width, out float height, out float top)
    {
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 a = host.InverseTransformPoint(c[0]);
        Vector2 b = host.InverseTransformPoint(c[2]);
        center = (a + b) * 0.5f;
        width = Mathf.Abs(b.x - a.x);
        height = Mathf.Abs(b.y - a.y);
        top = Mathf.Max(a.y, b.y);
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
