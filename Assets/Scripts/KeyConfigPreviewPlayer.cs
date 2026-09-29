using UnityEngine;

/// <summary>
/// KEY ESCAPE - Key Config画面右下の「キーのプレビュー」欄を管理するスクリプト。
///
/// 「現在の設定」の行（ActionBindingRow）をクリックすると、そのアクションに
/// 対応するアニメーションをプレビュー欄のキャラクターに再生させる
/// （例：ジャンプの行をクリック→プレビューでジャンプする）。
///
/// プレビュー欄のキャラクターは実際のプレイヤー（PlayerMovement/
/// CharacterController2D）とは別の、見た目だけのAnimatorを想定している
/// （物理演算や実際の移動はしない、その場でアニメーションを再生するだけ）。
/// そのため、どのGameActionでどのTriggerを鳴らすかはInspector上で自由に
/// 対応付けできるようにしてある - プレビュー用に別のAnimator Controllerを
/// 作った場合でも、Trigger名をここで合わせるだけで動く。
///
/// 使い方：
///   1. プレビュー欄のキャラクターにAnimatorを付け、Animator Controllerに
///      Idle・Move・Jump・Attack・Dashなど、アクションごとのStateとTriggerを
///      用意する（実際のプレイヤーと同じ絵を使ってもいいし、プレビュー専用の
///      軽いものでもいい）。
///   2. このスクリプトをプレビュー欄（か、その親）に付けて、
///      Preview AnimatorにそのAnimatorを割り当てる。
///   3. Triggersに、GameActionの数だけ要素を追加し、それぞれAction欄に
///      対応するアクション、Animator Trigger欄にAnimator Controller側の
///      Trigger名（例："Jump"）を入力する。
///   4. KeyConfigUIのPreview Playerフィールドにこのスクリプトを割り当てる。
///   5. 各ActionBindingRowのowner（KeyConfigUI）が正しく割り当たっていれば、
///      行をクリックするだけで自動的にPlayAction()が呼ばれる。
/// </summary>
public class KeyConfigPreviewPlayer : MonoBehaviour
{
    [System.Serializable]
    public struct ActionPreviewTrigger
    {
        public GameAction action;
        [Tooltip("このアクションがクリックされた時にAnimatorへ送るTrigger名。Animator Controller側に同じ名前のTriggerパラメータを用意しておくこと。")]
        public string animatorTrigger;
    }

    [Tooltip("プレビュー欄のキャラクターのAnimator。")]
    public Animator previewAnimator;

    [Tooltip("GameActionごとに、プレビューで再生するAnimator Triggerを対応付ける。")]
    public ActionPreviewTrigger[] triggers;

    /// <summary>KeyConfigUI.PlayActionPreview()から呼ばれる。該当するTriggerを鳴らす。</summary>
    public void PlayAction(GameAction action)
    {
        if (previewAnimator == null || triggers == null)
            return;

        foreach (var t in triggers)
        {
            if (t.action != action || string.IsNullOrEmpty(t.animatorTrigger))
                continue;

            // 連打された時に前のTriggerが残ったまま二重に発火しないよう、
            // 一度リセットしてから鳴らし直す。
            previewAnimator.ResetTrigger(t.animatorTrigger);
            previewAnimator.SetTrigger(t.animatorTrigger);
            return;
        }

        Debug.LogWarning($"[KeyConfigPreviewPlayer] {action}に対応するTrigger設定が見つかりません。Triggersに追加してください。");
    }
}
