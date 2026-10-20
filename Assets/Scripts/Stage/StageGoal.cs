using System.Collections;
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

    private bool _done;
    private bool _bossSeen;
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
        if (_done)
            return;
        _done = true;

        StageRun.Complete(stageId); // このステージで入手したキーを、確定する（拠点に帰っても外されない）
        StageProgress.MarkCleared(stageId);
        GrantRewards();
        CheckpointManager.Clear();

        if (clearMessage != null)
            clearMessage.SetActive(true);

        StartCoroutine(ReturnToHub());
    }

    /// <summary>
    /// StageCatalog のこのステージの欄に設定した「クリア報酬」を解放する。
    /// すでにクリア済みのステージをもう一度クリアしても、まだ解放していない報酬は解放される。
    /// </summary>
    private void GrantRewards()
    {
        StageCatalog catalog = StageCatalog.Instance;
        StageCatalog.Entry entry = catalog != null ? catalog.Find(stageId) : null;
        if (entry == null || entry.rewards == null || entry.rewards.Count == 0)
            return;

        KeyBindingManager keys = KeyBindingManager.Instance;
        for (int i = 0; i < entry.rewards.Count; i++)
        {
            switch (entry.rewards[i])
            {
                case StageReward.Dash:
                    if (keys != null) keys.UnlockAction(GameAction.Dash);
                    break;
                case StageReward.RangedAttack:
                    if (keys != null) keys.UnlockAction(GameAction.RangedAttack);
                    break;
                case StageReward.ModifierKey:
                    if (keys != null) keys.UnlockModifierKey();
                    break;
                case StageReward.DoubleJump:
                    SaveManager.Data.doubleJumpUnlocked = true;
                    break;
            }
        }

        if (keys == null)
            Debug.LogWarning("[StageGoal] KeyBindingManagerが見つからないため、キーに関する報酬（ダッシュ・遠距離攻撃・特殊キー）を解放できませんでした（Bootstrapが無い？）。");

        SaveManager.Save();
    }

    private IEnumerator ReturnToHub()
    {
        yield return new WaitForSecondsRealtime(delayBeforeReturn);

        StageCatalog catalog = StageCatalog.Instance;
        string hub = catalog != null ? catalog.hubSceneName : null;
        if (string.IsNullOrEmpty(hub) || !Application.CanStreamedLevelBeLoaded(hub))
        {
            Debug.LogWarning("[StageGoal] 拠点のシーン「" + hub + "」を読み込めません。StageCatalogのHub Scene Nameと、Build Settingsを確認してください。");
            yield break;
        }
        SceneManager.LoadScene(hub);
    }
}
