using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// KEY ESCAPE - one row of the Key Config screen, representing a single
/// GameAction (spec Ver.0.1 §49 / Ver.0.2 §20). Chapter 1 has exactly 6
/// bindable actions, so this is meant to be placed 6 times by hand in the
/// scene (one per GameAction) rather than instantiated from a prefab - see
/// シーン構築ガイド.md.
///
/// 新デザイン（Ver.0.3想定）ではドラッグ&ドロップで割り当てる：左のキー一覧
/// （KeyGridButtonUI）からこの行のどこかにドラッグしたキーをドロップすると
/// OnDrop()が呼ばれ、owner.TryBindByDrag(action, key)を試みる。行のどこかに
/// Raycast Target有効なImage（背景の枠など）が必要 - それが無いとUnityの
/// EventSystemがこの行を「ドロップ先」として検出できない。
///
/// この行自体をクリックすると、右下の「キーのプレビュー」欄でそのアクションの
/// 動作（ジャンプ・攻撃など）をアニメーションで再生する
/// （owner.PlayActionPreview(action)経由。実体はKeyConfigPreviewPlayer）。
/// ドロップ判定に使っているRaycast Target有効なImageがそのままクリック判定にも
/// 使われるので、追加のUI設定は不要。
/// </summary>
public class ActionBindingRow : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Tooltip("Which action this row represents.")]
    public GameAction action;

    [Header("UI references")]
    public TMP_Text actionLabel;
    [Tooltip("現在のキーの文字（例：\"A\"、\"Space\"、「ロック中」「未設定」）。Current Key Backgroundの上に重ねて表示する。")]
    public TMP_Text currentKeyLabel;
    [Tooltip("文字の後ろに敷く、キーキャップ風の背景Image。左一覧（KeyGridButton）のBackgroundと同じスプライトを使い回せばよい - キーごとに別々の画像を用意する必要はない。全てのActionBindingRowで共通の1枚でOK。")]
    public Image currentKeyBackground;
    [Tooltip("Optional - shown while the action is still locked (e.g. a padlock icon).")]
    public GameObject lockedIndicator;

    [Tooltip("The Key Config screen this row belongs to.")]
    public KeyConfigUI owner;

    private void Awake()
    {
        if (actionLabel != null)
            actionLabel.text = GameActionLabel.Get(action);
    }

    /// <summary>
    /// 左のキー一覧からドラッグしてきたKeyGridButtonUIがこの行にドロップされた
    /// 時にUnityのEventSystemから自動的に呼ばれる。
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"[DEBUG] OnDrop on row {action}: owner={(owner != null)}, pointerDrag={(eventData.pointerDrag != null ? eventData.pointerDrag.name : "null")}");
        if (owner == null || eventData.pointerDrag == null)
            return;

        var dragged = eventData.pointerDrag.GetComponent<KeyGridButtonUI>();
        Debug.Log($"[DEBUG] OnDrop: dragged component found={(dragged != null)}, dragged.Key={(dragged != null && dragged.Key != null ? dragged.Key.displayName : "null")}");
        if (dragged == null || dragged.Key == null)
            return;

        owner.TryBindByDrag(action, dragged.Key);
    }

    /// <summary>
    /// 行のどこかをクリックした時にUnityのEventSystemから自動的に呼ばれる。
    /// 右下のプレビュー欄で、このアクションの動作を再生させる。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null)
            owner.PlayActionPreview(action);
    }

    /// <summary>Called by KeyConfigUI.RefreshAll() whenever bindings/locks change.</summary>
    public void Refresh()
    {
        var mgr = KeyBindingManager.Instance;
        if (mgr == null)
            return;

        bool unlocked = mgr.IsActionUnlocked(action);
        var key = mgr.GetBinding(action);
        bool hasKey = unlocked && key != null;

        if (currentKeyLabel != null)
            currentKeyLabel.text = !unlocked ? "ロック中" : (key != null ? key.displayName : "未設定");

        // キーキャップの背景は、左一覧と同じ共通スプライト（Inspectorで固定済み）を
        // そのまま使う - キーごとに差し替える必要はない。実際にキーが割り当たって
        // いる時だけ表示し、「ロック中」「未設定」の時は今まで通り文字だけにする。
        if (currentKeyBackground != null)
            currentKeyBackground.enabled = hasKey;

        if (lockedIndicator != null)
            lockedIndicator.SetActive(!unlocked);
    }
}
