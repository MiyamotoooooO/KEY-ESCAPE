using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - ステージクリア後の結果画面（StageClearDirector が出す）。
///
/// 見た目は、Editor のメニュー「Tools > KEY ESCAPE > リザルト > 結果画面のプレハブを作る」で作ったプレハブを、
/// Unity 上で自由に直して使う（絵・フォント・大きさ・位置）。このスクリプトは、中身を入れて、順番に出すだけ。
///
/// 出る順番：紙の背景 → クリアタイム（数字が増えていく）→ 死亡シーン（写真と一言コメント）→ 回収したキー（ランクの星つき）→ 拠点に戻るボタン。
/// プレイヤーとキーは、動くキャラクターで見せる（画面の外に小さな撮影場所を作り、その映像を貼る。ResultCharacterStage）。
/// 「拠点に戻る」は、クリックか Enter で押せる。押すと Confirmed が true になり、StageClearDirector が拠点に戻る。
/// 時間はすべて「実時間」。
/// </summary>
public class StageResultPanel : MonoBehaviour
{
    /// <summary>死んだ瞬間の写真1枚分の表示枠。</summary>
    [System.Serializable]
    public class PhotoSlot
    {
        [Tooltip("この枠全体。使わない枠は、非表示にする。")]
        public GameObject root;
        public RawImage photo;
        public TMP_Text comment;
    }

    // ================= 調整用の数値 =================

    const int   MaxPhotos        = 3;     // 並べる写真の最大数
    const float PaperFadeTime    = 0.4f;  // 紙の背景が出る秒数
    const float PopTime          = 0.3f;  // 各部分が、ぽんと出る秒数
    const float TimeCountTime    = 1.2f;  // タイムの数字が増えていく秒数
    const float PartInterval     = 0.2f;  // 部分と部分の間の秒数
    const float PhotoInterval    = 0.25f; // 写真が1枚ずつ出る間隔
    const float KeyInterval      = 0.12f; // キーが1つずつ出る間隔
    const float PhotoCropHeight  = 0.7f;  // 写真の切り抜き：画面の高さの何割を使うか（小さいほど、プレイヤーに寄る）
    const float DefaultFrameAspect = 1.2f; // 枠の大きさが分からない時の、縦横比（横/縦）
    const float MaxDt            = 0.05f;

    // ==============================================

    [Header("全体")]
    [Tooltip("結果画面の文字に使うフォント。空なら、プレハブの文字に設定されたフォントのまま。")]
    public TMP_FontAsset font;
    [Tooltip("紙の背景（画面いっぱいの画像）。最初は透明で、ふわっと出る。")]
    public Graphic paper;
    [Tooltip("STAGE CLEAR! の看板が、移ってくる位置と大きさ（幅）と傾き。看板の絵そのものは StageClearDirector の Clear Sprite。")]
    public RectTransform signAnchor;
    [Tooltip("任意。左側に出すプレイヤーの枠。Player Controller を入れると、この枠の大きさに、動くプレイヤーが映る。入れない時は、この Image の絵（Sprite）を出す（絵が空なら出さない）。")]
    public Image playerArt;

    [Header("プレイヤー（動くキャラクター）")]
    [Tooltip("プレイヤーのAnimator Controller（DrawCharacter.controller）。空なら、動くプレイヤーは出さない。")]
    public RuntimeAnimatorController playerController;
    [Tooltip("枠に映る高さ（ワールドの単位）。大きいほど、プレイヤーが小さく映る。")]
    public float playerViewHeight = 3.4f;
    [Tooltip("オンなら、右向き。オフなら、左向き（DrawCharacterの絵は、もともと左向き）。向きが逆に見えたら、切り替える。")]
    public bool playerFacesRight = false;
    [Tooltip("繰り返し流す動き。キーコンフィグの確認と同じ設定（パラメーター名は、Animator Controller のもの）。")]
    public KeyConfigPreviewPlayer.ActionPreviewTrigger[] playerActions = new KeyConfigPreviewPlayer.ActionPreviewTrigger[]
    {
        new KeyConfigPreviewPlayer.ActionPreviewTrigger { action = GameAction.Jump,   paramType = KeyConfigPreviewPlayer.PreviewParamType.Bool, animatorParam = "IsJumping",   holdDuration = 0.6f },
        new KeyConfigPreviewPlayer.ActionPreviewTrigger { action = GameAction.Attack, paramType = KeyConfigPreviewPlayer.PreviewParamType.Bool, animatorParam = "IsAttacking", holdDuration = 0.4f },
        new KeyConfigPreviewPlayer.ActionPreviewTrigger { action = GameAction.Dash,   paramType = KeyConfigPreviewPlayer.PreviewParamType.Bool, animatorParam = "IsDashing",   holdDuration = 0.4f },
    };
    [Tooltip("動きと動きの間の待ち時間（秒）。この範囲で、ランダムに決まる。")]
    public Vector2 playerActionInterval = new Vector2(0.8f, 1.8f);
    [Tooltip("オンなら、ジャンプや攻撃の動きに付いている音（Animator Controller に仕込まれた音）を鳴らす。オフなら、鳴らさない。")]
    public bool playerPlaysSounds = false;

    [Header("クリアタイム")]
    public RectTransform timePanel;
    public TMP_Text timeText;
    [Tooltip("任意。ベストタイム（記録を更新しなかった時だけ出す）。")]
    public TMP_Text bestTimeText;
    [Tooltip("ベストタイムの文字。{0} にタイムが入る。")]
    public string bestTimeFormat = "ベスト {0}";
    [Tooltip("任意。記録を更新した（初クリアも含む）時に出す「NEW RECORD!」。")]
    public GameObject newRecordBadge;
    [Tooltip("任意。記録を更新した時に出す王冠。絵（Sprite）が空なら、出さない。")]
    public Image crown;

    [Header("死亡シーン")]
    public RectTransform deathPanel;
    [Tooltip("任意。死んだ回数の文字。")]
    public TMP_Text deathCountText;
    [Tooltip("死んだ回数の文字。{0} に回数が入る。")]
    public string deathCountFormat = "死亡 {0} 回";
    [Tooltip("写真の枠（左から）。3つまで使う。")]
    public PhotoSlot[] photoSlots;
    [Tooltip("1回も死ななかった時に出す「NO DEATH!」。")]
    public GameObject noDeathObject;

    [Header("一言コメント（死因ごとのリスト。写真ごとに、ランダムで1つ選ぶ）")]
    public string[] commentsFall = { "ここで落ちる？？？", "足元、見えてた？", "そこ、穴だよ！", "重力には勝てなかった…" };
    public string[] commentsEnemy = { "ここで死ぬ？？？", "あと少しだった…！", "油断したね！", "敵のほうが一枚上手だった" };
    public string[] commentsOther = { "なぜ、ここで…", "惜しい！", "次はいけるよ！" };

    [Header("回収したキー")]
    public RectTransform keysPanel;
    [Tooltip("キーを並べる領域。ここの幅に収まるように、数が多ければ自動で縮む。")]
    public RectTransform keysContainer;
    [Tooltip("キー1つ分の見本（最初は非表示にしておく）。StageResultKeyItem を付ける。")]
    public StageResultKeyItem keyItemTemplate;
    [Tooltip("キーとキーの間隔（基準解像度でのピクセル）。")]
    public float keyItemSpacing = 14f;
    [Tooltip("キーを並べる領域の、左右の余白。")]
    public float keysAreaPadding = 30f;
    [Tooltip("予備。キーコンフィグ画面（KeyConfigUI）から星の絵を借りられなかった時に使う。どちらも空なら、コードで作った星を出す。")]
    public Sprite starFilledSprite;
    public Sprite starEmptySprite;

    [Header("キーのキャラクター（手足つきで、手を上げて喜ぶ）")]
    [Tooltip("キーの見た目（KeyNPC_Visual）。空でよい（StageClearDirector の Key Prefab を借りる）。どちらも空なら、キーの絵（Key Sprite）を出す。")]
    public GameObject keyCharacterPrefab;
    [Tooltip("キーの絵が右向きならオン。（KeyNPC_Visual は左向きなので、オフのままでよい）結果画面では、左（プレイヤーのほう）を向く。")]
    public bool keyCharacterFacesRight = false;
    [Tooltip("キーの大きさの倍率。")]
    public float keyCharacterScale = 1f;
    [Tooltip("1つの枠に映る高さ（ワールドの単位）。手を上げると背が伸びるので、余裕を持たせる。大きいほど、キーが小さく映る。")]
    public float keyViewHeight = 2.8f;
    [Tooltip("キーが出てから、手を上げ始めるまでの遅れ（秒）。キーは1つずつ出るので、手を上げ始めるのも、自然にずれる。")]
    public float keyCheerDelay = 0.15f;

    [Header("覚えた操作（このステージのクリアで、新しく解放されたもの）")]
    [Tooltip("任意。「覚えた！ ジャンプ」を出す枠。空なら、「拠点に戻る」ボタンの左に、コードで枠を作って出す。新しく解放されたものが無ければ、出さない。")]
    public RectTransform rewardPanel;
    [Tooltip("任意。上の枠の中の文字。")]
    public TMP_Text rewardText;
    [Tooltip("表示する文字。{0} に、解放された物の名前（ジャンプ・攻撃のように、「・」でつなぐ）が入る。")]
    public string rewardFormat = "覚えた！ {0}";
    [Tooltip("Reward Panel が空の時に、コードで作る枠の位置と大きさ（画面の中央が基準）。")]
    public Vector2 rewardFallbackPos = new Vector2(-520f, -448f);
    public Vector2 rewardFallbackSize = new Vector2(620f, 92f);

    [Header("ボタン")]
    public Button returnButton;

    /// <summary>「拠点に戻る」が押されたか。</summary>
    public bool Confirmed { get; private set; }

    /// <summary>クリアタイムの数字が、止まった時に呼ばれる（紙吹雪を出すのに使う）。</summary>
    public event System.Action TimeCounted;

    private readonly List<Transform> _popTargets = new List<Transform>();
    private readonly List<Vector3> _popScales = new List<Vector3>();
    private readonly List<string>[] _commentBags = { new List<string>(), new List<string>(), new List<string>() };
    private bool _ready;
    private bool _prevCursorVisible;
    private CursorLockMode _prevCursorLock;
    private bool _cursorSaved;
    private ResultCharacterStage _playerStage;
    private ResultCharacterStage _keyStage;
    private Vector3 _playerArtScale = Vector3.one;
    private List<string> _rewards;

    /// <summary>キーの見た目を、あとから渡す（Key Character Prefab が空の時に、StageClearDirector が呼ぶ）。</summary>
    public void SetKeyCharacter(GameObject prefab, bool facesRight, float scale)
    {
        if (keyCharacterPrefab != null)
            return;
        keyCharacterPrefab = prefab;
        keyCharacterFacesRight = facesRight;
        keyCharacterScale = scale;
    }

    /// <summary>新しく解放された操作の名前を渡す（Playの前に呼ぶ）。空か null なら、出さない。</summary>
    public void SetRewards(List<string> rewards)
    {
        _rewards = rewards;
    }

    private void Awake()
    {
        // 最初は、すべて隠しておく（出す順番に、Play() が出していく）。
        if (paper != null)
            paper.canvasRenderer.SetAlpha(0f);

        RegisterPop(timePanel);
        RegisterPop(deathPanel);
        RegisterPop(keysPanel);
        RegisterPop(rewardPanel);
        if (returnButton != null)
            RegisterPop(returnButton.transform);

        if (newRecordBadge != null) newRecordBadge.SetActive(false);
        if (crown != null) crown.gameObject.SetActive(false);
        if (noDeathObject != null) noDeathObject.SetActive(false);
        if (keyItemTemplate != null) keyItemTemplate.gameObject.SetActive(false);
        if (photoSlots != null)
        {
            for (int i = 0; i < photoSlots.Length; i++)
            {
                if (photoSlots[i] != null && photoSlots[i].root != null)
                    photoSlots[i].root.SetActive(false);
            }
        }
        if (playerArt != null)
            playerArt.enabled = playerArt.sprite != null;

        if (returnButton != null)
        {
            returnButton.interactable = false;
            returnButton.onClick.AddListener(Confirm);
        }
    }

    private void OnDestroy()
    {
        if (returnButton != null)
            returnButton.onClick.RemoveListener(Confirm);
        RestoreCursor();
        if (_playerStage != null)
            _playerStage.Dispose();
        if (_keyStage != null)
            _keyStage.Dispose();
    }

    private void Update()
    {
        if (_ready && !Confirmed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            Confirm();
    }

    private void RegisterPop(Transform t)
    {
        if (t == null)
            return;
        _popTargets.Add(t);
        _popScales.Add(t.localScale);
        t.localScale = Vector3.zero;
    }

    private Vector3 ScaleOf(Transform t)
    {
        int i = _popTargets.IndexOf(t);
        return i >= 0 ? _popScales[i] : Vector3.one;
    }

    private static float Dt()
    {
        return Mathf.Min(Time.unscaledDeltaTime, MaxDt);
    }

    // ================= 表示 =================

    /// <summary>結果を順番に出す。出し終わると、ボタンが押せるようになる。keysは、このステージで回収したキー。</summary>
    public IEnumerator Play(List<KeyDefinition> keys, bool newRecord)
    {
        ApplyFont();
        ShowCursor();
        SoundEffects.Play(SEId.StageResultShow);

        // 紙の背景
        yield return Fade(paper, PaperFadeTime);

        // プレイヤー（動くキャラクター）
        if (SetupPlayerLive())
            StartCoroutine(Pop(playerArt.transform, _playerArtScale));

        // クリアタイム
        string timeLabel = "--:--.--";
        float time = StageStats.ElapsedTime;
        bool hasTime = StageStats.IsTracking && time > 0f;
        if (timeText != null)
            timeText.text = hasTime ? StageProgress.FormatTime(0f) : timeLabel;
        yield return Pop(timePanel);
        if (hasTime && timeText != null)
            yield return CountUp(time);
        if (TimeCounted != null)
            TimeCounted(); // 集計が終わった（紙吹雪）

        if (newRecord && hasTime)
        {
            if (bestTimeText != null)
                bestTimeText.text = "";
            if (newRecordBadge != null)
            {
                newRecordBadge.SetActive(true);
                yield return Pop(newRecordBadge.transform, Vector3.one);
            }
            if (crown != null && crown.sprite != null)
            {
                crown.gameObject.SetActive(true);
                yield return Pop(crown.transform, Vector3.one);
            }
        }
        else if (bestTimeText != null)
        {
            float best;
            bestTimeText.text = StageProgress.TryGetBestTime(StageStats.StageId, out best)
                ? string.Format(bestTimeFormat, StageProgress.FormatTime(best))
                : "";
        }
        yield return Wait(PartInterval);

        // 死亡シーン
        FillDeaths();
        yield return Pop(deathPanel);
        yield return RevealPhotos();
        yield return Wait(PartInterval);

        // 回収したキー
        yield return Pop(keysPanel);
        yield return RevealKeys(keys);
        yield return Wait(PartInterval);

        // 覚えた操作（新しく解放されたものだけ）
        yield return RevealRewards();

        // ボタン
        if (returnButton != null)
        {
            yield return Pop(returnButton.transform);
            returnButton.interactable = true;
        }
        _ready = true;
    }

    private void ApplyFont()
    {
        if (font == null)
            return;
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
            texts[i].font = font;
    }

    private IEnumerator CountUp(float time)
    {
        float t = 0f;
        while (t < TimeCountTime)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / TimeCountTime);
            float eased = 1f - (1f - k) * (1f - k);
            timeText.text = StageProgress.FormatTime(time * eased);
            yield return null;
        }
        timeText.text = StageProgress.FormatTime(time);
    }

    // ---- プレイヤー（動くキャラクター） -----------------------------------

    /// <summary>画面の外に撮影場所を作って、プレイヤーを置き、その映像を、Player Art の枠に貼る。</summary>
    private bool SetupPlayerLive()
    {
        if (playerController == null || playerArt == null)
            return false;

        RectTransform area = playerArt.rectTransform;
        float aspect = (area.rect.width > 1f && area.rect.height > 1f) ? area.rect.width / area.rect.height : 1.33f;

        _playerStage = new ResultCharacterStage("ResultPlayerStage", new Vector3(0f, -6000f, 0f), 1, playerViewHeight, aspect, 384);

        GameObject go = new GameObject("ResultPlayer");
        go.AddComponent<SpriteRenderer>(); // 絵は、Animatorが入れる

        // プレイヤーのAnimator Controllerは、動きに入ると、同じオブジェクトのAudioSourceで音を鳴らし
        // （PlaySoundBehaviour／JumpStateBehaviour）、攻撃の動きからDoDashDamageを呼ぶ。
        // 受け取り先が無いとエラーになるので、付けておく（音は、Player Plays Soundsがオフなら鳴らさない）。
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.mute = !playerPlaysSounds;
        go.AddComponent<PreviewAnimationEventSink>();

        Animator anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = playerController;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        _playerStage.Place(go, 0, true);
        go.transform.localScale = new Vector3(playerFacesRight ? -1f : 1f, 1f, 1f);

        playerArt.enabled = false;
        GameObject viewObj = new GameObject("PlayerLive", typeof(RectTransform));
        viewObj.transform.SetParent(area, false);
        RectTransform vr = (RectTransform)viewObj.transform;
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = Vector2.zero;
        vr.offsetMax = Vector2.zero;
        RawImage raw = viewObj.AddComponent<RawImage>();
        raw.texture = _playerStage.Texture;
        raw.raycastTarget = false;

        _playerArtScale = area.localScale;
        area.localScale = Vector3.zero; // ぽんと出るまで、見えない
        StartCoroutine(PlayerActionLoop(anim));
        return true;
    }

    /// <summary>ジャンプ・攻撃・ダッシュなどを、ランダムな順番で、間を空けて繰り返す（一巡するまで、同じものは出さない）。</summary>
    private IEnumerator PlayerActionLoop(Animator anim)
    {
        var bag = new List<int>();
        yield return Wait(0.6f);

        while (anim != null)
        {
            if (playerActions != null && playerActions.Length > 0)
            {
                if (bag.Count == 0)
                {
                    for (int i = 0; i < playerActions.Length; i++)
                        bag.Add(i);
                }
                int pick = Random.Range(0, bag.Count);
                int index = bag[pick];
                bag.RemoveAt(pick);
                yield return PlayPlayerAction(anim, playerActions[index]);
            }

            float a = Mathf.Max(0.1f, Mathf.Min(playerActionInterval.x, playerActionInterval.y));
            float b = Mathf.Max(a, Mathf.Max(playerActionInterval.x, playerActionInterval.y));
            yield return Wait(Random.Range(a, b));
        }
    }

    private IEnumerator PlayPlayerAction(Animator anim, KeyConfigPreviewPlayer.ActionPreviewTrigger t)
    {
        if (anim == null || string.IsNullOrEmpty(t.animatorParam))
            yield break;

        float hold = Mathf.Max(0.05f, t.holdDuration);
        switch (t.paramType)
        {
            case KeyConfigPreviewPlayer.PreviewParamType.Trigger:
                anim.ResetTrigger(t.animatorParam);
                anim.SetTrigger(t.animatorParam);
                yield return Wait(hold);
                break;

            case KeyConfigPreviewPlayer.PreviewParamType.Bool:
                anim.SetBool(t.animatorParam, true);
                yield return Wait(hold);
                if (anim != null)
                    anim.SetBool(t.animatorParam, false);
                break;

            case KeyConfigPreviewPlayer.PreviewParamType.Float:
                anim.SetFloat(t.animatorParam, t.floatValue);
                yield return Wait(hold);
                if (anim != null)
                    anim.SetFloat(t.animatorParam, 0f);
                break;
        }
    }

    // ---- 死亡シーン -----------------------------------------------------

    private readonly List<StageStats.DeathPhoto> _shown = new List<StageStats.DeathPhoto>();

    private void FillDeaths()
    {
        int deaths = StageStats.DeathCount;
        if (deathCountText != null)
            deathCountText.text = string.Format(deathCountFormat, deaths);

        _shown.Clear();
        int slots = photoSlots != null ? Mathf.Min(MaxPhotos, photoSlots.Length) : 0;
        if (deaths > 0)
            _shown.AddRange(StageStats.PickPhotos(slots));

        if (noDeathObject != null)
            noDeathObject.SetActive(deaths == 0);

        for (int i = 0; i < slots; i++)
        {
            PhotoSlot slot = photoSlots[i];
            if (slot == null || slot.root == null)
                continue;
            bool used = i < _shown.Count;
            slot.root.SetActive(used);
            if (!used)
                continue;

            StageStats.DeathPhoto p = _shown[i];
            if (slot.photo != null)
            {
                slot.photo.texture = p.texture;
                slot.photo.uvRect = CropRect(p, slot.photo);
            }
            if (slot.comment != null)
                slot.comment.text = PickComment(p.cause);
            slot.root.transform.localScale = Vector3.zero;
        }
    }

    /// <summary>写真を、プレイヤーを中心に、枠の縦横比に合わせて切り抜く。</summary>
    private static Rect CropRect(StageStats.DeathPhoto p, RawImage image)
    {
        if (p.texture == null)
            return new Rect(0f, 0f, 1f, 1f);

        Rect r = image.rectTransform.rect;
        float frameAspect = (r.width > 1f && r.height > 1f) ? r.width / r.height : DefaultFrameAspect;
        float texAspect = p.texture.width / (float)Mathf.Max(1, p.texture.height);

        float ch = PhotoCropHeight;
        float cw = ch * frameAspect / texAspect;
        if (cw > 1f)
        {
            cw = 1f;
            ch = Mathf.Min(1f, cw * texAspect / frameAspect);
        }

        float cx = Mathf.Clamp(p.playerView.x - cw * 0.5f, 0f, 1f - cw);
        float cy = Mathf.Clamp(p.playerView.y - ch * 0.5f, 0f, 1f - ch);
        return new Rect(cx, cy, cw, ch);
    }

    /// <summary>死因に合うリストから、ランダムで選ぶ（一巡するまで、同じものは出さない）。</summary>
    private string PickComment(DeathCause cause)
    {
        string[] list = cause == DeathCause.Fall ? commentsFall
                      : cause == DeathCause.Enemy ? commentsEnemy
                      : commentsOther;
        if (list == null || list.Length == 0)
            list = commentsOther;
        if (list == null || list.Length == 0)
            return "";

        List<string> bag = _commentBags[(int)cause];
        if (bag.Count == 0)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (!string.IsNullOrEmpty(list[i]))
                    bag.Add(list[i]);
            }
            if (bag.Count == 0)
                return "";
        }
        int pick = Random.Range(0, bag.Count);
        string s = bag[pick];
        bag.RemoveAt(pick);
        return s;
    }

    private IEnumerator RevealPhotos()
    {
        if (photoSlots == null)
            yield break;
        for (int i = 0; i < photoSlots.Length && i < _shown.Count; i++)
        {
            PhotoSlot slot = photoSlots[i];
            if (slot == null || slot.root == null)
                continue;
            StartCoroutine(Pop(slot.root.transform, Vector3.one));
            yield return Wait(PhotoInterval);
        }
        if (noDeathObject != null && noDeathObject.activeSelf)
            yield return Pop(noDeathObject.transform, Vector3.one);
    }

    // ---- 回収したキー ---------------------------------------------------

    private IEnumerator RevealKeys(List<KeyDefinition> keys)
    {
        if (keysContainer == null || keyItemTemplate == null || keys == null || keys.Count == 0)
            yield break;

        GetStarSprites(out Sprite filled, out Sprite empty);

        RectTransform tpl = (RectTransform)keyItemTemplate.transform;
        float w = tpl.rect.width > 1f ? tpl.rect.width : 120f;
        int n = keys.Count;

        float total = n * w + (n - 1) * keyItemSpacing;
        float avail = Mathf.Max(100f, keysContainer.rect.width - keysAreaPadding * 2f);
        float scale = Mathf.Min(1f, avail / total);

        // 手足つきのキャラクターで見せる（全員分を、1つの撮影場所でまとめて撮る）
        bool live = keyCharacterPrefab != null && keyItemTemplate.icon != null;
        if (live)
        {
            Rect ir = keyItemTemplate.icon.rectTransform.rect;
            float aspect = (ir.width > 1f && ir.height > 1f) ? ir.width / ir.height : 0.9f;
            _keyStage = new ResultCharacterStage("ResultKeyStage", new Vector3(0f, -5000f, 0f), n, keyViewHeight, aspect, 256);
        }

        var items = new List<Transform>();
        var rigs = new List<LimbRig>();
        for (int i = 0; i < n; i++)
        {
            StageResultKeyItem item = Instantiate(keyItemTemplate, keysContainer, false);
            item.gameObject.SetActive(true);
            item.Setup(keys[i], filled, empty);
            rigs.Add(live ? AttachKeyCharacter(item, keys[i], i) : null);

            RectTransform rt = (RectTransform)item.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float x = (-total * 0.5f + w * 0.5f + i * (w + keyItemSpacing)) * scale;
            rt.anchoredPosition = new Vector2(x, tpl.anchoredPosition.y * scale);
            rt.localScale = Vector3.zero;
            items.Add(item.transform);
        }

        for (int i = 0; i < items.Count; i++)
        {
            StartCoroutine(Pop(items[i], Vector3.one * scale));
            if (rigs[i] != null)
                StartCoroutine(CheerLater(rigs[i], keyCheerDelay));
            yield return Wait(KeyInterval);
        }
    }

    // ---- 覚えた操作 -----------------------------------------------------

    private IEnumerator RevealRewards()
    {
        if (_rewards == null || _rewards.Count == 0)
            yield break;

        RectTransform rt = rewardPanel;
        TMP_Text txt = rewardText;
        Vector3 target = Vector3.one;
        if (rt == null)
            rt = BuildRewardBanner(out txt);
        else
            target = ScaleOf(rt);

        if (txt != null)
            txt.text = string.Format(rewardFormat, string.Join("・", _rewards));

        yield return Pop(rt, target);
        yield return Wait(PartInterval);
    }

    /// <summary>Reward Panel が空の時に、枠と文字を、コードで作る。</summary>
    private RectTransform BuildRewardBanner(out TMP_Text text)
    {
        GameObject go = new GameObject("RewardBanner", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = rewardFallbackPos;
        rt.sizeDelta = rewardFallbackSize;
        rt.localScale = Vector3.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.96f, 0.96f, 0.95f, 1f);
        img.raycastTarget = false;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(rt, false);
        RectTransform tr = (RectTransform)textObj.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 6f);
        tr.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = font != null ? font : (timeText != null ? timeText.font : null);
        if (f != null)
            tmp.font = f;
        tmp.fontSize = 34f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 34f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        text = tmp;
        return rt;
    }

    /// <summary>キーのキャラクターを撮影場所に置いて、その映像を、キーの枠の「絵」の場所に貼る。</summary>
    private LimbRig AttachKeyCharacter(StageResultKeyItem item, KeyDefinition key, int slot)
    {
        GameObject go = Instantiate(keyCharacterPrefab);
        go.name = "ResultKey_" + (key != null ? key.name : "?");
        _keyStage.Place(go, slot, false);

        // 左（プレイヤーのほう）を向く。もとの絵が左向きなら、そのまま。
        float face = keyCharacterFacesRight ? -1f : 1f;
        Vector3 s = go.transform.localScale;
        go.transform.localScale = new Vector3(s.x * face * keyCharacterScale, s.y * keyCharacterScale, s.z * keyCharacterScale);

        // キートップの文字を、回収したキーにする
        KeyLabel[] labels = go.GetComponentsInChildren<KeyLabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].keyOverride = key;
            labels[i].Refresh();
        }

        LimbRig rig = go.GetComponentInChildren<LimbRig>(true);
        if (rig != null)
            rig.SetManualVelocity(0f);

        // 映像を貼る（キーの絵と、文字は、隠す）
        item.icon.enabled = false;
        if (item.label != null)
            item.label.gameObject.SetActive(false);

        GameObject viewObj = new GameObject("CharacterView", typeof(RectTransform));
        viewObj.transform.SetParent(item.icon.transform, false);
        RectTransform vr = (RectTransform)viewObj.transform;
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = Vector2.zero;
        vr.offsetMax = Vector2.zero;
        RawImage raw = viewObj.AddComponent<RawImage>();
        raw.texture = _keyStage.Texture;
        raw.uvRect = _keyStage.SlotUv(slot);
        raw.raycastTarget = false;
        return rig;
    }

    private IEnumerator CheerLater(LimbRig rig, float delay)
    {
        yield return Wait(delay);
        if (rig != null)
            rig.SetCheer(true);
    }

    /// <summary>星の絵：キーコンフィグ画面（KeyConfigUI）のものを借りる。無ければ、このパネルの予備。それも無ければ、コードで作る。</summary>
    private void GetStarSprites(out Sprite filled, out Sprite empty)
    {
        filled = null;
        empty = null;

        KeyConfigUI config = FindObjectOfType<KeyConfigUI>(true);
        if (config != null)
        {
            filled = config.starFilledSprite;
            empty = config.starEmptySprite;
        }
        if (filled == null) filled = starFilledSprite;
        if (empty == null) empty = starEmptySprite;
        if (filled == null) filled = BuildStarSprite(new Color32(255, 200, 40, 255));
        if (empty == null) empty = BuildStarSprite(new Color32(185, 185, 185, 255));
    }

    /// <summary>星の画像を、コードで作る（予備）。</summary>
    private static Sprite BuildStarSprite(Color32 color)
    {
        const int N = 64;
        var verts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float r = (i % 2 == 0) ? 0.95f : 0.40f;
            verts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < 3; sy++)
                {
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float fx = (x + (sx + 0.5f) / 3f) / N * 2f - 1f;
                        float fy = (y + (sy + 0.5f) / 3f) / N * 2f - 1f;
                        if (InsidePolygon(verts, fx, fy))
                            hit++;
                    }
                }
                px[y * N + x] = new Color32(color.r, color.g, color.b, (byte)(hit * 255 / 9));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool InsidePolygon(Vector2[] v, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            if ((v[i].y > y) != (v[j].y > y)
                && x < (v[j].x - v[i].x) * (y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                inside = !inside;
        }
        return inside;
    }

    // ================= ボタン =================

    private void Confirm()
    {
        if (!_ready || Confirmed)
            return;
        Confirmed = true;
        if (returnButton != null)
            returnButton.interactable = false;
        RestoreCursor();
    }

    private void ShowCursor()
    {
        if (!_cursorSaved)
        {
            _prevCursorVisible = Cursor.visible;
            _prevCursorLock = Cursor.lockState;
            _cursorSaved = true;
        }
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreCursor()
    {
        if (!_cursorSaved)
            return;
        _cursorSaved = false;
        Cursor.visible = _prevCursorVisible;
        Cursor.lockState = _prevCursorLock;
    }

    // ================= 動き =================

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Dt();
            yield return null;
        }
    }

    private static IEnumerator Fade(Graphic g, float time)
    {
        if (g == null)
            yield break;
        float t = 0f;
        while (t < time)
        {
            t += Dt();
            g.canvasRenderer.SetAlpha(Mathf.Clamp01(t / time));
            yield return null;
        }
        g.canvasRenderer.SetAlpha(1f);
    }

    private IEnumerator Pop(Transform t)
    {
        if (t == null)
            yield break;
        yield return Pop(t, ScaleOf(t));
    }

    private static IEnumerator Pop(Transform t, Vector3 target)
    {
        if (t == null)
            yield break;
        float time = 0f;
        while (time < PopTime)
        {
            time += Dt();
            if (t == null)
                yield break;
            t.localScale = target * EaseOutBack(Mathf.Clamp01(time / PopTime));
            yield return null;
        }
        if (t != null)
            t.localScale = target;
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
