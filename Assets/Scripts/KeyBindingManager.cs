using System;
using System.Collections.Generic;
using UnityEngine;

public class KeyBindingManager : MonoBehaviour
{
    public static KeyBindingManager Instance { get; private set; }

    public event Action OnBindingsChanged;

    [Serializable]
    public struct DefaultBinding
    {
        public GameAction action;
        public KeyDefinition key;
        public bool unlockedFromStart;
    }

    [Header("Chapter 1 defaults (spec Ver.0.2 §26)")]
    [SerializeField] private List<DefaultBinding> defaultBindings = new List<DefaultBinding>();

    [Header("Special key - Chapter 1 has exactly one slot (Ctrl)")]
    [SerializeField] private KeyDefinition modifierKeyDefinition;
    [SerializeField] private bool modifierUnlockedFromStart = false;

    private readonly Dictionary<GameAction, KeyDefinition> _bindings = new Dictionary<GameAction, KeyDefinition>();
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

    public bool IsActionUnlocked(GameAction action) => _unlockedActions.Contains(action);

    public void UnlockAction(GameAction action)
    {
        if (_unlockedActions.Add(action))
            OnBindingsChanged?.Invoke();
    }

    public KeyDefinition ModifierKeyDefinition => modifierKeyDefinition;

    public bool IsModifierUnlocked => _modifierUnlocked;

    public void UnlockModifierKey()
    {
        if (_modifierUnlocked) return;
        _modifierUnlocked = true;
        OnBindingsChanged?.Invoke();
    }

    public KeyDefinition GetBinding(GameAction action) =>
        _bindings.TryGetValue(action, out var key) ? key : null;

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

    /// <summary>
    /// Key Config画面の「配置をリセット」用：全アクションの割り当てを外し、
    /// 何も設定されていない初期状態に戻す。解放状態（IsActionUnlocked）と
    /// 特殊キー（Ctrl）の状態は変えない - リセットされるのはキー配置だけで、
    /// 一度解放したアクションが再びロックされることはない。
    /// </summary>
    public void ResetAllBindings()
    {
        _bindings.Clear();
        OnBindingsChanged?.Invoke();
    }

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

    public bool IsActionDown(GameAction action) => TryGetKeyCode(action, out var kc) && Input.GetKeyDown(kc);
    public bool IsActionHeld(GameAction action) => TryGetKeyCode(action, out var kc) && Input.GetKey(kc);
    public bool IsActionUp(GameAction action) => TryGetKeyCode(action, out var kc) && Input.GetKeyUp(kc);

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

    public float GetBonusMultiplier(GameAction action, KeyAttribute requiredAttribute)
    {
        var key = GetBinding(action);
        return key == null ? 0f : key.GetAttributeBonus(requiredAttribute);
    }
}