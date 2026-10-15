using UnityEngine;

/// <summary>
/// KEY ESCAPE - チェックポイント。プレイヤーが触れると、ここを再開位置として記録する。
///
/// 付け方：Is Triggerをオンにした Collider2D（縦に長い箱がおすすめ）と、このスクリプトを付ける。
/// 旗などの見た目は、子オブジェクトとして作り、下の欄に割り当てる（どちらも任意）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    public string playerTag = "Player";

    [Tooltip("ステージの進み具合の順番。手前から0, 1, 2…と付ける。小さい番号のチェックポイントに戻っても、再開位置は手前に戻らない。")]
    public int order = 0;

    [Tooltip("任意。再開する位置。空なら、このオブジェクトの位置。プレイヤーの足元が地面に着くよう、少し上に置く。")]
    public Transform respawnPoint;

    [Header("見た目（どちらも任意）")]
    [Tooltip("まだ通っていない時に表示する物（消えた旗など）。")]
    public GameObject inactiveVisual;
    [Tooltip("通った後に表示する物（点灯した旗など）。")]
    public GameObject activeVisual;

    private Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Start()
    {
        // シーンを読み直した後でも、ここが今の再開位置なら、点灯させておく。
        SetLit(CheckpointManager.IsCurrent(RespawnPosition));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (CheckpointManager.Set(RespawnPosition, order))
            SetLit(true);
    }

    private void SetLit(bool lit)
    {
        if (activeVisual != null) activeVisual.SetActive(lit);
        if (inactiveVisual != null) inactiveVisual.SetActive(!lit);
    }
}
