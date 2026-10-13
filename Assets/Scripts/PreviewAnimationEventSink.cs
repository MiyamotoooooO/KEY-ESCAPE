using UnityEngine;

/// <summary>
/// キー設定画面のプレビュー用キャラクター（PreviewChar）専用。
/// 本物のプレイヤーのアニメーションをそのまま流用しているため、アニメーションに
/// 設定されているアニメーションイベントの受け取り先が無く、
/// 「AnimationEvent 'xxx' has no receiver!」というエラーが出る。
/// ここでは受け取るだけで何もしない（プレビューではダメージ判定などは不要）。
/// </summary>
public class PreviewAnimationEventSink : MonoBehaviour
{
    public void DoDashDamage() { }
}