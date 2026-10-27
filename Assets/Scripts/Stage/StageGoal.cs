using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - ステージのゴール。クリアすると、そのステージをクリア済みにして保存し、
/// 少し待ってから拠点に戻る。クリアすると、次のステージが解放される。
///
/// モードが2つある：
///   ・Touch：ゴールの場所に、Is Triggerをオンにした Collider2D と、このスクリプトを付ける。
///           プレイヤーが触れるとクリア。
///   ・BossDefeated：Bossに割り当てた敵が、倒れるとクリア。Collider2Dは要らない。
///           空のオブジェクトに、このスクリプトを付けるだけでよい。
///           BossがEnemyまたはAllyスクリプトを持つ場合は、ライフが0になった時点でクリアにする。
///           それ以外の場合は、Destroy／非表示になった時点でクリアにする。
/// StageIdには、StageCatalogのid（例：stage1）を入れる。
/// </summary>
public class StageGoal : MonoBehaviour
{
    public enum GoalMode
    {
        Touch,
        BossDefeated,
    }

    [Tooltip("このステージのID。StageCatalogのidと同じにする。")]
    public string stageId = "stage1";
    public GoalMode mode = GoalMode.BossDefeated;
    public string playerTag = "Player";

    [Header("BossDefeatedモード")]
    [Tooltip("倒すとクリアになる敵（ボス）。EnemyまたはAllyスクリプトを持つ敵、またはそれ以外のオブジェクト。")]
    public GameObject boss;

    [Header("クリア後")]
    [Tooltip("クリアしてから、拠点に戻るまでの待ち時間（秒）。")]
    public float delayBeforeReturn = 1.5f;
    [Tooltip("任意。クリアした時に表示する物（「STAGE CLEAR」の文字など）。最初は非表示にしておく。")]
    public GameObject clearMessage;
    [Tooltip("任意。ゴールの演出（スロー・フラッシュ・紙吹雪・STAGE CLEAR!）を再生する部品（StageClearDirector）。空なら、シーンから自動で探す。無ければ、演出なしで拠点に戻る。")]
    public StageClearDirector director;

    [Header("試作品の最後の演出（最後のステージだけ）")]
    [Tooltip("オンのとき、StageCatalogの最後のステージをクリアして結果画面が終わったあとに、モニターの文章を出して、タイトル画面へ戻る。拠点へは戻らない。")]
    public bool showPrototypeEnding = true;
    [TextArea(2, 4)]
    [Tooltip("モニターに出す文章。改行は、そのまま改行で入れる。")]
    public string endingMessage = "試作品はここまでです。\n遊んでいただきありがとうございました。";
    [Tooltip("文章を出しておく秒数（1.5秒たったあと、Space・Enter・クリックで先へ進める）。")]
    public float endingHoldSeconds = 6f;
    [Tooltip("文章のフォント。空なら、StageClearDirectorのフォント、それも空なら、シーン内の文字から借りる。")]
    public TMPro.TMP_FontAsset endingFont;
    [Tooltip("モニターの絵。空なら、Assets/Resources/Ending/MonitorFront.png を使う。")]
    public Sprite endingMonitorSprite;
    [Tooltip("戻るタイトルのシーン名。")]
    public string titleSceneName = "TitleScene";

    private bool _done;

    /// <summary>クリアになったあと（ゴールしたあと）true。ゴールの紙吹雪などが、止まるのに使う。</summary>
    public bool IsDone { get { return _done; } }
    private bool _bossSeen;
    // 今回のクリアで、新しく解放されたアクション（結果画面のあとに、ロックを壊す演出で見せる）
    private readonly List<GameAction> _newActions = new List<GameAction>();
    private Enemy _bossEnemy;
    private Ally _bossAlly;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Start()
    {
        if (clearMessage != null)
            clearMessage.SetActive(false);

        if (boss != null)
        {
            _bossEnemy = boss.GetComponent<Enemy>();
            _bossAlly = boss.GetComponent<Ally>();
        }

        if (mode == GoalMode.BossDefeated && boss == null)
            Debug.LogWarning("[StageGoal] BossDefeatedモードですが、Bossが割り当てられていません。");
    }

    private void Update()
    {
        if (_done || mode != GoalMode.BossDefeated || boss == null)
            return;

        // ボスがEnemy／Allyなら、ライフが0になった時点でクリアにする（消えるのを待たない）。
        if ((_bossEnemy != null && _bossEnemy.life <= 0f)
            || (_bossAlly != null && _bossAlly.life <= 0f))
        {
            Clear();
            return;
        }

        // 最初から非表示のボス（あとで登場するボス）で、すぐにクリアにならないよう、
        // 一度表示された後で、非表示になったかを見る。
        if (boss.activeInHierarchy)
            _bossSeen = true;
        else if (_bossSeen)
            Clear();
    }

    // ボスが「Destroy」された場合は、bossがnullになるので、ここで見つける。
    private void LateUpdate()
    {
        if (_done || mode != GoalMode.BossDefeated)
            return;
        if (_bossSeen && boss == null)
            Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (mode != GoalMode.Touch || _done || !other.CompareTag(playerTag))
            return;
        Clear();
    }

    /// <summary>クリアにする。ほかのスクリプトから呼んでもよい（ボスのやられた時の処理など）。</summary>
    public void Clear()
    {
        // やられている最中は、クリアにしない（やられた時は、シーンが読み直される）。
        if (_done || HitFeedback.IsDying)
            return;
        _done = true;

        // このステージで入手したキーを、先に取っておく（確定すると、記録が消えるため）。演出で走らせる。
        List<KeyDefinition> gainedKeys = StageRun.GetRunKeys();

        // クリアタイムを止めて、ベストタイムなら更新する（結果画面のNEW RECORD用）。
        StageStats.Stop();
        bool newRecord = false;
        if (StageStats.IsTracking && StageStats.StageId == stageId)
            newRecord = StageProgress.TrySetBestTime(stageId, StageStats.ElapsedTime);

        StageRun.Complete(stageId); // このステージで入手したキーを、確定する（拠点に帰っても外されない）
        StageProgress.MarkCleared(stageId);
        List<string> newRewards = GrantRewards(); // 今回、新しく解放されたもの（結果画面で知らせる）
        CheckpointManager.Clear();

        StageClearDirector dir = director != null ? director : FindObjectOfType<StageClearDirector>();
        if (dir != null)
        {
            // ボス（BossA）が、演出で走ってきて踊るキーを足したいとき（このステージで入手したキーが少ないとき）
            BossA bossA = boss != null ? boss.GetComponent<BossA>() : null;
            if (bossA != null)
                dir.SetExtraCheerKeys(bossA.PickClearKeys(gainedKeys));

            // 演出と結果画面が終わったら（「拠点に戻る」を押したら）、拠点に戻る。
            // そのあと、新しく解放されたアクションがあれば、キーコンフィグ画面でロックを壊す演出を見せる。
            dir.Play(gainedKeys, newRecord, newRewards, () => StartCoroutine(FinishRoutine(0f, dir)));
        }
        else
        {
            if (clearMessage != null)
                clearMessage.SetActive(true);
            StartCoroutine(FinishRoutine(delayBeforeReturn, null));
        }
    }

    /// <summary>
    /// StageCatalog のこのステージの欄に設定した「クリア報酬」を解放する。
    /// すでにクリア済みのステージをもう一度クリアしても、まだ解放していない報酬は解放される。
    /// </summary>
    private List<string> GrantRewards()
    {
        // 今回、新しく解放されたものの名前（結果画面の「覚えた！」に出す）。すでに解放済みのものは入れない。
        var gained = new List<string>();
        _newActions.Clear();

        StageCatalog catalog = StageCatalog.Instance;
        StageCatalog.Entry entry = catalog != null ? catalog.Find(stageId) : null;
        if (entry == null || entry.rewards == null || entry.rewards.Count == 0)
            return gained;

        KeyBindingManager keys = KeyBindingManager.Instance;
        for (int i = 0; i < entry.rewards.Count; i++)
        {
            switch (entry.rewards[i])
            {
                case StageReward.Dash:
                    UnlockActionReward(keys, GameAction.Dash, "ダッシュ", gained, _newActions);
                    break;
                case StageReward.RangedAttack:
                    UnlockActionReward(keys, GameAction.RangedAttack, "遠距離攻撃", gained, _newActions);
                    break;
                case StageReward.Jump:
                    UnlockActionReward(keys, GameAction.Jump, "ジャンプ", gained, _newActions);
                    break;
                case StageReward.Attack:
                    UnlockActionReward(keys, GameAction.Attack, "攻撃", gained, _newActions);
                    break;
                case StageReward.MoveLeft:
                    UnlockActionReward(keys, GameAction.MoveLeft, "左移動", gained, _newActions);
                    break;
                case StageReward.ModifierKey:
                    if (keys != null)
                    {
                        if (!keys.IsModifierUnlocked)
                            gained.Add("特殊キー（Ctrl）");
                        keys.UnlockModifierKey();
                    }
                    break;
                case StageReward.DoubleJump:
                    if (!SaveManager.Data.doubleJumpUnlocked)
                        gained.Add("二段ジャンプ");
                    SaveManager.Data.doubleJumpUnlocked = true;
                    break;
            }
        }

        if (keys == null)
            Debug.LogWarning("[StageGoal] KeyBindingManagerが見つからないため、キーに関する報酬（ジャンプ・攻撃・ダッシュ・遠距離攻撃・特殊キー）を解放できませんでした（Bootstrapが無い？）。");

        SaveManager.Save();
        return gained;
    }

    /// <summary>アクションを解放する。まだ解放されていなかった時だけ、名前を gained に足す。</summary>
    private static void UnlockActionReward(KeyBindingManager keys, GameAction action, string label, List<string> gained, List<GameAction> newActions)
    {
        if (keys == null)
            return;
        if (!keys.IsActionUnlocked(action))
        {
            gained.Add(label);
            newActions.Add(action);
        }
        keys.UnlockAction(action);
    }

    /// <summary>
    /// 結果画面が終わったあとの流れ。新しく解放されたアクションがあれば、キーコンフィグ画面で
    /// 「ロックを壊す演出」を見せてから、拠点に戻る。無ければ（または演出が始められなければ）、そのまま拠点に戻る。
    /// </summary>
    private IEnumerator FinishRoutine(float delay, StageClearDirector dir)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (_newActions.Count > 0)
        {
            ActionLockBreakSequence seq = FindObjectOfType<ActionLockBreakSequence>(true);
            if (seq == null)
                seq = new GameObject("ActionLockBreakSequence").AddComponent<ActionLockBreakSequence>();

            if (seq.TryPlay(_newActions))
            {
                // キーコンフィグ画面が開くので、手前にある結果画面などは隠す。
                if (dir != null)
                    dir.HideOverlay();
                while (seq != null && seq.IsRunning)
                    yield return null;
            }
        }

        if (showPrototypeEnding && IsLastStage())
        {
            PlayPrototypeEnding(dir);
            yield break;
        }

        yield return ReturnToHub(0f);
    }

    /// <summary>StageCatalogの最後のステージか。</summary>
    private bool IsLastStage()
    {
        StageCatalog catalog = StageCatalog.Instance;
        if (catalog == null || catalog.stages == null || catalog.stages.Count == 0)
            return false;
        StageCatalog.Entry last = catalog.stages[catalog.stages.Count - 1];
        return last != null && string.Equals(last.id, stageId, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>モニターの文章を出してから、タイトルへ戻る演出（PrototypeEnding）を始める。</summary>
    private void PlayPrototypeEnding(StageClearDirector dir)
    {
        // 手前にある結果画面などは、隠す
        if (dir != null)
            dir.HideOverlay();

        TMPro.TMP_FontAsset f = endingFont != null ? endingFont : (dir != null ? dir.font : null);
        PrototypeEnding.Create(f, endingMonitorSprite, endingMessage, endingHoldSeconds, titleSceneName).Begin();
    }

    private IEnumerator ReturnToHub(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        StageCatalog catalog = StageCatalog.Instance;
        string hub = catalog != null ? catalog.hubSceneName : null;
        if (string.IsNullOrEmpty(hub) || !Application.CanStreamedLevelBeLoaded(hub))
        {
            Debug.LogWarning("[StageGoal] 拠点のシーン「" + hub + "」を読み込めません。StageCatalogのHub Scene Nameと、Build Settingsを確認してください。");
            yield break;
        }
        LoadingScreen.Load(hub);
    }
}
