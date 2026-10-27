using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// KEY ESCAPE - Stage 7 のボス「巨大なAキー」。
///
/// 戦いの流れ：
///   ・ムービーが終わると BeginBattle() が呼ばれる（Stage7Builder が自動でつなぐ）。
///   ・吠える → 体力ゲージが出る → 戦い始める。
///   ・主人公との距離で攻撃を選ぶ：
///       近い  → 近接攻撃（床に赤い範囲が出る。ジャンプかダッシュでかわせる。ハート1つ分）
///       遠い  → 落石（床に赤い印が出てから、上から石が落ちてくる）
///       どちらでもない → 主人公に向かって歩く
///   ・ボスがダメージを受けると、目の前に「川」が流れ、主人公は強制的に遠くへ流される。
///   ・遠いときは、落石か、キー封じの檻（当たると、キーが1つ、しばらく使えなくなる。KeySeal 参照）。
///   ・体力が半分になると、後半。動きが速くなり、落石が増え、近接が連続することもある。BGMも速くなる。
///   ・倒すと、うめいて座りこみ、消える。そのあと、Aキーを手に入れる（ポップアップ）。
///     そのあと、ボスのオブジェクトが非表示になるので、StageGoal（BossDefeatedモード）がステージクリアにする。
///     （クリアの演出＝回収したキーが横から走ってきて喜ぶ、のあと結果画面、は StageClearDirector が行う。
///      その間、主人公の操作は止まる。）
///
/// 主人公の攻撃は Attack.cs が「Enemy」タグのコライダーに ApplyDamage(float) を送るので、
/// このオブジェクトには「Enemy」タグと、トリガーのコライダーが要る（Stage7Builder が付ける）。
/// ※ Enemy.cs は付けない（付けると、StageGoal などが別の扱いをするため）。
/// </summary>
[DisallowMultipleComponent]
public class BossA : MonoBehaviour
{
    [Header("名前・体力")]
    [Tooltip("画面の上に出る名前")]
    public string bossName = "巨大キー A";
    [Tooltip("体に出す文字")]
    public string bossLabel = "A";
    [Tooltip("体力。主人公の通常攻撃は 1回 4 ダメージ（強攻撃は 6）")]
    public float maxHp = 40f;
    [Tooltip("この割合まで体力が減ると、後半（攻撃が激しくなる）")]
    [Range(0.05f, 0.95f)] public float phase2Ratio = 0.5f;
    [Tooltip("ダメージを受けてから、次のダメージを受けるまでの最短の秒数（1回の攻撃が二重に当たるのを防ぐ）")]
    public float hitCooldown = 0.12f;

    [Header("体力ゲージ")]
    [Tooltip("体力ゲージを、画面の下に出す（主人公のハートの体力ゲージは左上なので、重ならない）。OFFなら、上の中央")]
    public bool hpBarAtBottom = true;

    [Header("見た目")]
    [Tooltip("体の絵（KeyNPC_Visual のプレハブを子に置いたもの）。LimbRig は、この中から自動で探す")]
    public Transform visual;
    [Tooltip("日本語が出せるフォント（体力ゲージの名前に使う）")]
    public TMP_FontAsset font;
    [Tooltip("落石の絵（FallingStone のスプライト）。空なら、灰色の丸い石になる")]
    public Sprite rockSprite;
    [Tooltip("絵が元々右を向いているなら true（KeyNPC_Visual は左向きなので false）")]
    public bool visualFacesRight = false;
    [Tooltip("戦っている間、体のまわりに出る赤い光")]
    public Color auraColor = new Color(1f, 0.15f, 0.12f, 1f);
    [Tooltip("光の大きさ")]
    public float auraSize = 9f;
    [Tooltip("光の濃さ（0〜1）")]
    [Range(0f, 1f)] public float auraAlpha = 0.28f;
    [Tooltip("光の中心の高さ（足元から）")]
    public float auraHeight = 2.4f;

    [Header("戦う場所")]
    [Tooltip("ボスが行ける、いちばん左のx")]
    public float leftLimit = 97.5f;
    [Tooltip("ボスが行ける、いちばん右のx")]
    public float rightLimit = 134.5f;
    [Tooltip("床の高さ（床の上のy）")]
    public float groundY = -4.72f;
    [Tooltip("天井の高さ（落石は、ここから落ちてくる）")]
    public float ceilingY = 5.3f;

    [Header("戦いの始まり")]
    [Tooltip("戦いが始まるとき、カメラをこの割合まで引く（1が元の広さ。大きいほど広く見える）")]
    public float battleCameraZoom = 1.12f;
    [Tooltip("吠えている秒数（そのあと、体力ゲージが出て戦いが始まる）")]
    public float introSeconds = 2.4f;
    [Tooltip("戦いが始まってから、最初の攻撃までの秒数")]
    public float firstAttackDelay = 1.0f;
    [Tooltip("0より大きいと、ムービーなしでも、主人公がこの距離まで近づいたら戦いが始まる（テスト用）。0なら、使わない")]
    public float autoStartDistance = 0f;

    [Header("移動")]
    [Tooltip("歩く速さ")]
    public float walkSpeed = 1.8f;
    [Tooltip("後半の歩く速さ")]
    public float phase2WalkSpeed = 2.7f;

    [Header("近接攻撃")]
    [Tooltip("主人公がこの距離（ボスの中心から）より近いと、近接攻撃を選ぶ")]
    public float meleeRange = 4.6f;
    [Tooltip("1回あたりのダメージ（2 = ハート1つ分）")]
    public float meleeDamage = 2f;
    [Tooltip("赤い範囲が出てから、たたくまでの秒数。後半は 0.8倍")]
    public float meleeWindup = 0.6f;
    [Tooltip("たたく範囲の長さ（ボスの中心から前へ）")]
    public float meleeReach = 4.2f;
    [Tooltip("たたく範囲の高さ（床から）。これより高くジャンプすればかわせる")]
    public float meleeHeight = 2.4f;
    [Tooltip("たたくとき、前へ踏みこむ距離")]
    public float meleeLunge = 1.0f;
    [Tooltip("たたいたあとの、動けない秒数")]
    public float meleeRecover = 0.7f;
    [Tooltip("後半に、続けて2回たたく確率（0〜1）")]
    [Range(0f, 1f)] public float phase2ComboChance = 0.5f;

    [Header("攻撃の間かく")]
    [Tooltip("攻撃と攻撃の間の秒数")]
    public float attackInterval = 1.3f;
    [Tooltip("後半は、間かくをこの倍率にする（小さいほど激しい）")]
    public float phase2IntervalScale = 0.65f;

    [Header("落石")]
    [Tooltip("次の落石までの、最短の秒数")]
    public float rockInterval = 3.2f;
    [Tooltip("石を投げる前の、腕を上げる秒数")]
    public float rockRaiseSeconds = 0.7f;
    [Tooltip("落とす石の数")]
    public int rockCount = 3;
    [Tooltip("後半に落とす石の数")]
    public int rockCountPhase2 = 5;
    [Tooltip("石と石の、横のはなれ具合")]
    public float rockSpread = 2.6f;
    [Tooltip("印が出てから、石が落ちるまでの秒数")]
    public float rockWarnSeconds = 0.9f;
    [Tooltip("石を1つずつ出す間かく（秒）")]
    public float rockStagger = 0.2f;
    [Tooltip("石の大きさ")]
    public float rockSize = 1.3f;
    [Tooltip("石が当たったときのダメージ（2 = ハート1つ分）")]
    public float rockDamage = 2f;

    [Header("川（ダメージを受けると流れる）")]
    [Tooltip("ボスがダメージを受けたとき、川を流すか")]
    public bool riverOnHit = true;
    [Tooltip("主人公がこの距離より近いときだけ、川が流れる")]
    public float riverTriggerRange = 6.0f;
    [Tooltip("川がまた流れるまでの、最短の秒数")]
    public float riverCooldown = 0.7f;
    [Tooltip("川の長さ（ボスの前へ）")]
    public float riverWidth = 5.5f;
    [Tooltip("川の高さ")]
    public float riverHeight = 1.8f;
    [Tooltip("川が見えている秒数")]
    public float riverSeconds = 0.6f;
    [Tooltip("主人公を流す速さ")]
    public float riverPushSpeed = 17f;
    [Tooltip("主人公が流されて、操作できない秒数")]
    public float riverPushSeconds = 0.3f;

    [Header("後半に入るとき")]
    [Tooltip("吠えている秒数")]
    public float phase2PauseSeconds = 1.0f;
    [Tooltip("後半に入ると、BGMをこの速さにする（1が普通、1.15なら15%速い。音の高さも少し上がる）。1なら、変えない")]
    public float phase2BgmSpeed = 1.15f;
    [Tooltip("BGMが速くなるまでの秒数")]
    public float phase2BgmSpeedSeconds = 1.5f;

    [Header("キー封じの檻（遠いときに投げてくる）")]
    [Tooltip("檻を投げるか")]
    public bool cageEnabled = true;
    [Tooltip("戦いが始まってから、最初に投げるまでの、最短の秒数")]
    public float cageFirstDelay = 6f;
    [Tooltip("次に投げるまでの、最短の秒数。後半は短くなる")]
    public float cageInterval = 9f;
    [Tooltip("投げる前の、腕を上げる秒数")]
    public float cageWindup = 0.8f;
    [Tooltip("檻が飛んで、落ちるまでの秒数。長いほど、よけやすい")]
    public float cageFlightSeconds = 1.0f;
    [Tooltip("檻を投げる手の高さ（足元から）")]
    public float cageHandHeight = 4.2f;
    [Tooltip("檻の大きさ")]
    public float cageSize = 1.5f;
    [Tooltip("落ちたあと、地面をすべる秒数（この間も当たる）")]
    public float cageSlideSeconds = 0.7f;
    [Tooltip("キーが封じられている秒数")]
    public float cageSealSeconds = 14f;
    [Tooltip("後半に封じられる秒数")]
    public float cagePhase2SealSeconds = 18f;
    [Tooltip("同時に封じられる、キーの数の上限")]
    public int cageMaxSealed = 2;
    [Tooltip("当たったときのダメージ（0ならなし。2 = ハート1つ分）")]
    public float cageDamage = 0f;
    public AudioClip cageThrowSfx;
    public AudioClip cageHitSfx;

    [Header("せりふ")]
    [Tooltip("戦いが始まって、ボスが吠えたあとに、Kキーが話すヒント（ゲームが止まる。Spaceで進む）。空なら、出さない")]
    [TextArea(1, 3)]
    public string introHint = "あれはAキー!?なんであんな姿に…";
    [Tooltip("ヒントの説明パネル（Assets/Prefab/GimmickIntroUI）。空なら、シーンの GimmickIntroZone のものを使う")]
    public GimmickIntroUI hintUiPrefab;
    [Tooltip("ヒントが出てから、Spaceを受け付けるまでの秒数")]
    public float hintInputDelay = 0.6f;
    [Tooltip("ヒントの前に、カメラをAキー（ボス）へ向ける")]
    public bool hintLookAtBoss = true;
    [Tooltip("カメラがAキーへ動く秒数")]
    public float hintCameraSeconds = 1.2f;
    [Tooltip("ヒントが終わって、カメラが主人公へ戻る秒数")]
    public float hintCameraReturnSeconds = 1.0f;
    [Tooltip("カメラを寄せる広さ（1=寄せない、0.8=少し寄る）")]
    [Range(0.4f, 1f)] public float hintZoomRatio = 0.85f;
    [Tooltip("カメラが映す点の高さ（ボスの足元から）。0なら、ボスの体の中心")]
    public float hintLookHeight = 0f;
    [Tooltip("体力が半分以下になったとき、ボスが頭の上に出すせりふ。空なら、出さない")]
    [TextArea(1, 3)]
    public string phase2Line = "お前はいつも誰のお陰で左に進んでいたか忘れたのか?";
    [Tooltip("せりふを出しておく秒数")]
    public float lineSeconds = 5f;
    [Tooltip("せりふの文字が出る速さ（1秒に何文字）")]
    public float lineCharsPerSecond = 14f;
    [Tooltip("せりふの位置（頭の上からのずれ）")]
    public Vector2 lineOffset = new Vector2(0f, 1.6f);
    [Tooltip("せりふをしゃべる音。空なら、会話の音")]
    public AudioClip lineTalkSfx;

    [Header("倒したとき：ごほうび")]
    [Tooltip("倒すと手に入るキー（Key_A）。空なら、持っているキー一覧の中から「A」を探す")]
    public KeyDefinition keyDrop;
    [Tooltip("ステージクリアの演出で、横から走ってきて踊るキーの数の目安（最大）。このステージで入手したキーに足りない分は、持っているキーからランダムに足す（Aキーは優先）。0なら、足さない")]
    public int clearKeyCount = 4;

    [Header("倒れるとき")]
    [Tooltip("座りこんで、かけらが飛び散る秒数")]
    public float defeatSeconds = 2.2f;
    [Tooltip("すうっと消える秒数")]
    public float defeatFadeSeconds = 0.9f;
    [Tooltip("倒れて消えたときに呼ばれる")]
    public UnityEvent onDefeated;

    [Header("効果音（空なら、鳴らさない）")]
    public AudioClip roarSfx;
    public AudioClip windupSfx;
    public AudioClip swingSfx;
    public AudioClip rockRaiseSfx;
    public AudioClip rockBreakSfx;
    public AudioClip hurtSfx;
    public AudioClip riverSfx;
    public AudioClip phase2Sfx;
    public AudioClip defeatSfx;
    public AudioClip footstepSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // ---- 状態 ----
    private LimbRig _rig;
    private Vector3 _baseScale = Vector3.one;
    private float _facing = -1f;
    private float _hp;
    private bool _started;
    private bool _fighting;
    private bool _phase2;
    private bool _phaseChanging;
    private bool _dead;
    private float _nextRockTime;
    private float _nextCageTime;
    private float _nextRiverTime;
    private float _lastHitTime = -10f;
    private float _stepTimer;

    private GameObject _player;
    private CharacterController2D _playerCc;
    private int _order;          // 主人公の絵の手前の順番
    private int _layerId;
    private bool _sortDone;

    private BossHpBar _bar;
    private SpriteRenderer _auraSr;
    private float _auraLevel;

    private GameObject _zoneRoot;
    private SpriteRenderer _zoneBox;
    private SpriteRenderer _zoneLine;

    private readonly List<BossRock> _rocks = new List<BossRock>();
    private readonly List<BossCage> _cages = new List<BossCage>();
    private BossSpeechBubble _bubble;

    // ---- 外から読めるもの ----
    public bool IsDead { get { return _dead; } }
    public bool IsFighting { get { return _fighting && !_dead; } }
    public bool IsPhase2 { get { return _phase2; } }
    public float Hp { get { return _hp; } }
    public float HpRatio { get { return maxHp > 0f ? Mathf.Clamp01(_hp / maxHp) : 0f; } }

    // ================= 準備 =================

    private void Awake()
    {
        _hp = maxHp;

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2.8f, 4.6f);
            box.offset = new Vector2(0f, 2.3f);
        }
        if (CompareTag("Untagged"))
            gameObject.tag = "Enemy";

        if (visual != null)
        {
            Vector3 s = visual.localScale;
            _baseScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
            _rig = visual.GetComponentInChildren<LimbRig>(true);
        }
    }

    private void Start()
    {
        FindPlayer();
        ApplySorting();
        BuildAura();

        // 封じられたキーの檻（画面の上）に出す文字のフォント
        if (font != null)
            KeySeal.HudFont = font;

        // はじめは、主人公の来る方（左）を向く
        Face(-1f);

        if (visual != null)
        {
            KeyLabel[] labels = visual.GetComponentsInChildren<KeyLabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].textOverride = bossLabel;
                labels[i].Refresh();
            }
        }
        if (_rig != null)
            _rig.SetManualVelocity(0f);
    }

    private void FindPlayer()
    {
        if (_player != null)
            return;
        _player = NewGameIntro.FindMainPlayerObject();
        if (_player != null)
            _playerCc = _player.GetComponent<CharacterController2D>();
    }

    /// <summary>ボスの絵を、主人公の少しうしろに並べ直す（一度だけ）。</summary>
    private void ApplySorting()
    {
        if (_sortDone)
            return;
        if (_player == null)
            return;
        SpriteRenderer psr = _player.GetComponentInChildren<SpriteRenderer>();
        if (psr != null)
        {
            _order = psr.sortingOrder;
            _layerId = psr.sortingLayerID;
        }
        _sortDone = true;

        if (visual != null)
        {
            Renderer[] rs = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                rs[i].sortingLayerID = _layerId;
                rs[i].sortingOrder = _order - 30 + rs[i].sortingOrder;
            }
        }
        if (_auraSr != null)
        {
            _auraSr.sortingLayerID = _layerId;
            _auraSr.sortingOrder = _order - 40;
        }
    }

    private void BuildAura()
    {
        GameObject g = new GameObject("BossAura");
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(0f, auraHeight, 0f);
        g.transform.localScale = Vector3.one * (auraSize / 1.28f);
        _auraSr = g.AddComponent<SpriteRenderer>();
        _auraSr.sprite = BossSpriteUtil.Radial();
        _auraSr.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0f);
        _auraSr.sortingLayerID = _layerId;
        _auraSr.sortingOrder = _order - 40;
    }

    // ================= 毎フレーム =================

    private void Update()
    {
        // テスト用：ムービーなしで戦いを始める
        if (!_started && !_dead && autoStartDistance > 0f)
        {
            FindPlayer();
            if (_player != null && Mathf.Abs(_player.transform.position.x - transform.position.x) <= autoStartDistance)
                BeginBattle();
        }

        // 赤い光
        if (_auraSr != null)
        {
            float target = (!_fighting || _dead) ? 0f : (_phase2 ? 1.6f : 1f);
            _auraLevel = Mathf.MoveTowards(_auraLevel, target, Time.deltaTime * 1.2f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (_phase2 ? 7f : 3f));
            Color c = auraColor;
            if (_phase2)
                c = Color.Lerp(auraColor, new Color(1f, 0.5f, 0.1f, 1f), 0.5f);
            c.a = Mathf.Clamp01(auraAlpha * _auraLevel * pulse);
            _auraSr.color = c;
        }
    }

    // ================= 戦いの始まり =================

    /// <summary>戦いを始める。ムービーが終わったとき（BossIntroMovie.onMovieEnd）に呼ばれる。</summary>
    public void BeginBattle()
    {
        if (_started || _dead)
            return;
        _started = true;
        FindPlayer();
        ApplySorting();
        StartCoroutine(BattleRoutine());
    }

    private IEnumerator BattleRoutine()
    {
        // 吠える
        BossSpriteUtil.Zoom(battleCameraZoom, 1.2f);
        BossSpriteUtil.PlaySfx(roarSfx, sfxVolume);
        BossSpriteUtil.Shake(Mathf.Min(introSeconds, 1.2f), 0.14f);
        if (_rig != null)
            _rig.SetMotion(LimbRig.KeyMotion.Cheer);
        _auraLevel = 0f;
        _fighting = true;   // 光が出始める（ダメージは、ゲージが出てから）

        yield return new WaitForSeconds(Mathf.Max(0.2f, introSeconds));

        if (_rig != null)
            _rig.SetMotion(LimbRig.KeyMotion.None);

        // Kキーのヒント「あれはAキー!?…」（ゲームが止まる）
        yield return IntroHint();

        _bar = BossHpBar.Create(font, bossName, hpBarAtBottom);
        _bar.SetRatio(HpRatio);

        _acceptDamage = true;
        yield return new WaitForSeconds(Mathf.Max(0f, firstAttackDelay));

        _nextRockTime = Time.time + 1.0f;
        _nextCageTime = Time.time + Mathf.Max(0f, cageFirstDelay);
        yield return FightLoop();
    }

    private bool _acceptDamage;

    // ================= せりふ =================

    /// <summary>Kキーが、画面の下の説明パネルで話す（ゲームを止めて、Spaceで進める）。</summary>
    private IEnumerator IntroHint()
    {
        if (string.IsNullOrEmpty(introHint))
            yield break;

        GimmickIntroUI prefab = hintUiPrefab;
        if (prefab == null)
        {
            GimmickIntroZone zone = FindObjectOfType<GimmickIntroZone>(true);
            if (zone != null)
                prefab = zone.uiPrefab;
        }
        if (prefab == null)
        {
            Debug.LogWarning("[BossA] ヒントの説明パネル（Hint Ui Prefab）が無いので、Kキーのヒントは出ません。Assets/Prefab/GimmickIntroUI を入れてください。");
            yield break;
        }

        FindPlayer();
        PlayerMovement pm = _player != null ? _player.GetComponent<PlayerMovement>() : null;

        GamePause.Acquire(this);
        if (pm != null)
            pm.SetInputLocked(true);

        // カメラを、Aキー（ボス）へ向ける
        CameraFollow cam = hintLookAtBoss ? FindObjectOfType<CameraFollow>() : null;
        Transform originalTarget = null;
        GameObject lookPoint = null;
        if (cam != null)
        {
            originalTarget = cam.Target != null ? cam.Target : (_player != null ? _player.transform : null);
            cam.followUnscaled = true;

            Vector3 p = transform.position;
            Collider2D bc = GetComponent<Collider2D>();
            if (hintLookHeight > 0.01f)
                p.y = transform.position.y + hintLookHeight;
            else if (bc != null)
                p = bc.bounds.center;
            lookPoint = new GameObject("BossHintLookPoint");
            lookPoint.transform.position = new Vector3(p.x, p.y, transform.position.z);

            float move = Mathf.Max(0f, hintCameraSeconds);
            cam.MoveTo(lookPoint.transform, move);
            if (hintZoomRatio < 0.999f)
                cam.ZoomTo(hintZoomRatio, Mathf.Max(0.01f, move));
            yield return WaitHintCamera(cam, lookPoint.transform, move);
        }

        GimmickIntroUI ui = Instantiate(prefab);
        SoundEffects.Play(SEId.DialogueOpen);
        ui.Show(introHint);

        float t = 0f;
        float delay = Mathf.Max(0.1f, hintInputDelay);
        while (true)
        {
            t += Time.unscaledDeltaTime;
            bool typing = ui.IsTyping;
            ui.SetPromptVisible(!typing && t >= delay);
            if (t >= delay && Input.GetKeyDown(KeyCode.Space))
            {
                if (typing)
                    ui.CompleteText();
                else
                    break;
            }
            yield return null;
        }

        SoundEffects.Play(SEId.DialogueAdvance);
        ui.Hide();
        Destroy(ui.gameObject);
        SoundEffects.Play(SEId.DialogueClose);

        // カメラを、主人公へ戻す
        if (cam != null)
        {
            float back = Mathf.Max(0f, hintCameraReturnSeconds);
            cam.MoveTo(originalTarget, back);
            if (hintZoomRatio < 0.999f)
                cam.ZoomTo(1f, Mathf.Max(0.01f, back));
            yield return WaitHintCamera(cam, originalTarget, back);
            cam.followUnscaled = false;
            if (originalTarget != null)
                cam.Target = originalTarget;
            if (lookPoint != null)
                Destroy(lookPoint);
        }

        if (pm != null)
            pm.SetInputLocked(false);
        GamePause.Release(this);
    }

    /// <summary>カメラが目的地に着くまで待つ（止まっている間も進む時間で、最長で minWait+1.5 秒）。</summary>
    private IEnumerator WaitHintCamera(CameraFollow cam, Transform dest, float minWait)
    {
        float t = 0f;
        while (t < minWait + 1.5f)
        {
            t += Time.unscaledDeltaTime;
            if (t >= minWait)
            {
                if (cam == null || dest == null)
                    break;
                Vector2 d = (Vector2)cam.transform.position - (Vector2)dest.position;
                if (d.magnitude < 0.2f)
                    break;
            }
            yield return null;
        }
    }

    /// <summary>ボスの頭の上に、せりふを出す。</summary>
    private void SaySpeech(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;
        if (_bubble != null)
            _bubble.Hide();

        float top = groundY + 5f;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            top = col.bounds.max.y;
        Vector3 offset = new Vector3(lineOffset.x, (top - transform.position.y) + lineOffset.y, 0f);

        _bubble = BossSpeechBubble.Show(transform, offset, message, font, lineSeconds, lineCharsPerSecond,
            _order + 60, _layerId, lineTalkSfx, sfxVolume);
    }

    // ================= 戦いの本体 =================

    private float IntervalScale
    {
        get { return _phase2 ? phase2IntervalScale : 1f; }
    }

    private bool PlayerAlive()
    {
        if (_player == null)
            FindPlayer();
        if (_player == null)
            return false;
        if (HitFeedback.IsDying)
            return false;
        if (_playerCc != null && _playerCc.life <= 0f)
            return false;
        return true;
    }

    private IEnumerator FightLoop()
    {
        while (!_dead)
        {
            while (_phaseChanging && !_dead)
                yield return null;

            if (!PlayerAlive())
            {
                StopWalk();
                yield return null;
                continue;
            }

            float dx = _player.transform.position.x - transform.position.x;
            Face(dx);
            float dist = Mathf.Abs(dx);

            if (dist <= meleeRange)
            {
                StopWalk();
                yield return MeleeAttack();
                yield return Pause(attackInterval);
                continue;
            }

            bool rockReady = Time.time >= _nextRockTime;
            bool cageReady = CageReady();
            if (rockReady || cageReady)
            {
                StopWalk();
                if (cageReady && (!rockReady || Random.value < 0.5f))
                    yield return CageAttack();
                else
                    yield return RockAttack();
                yield return Pause(attackInterval);
                continue;
            }

            Walk(dx >= 0f ? 1f : -1f);
            yield return null;
        }
    }

    private IEnumerator Pause(float seconds)
    {
        float total = Mathf.Max(0f, seconds) * IntervalScale;
        float t = 0f;
        while (t < total && !_dead)
        {
            t += Time.deltaTime;
            FacePlayer();
            yield return null;
        }
    }

    private void FacePlayer()
    {
        if (_player == null)
            return;
        float dx = _player.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.6f)
            Face(dx);
    }

    private void Face(float dir)
    {
        if (Mathf.Abs(dir) < 0.0001f)
            return;
        bool right = dir > 0f;
        _facing = right ? 1f : -1f;
        if (visual != null)
        {
            float s = (visualFacesRight == right) ? 1f : -1f;
            visual.localScale = new Vector3(_baseScale.x * s, _baseScale.y, _baseScale.z);
        }
    }

    private float ClampX(float x)
    {
        return Mathf.Clamp(x, leftLimit, rightLimit);
    }

    // ---------- 歩く ----------

    private void Walk(float dir)
    {
        float speed = _phase2 ? phase2WalkSpeed : walkSpeed;
        Vector3 p = transform.position;
        p.x = ClampX(p.x + dir * speed * Time.deltaTime);
        transform.position = p;
        if (_rig != null)
            _rig.SetManualVelocity(dir * speed);

        _stepTimer -= Time.deltaTime;
        if (_stepTimer <= 0f)
        {
            _stepTimer = _phase2 ? 0.34f : 0.48f;
            BossSpriteUtil.PlaySfx(footstepSfx, sfxVolume * 0.7f);
            BossSpriteUtil.Shake(0.06f, 0.025f);
        }
    }

    private void StopWalk()
    {
        if (_rig != null)
            _rig.SetManualVelocity(0f);
    }

    // ---------- 近接攻撃 ----------

    private void ZoneRect(float fromX, float dir, out Vector2 center, out Vector2 size)
    {
        float near = 0.4f;
        float far = Mathf.Max(near + 0.5f, meleeReach);
        center = new Vector2(fromX + dir * (near + far) * 0.5f, groundY + (meleeHeight - 0.4f) * 0.5f);
        size = new Vector2(far - near, meleeHeight + 0.4f);
    }

    private IEnumerator MeleeAttack()
    {
        int strikes = 1;
        if (_phase2 && Random.value < phase2ComboChance)
            strikes = 2;

        for (int i = 0; i < strikes; i++)
        {
            if (_dead || !PlayerAlive())
                yield break;

            FacePlayer();
            float dir = _facing;
            float windup = Mathf.Max(0.15f, meleeWindup * (_phase2 ? 0.8f : 1f) * (i > 0 ? 0.7f : 1f));
            float strikeX = ClampX(transform.position.x + dir * meleeLunge);

            ShowZone(strikeX, dir);
            BossSpriteUtil.PlaySfx(windupSfx, sfxVolume);

            float t = 0f;
            while (t < windup)
            {
                t += Time.deltaTime;
                UpdateZone(Mathf.Clamp01(t / windup), t);
                if (_dead)
                {
                    HideZone();
                    yield break;
                }
                yield return null;
            }

            // たたく：前へ踏みこんで、当たりを調べる
            if (_rig != null)
                _rig.PlayAttack();
            BossSpriteUtil.PlaySfx(swingSfx, sfxVolume);
            yield return Lunge(strikeX, 0.08f);
            DoMeleeHit(strikeX, dir);
            BossSpriteUtil.Shake(0.18f, 0.09f);
            BossBits.Burst(new Vector3(strikeX + dir * (meleeReach * 0.6f), groundY + 0.2f, 0f),
                new Color(0.75f, 0.7f, 0.6f, 1f), 8, 5f, 0.16f, 0.6f, _order + 20, 14f);
            HideZone();

            yield return new WaitForSeconds(i < strikes - 1 ? 0.12f : meleeRecover);
        }
    }

    private IEnumerator Lunge(float toX, float seconds)
    {
        float fromX = transform.position.x;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / seconds);
            Vector3 p = transform.position;
            p.x = Mathf.Lerp(fromX, toX, u);
            transform.position = p;
            yield return null;
        }
    }

    private void DoMeleeHit(float fromX, float dir)
    {
        Vector2 center;
        Vector2 size;
        ZoneRect(fromX, dir, out center, out size);
        Collider2D[] cols = Physics2D.OverlapBoxAll(center, size, 0f);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || !cols[i].CompareTag("Player"))
                continue;
            CharacterController2D cc = cols[i].GetComponentInParent<CharacterController2D>();
            if (cc == null)
                continue;
            cc.ApplyDamage(meleeDamage, new Vector3(transform.position.x, transform.position.y + 1f, 0f));
            return;
        }
    }

    // ---------- 近接の範囲の表示 ----------

    private void ShowZone(float fromX, float dir)
    {
        HideZone();
        Vector2 center;
        Vector2 size;
        ZoneRect(fromX, dir, out center, out size);

        _zoneRoot = new GameObject("BossMeleeZone");
        _zoneRoot.transform.position = new Vector3(center.x, 0f, 0f);

        GameObject b = new GameObject("Box");
        b.transform.SetParent(_zoneRoot.transform, false);
        b.transform.position = new Vector3(center.x, groundY + meleeHeight * 0.5f, 0f);
        b.transform.localScale = new Vector3(size.x, meleeHeight, 1f);
        _zoneBox = b.AddComponent<SpriteRenderer>();
        _zoneBox.sprite = BossSpriteUtil.White();
        _zoneBox.color = new Color(1f, 0.1f, 0.1f, 0f);
        _zoneBox.sortingLayerID = _layerId;
        _zoneBox.sortingOrder = _order - 20;

        GameObject l = new GameObject("Line");
        l.transform.SetParent(_zoneRoot.transform, false);
        l.transform.position = new Vector3(center.x, groundY + 0.07f, 0f);
        l.transform.localScale = new Vector3(size.x, 0.14f, 1f);
        _zoneLine = l.AddComponent<SpriteRenderer>();
        _zoneLine.sprite = BossSpriteUtil.White();
        _zoneLine.color = new Color(1f, 0.2f, 0.15f, 0f);
        _zoneLine.sortingLayerID = _layerId;
        _zoneLine.sortingOrder = _order - 19;
    }

    private void UpdateZone(float u, float t)
    {
        float blink = 0.8f + 0.2f * Mathf.Sin(t * 30f);
        if (_zoneBox != null)
            _zoneBox.color = new Color(1f, 0.1f, 0.1f, Mathf.Lerp(0.06f, 0.3f, u) * blink);
        if (_zoneLine != null)
            _zoneLine.color = new Color(1f, 0.25f, 0.2f, Mathf.Lerp(0.3f, 0.95f, u));
    }

    private void HideZone()
    {
        if (_zoneRoot != null)
            Destroy(_zoneRoot);
        _zoneRoot = null;
        _zoneBox = null;
        _zoneLine = null;
    }

    // ---------- 落石 ----------

    private IEnumerator RockAttack()
    {
        FacePlayer();
        int n = Mathf.Max(1, _phase2 ? rockCountPhase2 : rockCount);

        if (_rig != null)
            _rig.SetMotion(LimbRig.KeyMotion.Cheer);
        BossSpriteUtil.PlaySfx(rockRaiseSfx, sfxVolume);
        BossSpriteUtil.Shake(0.5f, 0.05f);
        yield return new WaitForSeconds(Mathf.Max(0.1f, rockRaiseSeconds) * (_phase2 ? 0.75f : 1f));

        float topY = ceilingY - rockSize * 0.5f - 0.1f;
        for (int i = 0; i < n; i++)
        {
            if (_dead || !PlayerAlive())
                break;

            float px = _player.transform.position.x;
            float off = 0f;
            if (i > 0)
            {
                int ring = (i + 1) / 2;
                float side = (i % 2 == 1) ? 1f : -1f;
                off = side * ring * rockSpread + Random.Range(-0.4f, 0.4f);
            }
            float x = Mathf.Clamp(px + off, leftLimit, rightLimit);

            _rocks.RemoveAll(r => r == null);
            BossRock rock = BossRock.Spawn(rockSprite, x, topY, groundY, rockSize, rockWarnSeconds,
                rockDamage, rockBreakSfx, sfxVolume);
            _rocks.Add(rock);

            yield return new WaitForSeconds(Mathf.Max(0.05f, rockStagger));
        }

        if (_rig != null)
            _rig.SetMotion(LimbRig.KeyMotion.None);
        _nextRockTime = Time.time + rockInterval * IntervalScale;
        yield return new WaitForSeconds(0.5f);
    }

    // ---------- キー封じの檻 ----------

    private bool CageReady()
    {
        if (!cageEnabled || Time.time < _nextCageTime)
            return false;
        if (KeySeal.Count >= Mathf.Max(1, cageMaxSealed))
            return false;
        // 封じられたとき、代わりに設定できるキーが無いと、動けなくなってしまうので、投げない
        if (KeySeal.SpareKeyCount() < 1)
            return false;
        return KeySeal.PickableKeys().Count > 0;
    }

    private IEnumerator CageAttack()
    {
        List<KeyDefinition> list = KeySeal.PickableKeys();
        if (list.Count == 0)
        {
            _nextCageTime = Time.time + 2f;
            yield break;
        }
        KeyDefinition target = list[Random.Range(0, list.Count)];

        FacePlayer();
        if (_rig != null)
            _rig.SetMotion(LimbRig.KeyMotion.Cheer);
        BossSpriteUtil.PlaySfx(rockRaiseSfx, sfxVolume);
        BossSpriteUtil.Shake(0.4f, 0.04f);
        yield return new WaitForSeconds(Mathf.Max(0.1f, cageWindup) * (_phase2 ? 0.8f : 1f));

        if (_dead || !PlayerAlive())
        {
            if (_rig != null)
                _rig.SetMotion(LimbRig.KeyMotion.None);
            yield break;
        }

        // 投げる：いまの主人公の場所へ
        if (_rig != null)
        {
            _rig.SetMotion(LimbRig.KeyMotion.None);
            _rig.PlayThrow();
        }
        BossSpriteUtil.PlaySfx(cageThrowSfx, sfxVolume);

        Vector3 hand = new Vector3(transform.position.x + _facing * 1.8f, groundY + cageHandHeight, 0f);
        float landX = Mathf.Clamp(_player.transform.position.x, leftLimit - 1f, rightLimit + 1f);
        float seal = _phase2 ? cagePhase2SealSeconds : cageSealSeconds;

        _cages.RemoveAll(c => c == null);
        _cages.Add(BossCage.Spawn(hand, landX, groundY, target, cageSize, cageFlightSeconds, cageSlideSeconds,
            seal, cageDamage, _order + 15, _layerId, font, cageHitSfx, sfxVolume));

        _nextCageTime = Time.time + cageInterval * IntervalScale;
        yield return new WaitForSeconds(0.5f);
    }

    private void CancelCages()
    {
        for (int i = 0; i < _cages.Count; i++)
        {
            if (_cages[i] != null)
                _cages[i].Cancel();
        }
        _cages.Clear();
    }

    private void CancelRocks()
    {
        for (int i = 0; i < _rocks.Count; i++)
        {
            if (_rocks[i] != null)
                _rocks[i].Cancel();
        }
        _rocks.Clear();
    }

    // ================= ダメージ =================

    /// <summary>
    /// 主人公の攻撃（Attack.cs）から SendMessage で呼ばれる。
    /// 引数の正負は向きを表すだけなので、大きさ（絶対値）がダメージ。
    /// </summary>
    public void ApplyDamage(float damage)
    {
        if (_dead || !_acceptDamage)
            return;
        if (Time.time - _lastHitTime < hitCooldown)
            return;
        _lastHitTime = Time.time;

        float dmg = Mathf.Abs(damage);
        _hp = Mathf.Max(0f, _hp - dmg);

        if (_bar != null)
            _bar.SetRatio(HpRatio);
        if (_rig != null)
            _rig.PlayHit();
        BossSpriteUtil.PlaySfx(hurtSfx, sfxVolume);

        float dirToPlayer = 1f;
        if (_player != null)
            dirToPlayer = _player.transform.position.x >= transform.position.x ? 1f : -1f;
        BossBits.Burst(new Vector3(transform.position.x + dirToPlayer * 1.2f, groundY + 2.2f, 0f),
            new Color(1f, 0.85f, 0.5f, 1f), 7, 6f, 0.14f, 0.5f, _order + 20, 18f);

        if (_hp <= 0f)
        {
            Die();
            return;
        }

        TriggerRiver();

        if (!_phase2 && _hp <= maxHp * phase2Ratio)
            StartCoroutine(Phase2Routine());
    }

    // ---------- 川 ----------

    private void TriggerRiver()
    {
        if (!riverOnHit || _dead || _player == null)
            return;
        if (Time.time < _nextRiverTime)
            return;
        float dx = _player.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) > riverTriggerRange)
            return;

        float dir = dx >= 0f ? 1f : -1f;
        _nextRiverTime = Time.time + riverCooldown;
        Vector3 feet = new Vector3(transform.position.x + dir * 0.8f, groundY, 0f);
        BossRiverBurst.Spawn(feet, dir, riverWidth, riverHeight, riverSeconds,
            _player, dir, riverPushSpeed, riverPushSeconds, _order + 25);
        BossSpriteUtil.PlaySfx(riverSfx, sfxVolume);
    }

    // ---------- 後半に入る ----------

    private IEnumerator Phase2Routine()
    {
        _phaseChanging = true;
        _phase2 = true;
        if (_bar != null)
            _bar.SetPhase2();

        // BGMを速くする
        if (Mathf.Abs(phase2BgmSpeed - 1f) > 0.001f)
            AudioManager.SetBgmSpeed(phase2BgmSpeed, phase2BgmSpeedSeconds);

        SaySpeech(phase2Line);

        BossSpriteUtil.PlaySfx(phase2Sfx, sfxVolume);
        BossSpriteUtil.Shake(Mathf.Max(0.3f, phase2PauseSeconds), 0.15f);
        if (_rig != null)
        {
            _rig.SetManualVelocity(0f);
            _rig.SetMotion(LimbRig.KeyMotion.Cheer);
        }
        BossBits.Burst(new Vector3(transform.position.x, groundY + 2.5f, 0f),
            new Color(1f, 0.4f, 0.15f, 1f), 18, 9f, 0.22f, 0.9f, _order + 20, 10f);

        yield return new WaitForSeconds(Mathf.Max(0.2f, phase2PauseSeconds));

        if (_rig != null)
            _rig.SetMotion(LimbRig.KeyMotion.None);
        _phaseChanging = false;
    }

    // ================= 倒れる =================

    private void Die()
    {
        if (_dead)
            return;
        _dead = true;
        StopAllCoroutines();
        StartCoroutine(DefeatRoutine());
    }

    private IEnumerator DefeatRoutine()
    {
        if (_bar != null)
            _bar.Hide();
        HideZone();
        CancelRocks();
        CancelCages();
        if (_bubble != null)
            _bubble.Hide();

        // 封じていたキーを全部解いて、BGMの速さをもどす
        KeySeal.ReleaseAll();
        AudioManager.SetBgmSpeed(1f, 1.2f);

        Collider2D[] cols = GetComponents<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        if (_rig != null)
        {
            _rig.SetManualVelocity(0f);
            _rig.SetMotion(LimbRig.KeyMotion.Injured);
        }
        BossSpriteUtil.PlaySfx(defeatSfx, sfxVolume);
        BossSpriteUtil.Shake(1.0f, 0.16f);
        BossSpriteUtil.Zoom(1f, 1.5f);

        float t = 0f;
        float nextBurst = 0f;
        while (t < defeatSeconds)
        {
            t += Time.deltaTime;
            if (t >= nextBurst)
            {
                nextBurst = t + 0.28f;
                Vector3 p = new Vector3(transform.position.x + Random.Range(-1.4f, 1.4f),
                    groundY + Random.Range(0.8f, 3.4f), 0f);
                BossBits.Burst(p, Random.value < 0.5f ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(0.95f, 0.95f, 1f, 1f),
                    8, 7f, 0.2f, 1.0f, _order + 20, 14f);
            }
            yield return null;
        }

        // すうっと消える
        List<SpriteRenderer> srs = new List<SpriteRenderer>();
        List<Color> srColors = new List<Color>();
        List<TextMeshPro> tms = new List<TextMeshPro>();
        List<Color> tmColors = new List<Color>();
        if (visual != null)
        {
            SpriteRenderer[] a = visual.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < a.Length; i++)
            {
                srs.Add(a[i]);
                srColors.Add(a[i].color);
            }
            TextMeshPro[] b = visual.GetComponentsInChildren<TextMeshPro>(true);
            for (int i = 0; i < b.Length; i++)
            {
                tms.Add(b[i]);
                tmColors.Add(b[i].color);
            }
        }
        float fade = Mathf.Max(0.1f, defeatFadeSeconds);
        float ft = 0f;
        while (ft < fade)
        {
            ft += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(ft / fade);
            for (int i = 0; i < srs.Count; i++)
            {
                if (srs[i] == null) continue;
                Color c = srColors[i];
                c.a *= k;
                srs[i].color = c;
            }
            for (int i = 0; i < tms.Count; i++)
            {
                if (tms[i] == null) continue;
                Color c = tmColors[i];
                c.a *= k;
                tms[i].color = c;
            }
            yield return null;
        }

        // ごほうび：Aキー（ポップアップが出て、ゲームが止まる）
        yield return GrantKeyReward();

        if (onDefeated != null)
            onDefeated.Invoke();

        // 非表示にすると、StageGoal（BossDefeated）がステージクリアにする
        gameObject.SetActive(false);
    }

    private KeyDefinition ResolveKeyDrop()
    {
        if (keyDrop != null)
            return keyDrop;
        if (KeyInventory.Instance == null)
            return null;
        IReadOnlyList<KeyDefinition> all = KeyInventory.Instance.AllKeyDefinitions;
        for (int i = 0; i < all.Count; i++)
        {
            KeyDefinition k = all[i];
            if (k != null && !k.isSpecialKey && k.keyCode == KeyCode.A)
                return k;
        }
        return null;
    }

    /// <summary>
    /// ステージクリアの演出（StageGoal から呼ばれる）：走ってきて踊るキーが clearKeyCount に足りないとき、
    /// 足す分のキーを返す。already は、すでに演出に出ることが決まっているキー（このステージで入手したキー）。
    /// Aキーを優先し、あとは持っているキーからランダムに選ぶ。
    /// </summary>
    public List<KeyDefinition> PickClearKeys(List<KeyDefinition> already)
    {
        List<KeyDefinition> extra = new List<KeyDefinition>();
        if (clearKeyCount <= 0 || KeyInventory.Instance == null)
            return extra;

        int have = already != null ? already.Count : 0;
        int need = clearKeyCount - have;
        if (need <= 0)
            return extra;

        List<KeyDefinition> pool = new List<KeyDefinition>();
        IReadOnlyList<KeyDefinition> owned = KeyInventory.Instance.OwnedKeys;
        for (int i = 0; i < owned.Count; i++)
        {
            KeyDefinition k = owned[i];
            if (k != null && (already == null || !already.Contains(k)))
                pool.Add(k);
        }

        KeyDefinition drop = ResolveKeyDrop();
        if (drop != null && pool.Contains(drop))
        {
            extra.Add(drop);
            pool.Remove(drop);
        }

        while (extra.Count < need && pool.Count > 0)
        {
            int j = Random.Range(0, pool.Count);
            extra.Add(pool[j]);
            pool.RemoveAt(j);
        }
        return extra;
    }

    private IEnumerator GrantKeyReward()
    {
        KeyDefinition drop = ResolveKeyDrop();
        if (drop == null || KeyInventory.Instance == null)
            yield break;
        if (KeyInventory.Instance.Owns(drop))
            yield break;

        // キーを入手する。「新しいキーを獲得した！」のポップアップ（と、そのあとのキーコンフィグでの演出）が
        // ゲームを止めるので、終わるまで待つ。
        KeyInventory.Instance.AddKey(drop);

        float w = 0f;
        while (!GamePause.IsPaused && w < 1.5f)
        {
            w += Time.unscaledDeltaTime;
            yield return null;
        }
        while (GamePause.IsPaused)
            yield return null;

        KeyConfigUI cfg = FindObjectOfType<KeyConfigUI>(true);
        while (cfg != null && cfg.IsOpen)
            yield return null;

        yield return new WaitForSeconds(0.4f);
    }

    private void OnDestroy()
    {
        HideZone();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(leftLimit, groundY, 0f), new Vector3(rightLimit, groundY, 0f));
        Gizmos.DrawLine(new Vector3(leftLimit, groundY, 0f), new Vector3(leftLimit, ceilingY, 0f));
        Gizmos.DrawLine(new Vector3(rightLimit, groundY, 0f), new Vector3(rightLimit, ceilingY, 0f));
        Gizmos.color = Color.red;
        Vector3 p = transform.position;
        Gizmos.DrawWireCube(new Vector3(p.x - meleeReach * 0.5f, groundY + meleeHeight * 0.5f, 0f), new Vector3(meleeReach, meleeHeight, 0f));
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawLine(new Vector3(p.x - meleeRange, groundY, 0f), new Vector3(p.x - meleeRange, groundY + 1f, 0f));
        Gizmos.DrawLine(new Vector3(p.x + meleeRange, groundY, 0f), new Vector3(p.x + meleeRange, groundY + 1f, 0f));
    }
}
