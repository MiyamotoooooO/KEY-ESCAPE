using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - Key Config画面右下の「キーのプレビュー」欄を管理するスクリプト。
///
/// 再生されるタイミングは2つ：
///   1. 「現在の設定」の行（ActionBindingRow）をクリックした時
///      （owner.PlayActionPreview(action)経由）。
///   2. 左一覧からキーをドラッグ&ドロップして実際にそのアクションへ割り当てた時
///      （KeyConfigUI.TryBindByDrag()経由。割り当てに成功した時だけ）。
///
/// 1回のクリック／設定につき、同じアニメーションをrepeatCount回（既定2回）
/// 連続で再生する。
///
/// プレビューのキャラクターには、実際のプレイヤー（PlayerMovement/
/// CharacterController2D）が使っているのと同じAnimator Controllerを
/// そのまま流用する想定。そのAnimator Controllerのパラメーターは
/// Trigger（例：なし）ではなくBool（IsJumping/IsAttacking/IsDashing等）と
/// Float（Speed）なので、このスクリプトは3種類ともサポートしている。
///
/// 実際のゲームでは「着地したらfalseに戻す」「速度が落ちたらSpeedも下がる」
/// ように物理演算やコリジョンイベントで自動的に元の状態（Iddle）へ戻るが、
/// プレビュー欄には物理演算が無いため、代わりにこのスクリプトが
/// 「true/値をセットして、一定時間後に自動でfalse/0へ戻す」タイマー式で
/// 同じことをしている。2回目を再生する前には一度false/0に戻してから
/// 少し間（repeatGap）を空けている - Animator Controller側のAny State
/// からの遷移は「falseから改めてtrueになった」ことを条件に再評価されるため、
/// trueのまま連続で呼んでも2回目が発生しないことがある。
///
/// 重要：Key Config画面はTime.timeScale = 0で開く
/// （KeyConfigUI.pauseGameWhileOpen）。プレビューキャラクターのAnimator
/// コンポーネントのUpdate Modeが初期値のNormalのままだと、timeScaleの
/// 影響でアニメーションが一切再生されない。必ずUpdate Modeを
/// Unscaled Timeに変更すること（Animatorコンポーネントの一番下にある項目）。
/// </summary>
public class KeyConfigPreviewPlayer : MonoBehaviour
{
    public enum PreviewParamType
    {
        Trigger,
        Bool,
        Float,
    }

    public enum FacingDirection
    {
        [InspectorName("変えない")]
        None,
        Left,
        Right,
    }

    [System.Serializable]
    public struct ActionPreviewTrigger
    {
        public GameAction action;
        [Tooltip("Animator Controller側のパラメーターの種類。実プレイヤーのControllerを流用する場合、Jump/Attack/DashはBool、移動はFloatになっているはず。")]
        public PreviewParamType paramType;
        [Tooltip("Animator Controller側のパラメーター名（例：IsJumping、IsAttacking、IsDashing、Speed）。1文字でも違うと反応しない。")]
        public string animatorParam;
        [Tooltip("1回分の再生時間（秒）。Bool/Floatの時はtrue・値を保持する時間、Triggerの時は次に再生するまでの待ち時間の目安として使う。")]
        public float holdDuration;
        [Tooltip("Floatの時だけ使う。再生中にセットする値（例：Speedなら40前後。実際のPlayerMovement.runSpeedに近い値にすると自然に見える）。")]
        public float floatValue;
        [Tooltip("このアクションを再生する時に、キャラクターの向きを左右どちらかに固定したい場合に設定する（MoveRightならRight等）。「変えない」なら今の向きのまま再生する。")]
        public FacingDirection facing;
    }

    [Tooltip("プレビュー欄のキャラクターのAnimator。Update ModeをUnscaled Timeにしておくこと（Key Config画面はTime.timeScale=0で開くため）。")]
    public Animator previewAnimator;

    [Header("向き（左右移動のプレビュー時に反転）")]
    [Tooltip("反転させる対象のTransform。プレビューキャラクター自身（SpriteRendererが付いているオブジェクト）を割り当てる。空なら向きの制御はしない。")]
    public Transform facingTransform;
    [Tooltip("このキャラクターが「右向き」の時、localScale.xが正と負のどちらか。実際のプレイヤーのFlip()と同じ符号に合わせること。見た目が逆になっていたら-1にする。")]
    public float rightFacingScaleSign = 1f;

    [Header("再生回数")]
    [Min(1)]
    [Tooltip("1回のクリック／キー設定につき、同じアニメーションを何回連続で再生するか。")]
    public int repeatCount = 2;
    [Tooltip("1回分の再生が終わってから、次の再生を始めるまでの間（秒）。Animatorが一度元の状態（Iddle）へ戻ってから、再び同じStateに入れるよう短い間を空ける。")]
    public float repeatGap = 0.1f;

    [Header("アクション→Animatorパラメーター対応表")]
    [Tooltip("GameActionごとに、プレビューで操作するAnimatorパラメーターを対応付ける。")]
    public ActionPreviewTrigger[] triggers = new ActionPreviewTrigger[]
    {
        new ActionPreviewTrigger { action = GameAction.MoveLeft,    paramType = PreviewParamType.Float, animatorParam = "Speed",      holdDuration = 0.6f, floatValue = 40f, facing = FacingDirection.Left },
        new ActionPreviewTrigger { action = GameAction.MoveRight,   paramType = PreviewParamType.Float, animatorParam = "Speed",      holdDuration = 0.6f, floatValue = 40f, facing = FacingDirection.Right },
        new ActionPreviewTrigger { action = GameAction.Jump,        paramType = PreviewParamType.Bool,  animatorParam = "IsJumping",  holdDuration = 0.6f },
        new ActionPreviewTrigger { action = GameAction.Attack,      paramType = PreviewParamType.Bool,  animatorParam = "IsAttacking", holdDuration = 0.4f },
        new ActionPreviewTrigger { action = GameAction.Dash,        paramType = PreviewParamType.Bool,  animatorParam = "IsDashing",  holdDuration = 0.4f },
        new ActionPreviewTrigger { action = GameAction.RangedAttack,paramType = PreviewParamType.Bool,  animatorParam = "IsAttacking", holdDuration = 0.4f },
    };

    // パラメーター名ごとに「今まさに再生中のコルーチン」を覚えておく -
    // 同じ行を連打された時に前の再生と衝突しないようにするため。
    private readonly Dictionary<string, Coroutine> _activeRoutines = new Dictionary<string, Coroutine>();

    /// <summary>KeyConfigUI.PlayActionPreview()から呼ばれる。該当するパラメーターをrepeatCount回再生する。</summary>
    public void PlayAction(GameAction action)
    {
        if (previewAnimator == null || triggers == null)
            return;

        foreach (var t in triggers)
        {
            if (t.action != action || string.IsNullOrEmpty(t.animatorParam))
                continue;

            RestartRoutine(t.animatorParam, PlayRepeated(t));
            return;
        }

        Debug.LogWarning($"[KeyConfigPreviewPlayer] {action}に対応するTrigger設定が見つかりません。Triggersに追加してください。");
    }

    private void RestartRoutine(string paramName, IEnumerator routine)
    {
        if (_activeRoutines.TryGetValue(paramName, out var running) && running != null)
            StopCoroutine(running);

        _activeRoutines[paramName] = StartCoroutine(routine);
    }

    /// <summary>t.facingの指定に従って、キャラクターの左右の向きを合わせる。</summary>
    private void ApplyFacing(FacingDirection facing)
    {
        if (facingTransform == null || facing == FacingDirection.None)
            return;

        float sign = Mathf.Sign(rightFacingScaleSign == 0f ? 1f : rightFacingScaleSign);
        float targetSignedScale = (facing == FacingDirection.Right) ? sign : -sign;

        Vector3 scale = facingTransform.localScale;
        scale.x = Mathf.Abs(scale.x) * targetSignedScale;
        facingTransform.localScale = scale;
    }

    /// <summary>1アクション分を、同じ波形でrepeatCount回繰り返す。</summary>
    private IEnumerator PlayRepeated(ActionPreviewTrigger t)
    {
        ApplyFacing(t.facing);

        int count = Mathf.Max(1, repeatCount);
        float hold = Mathf.Max(0.05f, t.holdDuration);

        for (int i = 0; i < count; i++)
        {
            switch (t.paramType)
            {
                case PreviewParamType.Trigger:
                    previewAnimator.ResetTrigger(t.animatorParam);
                    previewAnimator.SetTrigger(t.animatorParam);
                    yield return WaitRealtime(hold);
                    break;

                case PreviewParamType.Bool:
                    previewAnimator.SetBool(t.animatorParam, true);
                    yield return WaitRealtime(hold);
                    previewAnimator.SetBool(t.animatorParam, false);
                    break;

                case PreviewParamType.Float:
                    previewAnimator.SetFloat(t.animatorParam, t.floatValue);
                    yield return WaitRealtime(hold);
                    previewAnimator.SetFloat(t.animatorParam, 0f);
                    break;
            }

            bool isLast = i == count - 1;
            if (!isLast && repeatGap > 0f)
                yield return WaitRealtime(repeatGap);
        }

        _activeRoutines.Remove(t.animatorParam);
    }

    // Key Config画面はTime.timeScale = 0で開くため、WaitForSecondsではなく
    // Time.unscaledDeltaTimeで進める（AnimatorのUpdate ModeをUnscaled Timeに
    // しておくのとセットで、画面が一時停止中でもプレビューが動くようにする）。
    private IEnumerator WaitRealtime(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
