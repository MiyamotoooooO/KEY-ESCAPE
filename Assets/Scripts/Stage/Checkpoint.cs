using UnityEngine;

/// <summary>
/// KEY ESCAPE - チェックポイント。プレイヤーが触れると、ここを再開位置として記録する。
///
/// 付け方：Is Triggerをオンにした Collider2D（縦に長い箱がおすすめ）と、このスクリプトを付ける。
/// ふだんは Assets/Prefab/Object/Checkpoint.prefab を置くだけでよい
/// （Tools > KEY ESCAPE > チェックポイント > 旗のプレハブを作る で作れる）。
///
/// 見た目の切り替え（どれも任意。空なら何もしない）：
///   ・Inactive Visual / Active Visual … 通る前／通った後に表示する物（子オブジェクトを割り当てる）
///   ・エモート … 旗の上に、通る前と通った後で別のエモート（Assets/Prefab/Emotes のプレハブ）を出す
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

    [Header("エモート（どちらも任意。空なら何も出さない）")]
    [Tooltip("まだ通っていない時に、旗の上に出すエモート（Assets/Prefab/Emotes のプレハブ）。")]
    public GameObject emoteBeforePrefab;
    [Tooltip("通った後に、旗の上に出すエモート（Assets/Prefab/Emotes のプレハブ）。")]
    public GameObject emoteAfterPrefab;
    [Tooltip("エモートを出す位置（このオブジェクトの位置からのずれ）。")]
    public Vector3 emoteOffset = new Vector3(0.6f, 3f, 0f);
    [Tooltip("エモートの大きさの倍率。")]
    public float emoteScale = 1f;
    [Tooltip("通った後のエモートを出しておく秒数。0ならずっと出す。")]
    public float emoteAfterDuration = 0f;
    [Tooltip("エモートの描く順番の底上げ。大きいほど手前に描く（旗やキャラに隠れないように）。")]
    public int emoteSortingBoost = 100;

    private GameObject _before;
    private GameObject _after;
    private float _afterTimer;

    private Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Start()
    {
        BuildEmotes();

        // シーンを読み直した後でも、ここが今の再開位置なら、点灯させておく。
        SetLit(CheckpointManager.IsCurrent(RespawnPosition));
    }

    private void Update()
    {
        // 通った後のエモートを、決めた秒数だけ出して消す。
        if (_afterTimer > 0f)
        {
            _afterTimer -= Time.deltaTime;
            if (_afterTimer <= 0f && _after != null)
                _after.SetActive(false);
        }
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

        if (_before != null) _before.SetActive(!lit);
        if (_after != null) _after.SetActive(lit);
        _afterTimer = (lit && emoteAfterDuration > 0f) ? emoteAfterDuration : 0f;
    }

    /// <summary>エモートを、旗の上に作っておく（出す／消すは SetLit が切り替える）。</summary>
    private void BuildEmotes()
    {
        if (emoteBeforePrefab == null && emoteAfterPrefab == null)
            return;

        var anchor = new GameObject("EmoteAnchor");
        anchor.transform.SetParent(transform, false);
        anchor.transform.localPosition = emoteOffset;
        anchor.transform.localScale = Vector3.one * emoteScale;

        _before = SpawnEmote(emoteBeforePrefab, anchor.transform);
        _after = SpawnEmote(emoteAfterPrefab, anchor.transform);
    }

    private GameObject SpawnEmote(GameObject prefab, Transform parent)
    {
        if (prefab == null)
            return null;

        GameObject go = Instantiate(prefab, parent);
        go.transform.localPosition = Vector3.zero; // プレハブに残っている位置は使わない
        go.transform.localRotation = Quaternion.identity;

        if (emoteSortingBoost != 0)
        {
            SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder += emoteSortingBoost;
        }
        return go;
    }
}
