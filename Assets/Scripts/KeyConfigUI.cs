using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - the Key Config screen (spec Ver.0.1 §49, Ver.0.2 §20 /
/// 新デザインVer.0.3想定)。プレイヤーが各アクションに割り当てられているキーを
/// 確認し、現在所有しているキーの中からドラッグ&ドロップで再割り当てできる画面。
/// 「1キー＝1アクション、操作は自分で組み立てる」という仕組み（仕様書§7）を、
/// コードやデバッグスクリプトだけでなく実際にプレイヤーが使えるようにする画面。
///
/// このスクリプトはコントローラーであり、それ自体は何も描画しない。
///
/// 流れ:
///   1. プレイヤーが画面を開く（toggleKey、またはポーズメニューができたら
///      そのボタンにOpen()/Close()を繋ぐ）。
///   2. 左側に、ゲーム内の全キー（KeyInventory.AllKeyDefinitions）がタブ
///      （すべて／移動／アクション／特殊／未入手）で絞り込まれて並ぶ
///      （KeyGridButtonUI）。所持済みは文字が見え、未入手は「？」＋ロック
///      表示になり、クリック・ドラッグどちらも無効。
///   3. 所持済みのキーをクリックすると右のキー情報パネル（ランク／系統／
///      説明文）が更新される。ドラッグして中央のActionBindingRowへドロップ
///      すると、そのアクションに割り当てられる（TryBindByDrag経由）。
///   4. 6つあるActionBindingRowそれぞれが、現在のキーを表示する。アクション
///      自体がまだ解放されていなければ「ロック中」と表示する
///      （KeyBindingManager.IsActionUnlocked参照）。
///   5. 「配置をリセット」ボタンは確認ダイアログ（resetConfirmPanel）を
///      挟んでから、全アクションの割り当てを未設定に戻す
///      （KeyBindingManager.ResetAllBindings - 解放状態はそのまま）。
/// </summary>
public class KeyConfigUI : MonoBehaviour
{
    [Header("画面")]
    [Tooltip("Key Config画面全体のオーバーレイ。シーン内では非アクティブな状態で開始する。")]
    public GameObject panelRoot;
    [Tooltip("本物のポーズメニューができるまでの仮の開閉手段。ポーズメニューができたら、そのメニューボタンにOpen()/Close()/Toggle()を繋ぐこと。")]
    public KeyCode toggleKey = KeyCode.Tab;
    [Tooltip("この画面が開いている間、ゲームプレイを停止する（Time.timeScale = 0）。timeScaleが0でも、入力の読み取りとUIクリックはどちらも動作する。")]
    public bool pauseGameWhileOpen = true;

    [Header("アクション行（Chapter 1の各アクションにつき1つ、手動配置）")]
    public List<ActionBindingRow> rows = new List<ActionBindingRow>();

    [Header("特殊キー（Ctrl）の状態表示 - Chapter 1では再割り当て不可")]
    public TMP_Text modifierStatusText;

    [Header("キー一覧（左：所持＋未入手を全部表示）")]
    public Transform keyGridContent;
    public GameObject keyGridButtonPrefab;

    [Header("絞り込みタブ（すべて／移動／アクション／特殊／未入手）")]
    public KeyConfigFilter currentFilter = KeyConfigFilter.All;

    [Header("キー情報パネル（右：ランク／系統／説明文）")]
    public GameObject infoPanel;
    public TMP_Text infoNameText;
    public TMP_Text infoCategoryText;
    public TMP_Text infoRankText;
    public TMP_Text infoFlavorText;
    [Tooltip("任意。ランク・属性から自動計算した補正値（例：「移動+5%」）を表示したい場合に割り当てる。対象がなければ「ー」を表示する。")]
    public TMP_Text infoSpecialEffectText;

    [Header("配置をリセット")]
    public GameObject resetConfirmPanel;

    private readonly List<GameObject> _spawnedKeyButtons = new List<GameObject>();
    private bool _isOpen;

    // プロジェクト内の別のスクリプト（おそらく遠距離攻撃の照準ロジック）が、
    // 通常のゲームプレイ中にマウスカーソルを隠す/ロックしている。プレイ中は
    // それで問題ないが、そのままだとこの画面のボタンをクリックしようとしても
    // カーソルが見えない。そこで、開く直前のカーソル状態を保存しておき、
    // 開いている間は強制的に表示＋ロック解除にし、閉じるときに保存していた
    // 状態へ戻す - こうすることでゲームプレイ本来のカーソル挙動には触れない。
    private bool _prevCursorVisible;
    private CursorLockMode _prevCursorLockState;

    private void Start()
    {
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingsChanged += RefreshBindingsOnly;
        if (KeyInventory.Instance != null)
            KeyInventory.Instance.OnKeyAdded += OnKeyAdded;

        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (infoPanel != null)
            infoPanel.SetActive(false);
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingsChanged -= RefreshBindingsOnly;
        if (KeyInventory.Instance != null)
            KeyInventory.Instance.OnKeyAdded -= OnKeyAdded;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    private void OnKeyAdded(KeyDefinition _) => RefreshAll();

    // ---- 開く／閉じる ---------------------------------------------------

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        _isOpen = true;
        if (panelRoot != null)
            panelRoot.SetActive(true);
        if (pauseGameWhileOpen)
            Time.timeScale = 0f;

        _prevCursorVisible = Cursor.visible;
        _prevCursorLockState = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshAll();

        // panelRootは非アクティブな状態で開始し（Start()がSetActive(false)を
        // 呼んでいる）、Unityは非アクティブなオブジェクトに対してLayout Group /
        // Content Size Fitterの計算を行わない。この画面を最初に開いたときだけ
        // レイアウトがまだ計算されておらず、1フレームだけ崩れて表示されること
        // がある - 一度閉じて開き直すと、前回の（正しい）レイアウトが既に
        // キャッシュされているため問題なく見える。アクティブ化とテキスト更新の
        // 直後に強制的にレイアウトを再計算させることで、最初に開いたときの
        // ケースにも対応している。
        if (panelRoot != null)
        {
            var rt = panelRoot.GetComponent<RectTransform>();
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    public void Close()
    {
        _isOpen = false;
        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
        if (pauseGameWhileOpen)
            Time.timeScale = 1f;

        Cursor.visible = _prevCursorVisible;
        Cursor.lockState = _prevCursorLockState;
    }

    // ---- タブ絞り込み -----------------------------------------------------

    // Inspector上でButtonのOnClickにそれぞれ直接繋げられるよう、フィルターごとに
    // 個別のメソッドも用意している（SetFilter(KeyConfigFilter)はenum引数を
    // 持つため、UnityのOnClickの標準ドロップダウンからは選べないので）。
    public void SetFilterAll() => SetFilter(KeyConfigFilter.All);
    public void SetFilterMovement() => SetFilter(KeyConfigFilter.Movement);
    public void SetFilterAttack() => SetFilter(KeyConfigFilter.Attack);
    public void SetFilterSpecial() => SetFilter(KeyConfigFilter.Special);
    public void SetFilterUnowned() => SetFilter(KeyConfigFilter.Unowned);

    public void SetFilter(KeyConfigFilter filter)
    {
        currentFilter = filter;
        RefreshKeyList();
    }

    private bool PassesFilter(KeyDefinition key, bool owned)
    {
        switch (currentFilter)
        {
            case KeyConfigFilter.Movement: return key.attribute == KeyAttribute.Movement;
            case KeyConfigFilter.Attack: return key.attribute == KeyAttribute.Attack;
            case KeyConfigFilter.Special: return key.isSpecialKey;
            case KeyConfigFilter.Unowned: return !owned;
            default: return true; // All
        }
    }

    // ---- キー選択（左一覧のKeyGridButtonUIから呼ばれる） -----------------------

    /// <summary>所持済みキーがクリックされた時にKeyGridButtonUIから呼ばれる。右の情報パネルを更新するだけ（割り当てはドラッグ&ドロップ側で行う）。</summary>
    public void SelectKey(KeyDefinition key)
    {
        if (key == null)
            return;

        ShowKeyInfo(key);
    }

    /// <summary>
    /// 左一覧からActionBindingRowへドラッグ&ドロップされた時に、そのRow
    /// （ActionBindingRow.OnDrop）から呼ばれる。
    /// </summary>
    public void TryBindByDrag(GameAction action, KeyDefinition key)
    {
        if (key == null || KeyBindingManager.Instance == null)
            return;

        bool ok = KeyBindingManager.Instance.TryBindKey(action, key);
        Debug.Log($"[DEBUG] TryBindByDrag: action={action}, key={key.displayName}, TryBindKey result={ok}");
        if (ok)
        {
            ShowKeyInfo(key);
            // 注意：ここでRefreshKeyList()を呼んではいけない（RefreshAll()も同様）。
            // この関数はActionBindingRow.OnDrop経由、つまりドラッグ中のタイル
            // （KeyGridButtonUI）のOnEndDragがまだ発火していない状態で呼ばれている。
            // RefreshKeyList()はキー一覧の全タイルをDestroyして作り直すため、
            // ドラッグ中のタイル自身を破壊してしまい、その後に呼ばれるはずの
            // OnEndDragが実行されなくなる（ドラッグ用ゴーストアイコンが消えずに
            // 残る／以降のドラッグ操作が壊れる原因になっていた）。
            // 行の表示更新はKeyBindingManager.OnBindingsChanged→RefreshBindingsOnly()
            // で行われるので、ここでは何もしなくてよい。
        }
        else
        {
            Debug.LogWarning($"[KeyConfigUI] {key.displayName}を{action}に割り当てられませんでした（アクションがロック中か、そのキーを所有していません）。");
        }
    }

    // ---- 配置をリセット ----------------------------------------------------

    /// <summary>「配置をリセット」ボタンから呼ぶ。即座には実行せず、確認ダイアログを開く。</summary>
    public void OnResetButtonClicked()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(true);
    }

    /// <summary>確認ダイアログの「はい」から呼ぶ。</summary>
    public void ConfirmReset()
    {
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.ResetAllBindings();
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    /// <summary>確認ダイアログの「キャンセル」から呼ぶ。</summary>
    public void CancelResetConfirm()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    // ---- 更新 ---------------------------------------------------

    /// <summary>画面を開いた時に呼ぶ完全版。行の表示に加えて、キー一覧（左）も作り直す。</summary>
    public void RefreshAll()
    {
        RefreshBindingsOnly();
        RefreshKeyList();
    }

    /// <summary>
    /// キー割り当てが変わった時（KeyBindingManager.OnBindingsChanged、
    /// TryBindByDragの成功時）専用の軽量版。キー一覧（左）は割り当て状況に
    /// 左右されないので作り直さない - ドラッグ中に呼ばれる可能性があるため、
    /// ここでRefreshKeyList()を呼ぶとドラッグ中のタイルを破壊してしまう。
    /// </summary>
    private void RefreshBindingsOnly()
    {
        foreach (var row in rows)
        {
            if (row != null)
                row.Refresh();
        }
        RefreshModifierStatus();
    }

    private void RefreshModifierStatus()
    {
        if (modifierStatusText == null || KeyBindingManager.Instance == null)
            return;

        var mgr = KeyBindingManager.Instance;
        string name = mgr.ModifierKeyDefinition != null ? mgr.ModifierKeyDefinition.displayName : "(未設定)";
        modifierStatusText.text = mgr.IsModifierUnlocked ? $"特殊キー：{name}（使用可能）" : $"特殊キー：{name}（未解放）";
    }

    private void RefreshKeyList()
    {
        if (keyGridContent == null || keyGridButtonPrefab == null || KeyInventory.Instance == null)
            return;

        foreach (var go in _spawnedKeyButtons)
        {
            if (go != null)
                Destroy(go);
        }
        _spawnedKeyButtons.Clear();

        foreach (var key in KeyInventory.Instance.AllKeyDefinitions)
        {
            if (key == null)
                continue;

            bool owned = KeyInventory.Instance.Owns(key);
            if (!PassesFilter(key, owned))
                continue;

            GameObject go = Instantiate(keyGridButtonPrefab, keyGridContent);
            var ui = go.GetComponent<KeyGridButtonUI>();
            if (ui != null)
                ui.Setup(key, owned, this);
            _spawnedKeyButtons.Add(go);
        }
    }

    private void ShowKeyInfo(KeyDefinition key)
    {
        if (infoPanel != null)
            infoPanel.SetActive(true);

        if (infoNameText != null)
            infoNameText.text = $"{key.displayName}キー";

        if (infoRankText != null)
        {
            int filled = (int)key.rank;
            // ★☆は今使っているフォントに字形が無く表示されないため、
            // 確実に表示できるASCII文字に差し替えている。星の字形を含む
            // フォントに変えたら、'*'と'-'を'★'と'☆'に戻せばよい。
            infoRankText.text = new string('*', filled) + new string('-', 3 - filled);
        }

        if (infoCategoryText != null)
        {
            if (key.isSpecialKey)
                infoCategoryText.text = "特殊系";
            else
                infoCategoryText.text = key.attribute switch
                {
                    KeyAttribute.Movement => "移動系",
                    KeyAttribute.Attack => "攻撃系",
                    _ => "系統：なし",
                };
        }

        if (infoFlavorText != null)
            infoFlavorText.text = key.flavorText;

        if (infoSpecialEffectText != null)
        {
            float bonus = key.isSpecialKey ? 0f : key.GetAttributeBonus(key.attribute);
            infoSpecialEffectText.text = bonus > 0f
                ? $"{(key.attribute == KeyAttribute.Movement ? "移動" : "攻撃")}+{bonus * 100f:0}%"
                : "ー";
        }
    }
}