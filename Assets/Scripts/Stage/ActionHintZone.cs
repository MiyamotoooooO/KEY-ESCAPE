using UnityEngine;
using TMPro;

/// <summary>
/// KEY ESCAPE - 「このアクションにキーを割り当てないと進めない」場所のヒント。
/// プレイヤーがこの範囲に入っていて、そのアクションにキーが割り当てられていない間だけ、ヒントを表示する。
/// 割り当てると、すぐに消える。
///
/// 付け方：Is Triggerをオンにした Collider2D と、このスクリプトを付ける。
/// ヒントの表示（吹き出しなど）は、子オブジェクトとして作り、Hint Objectに割り当てる。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ActionHintZone : MonoBehaviour
{
    [Tooltip("このアクションにキーが割り当てられていない時に、ヒントを出す。")]
    public GameAction requiredAction = GameAction.Jump;
    public string playerTag = "Player";

    [Header("表示")]
    [Tooltip("ヒントの表示全体（吹き出し、文字など）。最初は非表示にしておく。")]
    public GameObject hintObject;
    [Tooltip("任意。割り当てると、下のメッセージの文字を自動で入れる。")]
    public TMP_Text hintText;
    [TextArea]
    public string message = "ジャンプのキーを割り当てよう（Tabキー）";

    private bool _inside;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Start()
    {
        if (hintText != null)
            hintText.text = message;
        SetHint(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            _inside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            _inside = false;
            SetHint(false);
        }
    }

    // 範囲の中にいる間、割り当ての状態が変わっても追いつくよう、毎フレーム確認する。
    private void Update()
    {
        if (!_inside)
            return;
        SetHint(NeedsHint());
    }

    private bool NeedsHint()
    {
        KeyBindingManager mgr = KeyBindingManager.Instance;
        if (mgr == null)
            return false;
        // 解放されていないアクションは、割り当てられない。ヒントを出さない。
        if (!mgr.IsActionUnlocked(requiredAction))
            return false;
        return mgr.GetBinding(requiredAction) == null;
    }

    private void SetHint(bool show)
    {
        if (hintObject != null && hintObject.activeSelf != show)
            hintObject.SetActive(show);
    }
}
