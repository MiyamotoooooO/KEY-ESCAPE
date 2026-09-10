using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KEY ESCAPE - the Key Config screen (spec Ver.0.1 §49, Ver.0.2 §20).
/// Lets the player see which key is assigned to each action, and reassign
/// it from the keys they currently own. This is the screen that makes the
/// whole "1 key = 1 action, build your own controls" system (spec §7)
/// actually usable by a player instead of only by code/debug scripts.
///
/// This script is the controller; it doesn't draw anything itself. See
/// シーン構築ガイド.md for the Canvas/Button/Text hierarchy this expects,
/// and ActionBindingRow.cs / OwnedKeyButtonUI.cs for the two small pieces
/// it drives.
///
/// Flow:
///   1. Player opens the screen (toggleKey, or wire Open()/Close() to a
///      pause menu button instead once one exists).
///   2. Each of the 6 ActionBindingRow entries shows its current key, or
///      "ロック中" if the action itself isn't unlocked yet (see
///      KeyBindingManager.IsActionUnlocked).
///   3. Clicking "変更" on a row calls BeginRebind(action), which opens the
///      key-picker list of everything currently owned (KeyInventory).
///   4. Clicking a key in that list calls SelectKey(key): if a rebind is in
///      progress, it attempts KeyBindingManager.TryBindKey(action, key);
///      either way it also refreshes the key-info panel (rank/attribute/
///      flavor text) for that key, so browsing keys without reassigning
///      anything works too.
/// </summary>
public class KeyConfigUI : MonoBehaviour
{
    [Header("Screen")]
    [Tooltip("The whole Key Config overlay. Starts inactive in the scene.")]
    public GameObject panelRoot;
    [Tooltip("Temporary way to open/close this screen before a real pause menu exists. Wire a menu button to Open()/Close()/Toggle() instead when you have one.")]
    public KeyCode toggleKey = KeyCode.Tab;
    [Tooltip("Stops gameplay (Time.timeScale = 0) while this screen is open. Input reading and UI clicks both still work at timeScale 0.")]
    public bool pauseGameWhileOpen = true;

    [Header("Action rows (one per Chapter 1 action, placed by hand)")]
    public List<ActionBindingRow> rows = new List<ActionBindingRow>();

    [Header("Special key (Ctrl) status - not rebindable in Chapter 1")]
    public TMP_Text modifierStatusText;

    [Header("Key picker (list of owned keys)")]
    public GameObject keyPickerPanel;
    public Transform keyListContent;
    public GameObject ownedKeyButtonPrefab;

    [Header("Key info panel (rank / attribute / flavor text)")]
    public GameObject infoPanel;
    public TMP_Text infoNameText;
    public TMP_Text infoRankText;
    public TMP_Text infoAttributeText;
    public TMP_Text infoFlavorText;

    private GameAction? _pendingAction;
    private readonly List<GameObject> _spawnedKeyButtons = new List<GameObject>();
    private bool _isOpen;

    // Some other script in the project (likely the ranged-attack aim logic)
    // hides/locks the mouse cursor during normal gameplay. That's fine during
    // play, but it means the cursor is invisible while trying to click
    // buttons on this screen. We save whatever the cursor state was right
    // before opening, force it visible+unlocked while open, then restore the
    // saved state on close - so gameplay's own cursor behavior is untouched.
    private bool _prevCursorVisible;
    private CursorLockMode _prevCursorLockState;

    private void Start()
    {
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingsChanged += RefreshAll;
        if (KeyInventory.Instance != null)
            KeyInventory.Instance.OnKeyAdded += OnKeyAdded;

        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (keyPickerPanel != null)
            keyPickerPanel.SetActive(false);
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.OnBindingsChanged -= RefreshAll;
        if (KeyInventory.Instance != null)
            KeyInventory.Instance.OnKeyAdded -= OnKeyAdded;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    private void OnKeyAdded(KeyDefinition _) => RefreshAll();

    // ---- open / close ---------------------------------------------------

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

        // panelRoot starts inactive (Start() calls SetActive(false)), and Unity
        // does not run Layout Group / Content Size Fitter calculations on
        // inactive objects. The very first time this screen opens, the layout
        // hasn't been computed yet, so it can render misaligned for a frame -
        // closing and reopening looks fine afterwards because the previous
        // (correct) layout is already cached. Forcing an immediate rebuild
        // right after activating + refreshing text fixes the first-open case.
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
        if (keyPickerPanel != null)
            keyPickerPanel.SetActive(false);
        if (pauseGameWhileOpen)
            Time.timeScale = 1f;

        Cursor.visible = _prevCursorVisible;
        Cursor.lockState = _prevCursorLockState;

        _pendingAction = null;
    }

    // ---- rebinding --------------------------------------------------------

    /// <summary>Wired from each ActionBindingRow's "変更" button.</summary>
    public void BeginRebind(GameAction action)
    {
        _pendingAction = action;
        if (keyPickerPanel != null)
            keyPickerPanel.SetActive(true);
        RefreshKeyList();
    }

    /// <summary>Wired from the key-picker panel's "キャンセル" button.</summary>
    public void CancelRebind()
    {
        _pendingAction = null;
        if (keyPickerPanel != null)
            keyPickerPanel.SetActive(false);
    }

    /// <summary>
    /// Wired from each OwnedKeyButtonUI. If a rebind is in progress, tries to
    /// bind this key to the pending action. Always also updates the key-info
    /// panel, so tapping a key to just look at it (no pending rebind) works.
    /// </summary>
    public void SelectKey(KeyDefinition key)
    {
        if (key == null)
            return;

        ShowKeyInfo(key);

        if (_pendingAction.HasValue)
        {
            bool ok = KeyBindingManager.Instance != null && KeyBindingManager.Instance.TryBindKey(_pendingAction.Value, key);
            if (ok)
            {
                _pendingAction = null;
                if (keyPickerPanel != null)
                    keyPickerPanel.SetActive(false);
                // RefreshAll() also fires via OnBindingsChanged, but calling
                // it here too keeps the screen in sync even if this script
                // is used without that event wired up for some reason.
                RefreshAll();
            }
            else
            {
                Debug.LogWarning($"[KeyConfigUI] Could not bind {key.displayName} to {_pendingAction.Value} (locked action, or key not owned).");
            }
        }
    }

    // ---- refreshing ---------------------------------------------------

    public void RefreshAll()
    {
        foreach (var row in rows)
        {
            if (row != null)
                row.Refresh();
        }
        RefreshModifierStatus();
        RefreshKeyList();
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
        if (keyListContent == null || ownedKeyButtonPrefab == null || KeyInventory.Instance == null)
            return;

        foreach (var go in _spawnedKeyButtons)
        {
            if (go != null)
                Destroy(go);
        }
        _spawnedKeyButtons.Clear();

        foreach (var key in KeyInventory.Instance.OwnedKeys)
        {
            // Special keys (Ctrl) use the separate modifier slot, not a
            // per-action binding - keep them out of the assignable list.
            if (key == null || key.isSpecialKey)
                continue;

            GameObject go = Instantiate(ownedKeyButtonPrefab, keyListContent);
            var ui = go.GetComponent<OwnedKeyButtonUI>();
            if (ui != null)
                ui.Setup(key, this);
            _spawnedKeyButtons.Add(go);
        }
    }

    private void ShowKeyInfo(KeyDefinition key)
    {
        if (infoPanel != null)
            infoPanel.SetActive(true);

        if (infoNameText != null)
            infoNameText.text = key.displayName;

        if (infoRankText != null)
        {
            int filled = (int)key.rank;
            infoRankText.text = new string('★', filled) + new string('☆', 3 - filled);
        }

        if (infoAttributeText != null)
        {
            infoAttributeText.text = key.attribute switch
            {
                KeyAttribute.Movement => "属性：移動",
                KeyAttribute.Attack => "属性：攻撃",
                _ => "属性：なし",
            };
        }

        if (infoFlavorText != null)
            infoFlavorText.text = key.flavorText;
    }
}
