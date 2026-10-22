using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - 「新しいキーを獲得した！」ポップアップの演出。
/// KeyPickup／Ally.keyDrop／DialogueKeyRecruitなど、キーを入手する手段が
/// 増えても呼び出し側を個別に書き換えなくて済むよう、KeyInventory.OnKeyAdded
/// （新しいキーが追加された瞬間に発火するイベント）を直接購読して動く。
/// つまりこのスクリプトはシーンに1つ置いておくだけで良く、KeyPickup側などから
/// 明示的に呼び出す必要はない。
///
/// なお、KeyInventoryは起動時（Awake）にStarting Keysをこの同じOnKeyAddedで
/// 追加するが、このスクリプトの購読はStart()で行う。UnityのAwakeは全オブジェクト
/// 分が先に呼ばれ、Start()はその後なので、ゲーム開始時点で最初から持っている
/// キー分についてはこのポップアップは（何もしなくても）出ない。
///
/// 見た目のパーツは全部バラバラのオブジェクトとして扱う想定（Inspectorで
/// それぞれ割り当てる）。文字は1文字ずつスプライトを用意するのではなく、
/// keyLetterText（TMP_Text）にKeyDefinition.displayNameをそのまま流し込む。
///
/// 演出の流れ:
///   1. 背景（backgroundRect）が小さい状態(backgroundStartScale)から
///      等倍(1.0)へスケールアップする。
///   2. タイトル文言（titleTextObject）と、横のキラキラ（sparkleObjects）が
///      同時に表示される（この時点ではまだ点滅しない、ただ出ているだけ）。
///   3. キー本体（keyCapGroup＝キーの枠＋文字をまとめたCanvasGroup）が
///      フェードインする。
///   4. 下の線のスプライト（underlineGroup）がフェードインする。
///   5. 横のキラキラ（sparkleObjects）が点滅ループを開始する（点灯）。
///   6. 右下の▼（continueArrow）が表示される。
///   7. ▼も点滅ループを開始する（点灯）。
///   8. ここでSpaceキー、またはcloseClickTarget（未設定ならpanelRoot）への
///      クリックを受け付けるようになり、閉じられる。
///
/// Time.timeScaleは変更しない（DialogueKeyRecruitと同じ方針）。代わりに
/// lockPlayerWhileShowingがtrueなら、表示中だけPlayerMovement.SetInputLocked()
/// でプレイヤーの入力を個別にロックする。
/// </summary>
// 【更新】表示中は GamePause でゲームを止める（pauseGameWhileShowing）。上の説明の
// 「Time.timeScaleは変更しない」は古い。止めても、このポップアップの演出は
// 実時間（unscaled）で動くので影響を受けない。敵やプレイヤーのアニメは GamePause が流し続ける。
public class KeyAcquiredPopup : MonoBehaviour
{
    [Header("UI参照（パーツごとに別オブジェクト）")]
    [Tooltip("ポップアップ全体の親。最初は非アクティブにしておく。")]
    public GameObject panelRoot;
    [Tooltip("背景の紙のスプライト。小さい状態から等倍へスケールアップする対象。")]
    public RectTransform backgroundRect;
    [Tooltip("「新しいキーを獲得した！」の文言。常に同じ文なのでテキスト差し替えはしない。")]
    public GameObject titleTextObject;
    [Tooltip("獲得したキーの文字（例：\"C\"）。KeyDefinition.displayNameを流し込む。")]
    public TMP_Text keyLetterText;
    [Tooltip("キーの見た目（枠＋文字）をまとめてフェードインさせるためのCanvasGroup。keyLetterTextを含む親に付けておく。")]
    public CanvasGroup keyCapGroup;
    [Tooltip("キーの下にある線のスプライト用CanvasGroup。")]
    public CanvasGroup underlineGroup;
    [Tooltip("キーの横にあるキラキラのスプライト一式（複数可）。表示→点滅ループの2段階で使う。")]
    public GameObject[] sparkleObjects;
    [Tooltip("右下の▼（続きを促すマーク）。表示→点滅ループの2段階で使う。")]
    public GameObject continueArrow;

    [Header("閉じる操作")]
    [Tooltip("（今は使わない）ポップアップは、Spaceキーだけで閉じる。クリックでは閉じない。")]
    public GameObject closeClickTarget;

    [Header("演出タイミング（調整用）")]
    [Tooltip("背景が『小さい状態』のスケール（1が等倍）。")]
    [Range(0.1f, 1f)] public float backgroundStartScale = 0.6f;
    [Tooltip("背景が小→大にスケールするのにかかる秒数。")]
    public float backgroundScaleDuration = 0.25f;
    [Tooltip("キー本体がフェードインするのにかかる秒数。")]
    public float keyFadeInDuration = 0.3f;
    [Tooltip("下の線のスプライトがフェードインするのにかかる秒数。")]
    public float underlineFadeInDuration = 0.2f;
    [Tooltip("横のキラキラの、点灯している時間（秒）。")]
    public float sparkleOnDuration = 0.12f;
    [Tooltip("横のキラキラの、消えている時間（秒）。")]
    public float sparkleOffDuration = 0.12f;
    [Tooltip("右下の▼の、点灯している時間（秒）。")]
    public float arrowOnDuration = 0.4f;
    [Tooltip("右下の▼の、消えている時間（秒）。")]
    public float arrowOffDuration = 0.4f;

    [Header("表示中の操作ロック（任意）")]
    [Tooltip("ポップアップを表示している間、プレイヤーの入力をロックするかどうか。DialogueKeyRecruitと同じ方針でTime.timeScaleは変更しない。")]
    public bool lockPlayerWhileShowing = true;
    [Tooltip("ロック対象のPlayerMovement。未設定ならシーン内から自動で探す。")]
    public PlayerMovement playerMovement;

    [Tooltip("ポップアップが出ている間、後ろのゲームを止める（敵もプレイヤーも動かない。アニメーションだけ流れる）。")]
    public bool pauseGameWhileShowing = true;

    [Header("キーキャラクター（キーの文字の代わりに出す）")]
    [Tooltip("手足の生えたキー（Assets/Prefab/Mob/Enemy/KeyNPC_Visual）。空なら、今までどおり文字を出す。")]
    public GameObject keyCharacterPrefab;
    [Tooltip("キャラクターの映像を映す RawImage。キーの枠（keyCapGroup）の中に置くと、キーと一緒にフェードインする。")]
    public RawImage characterView;
    [Tooltip("キャラクターの拡大率。")]
    public float characterScale = 1f;
    [Tooltip("映像に映る高さ（ワールドの単位）。大きいほど、キャラクターが小さく映る。")]
    public float characterViewHeight = 2.2f;
    [Tooltip("ONなら右向き。もとの絵は左向き。")]
    public bool characterFacesRight = false;
    [Tooltip("キャラクターの動き（ランダムに切り替えて踊る）。")]
    public KeyMotionSettings keyMotion = new KeyMotionSettings();

    [Header("一言（KeyDefinition の Flavor Text）")]
    [Tooltip("キーキャラクターの下に出す一言の文字。空の一言のキーでは、何も出さない。")]
    public TMP_Text commentText;

    [Header("効果音")]
    [Tooltip("ポップアップが出る効果音は、SoundEffects の Key Acquired Show に割り当てる。")]
    public bool playShowSound = true;

    [Header("キーの紙吹雪（ポップアップが出ている間、上から降る）")]
    [Tooltip("紙吹雪を出す場所（画面いっぱいの大きさのRectTransform）。空なら、panelRootの中に出す。")]
    public RectTransform confettiLayer;
    [Tooltip("紙吹雪の絵（何種類でも）。空なら、獲得したキーの Key Sprite。それも無ければ、コードで作ったキーキャップ風の四角。")]
    public Sprite[] confettiSprites;
    [Tooltip("1秒間に降らせる数。0なら出さない。")]
    public float confettiPerSecond = 9f;
    public float confettiSizeMin = 40f;
    public float confettiSizeMax = 80f;
    [Tooltip("落ちる速さ（画面の単位／秒）。")]
    public float confettiFallMin = 220f;
    public float confettiFallMax = 420f;

    [Header("BGM")]
    [Tooltip("ONなら、ポップアップが出ている間、後ろのBGMを消す。閉じてキーコンフィグ画面へ移る瞬間に、BGMが戻る。")]
    public bool muteBgmWhileShowing = true;
    [Tooltip("BGMが消えるまでの秒数。")]
    public float bgmFadeOutTime = 0.4f;
    [Tooltip("BGMが戻るまでの秒数。")]
    public float bgmFadeInTime = 0.5f;

    [Header("閉じたあとの演出（任意）")]
    [Tooltip("ポップアップを閉じたあとに、キーコンフィグ画面で、キーキャラクターが走って来て置かれる演出。空なら、シーンの中から探す。それも無ければ、演出なし。")]
    public KeyConfigUnlockSequence unlockSequence;

    private KeyDefinition _lastKey;
    private bool _bgmPushed;

    // キーキャラクター
    private ResultCharacterStage _stage;
    private GameObject _characterObject;

    // 紙吹雪
    private class ConfettiPiece
    {
        public RectTransform rt;
        public float baseX;
        public float y;
        public float size;
        public float speed;
        public float sway;
        public float swayFreq;
        public float phase;
        public float spin;
        public float rot;
    }
    private readonly List<ConfettiPiece> _confetti = new List<ConfettiPiece>();
    private Coroutine _confettiCoroutine;
    private static Sprite _fallbackConfettiSprite;
    private bool _waitingForClose;
    private Coroutine _sequenceCoroutine;
    private Coroutine _sparkleBlinkCoroutine;
    private Coroutine _arrowBlinkCoroutine;

    private void Start()
    {
        if (KeyInventory.Instance != null)
            KeyInventory.Instance.OnKeyAdded += OnKeyAdded;
        else
            Debug.LogWarning($"[KeyAcquiredPopup] {name}: シーンにKeyInventoryが見つかりません（Bootstrap未配置？） - キー獲得ポップアップは動作しません。", this);

        if (panelRoot != null)
            panelRoot.SetActive(false);

    }

    private void OnDestroy()
    {
        if (KeyInventory.Instance != null)
            KeyInventory.Instance.OnKeyAdded -= OnKeyAdded;

        StopConfetti();
        TeardownCharacter();
        RestoreBgm();
        GamePause.Release(this);
    }

    private void Update()
    {
        if (_waitingForClose && Input.GetKeyDown(KeyCode.Space))
            RequestClose();
    }

    private void OnKeyAdded(KeyDefinition key)
    {
        // ★調査用ログ（原因が分かったら削除してください）
        Debug.Log($"[KeyAcquiredPopup] OnKeyAdded呼び出し: key={(key != null ? key.displayName : "null")}");

        // 既に表示中に別のキーが追加された場合（同時取得など）は、
        // 前の演出を打ち切って新しい方を優先する。
        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);

        _sequenceCoroutine = StartCoroutine(PlaySequence(key));
    }

    private IEnumerator PlaySequence(KeyDefinition key)
    {
        _waitingForClose = false;
        _lastKey = key;
        StopSparkleBlink(keepVisible: false);
        StopArrowBlink(keepVisible: false);

        if (lockPlayerWhileShowing)
        {
            if (playerMovement == null)
                playerMovement = FindObjectOfType<PlayerMovement>();
            if (playerMovement != null)
                playerMovement.SetInputLocked(true);
        }

        if (pauseGameWhileShowing)
            GamePause.Acquire(this);

        if (keyLetterText != null)
            keyLetterText.text = key != null ? key.displayName : "?";

        // 全パーツを初期状態に戻す。
        if (panelRoot != null)
            panelRoot.SetActive(true);

        // ★調査用ログ（原因が分かったら削除してください）
        Debug.Log($"[KeyAcquiredPopup] panelRoot.SetActive(true)実行直後: " +
            $"panelRoot={(panelRoot != null ? panelRoot.name : "null")}, " +
            $"activeSelf={(panelRoot != null ? panelRoot.activeSelf.ToString() : "N/A")}, " +
            $"activeInHierarchy={(panelRoot != null ? panelRoot.activeInHierarchy.ToString() : "N/A")}");

        // 後ろのBGMを覚えて、消す（すでに消してあるときは、二重に覚えない）。
        if (muteBgmWhileShowing && !_bgmPushed)
        {
            AudioManager.PushBGM(null);
            AudioManager.StopBGM(bgmFadeOutTime);
            _bgmPushed = true;
        }

        // 出現の効果音、キーキャラクター、一言、紙吹雪の準備。
        if (playShowSound)
            SoundEffects.Play(SEId.KeyAcquiredShow);
        StopConfetti();
        SetupCharacter(key);
        if (commentText != null)
            commentText.gameObject.SetActive(false);

        if (backgroundRect != null)
            backgroundRect.localScale = Vector3.one * backgroundStartScale;
        if (titleTextObject != null)
            titleTextObject.SetActive(false);
        foreach (var s in sparkleObjects)
            if (s != null) s.SetActive(false);
        if (keyCapGroup != null)
        {
            keyCapGroup.gameObject.SetActive(true);
            keyCapGroup.alpha = 0f;
        }
        if (underlineGroup != null)
        {
            underlineGroup.gameObject.SetActive(true);
            underlineGroup.alpha = 0f;
        }
        if (continueArrow != null)
            continueArrow.SetActive(false);

        // 1. 背景：小さい状態から等倍へ。
        yield return ScaleUp(backgroundRect, backgroundStartScale, 1f, backgroundScaleDuration);

        // 背景が出たら、キーの紙吹雪を降らせ始める（閉じるまで続く）。
        StartConfetti(key);

        // 2. タイトル文言と横のキラキラを同時に表示（この時点ではまだ点滅しない）。
        if (titleTextObject != null)
            titleTextObject.SetActive(true);
        foreach (var s in sparkleObjects)
            if (s != null) s.SetActive(true);

        // 3. キー本体をフェードイン。
        yield return FadeCanvasGroup(keyCapGroup, 0f, 1f, keyFadeInDuration);

        // 4. 下の線のスプライトをフェードイン。
        yield return FadeCanvasGroup(underlineGroup, 0f, 1f, underlineFadeInDuration);

        // 4.5. 一言を、ふわっと表示する。
        yield return ShowComment(key);

        // 5. 横のキラキラを点灯（点滅ループ開始）。
        StartSparkleBlink();

        // 6. 右下の▼を表示。
        if (continueArrow != null)
            continueArrow.SetActive(true);

        // 7. ▼も点灯（点滅ループ開始）。
        StartArrowBlink();

        _sequenceCoroutine = null;
        _waitingForClose = true;

        // ★調査用ログ（原因が分かったら削除してください）
        Debug.Log("[KeyAcquiredPopup] PlaySequence完了、_waitingForClose=true");
    }

    /// <summary>Spaceキー、またはcloseClickTargetのクリックのどちらから来ても、ここに集約する。</summary>
    private void RequestClose()
    {
        if (!_waitingForClose)
            return;

        _waitingForClose = false;

        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        StopSparkleBlink(keepVisible: false);
        StopArrowBlink(keepVisible: false);
        StopConfetti();
        TeardownCharacter();
        RestoreBgm(); // キーコンフィグ画面へ移る前に、BGMを戻す（画面に専用のBGMがあれば、そちらに切り替わる）

        if (panelRoot != null)
            panelRoot.SetActive(false);

        // キーコンフィグ画面での演出に引き継ぐ。引き継いだときは、演出が先にゲームを止め、操作もロックし続ける
        // （演出が終わったら、演出が解除する）。
        bool handedOver = false;
        if (_lastKey != null)
        {
            if (unlockSequence == null)
                unlockSequence = FindObjectOfType<KeyConfigUnlockSequence>(true);
            if (unlockSequence != null)
                handedOver = unlockSequence.TryPlay(_lastKey);
        }

        if (!handedOver && lockPlayerWhileShowing && playerMovement != null)
            playerMovement.SetInputLocked(false);

        GamePause.Release(this);
    }

    /// <summary>消していたBGMを、覚えた位置から戻す。</summary>
    private void RestoreBgm()
    {
        if (!_bgmPushed)
            return;
        _bgmPushed = false;
        AudioManager.PopBGM(bgmFadeInTime);
    }

    // ---- キーキャラクター -----------------------------------------------

    /// <summary>キーの文字の代わりに、キーキャラクターを出す（プレハブと映像の欄が入っているときだけ）。</summary>
    private void SetupCharacter(KeyDefinition key)
    {
        TeardownCharacter();

        bool hasCharacter = keyCharacterPrefab != null && characterView != null;
        if (keyLetterText != null)
            keyLetterText.gameObject.SetActive(!hasCharacter);
        if (characterView != null)
            characterView.gameObject.SetActive(hasCharacter);
        if (!hasCharacter)
            return;

        Rect r = characterView.rectTransform.rect;
        float aspect = (r.width > 1f && r.height > 1f) ? r.width / r.height : 1f;

        _stage = new ResultCharacterStage("KeyAcquiredStage", new Vector3(0f, -9000f, 0f), 1, characterViewHeight, aspect, 384);

        GameObject go = Instantiate(keyCharacterPrefab);
        go.name = "KeyAcquiredKey";
        _stage.Place(go, 0, false);

        // もとの絵は左向き。右向きにしたいときだけ、左右を反転する。
        float face = characterFacesRight ? -1f : 1f;
        Vector3 s = go.transform.localScale;
        go.transform.localScale = new Vector3(s.x * face * characterScale, s.y * characterScale, s.z * characterScale);

        KeyLabel[] labels = go.GetComponentsInChildren<KeyLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].keyOverride = key;
            labels[i].Refresh();
        }

        LimbRig rig = go.GetComponentInChildren<LimbRig>(true);
        if (rig != null)
        {
            rig.SetManualVelocity(0f);
            KeyMotionRandomizer.Begin(rig, keyMotion);
        }

        characterView.texture = _stage.Texture;
        characterView.uvRect = _stage.SlotUv(0);
        _characterObject = go;
    }

    private void TeardownCharacter()
    {
        if (_characterObject != null)
            Destroy(_characterObject);
        _characterObject = null;
        if (_stage != null)
            _stage.Dispose();
        _stage = null;
    }

    // ---- 一言 ---------------------------------------------------------

    private IEnumerator ShowComment(KeyDefinition key)
    {
        if (commentText == null)
            yield break;

        string text = key != null ? key.flavorText : "";
        if (string.IsNullOrEmpty(text))
        {
            commentText.gameObject.SetActive(false);
            yield break;
        }

        commentText.text = text;
        Color c = commentText.color;
        c.a = 0f;
        commentText.color = c;
        commentText.gameObject.SetActive(true);

        const float dur = 0.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Clamp01(t / dur);
            commentText.color = c;
            yield return null;
        }
        c.a = 1f;
        commentText.color = c;
    }

    // ---- キーの紙吹雪 -----------------------------------------------------

    private void StartConfetti(KeyDefinition key)
    {
        StopConfetti();
        if (confettiPerSecond <= 0f)
            return;
        _confettiCoroutine = StartCoroutine(ConfettiRoutine(key));
    }

    private void StopConfetti()
    {
        if (_confettiCoroutine != null)
        {
            StopCoroutine(_confettiCoroutine);
            _confettiCoroutine = null;
        }
        for (int i = 0; i < _confetti.Count; i++)
        {
            if (_confetti[i] != null && _confetti[i].rt != null)
                Destroy(_confetti[i].rt.gameObject);
        }
        _confetti.Clear();
    }

    private IEnumerator ConfettiRoutine(KeyDefinition key)
    {
        RectTransform layer = confettiLayer != null ? confettiLayer
                            : (panelRoot != null ? panelRoot.transform as RectTransform : null);
        if (layer == null)
            yield break;

        // 使う絵：Inspectorで入れたもの → キーの絵 → コードで作った四角
        List<Sprite> sprites = new List<Sprite>();
        if (confettiSprites != null)
        {
            for (int i = 0; i < confettiSprites.Length; i++)
                if (confettiSprites[i] != null) sprites.Add(confettiSprites[i]);
        }
        bool generated = false;
        if (sprites.Count == 0 && key != null && key.keySprite != null)
            sprites.Add(key.keySprite);
        if (sprites.Count == 0)
        {
            sprites.Add(GetFallbackConfettiSprite());
            generated = true;
        }

        // 最初から、上のほうに何枚か出しておく（少し寂しくならないように）。
        Rect first = layer.rect;
        int prewarm = Mathf.Clamp(Mathf.RoundToInt(confettiPerSecond * 1.2f), 0, 24);
        for (int i = 0; i < prewarm; i++)
            SpawnConfetti(layer, sprites, generated, Random.Range(first.center.y, first.yMax));

        float acc = 0f;
        float time = 0f;
        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            Rect r = layer.rect;

            acc += confettiPerSecond * dt;
            while (acc >= 1f)
            {
                acc -= 1f;
                SpawnConfetti(layer, sprites, generated, r.yMax + confettiSizeMax);
            }

            for (int i = _confetti.Count - 1; i >= 0; i--)
            {
                ConfettiPiece p = _confetti[i];
                if (p == null || p.rt == null)
                {
                    _confetti.RemoveAt(i);
                    continue;
                }
                p.y -= p.speed * dt;
                p.rot += p.spin * dt;
                float x = p.baseX + Mathf.Sin(time * p.swayFreq + p.phase) * p.sway;
                p.rt.localPosition = new Vector3(x, p.y, 0f);
                p.rt.localRotation = Quaternion.Euler(0f, 0f, p.rot);

                if (p.y < r.yMin - p.size)
                {
                    Destroy(p.rt.gameObject);
                    _confetti.RemoveAt(i);
                }
            }
            yield return null;
        }
    }

    private void SpawnConfetti(RectTransform layer, List<Sprite> sprites, bool generated, float y)
    {
        Rect r = layer.rect;
        float size = Random.Range(confettiSizeMin, Mathf.Max(confettiSizeMin, confettiSizeMax));

        GameObject go = new GameObject("KeyConfetti", typeof(RectTransform));
        go.transform.SetParent(layer, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        Image img = go.AddComponent<Image>();
        img.sprite = sprites[Random.Range(0, sprites.Count)];
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = generated
            ? Color.HSVToRGB(Random.value, 0.35f, 1f)
            : Color.Lerp(Color.white, new Color(0.85f, 0.85f, 0.85f, 1f), Random.value * 0.5f);

        ConfettiPiece p = new ConfettiPiece
        {
            rt = rt,
            baseX = Random.Range(r.xMin, r.xMax),
            y = y,
            size = size,
            speed = Random.Range(confettiFallMin, Mathf.Max(confettiFallMin, confettiFallMax)),
            sway = Random.Range(15f, 60f),
            swayFreq = Random.Range(1.2f, 3.2f),
            phase = Random.Range(0f, 6.28f),
            spin = Random.Range(-220f, 220f),
            rot = Random.Range(0f, 360f),
        };
        rt.localPosition = new Vector3(p.baseX, y, 0f);
        rt.localRotation = Quaternion.Euler(0f, 0f, p.rot);
        _confetti.Add(p);
    }

    /// <summary>絵が無いときの、キーキャップ風の四角（白）。色は、紙吹雪ごとに付ける。</summary>
    private static Sprite GetFallbackConfettiSprite()
    {
        if (_fallbackConfettiSprite != null)
            return _fallbackConfettiSprite;

        const int N = 64;
        const float radius = 14f;
        const float border = 4f;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;
                float dx = Mathf.Max(radius - fx, fx - (N - radius), 0f);
                float dy = Mathf.Max(radius - fy, fy - (N - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius; // 角丸の外側で正
                if (dist > 0.5f)
                {
                    px[y * N + x] = new Color32(0, 0, 0, 0);
                    continue;
                }
                bool edge = dist > -border;
                byte v = edge ? (byte)150 : (byte)255;
                px[y * N + x] = new Color32(v, v, v, 255);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        _fallbackConfettiSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return _fallbackConfettiSprite;
    }

    // ---- 演出の細かい部品 ----------------------------------------------

    private IEnumerator ScaleUp(RectTransform rt, float fromScale, float toScale, float duration)
    {
        if (rt == null)
            yield break;

        if (duration <= 0f)
        {
            rt.localScale = Vector3.one * toScale;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            rt.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, ratio);
            yield return null;
        }
        rt.localScale = Vector3.one * toScale;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = to;
    }

    private void StartSparkleBlink()
    {
        StopSparkleBlink(keepVisible: false);
        _sparkleBlinkCoroutine = StartCoroutine(BlinkLoop(sparkleObjects, sparkleOnDuration, sparkleOffDuration));
    }

    /// <summary>keepVisible: 止めた後、消灯状態のままにせず点灯状態に揃えるかどうか。</summary>
    private void StopSparkleBlink(bool keepVisible)
    {
        if (_sparkleBlinkCoroutine != null)
        {
            StopCoroutine(_sparkleBlinkCoroutine);
            _sparkleBlinkCoroutine = null;
        }

        if (keepVisible)
        {
            foreach (var s in sparkleObjects)
                if (s != null) s.SetActive(true);
        }
    }

    private void StartArrowBlink()
    {
        StopArrowBlink(keepVisible: false);
        if (continueArrow != null)
            _arrowBlinkCoroutine = StartCoroutine(BlinkLoop(new[] { continueArrow }, arrowOnDuration, arrowOffDuration));
    }

    private void StopArrowBlink(bool keepVisible)
    {
        if (_arrowBlinkCoroutine != null)
        {
            StopCoroutine(_arrowBlinkCoroutine);
            _arrowBlinkCoroutine = null;
        }

        if (keepVisible && continueArrow != null)
            continueArrow.SetActive(true);
    }

    /// <summary>渡されたオブジェクト群をまとめて、on/offを繰り返し表示し続ける（点滅）。</summary>
    private IEnumerator BlinkLoop(GameObject[] targets, float onDuration, float offDuration)
    {
        while (true)
        {
            foreach (var t in targets)
                if (t != null) t.SetActive(true);
            yield return new WaitForSecondsRealtime(onDuration);

            foreach (var t in targets)
                if (t != null) t.SetActive(false);
            yield return new WaitForSecondsRealtime(offDuration);
        }
    }
}
