using UnityEngine;
using TMPro;

/// <summary>
/// 敵キー（手足の生えたキー）のキートップに文字を出す。
///
/// ・文字は「手に入るキー」の KeyDefinition.displayName から自動で入る。
///   親にある Enemy.keyDrop / Ally.keyDrop / DialogueKeyRecruit.keyDefinition / KeyPickup.keyDefinition を順に探す。
///   → モデルは1つだけ作って、keyDrop を変えるだけで全キーの敵が作れる。
/// ・keyOverride / textOverride で手動指定もできる。
/// ・Enemy / Ally は向きが変わるとき transform.localScale.x を反転させるので、
///   そのままだと文字が鏡文字になる。keepUpright が true のあいだは文字だけ反転を打ち消す。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshPro))]
public class KeyLabel : MonoBehaviour
{
    [Tooltip("空欄なら、親の Enemy / Ally の keyDrop から文字を取る")]
    public KeyDefinition keyOverride;

    [Tooltip("文字を直接指定したいときに入力（空欄なら無視）")]
    public string textOverride = "";

    [Tooltip("親が左右反転しても、文字が鏡文字にならないようにする")]
    public bool keepUpright = true;

    private TextMeshPro _tmp;
    private Enemy _enemy;
    private Ally _ally;
    private DialogueKeyRecruit _dialogue;
    private KeyPickup _pickup;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshPro>();
        _enemy = GetComponentInParent<Enemy>();
        _ally = GetComponentInParent<Ally>();
        _dialogue = GetComponentInParent<DialogueKeyRecruit>();
        _pickup = GetComponentInParent<KeyPickup>();
    }

    private void Start()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        if (!keepUpright)
            return;

        Transform p = transform.parent;
        float sign = (p != null && p.lossyScale.x < 0f) ? -1f : 1f;
        Vector3 s = transform.localScale;
        float want = Mathf.Abs(s.x) * sign;
        if (!Mathf.Approximately(s.x, want))
        {
            s.x = want;
            transform.localScale = s;
        }
    }

    /// <summary>表示する文字を取り直す。keyDrop を実行中に差し替えたときに呼ぶ。</summary>
    public void Refresh()
    {
        if (_tmp == null)
            _tmp = GetComponent<TextMeshPro>();
        if (_tmp == null)
            return;

        string text = textOverride;

        if (string.IsNullOrEmpty(text))
        {
            KeyDefinition key = keyOverride;
            if (key == null && _enemy != null) key = _enemy.keyDrop;
            if (key == null && _ally != null) key = _ally.keyDrop;
            if (key == null && _dialogue != null) key = _dialogue.keyDefinition;
            if (key == null && _pickup != null) key = _pickup.keyDefinition;

            if (key != null)
                text = string.IsNullOrEmpty(key.displayName) ? key.keyCode.ToString() : key.displayName;
        }

        _tmp.text = text ?? "";
    }
}
