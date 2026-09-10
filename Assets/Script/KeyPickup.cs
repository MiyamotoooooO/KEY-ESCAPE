using UnityEngine;

/// <summary>
/// KEY ESCAPE - a physical key sitting in a stage. Touch it as the Player
/// and it's added to KeyInventory (spec Ver.0.1 section on key acquisition -
/// "ステージでキーを拾う"). This is the missing link between the key-layer
/// scripts (which already assumed AddKey() gets called from somewhere - see
/// README.md "使い方") and an actual object you can place in a scene.
///
/// This script only adds the key to the player's inventory (ownership).
/// It does NOT unlock the action that key might eventually be bound to -
/// that's a separate, deliberate step (KeyBindingManager.UnlockAction),
/// normally tied to story/stage-clear progress rather than a single pickup.
/// A picked-up key you can't yet bind to anything just sits in the
/// inventory, ready for the Key Config screen once an action unlocks.
///
/// Setup:
///  - Put this on a GameObject with a Collider2D set to "Is Trigger".
///  - Assign a KeyDefinition asset to `keyDefinition` - this is which key
///    the player receives (its sprite is NOT read from here automatically;
///    put whatever visual you want on the same object's SpriteRenderer).
///  - Tag doesn't matter for this object itself; it reacts to anything
///    tagged "Player" that touches it.
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
