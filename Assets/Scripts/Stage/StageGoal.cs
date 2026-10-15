using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - ステージのゴール。プレイヤーが触れると、そのステージをクリア済みにして保存し、
/// 少し待ってから拠点に戻る。クリアすると、次のステージが解放される。
///
/// 付け方：ゴールの場所に、Is Triggerをオンにした Collider2D と、このスクリプトを付ける。
/// StageIdには、StageCatalogのid（例：stage1）を入れる。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StageGoal : MonoBehaviour
{
    [Tooltip("このステージのID。StageCatalogのidと同じにする。")]
    public string stageId = "stage1";
    public string playerTag = "Player";
    [Tooltip("ゴールしてから、拠点に戻るまでの待ち時間（秒）。")]
    public float delayBeforeReturn = 1.5f;

    private bool _done;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_done || !other.CompareTag(playerTag))
            return;
        _done = true;

        StageProgress.MarkCleared(stageId);
        StartCoroutine(ReturnToHub());
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