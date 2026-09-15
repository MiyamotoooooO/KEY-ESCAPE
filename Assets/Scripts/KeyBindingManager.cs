using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// あらゆるゲームプレイ側のスクリプトが「このアクションは今押されているか？」を
/// 尋ねるための中央窓口。Input.GetKeyDown(KeyCode...)を直接読む代わりに使う。
/// これがあるからこそキーの再割り当てが成立する：ここで割り当てを変更すれば
/// （例えばKey Config画面から、仕様書Ver.0.1 §49）、IsActionDown/Held/Upを
/// 通じて読んでいるすべてのスクリプトに自動的に反映される。
///
/// KeyInventoryと同じ、常駐するBootstrap用のGameObject（DontDestroyOnLoad）に
/// これを1つ付けること。InspectorのdefaultBindingsをChapter 1の初期装備に
/// 合わせて設定する（仕様書Ver.0.2 §26の例：MoveLeft->A、MoveRight->D、
/// Jump->Space、Attack->Fはすべて開始時点で解放済み。DashとRangedAttackは
/// 未割り当て・ロック状態のままにしておく）。
/// </summary>
public class KeyBindingManager : MonoBehaviour
{
    public static KeyBindingManager Instance { get; private set; }

    /// <summary>Fired whenever a binding, lock state, or the modifier key changes - refresh UI here.</summary>
    public event Action OnBindingsChanged;

    [Serializable]
    public struct DefaultBinding
    {
        public GameAction action;
        public KeyDefinition key;
        [Tooltip("Chapter 1: true for MoveLeft/MoveRight/Jump/Attack, false for Dash/RangedAttack.")]
        public bool unlockedFromStart;
    }

    [Header("Chapter 1 defaults (spec Ver.0.2 §26)")]
    [SerializeField] private List<DefaultBinding> defaultBindings = new List<DefaultBinding>();

    [Header("Special key - Chapter 1 has exactly one slot (Ctrl)")]
    [SerializeField] private KeyDefinition modifierKeyDefinition;
    [SerializeField] private bool modifierUnlockedFromStart = false;

    // action -> key currently assigned to it (missing entry = unassigned)
    private readonly Dictionary<GameAction, KeyDefinition> _bindings = new Dictionary<GameAction, KeyDefinition>();
    // which actions the player is allowed to configure/use at all yet
    private readonly HashSet<GameAction> _unlockedActions = new HashSet<GameAction>();
    private bool _modifierUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var d in defaultBindings)
        {
            if (d.key != null)
                _bindings[d.action] = d.key;
            if (d.unlockedFromStart)
                _unlockedActions.Add(d.action);
        }

        _modifierUnlocked = modifierUnlockedFromStart;
    }

    // ---- unlocking ----------------------------------------------------

    public bool IsActionUnlocked(GameAction action) => _unlockedActions.Contains(action);

    /// <summary>Call on a stage-clear / boss-defeat reward (spec Ver.0.2 §12 roadmap).</summary>
    public void UnlockAction(GameAction action)
    {
        if (_unlockedActions.Add(action))
            OnBindingsChanged?.Invoke();
    }

    /// <summary>Which key fills the Chapter 1 special-key slot (e.g. Ctrl). For UI display only - not rebindable at runtime in Chapter 1.</summary>
    public KeyDefinition ModifierKeyDefinition => modifierKeyDefinition;

    public bool IsModifierUnlocked => _modifierUnlocked;

    public void UnlockModifierKey()
    {
        if (_modifierUnlocked) return;
        _modifierUnlocked = true;
        OnBindingsChanged?.Invoke();
    }

    // ---- binding --------------------------------------------------------

    public KeyDefinition GetBinding(GameAction action) =>
        _bindings.TryGetValue(action, out var key) ? key : null;

    /// <summary>
    /// Spec Ver.0.1 §7-2: one key = one action. Binding a key that is
    /// already used elsewhere unbinds it there first. Fails (returns false)
    /// for a locked action or a key the player doesn't own, so the Key
    /// Config UI can rely on the return value instead of re-checking both.
    /// </summary>
    public bool TryBindKey(GameAction action, KeyDefinition key)
    {
        if (key == null || !IsActionUnlocked(action))
            return false;
        if (KeyInventory.Instance != null && !KeyInventory.Instance.Owns(key))
            return false;

        foreach (var kvp in new List<KeyValuePair<GameAction, KeyDefinition>>(_bindings))
        {
            if (kvp.Key != action && kvp.Value == key)
                _bindings.Remove(kvp.Key);
        }

        _bindings[action] = key;
        OnBindingsChanged?.Invoke();
        return true;
    }

    public void UnbindAction(GameAction action)
    {
        if (_bindings.Remove(action))
            OnBindingsChanged?.Invoke();
    }

    /// <summary>For the Key Config screen: "this key is currently used by ○○" labels.</summary>
    public bool TryGetActionForKey(KeyDefinition key, out GameAction action)
    {
        foreach (var kvp in _bindings)
        {
            if (kvp.Value == key)
            {
                action = kvp.Key;
                return true;
            }
        }
        action = default;
        return false;
    }

    // ---- runtime queries: drop-in replacement for Input.GetKey* ---------

    public bool IsActionDown(GameAction action) => TryGetKeyCode(action, out var kc) && Input.GetKeyDown(kc);
    public bool IsActionHeld(GameAction action) => TryGetKeyCode(action, out var kc) && Input.GetKey(kc);
    public bool IsActionUp(GameAction action) => TryGetKeyCode(action, out var kc) && Input.GetKeyUp(kc);

    /// <summary>Special-key combo, e.g. Ctrl+Attack = strong attack (spec §15-16).</summary>
    public bool IsActionDownWithModifier(GameAction action)
    {
        if (!_modifierUnlocked || modifierKeyDefinition == null)
            return false;
        return Input.GetKey(modifierKeyDefinition.keyCode) && IsActionDown(action);
    }

    private bool TryGetKeyCode(GameAction action, out KeyCode keyCode)
    {
        keyCode = KeyCode.None;
        if (!IsActionUnlocked(action))
            return false;
        if (!_bindings.TryGetValue(action, out var key) || key == null)
            return false;
        keyCode = key.keyCode;
        return true;
    }

    /// <summary>Ver.0.2 §9: rank/attribute bonus for whatever key is currently bound to this action.</summary>
    public float GetBonusMultiplier(GameAction action, KeyAttribute requiredAttribute)
    {
        var key = GetBinding(action);
        return key == null ? 0f : key.GetAttributeBonus(requiredAttribute);
    }
}
