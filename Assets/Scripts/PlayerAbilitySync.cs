using UnityEngine;

/// <summary>
/// KEY ESCAPE - キーの解放状態を、プレイヤーの能力フラグ（CharacterController2D）にそろえる。
///
/// 「ダッシュが解放されている」＝KeyBindingManagerのDashが解放済み、の時だけ、
/// CharacterController2D.hasDashAbilityをtrueにする。
/// セーブから復元した場合も、ステージクリアの報酬で解放した場合も、同じように動く。
///
/// 付け方：CharacterController2Dが付いているオブジェクト（プレイヤーのプレハブ）に付ける。
/// CharacterController2Dが無いオブジェクト（キーコンフィグのプレビュー用キャラなど）に付いても、何もしない。
/// （RequireComponentにすると、プレビュー用キャラに、外してあるCharacterController2Dが自動で戻されてしまうため、付けていない。）
/// </summary>
public class PlayerAbilitySync : MonoBehaviour
{
    private CharacterController2D _controller;

    private void Awake()
    {
        _controller = GetComponent<CharacterController2D>();
    }

    private void Update()
    {
        KeyBindingManager keys = KeyBindingManager.Instance;
        if (keys == null || _controller == null)
            return;

        _controller.hasDashAbility = keys.IsActionUnlocked(GameAction.Dash);

        // 二段ジャンプ：ステージクリアの報酬で解放済みなら、使えるようにする。
        // （Inspectorで手動でオンにしてある場合は、そのままオンのままにする。）
        if (SaveManager.Data.doubleJumpUnlocked)
            _controller.hasDoubleJumpAbility = true;
    }
}