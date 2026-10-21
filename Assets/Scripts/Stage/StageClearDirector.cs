using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - ステージクリアの演出（ステージのシーンに、1つ置く）。
///
/// StageGoalがクリアになると、StageGoalから呼ばれる。流れ：
///   1. ゴール：時間がスローになり、白いフラッシュが光り、カメラがプレイヤーに寄る。ステージのBGMは消えていく。
///   2. さらにスローになり（もう一度、弱めのフラッシュが光る）、カメラがもう少し寄る。
///   3. 紙吹雪が降り、「STAGE CLEAR!」の看板が出る。クリア用のBGMと効果音が鳴り、スローが解除される。
///   4. このステージで入手したキーが、拾った順に、左から走ってきて、プレイヤーの左側に並び、手を上げて喜ぶ（声が鳴り、頭の上にエモートが出る）。
///   5. 結果画面が出る（看板が左上へ移り、クリアタイム・死亡シーン・回収したキーが出る）。「拠点に戻る」を押すと、StageGoalに戻る（StageGoalが拠点へ戻る）。
///      結果画面のプレハブ（Result Panel Prefab）が空のときは、しばらく待ってから戻る。
/// 演出の間、プレイヤーは操作できず、ダメージも受けない（無敵）。
///
/// 時間はすべて「実時間」で数えるので、スロー中でも演出がずれない。
/// 数値は、下の「調整用の数値」を変えると、演出が変わる。
/// 効果音は SoundEffects の StageClearGoal / StageClearGoal2 / StageClearConfetti / StageClearJingle / StageClearKeyVoice に clip を入れると鳴る。
///
/// セットアップ：ステージのシーンに空のオブジェクトを作り、このスクリプトを付ける。
///   Clear Sprite（看板の画像）、Confetti Sprites（紙吹雪の画像）、Clear Bgm は、空でも動く。
/// </summary>
public class StageClearDirector : MonoBehaviour
{
    // ================= 調整用の数値（ここを変えると演出が変わる） =================

    // --- ゴール（1回目のスロー） ---
    const float SlowScale1     = 0.25f; // 時間の速さ（0.25 = 1/4）
    const float SlowTime1      = 0.9f;  // この秒数（実時間）のあいだ
    const float ZoomRatio1     = 0.8f;  // カメラが寄る量（0.8 = 画面の広さが8割になる）
    const float ZoomTime1      = 0.7f;  // 寄るのにかける秒数
    const float FlashAlpha     = 0.9f;  // 白フラッシュの最初の濃さ
    const float FlashFade      = 0.4f;  // フラッシュが消える秒数
    const float BgmFadeOut     = 0.8f;  // ステージのBGMが消える秒数

    // --- さらにスロー（2回目のスロー） ---
    const float SlowScale2     = 0.08f; // 時間の速さ
    const float SlowTime2      = 0.9f;  // この秒数（実時間）のあいだ
    const float ZoomRatio2     = 0.65f; // カメラがさらに寄る量
    const float ZoomTime2      = 0.9f;  // 寄るのにかける秒数
    const float Flash2Alpha    = 0.6f;  // 2回目の白フラッシュの最初の濃さ（さらにスローになる瞬間）

    // --- スロー解除 ---
    const float ReleaseTime    = 0.35f; // 通常の速さへ戻していく秒数
    const float ResetZoomTime  = 0.9f;  // カメラを元の大きさへ戻す秒数

    // --- 看板（STAGE CLEAR!） ---
    const float SignWidth      = 960f;  // 看板の幅（基準解像度1920x1080でのピクセル）
    const float SignY          = 246f;  // 看板の高さ（画面の中心から上へ）
    const float SignTilt       = 2f;    // 看板の傾き（度）
    const float SignPopTime    = 0.45f; // ぽんと出る秒数
    const float SignMoveTime   = 0.6f;  // 結果画面が出る時に、看板が左上へ移る秒数
    // 結果画面のプレハブに Sign Anchor が無い時の、看板の移り先
    static readonly Vector2 SignFallbackPos = new Vector2(-420f, 350f);
    const float SignFallbackScale = 0.86f;
    const float SignFallbackTilt  = 4f;

    // --- 結果画面 ---
    const float ResultDelayNoKeys = 0.8f; // キーが無い時：スロー解除のあと、結果が出るまでの秒数
    const int   ResultConfettiBurst = 60;   // クリアタイムの集計が終わった時に出る紙吹雪の数
    const float ResultConfettiRain  = 1.8f; // そのあと降らせ続ける秒数（1秒あたりの数は、ConfettiPerSec）

    // --- 紙吹雪 ---
    const int   ConfettiBurst  = 70;    // 最初に出る数
    const float ConfettiRain   = 3.0f;  // そのあと降らせ続ける秒数
    const float ConfettiPerSec = 28f;   // 降らせ続けている間の、1秒あたりの数
    const int   ConfettiMax    = 180;   // 同時に出る最大数
    const float FallSpeedMin   = 380f;  // 落ちる速さ（ピクセル/秒）
    const float FallSpeedMax   = 760f;

    // --- キー（回収したキーが走ってきて、喜ぶ） ---
    const float KeyFirstDelay    = 0.6f;  // 看板が出てから、最初のキーが出てくるまでの秒数
    const float KeyStartInterval = 0.45f; // 次のキーが出てくるまでの間隔（秒）
    const float KeyRunSpeed      = 7f;    // 走る速さ（ワールドの単位/秒）
    const float KeyNearGap       = 1.4f;  // プレイヤーと、いちばん近いキーの間の距離
    const float KeySpacing       = 1.3f;  // キー同士の間隔（数が多い時は、自動で詰める）
    const float KeyMinSpacing    = 0.5f;  // 詰める時の、最小の間隔
    const float KeyOffscreen     = 3f;    // 画面の左端から、この距離だけ外で走り出す
    const float KeyCheerDelay    = 0.15f; // 止まってから、手を上げるまでの秒数
    const int   KeyVoiceCount    = 3;     // 喜ぶ時の声の回数
    const float KeyVoiceInterval = 0.09f; // 声の間隔（秒）
    const float KeyWaitLimit     = 20f;   // キーがそろうのを待つ、最大の秒数（念のため）
    const float LandWaitLimit    = 3f;    // プレイヤーが空中でゴールした時に、地面に降りるのを待つ最大の秒数
    const float LandStillTime    = 0.15f; // 縦の動きが止まったままの時間がこれ以上なら、降りたとみなす

    // 読み込み直後などの長いフレームで演出が飛ばないよう、1フレームの最大時間を決めておく
    const float MaxDt = 0.05f;

    // ==============================================================================

    [Header("看板")]
    [Tooltip("「STAGE CLEAR!」の看板の画像。空なら、文字で出す。")]
    public Sprite clearSprite;
    [Tooltip("看板を文字で出す時のフォント。空なら、シーン内の文字からフォントを借りる。")]
    public TMP_FontAsset font;

    [Header("紙吹雪")]
    [Tooltip("紙吹雪の画像（色違いを、いくつか入れる）。空なら、赤・黄・青の四角を、コードで作って降らせる。")]
    public Sprite[] confettiSprites;
    [Tooltip("紙吹雪1枚の高さ（基準解像度でのピクセル）。大きさは、少しずつばらつく。")]
    public float confettiSize = 40f;

    [Header("BGM")]
    [Tooltip("クリアした時に流すBGM。空なら、BGMはステージの曲が消えるだけで、新しい曲は流さない。")]
    public AudioClip clearBgm;
    public bool clearBgmLoop = true;

    [Header("キー（回収したキーが走ってきて、喜ぶ）")]
    [Tooltip("走ってくるキーの見た目。Assets/Prefab/Mob/Enemy/KeyNPC_Visual を入れる。空なら、キーは出ない。")]
    public GameObject keyPrefab;
    [Tooltip("キーの見た目が、右向きの絵ならオン。（KeyNPC_Visualは左向きなので、オフのままでよい。後ろ向きに走って見えたら、切り替える）")]
    public bool keyFacesRight = false;
    [Tooltip("キーの大きさの倍率。")]
    public float keyScale = 1f;

    [Header("キーのエモート（キーが手を上げた時に、頭の上に出す）")]
    [Tooltip("エモートのプレハブ（Assets/Prefab/Emotes）。何個でも入れられる。キーごとに、ランダムで1つ出す（一巡するまで、同じものは出さない）。空なら、エモートは出さない。")]
    public GameObject[] cheerEmotePrefabs;
    [Tooltip("エモートを出す位置（キーの足元からのずれ）。頭の上に来るように調整する。")]
    public Vector3 cheerEmoteOffset = new Vector3(0f, 1.6f, 0f);
    [Tooltip("エモートの大きさの倍率。")]
    public float cheerEmoteScale = 1f;
    [Tooltip("エモートを出しておく秒数。0なら、シーンが変わるまで出し続ける。")]
    public float cheerEmoteDuration = 0f;
    [Tooltip("エモートの描く順番の底上げ。大きいほど手前に描く（キャラに隠れないように）。")]
    public int cheerEmoteSortingBoost = 100;

    [Header("結果画面")]
    [Tooltip("結果画面のプレハブ（Assets/Prefab/UI/StageResultPanel）。Editorのメニュー「Tools > KEY ESCAPE > リザルト > 結果画面のプレハブを作る」で作れる。空なら、結果画面は出さず、下の秒数だけ待って戻る。")]
    public StageResultPanel resultPanelPrefab;
    [Tooltip("キーが出た時：最後のキーが手を上げてから、結果画面が出るまでの秒数（実時間）。キーとプレイヤーが、踊って喜ぶ時間。")]
    public float resultDelayAfterCheer = 2.5f;

    [Header("演出のあと（結果画面が無い時だけ使う）")]
    [Tooltip("キーを1つも拾っていない時：看板が出てから、StageGoalに戻るまでの秒数（実時間）。")]
    public float holdAfterSign = 3f;
    [Tooltip("キーが出た時：最後のキーが手を上げてから、StageGoalに戻るまでの秒数（実時間）。")]
    public float holdAfterCheer = 3f;

    /// <summary>演出の最中か。</summary>
    public static bool IsPlaying { get; private set; }

    private class Piece
    {
        public RectTransform rt;
        public float vy;
        public float sway;
        public float swayAmp;
        public float swaySpeed;
        public float spin;
    }

    private Image _flash;
    private RectTransform _confettiRoot;
    private RectTransform _signRect;
    private RectTransform _resultHost;
    private Coroutine _confettiMover;
    private float _rainLeft;
    private float _rainRate;
    private readonly List<Piece> _pieces = new List<Piece>();

    private int _keysDone;
    private readonly List<int> _emoteBag = new List<int>();
    private GameObject _player;
    private CharacterController2D _controller;
    private bool _slowActive;
    private float _baseFixedDelta = 0.02f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPlaying = false;
    }

    private void OnDestroy()
    {
        RestoreTime();
        IsPlaying = false;
    }

    /// <summary>演出を始める。keysは、このステージで入手したキー（走ってきて喜ぶ）。終わったら、onFinishedが呼ばれる。</summary>
    public void Play(List<KeyDefinition> keys, System.Action onFinished)
    {
        Play(keys, false, null, onFinished);
    }

    public void Play(List<KeyDefinition> keys, bool newRecord, System.Action onFinished)
    {
        Play(keys, newRecord, null, onFinished);
    }

    /// <summary>
    /// 演出を始める。newRecordは、ベストタイムを更新したか（結果画面のNEW RECORD!）。
    /// 結果画面が出る場合は、「拠点に戻る」が押されたあとに、onFinishedが呼ばれる。
    /// </summary>
    public void Play(List<KeyDefinition> keys, bool newRecord, List<string> rewards, System.Action onFinished)
    {
        if (IsPlaying)
            return;
        IsPlaying = true;
        StartCoroutine(Run(keys, newRecord, rewards, onFinished));
    }

    // ================= 演出の流れ =================

    private IEnumerator Run(List<KeyDefinition> keys, bool newRecord, List<string> rewards, System.Action onFinished)
    {
        _baseFixedDelta = Time.fixedDeltaTime;
        LockPlayer();
        BuildOverlay();

        CameraFollow cam = FindObjectOfType<CameraFollow>();
        if (cam != null && cam.Target == null && _player != null)
            cam.Target = _player.transform;

        // --- ゴール：スロー、フラッシュ、カメラが寄る ---
        SoundEffects.Play(SEId.StageClearGoal);
        AudioManager.StopBGM(BgmFadeOut);
        SetFlash(FlashAlpha);
        SetTimeScale(SlowScale1);
        if (cam != null)
        {
            cam.ShakeCamera(0.25f, cam.shakeAmount);
            cam.ZoomTo(ZoomRatio1, ZoomTime1);
        }

        float t = 0f;
        bool secondSlow = false;
        float total = SlowTime1 + SlowTime2;
        while (t < total)
        {
            t += Dt();
            KeepSafe();

            // --- さらにスロー（2回目のフラッシュと音） ---
            if (!secondSlow && t >= SlowTime1)
            {
                secondSlow = true;
                SoundEffects.Play(SEId.StageClearGoal2);
                SetTimeScale(SlowScale2);
                if (cam != null)
                    cam.ZoomTo(ZoomRatio2, ZoomTime2);
            }

            // フラッシュは、1回目（ゴール）と2回目（さらにスロー）の濃いほうを出す。
            float flash1 = FlashAlpha * (1f - Mathf.Clamp01(t / FlashFade));
            float flash2 = secondSlow ? Flash2Alpha * (1f - Mathf.Clamp01((t - SlowTime1) / FlashFade)) : 0f;
            SetFlash(Mathf.Max(flash1, flash2));
            yield return null;
        }
        SetFlash(0f);

        // --- 紙吹雪、看板、BGM。スローを解除して、カメラを戻す ---
        if (cam != null)
            cam.ZoomTo(1f, ResetZoomTime);

        SoundEffects.Play(SEId.StageClearConfetti);
        SoundEffects.Play(SEId.StageClearJingle);
        if (clearBgm != null)
            AudioManager.PlayBGM(clearBgm, 0.3f, clearBgmLoop);

        StartConfetti(ConfettiBurst, ConfettiRain, ConfettiPerSec);
        StartCoroutine(PopSignRoutine());

        float r = 0f;
        while (r < ReleaseTime)
        {
            r += Dt();
            KeepSafe();
            SetTimeScale(Mathf.Lerp(SlowScale2, 1f, Mathf.Clamp01(r / ReleaseTime)));
            yield return null;
        }
        RestoreTime();

        // --- キーが走ってきて、喜ぶ ---
        bool hasKeys = keys != null && keys.Count > 0;
        if (hasKeys && keyPrefab == null)
        {
            Debug.LogWarning("[StageClearDirector] Key Prefabが空のため、回収したキーを走らせられません。Assets/Prefab/Mob/Enemy/KeyNPC_Visual を入れてください。");
            hasKeys = false;
        }
        if (hasKeys && _player != null)
            yield return StartCoroutine(RunKeys(keys));
        else
            hasKeys = false;

        if (resultPanelPrefab != null)
        {
            // --- 結果画面：「拠点に戻る」が押されるまで待つ ---
            float d = 0f;
            float delay = hasKeys ? resultDelayAfterCheer : ResultDelayNoKeys;
            while (d < delay)
            {
                d += Dt();
                KeepSafe();
                yield return null;
            }
            yield return StartCoroutine(ShowResult(keys, newRecord, rewards));
        }
        else
        {
            // --- 結果画面が無い時：しばらく待つ ---
            float h = 0f;
            float wait = hasKeys ? holdAfterCheer : holdAfterSign;
            while (h < wait)
            {
                h += Dt();
                KeepSafe();
                yield return null;
            }
        }

        if (onFinished != null)
            onFinished();
    }

    // ================= 結果画面 =================

    private IEnumerator ShowResult(List<KeyDefinition> keys, bool newRecord, List<string> rewards)
    {
        EnsureEventSystem(); // ボタンをクリックできるように

        StageResultPanel panel = Instantiate(resultPanelPrefab, _resultHost, false);
        panel.SetKeyCharacter(keyPrefab, keyFacesRight, keyScale); // キーの見た目は、走ってきたキーと同じものを使う
        panel.SetRewards(rewards); // 新しく解放された操作（「覚えた！」の表示）
        panel.TimeCounted += OnResultTimeCounted;
        Canvas.ForceUpdateCanvases();
        StartCoroutine(MoveSignToResult(panel));

        yield return StartCoroutine(panel.Play(keys, newRecord));

        // 「拠点に戻る」が押されるまで待つ（クリックか、Enter）
        while (panel != null && !panel.Confirmed)
        {
            KeepSafe();
            yield return null;
        }
    }

    /// <summary>クリアタイムの集計が終わった時：紙吹雪を出す。</summary>
    private void OnResultTimeCounted()
    {
        SoundEffects.Play(SEId.StageClearConfetti);
        StartConfetti(ResultConfettiBurst, ResultConfettiRain, ConfettiPerSec);
    }

    /// <summary>画面にEventSystemが無いと、ボタンをクリックできないので、無ければ作る。</summary>
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindObjectOfType<EventSystem>() != null)
            return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>看板を、画面の中央から、結果画面の左上（Sign Anchor の位置）へ移す。</summary>
    private IEnumerator MoveSignToResult(StageResultPanel panel)
    {
        if (_signRect == null)
            yield break;

        Vector2 toPos = SignFallbackPos;
        float toScale = SignFallbackScale;
        float toTilt = SignFallbackTilt;

        RectTransform anchor = panel != null ? panel.signAnchor : null;
        RectTransform parent = _signRect.parent as RectTransform;
        if (anchor != null && parent != null)
        {
            Vector3 local = parent.InverseTransformPoint(anchor.position);
            toPos = new Vector2(local.x, local.y);
            float signWidth = Mathf.Max(1f, _signRect.rect.width * parent.lossyScale.x);
            toScale = anchor.rect.width * anchor.lossyScale.x / signWidth;
            toTilt = anchor.eulerAngles.z;
            if (toTilt > 180f)
                toTilt -= 360f;
        }

        Vector2 fromPos = _signRect.anchoredPosition;
        float fromScale = _signRect.localScale.x;
        float t = 0f;
        while (t < SignMoveTime)
        {
            t += Dt();
            if (_signRect == null)
                yield break;
            float k = Mathf.Clamp01(t / SignMoveTime);
            k = k * k * (3f - 2f * k);
            _signRect.anchoredPosition = Vector2.Lerp(fromPos, toPos, k);
            _signRect.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, k);
            _signRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(SignTilt, toTilt, k));
            yield return null;
        }
    }

    private static float Dt()
    {
        return Mathf.Min(Time.unscaledDeltaTime, MaxDt);
    }

    // ================= キー（走ってきて、喜ぶ） =================

    /// <summary>回収したキーを、左から順に走らせて、プレイヤーの左側に並べる。全員が手を上げるまで待つ。</summary>
    private IEnumerator RunKeys(List<KeyDefinition> keys)
    {
        _keysDone = 0;

        // プレイヤーが空中でゴールした時は、地面に降りて止まるまで待つ。
        // （空中の高さで、キーの足元の高さと並べる位置を決めないため）
        yield return StartCoroutine(WaitForPlayerLanding());

        // 足元の高さ：プレイヤーの当たり判定の、いちばん下
        float groundY = _player.transform.position.y - 1f;
        Collider2D col = _player.GetComponent<Collider2D>();
        if (col != null)
            groundY = col.bounds.min.y;
        float z = _player.transform.position.z;
        float playerX = _player.transform.position.x;

        Camera cam = Camera.main;
        float leftEdge = playerX - 12f;
        if (cam != null && cam.orthographic)
            leftEdge = cam.transform.position.x - cam.orthographicSize * cam.aspect;

        // 並べる間隔：画面の左端に収まるように、数が多ければ詰める
        int n = keys.Count;
        float spacing = KeySpacing;
        if (n > 1)
        {
            float available = (playerX - KeyNearGap) - (leftEdge + 1f);
            spacing = Mathf.Clamp(available / (n - 1), KeyMinSpacing, KeySpacing);
        }

        float wait = 0f;
        while (wait < KeyFirstDelay)
        {
            wait += Dt();
            KeepSafe();
            yield return null;
        }

        // 最初に拾ったキーが、プレイヤーにいちばん近く（走る距離はいちばん長い）。
        for (int i = 0; i < n; i++)
        {
            float stopX = playerX - KeyNearGap - i * spacing;
            StartCoroutine(KeyRoutine(keys[i], stopX, groundY, z));

            float d = 0f;
            while (d < KeyStartInterval)
            {
                d += Dt();
                KeepSafe();
                yield return null;
            }
        }

        // 全員が手を上げるまで待つ
        float limit = 0f;
        while (_keysDone < n && limit < KeyWaitLimit)
        {
            limit += Dt();
            KeepSafe();
            yield return null;
        }
    }

    /// <summary>プレイヤーの縦の動きが止まるまで待つ（落ちている間と、上っている間は待つ）。</summary>
    private IEnumerator WaitForPlayerLanding()
    {
        Rigidbody2D rb = _player != null ? _player.GetComponent<Rigidbody2D>() : null;
        if (rb == null)
            yield break;

        float t = 0f;
        float still = 0f;
        while (t < LandWaitLimit)
        {
            float dt = Dt();
            t += dt;
            KeepSafe();

            // ジャンプの頂点で、一瞬だけ速さが0になるので、止まったままの時間で見る。
            if (Mathf.Abs(rb.velocity.y) < 0.05f)
                still += dt;
            else
                still = 0f;

            if (still >= LandStillTime)
                yield break;
            yield return null;
        }
    }

    private IEnumerator KeyRoutine(KeyDefinition key, float stopX, float groundY, float z)
    {
        // 走り出す位置：いまの画面の左端の、少し外
        Camera cam = Camera.main;
        float startX = stopX - 12f;
        if (cam != null && cam.orthographic)
            startX = cam.transform.position.x - cam.orthographicSize * cam.aspect - KeyOffscreen;
        startX = Mathf.Min(startX, stopX - 1f);

        // 親（root）は反転しない。反転するのは見た目（visual）だけ。
        // → 親の子として出すエモートが、裏返らない。
        GameObject root = new GameObject("ClearKey_" + (key != null ? key.name : "?"));
        root.transform.position = new Vector3(startX, groundY, z);

        GameObject visual = Instantiate(keyPrefab, root.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        float face = keyFacesRight ? 1f : -1f; // 右へ走るので、左向きの絵は反転する
        Vector3 s = visual.transform.localScale;
        visual.transform.localScale = new Vector3(s.x * face * keyScale, s.y * keyScale, s.z * keyScale);

        // キートップの文字を、回収したキーにする
        KeyLabel[] labels = visual.GetComponentsInChildren<KeyLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].keyOverride = key;
            labels[i].Refresh();
        }

        LimbRig rig = visual.GetComponentInChildren<LimbRig>(true);

        // 走る
        float x = startX;
        while (x < stopX)
        {
            x = Mathf.Min(stopX, x + KeyRunSpeed * Time.deltaTime);
            root.transform.position = new Vector3(x, groundY, z);
            if (rig != null)
                rig.SetManualVelocity(KeyRunSpeed);
            yield return null;
        }

        // 止まって、手を上げて喜ぶ
        if (rig != null)
            rig.SetManualVelocity(0f);
        yield return new WaitForSeconds(KeyCheerDelay);

        if (rig != null)
            rig.SetCheer(true);
        StartCoroutine(VoiceRoutine());
        SpawnCheerEmote(root.transform);
        _keysDone++;
    }

    /// <summary>キーの頭の上に、エモートを1つ出す（ランダム。一巡するまで同じものは出さない）。</summary>
    private void SpawnCheerEmote(Transform parent)
    {
        GameObject prefab = NextEmotePrefab();
        if (prefab == null)
            return;

        var anchor = new GameObject("EmoteAnchor");
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = cheerEmoteOffset * keyScale;
        anchor.transform.localScale = Vector3.one * cheerEmoteScale;

        GameObject go = Instantiate(prefab, anchor.transform);
        go.transform.localPosition = Vector3.zero; // プレハブに残っている位置は使わない
        go.transform.localRotation = Quaternion.identity;

        if (cheerEmoteSortingBoost != 0)
        {
            SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder += cheerEmoteSortingBoost;
        }

        if (cheerEmoteDuration > 0f)
            Destroy(anchor, cheerEmoteDuration);
    }

    /// <summary>入っているプレハブから、1つ選ぶ。全部を一巡するまでは、同じものを選ばない。</summary>
    private GameObject NextEmotePrefab()
    {
        if (cheerEmotePrefabs == null || cheerEmotePrefabs.Length == 0)
            return null;

        if (_emoteBag.Count == 0)
        {
            for (int i = 0; i < cheerEmotePrefabs.Length; i++)
            {
                if (cheerEmotePrefabs[i] != null)
                    _emoteBag.Add(i);
            }
            if (_emoteBag.Count == 0)
                return null;
        }

        int pick = Random.Range(0, _emoteBag.Count);
        int index = _emoteBag[pick];
        _emoteBag.RemoveAt(pick);
        return cheerEmotePrefabs[index];
    }

    /// <summary>喜びの声：短い音を、数回続けて鳴らす。</summary>
    private IEnumerator VoiceRoutine()
    {
        for (int i = 0; i < KeyVoiceCount; i++)
        {
            SoundEffects.Play(SEId.StageClearKeyVoice);
            yield return new WaitForSecondsRealtime(KeyVoiceInterval);
        }
    }

    // ================= プレイヤー =================

    /// <summary>プレイヤーの操作を止める（演出の間は、攻撃もできない）。</summary>
    private void LockPlayer()
    {
        // Playerタグが付いたもののうち、Rigidbody2Dを持つものが本物のプレイヤー
        GameObject[] candidates = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].GetComponent<Rigidbody2D>() != null)
            {
                _player = candidates[i];
                break;
            }
        }
        if (_player == null)
            return;

        PlayerMovement move = _player.GetComponent<PlayerMovement>();
        if (move != null)
            move.SetInputLocked(true);

        Attack attack = _player.GetComponent<Attack>();
        if (attack != null)
            attack.enabled = false;

        _controller = _player.GetComponent<CharacterController2D>();
        KeepSafe();
    }

    /// <summary>無敵のままにする（被弾後の無敵が途中で切れても、また付け直す）。</summary>
    private void KeepSafe()
    {
        if (_controller != null)
            _controller.invincible = true;
    }

    // ================= 時間 =================

    private void SetTimeScale(float scale)
    {
        // ポーズ中は、触らない（ポーズを解除した時の速さが、狂わないように）
        if (GamePause.IsPaused)
            return;
        Time.timeScale = scale;
        Time.fixedDeltaTime = _baseFixedDelta * scale;
        _slowActive = true;
    }

    private void RestoreTime()
    {
        if (!_slowActive)
            return;
        _slowActive = false;
        if (!GamePause.IsPaused)
            Time.timeScale = 1f;
        Time.fixedDeltaTime = _baseFixedDelta;
    }

    // ================= 画面（コードで作る） =================

    private void BuildOverlay()
    {

        GameObject canvasObj = new GameObject("StageClearCanvas");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2500; // ほかのUIより手前（被弾・死亡の演出の、すぐ下）

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 結果画面の「拠点に戻る」ボタンを、クリックできるようにする（これが無いと、マウスが届かない）。
        canvasObj.AddComponent<GraphicRaycaster>();

        // 奥から：結果画面、紙吹雪、看板、フラッシュ
        _resultHost = MakeRect("Result", canvasObj.transform);
        Stretch(_resultHost);

        _confettiRoot = MakeRect("Confetti", canvasObj.transform);
        Stretch(_confettiRoot);

        BuildSign(canvasObj.transform);

        GameObject flashObj = new GameObject("Flash", typeof(RectTransform));
        flashObj.transform.SetParent(canvasObj.transform, false);
        Stretch((RectTransform)flashObj.transform);
        _flash = flashObj.AddComponent<Image>();
        _flash.color = Color.white;
        _flash.raycastTarget = false;
        SetFlash(0f);
    }

    private void BuildSign(Transform parent)
    {
        _signRect = MakeRect("Sign", parent);
        _signRect.anchorMin = _signRect.anchorMax = new Vector2(0.5f, 0.5f);
        _signRect.pivot = new Vector2(0.5f, 0.5f);
        _signRect.anchoredPosition = new Vector2(0f, SignY);
        _signRect.localRotation = Quaternion.Euler(0f, 0f, SignTilt);

        if (clearSprite != null)
        {
            Image img = _signRect.gameObject.AddComponent<Image>();
            img.sprite = clearSprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            Rect r = clearSprite.rect;
            float h = SignWidth * r.height / Mathf.Max(1f, r.width);
            _signRect.sizeDelta = new Vector2(SignWidth, h);
        }
        else
        {
            _signRect.sizeDelta = new Vector2(SignWidth, 260f);
            TextMeshProUGUI text = _signRect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset useFont = font != null ? font : FindSceneFont();
            if (useFont != null)
                text.font = useFont;
            text.text = "STAGE CLEAR!";
            text.fontSize = 150f;
            text.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
        }

        _signRect.localScale = Vector3.zero; // ぽんと出るまで、見えない
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void SetFlash(float a)
    {
        if (_flash == null)
            return;
        Color c = _flash.color;
        c.a = a;
        _flash.color = c;
        _flash.enabled = a > 0.001f; // 見えない時は描かない
    }

    /// <summary>シーン内の文字から、フォントを借りる（日本語が出るフォントを、自分で入れなくて済むように）。</summary>
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

    // ================= 看板 =================

    private IEnumerator PopSignRoutine()
    {
        float t = 0f;
        while (t < SignPopTime)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / SignPopTime);
            if (_signRect != null)
                _signRect.localScale = Vector3.one * EaseOutBack(k);
            yield return null;
        }
        if (_signRect != null)
            _signRect.localScale = Vector3.one;
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    // ================= 紙吹雪 =================

    /// <summary>
    /// 紙吹雪を出す。最初にburst枚をまとめて出し、そのあとrainTime秒のあいだ、1秒にperSec枚ずつ降らせる。
    /// 降っている最中にもう一度呼んでも、動かすのは1つの処理だけ（動きが二重にならない）。
    /// </summary>
    private void StartConfetti(int burst, float rainTime, float perSec)
    {
        // 最初に、まとめて出す（画面の上に、ばらばらの高さで並べて、順に降ってくるようにする）
        for (int i = 0; i < burst; i++)
            SpawnPiece(Random.Range(560f, 1250f));

        _rainLeft = Mathf.Max(_rainLeft, rainTime);
        _rainRate = perSec;
        if (_confettiMover == null)
            _confettiMover = StartCoroutine(ConfettiRoutine());
    }

    private IEnumerator ConfettiRoutine()
    {
        float spawnDebt = 0f;
        while (true)
        {
            float dt = Dt();

            // そのあとも、しばらく降らせ続ける
            if (_rainLeft > 0f)
            {
                _rainLeft -= dt;
                spawnDebt += _rainRate * dt;
                while (spawnDebt >= 1f)
                {
                    spawnDebt -= 1f;
                    SpawnPiece(Random.Range(560f, 700f));
                }
            }

            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                Piece p = _pieces[i];
                if (p.rt == null)
                {
                    _pieces.RemoveAt(i);
                    continue;
                }

                p.sway += p.swaySpeed * dt;
                Vector2 pos = p.rt.anchoredPosition;
                pos.y -= p.vy * dt;
                pos.x += Mathf.Sin(p.sway) * p.swayAmp * dt;
                p.rt.anchoredPosition = pos;
                p.rt.Rotate(0f, 0f, p.spin * dt);

                if (pos.y < -640f)
                {
                    Destroy(p.rt.gameObject);
                    _pieces.RemoveAt(i);
                }
            }

            if (_rainLeft <= 0f && _pieces.Count == 0)
            {
                _confettiMover = null;
                yield break;
            }
            yield return null;
        }
    }

    private void SpawnPiece(float y)
    {
        if (_confettiRoot == null || _pieces.Count >= ConfettiMax)
            return;

        GameObject go = new GameObject("Piece", typeof(RectTransform));
        go.transform.SetParent(_confettiRoot, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(Random.Range(-980f, 980f), y);
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;

        float h = confettiSize * Random.Range(0.7f, 1.3f);
        Sprite sprite = PickConfettiSprite();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
            Rect r = sprite.rect;
            rt.sizeDelta = new Vector2(h * r.width / Mathf.Max(1f, r.height), h);
        }
        else
        {
            // 画像が無い時は、赤・黄・青の四角
            img.color = PickFallbackColor();
            rt.sizeDelta = new Vector2(h * 0.6f, h);
        }

        var p = new Piece();
        p.rt = rt;
        p.vy = Random.Range(FallSpeedMin, FallSpeedMax);
        p.sway = Random.Range(0f, 6.28f);
        p.swayAmp = Random.Range(40f, 140f);
        p.swaySpeed = Random.Range(2f, 5f);
        p.spin = Random.Range(-260f, 260f);
        _pieces.Add(p);
    }

    private Sprite PickConfettiSprite()
    {
        if (confettiSprites == null || confettiSprites.Length == 0)
            return null;
        for (int tries = 0; tries < 4; tries++)
        {
            Sprite s = confettiSprites[Random.Range(0, confettiSprites.Length)];
            if (s != null)
                return s;
        }
        return null;
    }

    private static Color PickFallbackColor()
    {
        switch (Random.Range(0, 3))
        {
            case 0: return new Color(0.92f, 0.28f, 0.25f, 1f); // 赤
            case 1: return new Color(0.98f, 0.82f, 0.25f, 1f); // 黄
            default: return new Color(0.28f, 0.45f, 0.90f, 1f); // 青
        }
    }
}
