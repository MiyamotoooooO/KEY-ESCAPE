using UnityEngine;

/// <summary>
/// KEY ESCAPE - ステージに置かれている物理的なキー。Playerとして触れると
/// KeyInventoryに追加される（仕様書Ver.0.1のキー入手に関する節 -
/// 「ステージでキーを拾う」）。key層のスクリプト群（どこかからAddKey()が
/// 呼ばれることを既に前提にしている - README.md「使い方」参照）と、
/// 実際にシーンに配置できるオブジェクトとの間を繋ぐ、欠けていたリンク。
///
/// このスクリプトはプレイヤーのインベントリにキーを追加する（所有させる）だけ。
/// そのキーがいずれ割り当てられるかもしれないアクション自体を解放することは
/// しない - それは別の、意図的なステップ（KeyBindingManager.UnlockAction）で、
/// 通常は単なる1回の入手ではなくストーリーやステージクリアの進行に紐づく。
/// まだ何にも割り当てられないキーを拾っても、アクションが解放されて
/// Key Config画面が使えるようになるまで、インベントリの中で待機するだけ。
///
/// セットアップ:
///  - Collider2Dを「Is Trigger」に設定したGameObjectに付けること。
///  - `keyDefinition`にKeyDefinitionアセットを割り当てる - これがプレイヤーが
///    受け取るキーになる（そのスプライトはここから自動で読み込まれるわけでは
///    ないので、同じオブジェクトのSpriteRendererに好きな見た目を設定すること）。
///  - このオブジェクト自体のタグは何でもよい。「Player」タグが付いたものが
///    触れたときに反応する。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour
{
    [Tooltip("Which key this pickup grants. Required.")]
    public KeyDefinition keyDefinition;

    [Header("Feel (all optional)")]
    [Tooltip("Simple up/down bob so the pickup reads as interactive, not scenery.")]
    public bool bob = true;
    public float bobHeight = 0.12f;
    public float bobSpeed = 2f;

    [Tooltip("Spawned at this object's position when collected. Leave empty to skip.")]
    public GameObject pickupEffect;

    [Tooltip("Played at this object's position when collected. Leave empty to skip.")]
    public AudioClip pickupSound;

    private Vector3 _startPos;
    private bool _collected;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (!col.isTrigger)
            Debug.LogWarning($"[KeyPickup] {name}: Collider2D is not set to 'Is Trigger' - OnTriggerEnter2D won't fire.", this);

        _startPos = transform.position;
    }

    private void Update()
    {
        if (!bob || _collected)
            return;

        float y = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = _startPos + new Vector3(0f, y, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected || !other.CompareTag("Player"))
            return;

        if (keyDefinition == null)
        {
            Debug.LogWarning($"[KeyPickup] {name}: no KeyDefinition assigned - nothing to give the player.", this);
            return;
        }

        if (KeyInventory.Instance == null)
        {
            Debug.LogWarning($"[KeyPickup] {name}: no KeyInventory in the scene (Bootstrap missing?) - key not collected.", this);
            return;
        }

        _collected = true;
        KeyInventory.Instance.AddKey(keyDefinition);

        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        Destroy(gameObject);
    }
}
