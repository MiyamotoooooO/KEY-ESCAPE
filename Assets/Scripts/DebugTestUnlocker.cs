using UnityEngine;

/// <summary>
/// 一時的なデバッグ用ヘルパー - 完成版のゲームには含めない。テストが終わったら
/// このスクリプトと、これが付いているGameObjectごと削除すること。
///
/// 目的：DashとRangedAttackは最初、ロックされておりかつ未割り当ての状態で
/// 始まる（仕様書Ver.0.2 - これらの「Default Bindings」の行は意図的に空に
/// してある）。そのためKeyBindingManager.Instance.UnlockAction(...)を単独で
/// 呼んでも、見た目にも操作感にも何も変化がない。このスクリプトは、
/// アクションにキーを割り当てる処理も行う。これは、本物のKey Config UIや
/// ステージクリア報酬の仕組みができる前に、実際にテストするために欠けていた
/// ステップ。
///
/// セットアップ:
///  1. テスト用シーン内の任意のGameObject（例：Bootstrap）にこれをアタッチする。
///  2. 他の場所ではまだ使われていない、使い捨てのKeyDefinitionアセットを
///     2つ作成する（例：Key_V、Key_B - このテストではRank/Attributeは
///     関係ない）。
///  3. 両方をKeyInventoryの「Starting Keys」リストに追加する
///     （プレイヤーがまだそのキーを「所有」していないと、TryBindKeyが失敗する
///     - 失敗した場合はこのスクリプトがConsoleに出すログを参照。新しい
///     テスト用キーを作るとき忘れがちだが、KeyDefinitionアセットを作って
///     このスクリプトにドラッグすることと、それを所有していることは
///     イコールではない）。
///  4. それらを下のDash Test Key / Ranged Test Keyフィールドにドラッグする。
///  5. Player（CharacterController2Dが付いているGameObject）を下の
///     `playerController`にドラッグする。これにより、F9を押したときに
///     `hasDashAbility`もtrueに切り替わるようになる - このフラグは
///     Playモード中に手動でInspectorから変更した場合、Playを停止して
///     再度開始するたびに黙ってfalseに戻ってしまう。これをやっておかないと、
///     割り当て自体は成功しているのに「ダッシュが動かない」ように見えることが
///     ある。
///  6. Playを押す。
///
/// 操作:
///   F9  - Dashを解放し、Dash Test Keyに割り当て、
///         playerController.hasDashAbility = trueにし、さらにCtrl修飾キーも
///         解放する（そのためこのキーは、Ctrl+攻撃の強攻撃が動かない問題の
///         確認にも使える：F9を押した直後にCtrl+Fが動くようになるなら、
///         原因は修飾キーがロックされていたことだったとわかる）。
///   F10 - RangedAttackを解放し、Ranged Test Keyに割り当てる。
///
/// F9を押した後、Dash Test Keyに設定したキーを押すと実際にダッシュする
/// （IsDashingのAnimatorのboolと、前方への急な移動を確認すること）。F10を
/// 押した後は、RangedAttackのテスト用キーを押して、投げ物が生成されることを
/// 確認する。
/// </summary>
public class DebugTestUnlocker : MonoBehaviour
{
    [Tooltip("A KeyDefinition not already bound to anything (e.g. Key_V). Must also be in KeyInventory's Starting Keys.")]
    public KeyDefinition dashTestKey;

    [Tooltip("A second unused KeyDefinition (e.g. Key_B). Must also be in KeyInventory's Starting Keys.")]
    public KeyDefinition rangedTestKey;

    [Tooltip("The Player's CharacterController2D. Optional, but without it you must remember to check 'Has Dash Ability' by hand every single Play session.")]
    public CharacterController2D playerController;

    private void Update()
    {
        var keys = KeyBindingManager.Instance;
        if (keys == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            keys.UnlockAction(GameAction.Dash);
            keys.UnlockModifierKey();

            if (playerController != null)
                playerController.hasDashAbility = true;
            else
                Debug.LogWarning("[DebugTestUnlocker] playerController not assigned - remember to check 'Has Dash Ability' on CharacterController2D by hand, it resets every Play session.");

            if (dashTestKey == null)
            {
                Debug.LogWarning("[DebugTestUnlocker] Dash unlocked and Ctrl modifier unlocked, but no Dash Test Key is assigned - nothing to bind.");
            }
            else
            {
                bool ok = keys.TryBindKey(GameAction.Dash, dashTestKey);
                Debug.Log(ok
                    ? $"[DebugTestUnlocker] Dash unlocked and bound to {dashTestKey.displayName}. Ctrl modifier unlocked - try Ctrl+F too."
                    : "[DebugTestUnlocker] TryBindKey(Dash) failed - is dashTestKey in KeyInventory's Starting Keys?");
            }
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            keys.UnlockAction(GameAction.RangedAttack);

            if (rangedTestKey == null)
            {
                Debug.LogWarning("[DebugTestUnlocker] RangedAttack unlocked, but no Ranged Test Key is assigned - nothing to bind.");
            }
            else
            {
                bool ok = keys.TryBindKey(GameAction.RangedAttack, rangedTestKey);
                Debug.Log(ok
                    ? $"[DebugTestUnlocker] RangedAttack unlocked and bound to {rangedTestKey.displayName}."
                    : "[DebugTestUnlocker] TryBindKey(RangedAttack) failed - is rangedTestKey in KeyInventory's Starting Keys?");
            }
        }
    }
}
