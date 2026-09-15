# KEY ESCAPE - キー割り当て層（Metroidvania Controllerベース）

「Metroidvania Controller」(AisuKaze Studio / Unity Asset Store / 無料 / Standard EULA) を土台に、
KEY ESCAPEの核である「キーを自由に割り当てる」システムを被せたものです。

## 何が変わったか

元のアセットは `Input.GetKeyDown(KeyCode.Z)` のように、アクションと物理キーが直接結びついていました。
これを、`KeyBindingManager` という中間層を経由する形に置き換えています。

```
[変更前] Input.GetKeyDown(KeyCode.Z) がジャンプ
[変更後] KeyBindingManager.Instance.IsActionDown(GameAction.Jump) がジャンプ
         → Jumpに何のキーが割り当てられているかは実行時にプレイヤーが変更できる
```

### 新規追加ファイル

| ファイル | 役割 |
|---|---|
| `GameAction.cs` | 割り当て可能なアクションの列挙型（MoveLeft/MoveRight/Jump/Attack/Dash/RangedAttack） |
| `KeyRank.cs` | ☆1〜☆3のランク（☆が多いほど高ランク、仕様書Ver.0.2確定） |
| `KeyAttribute.cs` | キー属性（Chapter1は Movement / Attack のみ） |
| `KeyDefinition.cs` | 1つの物理キーを表すScriptableObject（KeyCode・ランク・属性・特殊キーフラグ） |
| `KeyInventory.cs` | プレイヤーが「所持している」キーの管理 |
| `KeyBindingManager.cs` | アクション⇔キーの割り当て、1キー1アクション制約、Ctrl修飾、Input.GetKey系の代替クエリ |

### 変更したファイル（元のアセットを上書き）

| ファイル | 変更内容 |
|---|---|
| `PlayerMovement.cs` | `KeyCode.Z`/`C`/軸入力 → `KeyBindingManager` 経由に置き換え。左移動と右移動は独立した2キーとして扱う（仕様書の「L=左移動、O=右移動」のような非対称割り当てにも対応するため） |
| `Attack.cs` | `KeyCode.X`/`V` → `KeyBindingManager` 経由に置き換え。遠距離攻撃はStage7解放までロック。Ctrl+攻撃で強攻撃（ダメージ`strongAttackMultiplier`倍）。攻撃属性ボーナスをダメージに反映。**バグ修正**：元コードは`dmgValue`を直接書き換えていたため、複数の敵を同時ヒットすると2体目以降のダメージ方向が壊れる可能性がありました。ローカル変数化して修正済みです |
| `CharacterController2D.cs` | 入力は元々受け取っていないので変更なし。代わりに `hasDoubleJumpAbility` / `hasWallJumpAbility` / `hasDashAbility` の3つのアンロックフラグを追加し、二段ジャンプ・壁ジャンプ・ダッシュをゲート。**壁ジャンプはChapter1では`false`のまま**にしてください（仕様書Ver.0.2で確定した見送り事項です） |

### 触っていないファイル

`Enemy.cs` / `Ally.cs` / `DestructibleObject.cs` / `Grass.cs` / `CameraFollow.cs` / `ThrowableProjectile.cs` /
`ThrowableWeapon.cs` / `KillZone.cs` は今回変更していません。プレイヤー入力を直接読んでいないので、
キー割り当てシステムとは無関係に動きます。ただし以下は今後気になったら見直してください（今回は未対応）：

- ダメージ処理が `SendMessage("ApplyDamage", ...)` という文字列ベースの呼び出しで、`Player`向け(引数2つ)と
  `Enemy`/`DestructibleObject`向け(引数1つ)でシグネチャが違う。将来 `IDamageable` インターフェースに揃えると安全
- `Ally.cs` は `GameObject.Find("DrawCharacter")` でプレイヤーを名前検索している（シーン内のオブジェクト名に依存）
- `KillZone.cs` は穴に落ちると即シーンリロード。仕様書のUI(§20)ではHP表示を想定しているので、
  「即死リスタート」か「ダメージを受けてスポーン地点に復帰」か、方針を決めるとよさそうです

## Unityプロジェクトへの組み込み手順

1. 元の `PlayerMovement.cs` / `Attack.cs` / `CharacterController2D.cs` を、このフォルダの同名ファイルで上書きする
2. 残りの6つの新規ファイルを `Assets/Scripts` などに追加する
3. シーンに空のGameObjectを作り（例：`Bootstrap`）、`KeyBindingManager` と `KeyInventory` の両方をアタッチする。
   タイトル画面など最初に読み込まれるシーンに置き、両スクリプトが内部で `DontDestroyOnLoad` するので
   ステージ間を移動しても保持されます
4. `Assets > Create > Key Escape > Key Definition` で、使うキーの数だけアセットを作成する（例：`Key_A`, `Key_D`,
   `Key_Space`, `Key_F`, `Key_Ctrl`, `Key_C`）。それぞれ `Key Code` / `Display Name` / `Rank` / `Attribute` を設定
5. `KeyInventory` の `Starting Keys` に、ゲーム開始時点で所持しているキー（例：A, D, Space, F）を登録する
6. `KeyBindingManager` の `Default Bindings` に、初期割り当てを登録する：

   | Action | Key | Unlocked From Start |
   |---|---|---|
   | MoveLeft | Key_A | ✓ |
   | MoveRight | Key_D | ✓ |
   | Jump | Key_Space | ✓ |
   | Attack | Key_F | ✓ |
   | Dash | (空でOK) | ✗ |
   | RangedAttack | (空でOK) | ✗ |

7. `KeyBindingManager` の `Modifier Key Definition` に `Key_Ctrl` を設定する。`Modifier Unlocked From Start` は、
   Ctrlをゲーム開始時から使わせるかどうかで決めてください（仕様書ではまだ未確定なので、ひとまず未チェック＝後で
   拾って解放、を推奨）
8. Player GameObjectの `CharacterController2D` で、`Has Double Jump Ability` / `Has Dash Ability` を必要に応じて
   ステージ攻略中にスクリプトから `true` にする（例：ボス撃破処理から）。`Has Wall Jump Ability` はChapter1中は
   触らずfalseのままにしておく

これでシーンを再生すれば、Inspectorで設定した初期キー（A/D/Space/F）でこれまで通り動けるはずです。
実際に「キー設定画面からキーを差し替える」UIはまだ含めていません（次のタスク候補です）。

## 使い方（コード側から呼ぶ操作）

```csharp
// ステージでキーを拾ったとき
KeyInventory.Instance.AddKey(someKeyDefinition);

// ステージクリア報酬でダッシュを解放するとき
KeyBindingManager.Instance.UnlockAction(GameAction.Dash);

// キー設定画面で、プレイヤーが「ジャンプ」に新しいキーを割り当てたとき
bool success = KeyBindingManager.Instance.TryBindKey(GameAction.Jump, someOwnedKeyDefinition);

// キー設定画面のUI更新
KeyBindingManager.Instance.OnBindingsChanged += RefreshKeyConfigUI;
```

## 既知の制約・次にやるとよいこと

- キー設定（KEY CONFIG）画面のUI自体はまだ未実装です（仕様書Ver.0.2 §20相当）
- 各キーの実際のランク・属性（どのキーが☆3か等）は仕様書でまだ個別に決めていないので、上の手順のランク/属性は
  仮の値を入れて構いません
- 強攻撃の見た目（`IsStrongAttacking`アニメーション）は、対応するアニメーションクリップができてから
  Animator Controllerに繋いでください。今は繋がなくてもエラーにはなりません
- `KeyBindingManager` / `KeyInventory` はC#コンパイラで文法チェックはしていますが、実際のUnityエディタでの
  動作確認はできていません（このクラウド環境にUnity Editorがないため）。取り込んだら一度Playして、
  Console上にエラーが出ないか確認してください
