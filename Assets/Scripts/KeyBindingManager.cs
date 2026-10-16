using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central place every gameplay script asks "is this action pressed right
/// now?" instead of reading Input.GetKeyDown(KeyCode...) directly. This is
/// the piece that makes keys re-assignable at all: change a binding here
/// (e.g. from the Key Config screen, spec Ver.0.1 §49) and every script
/// that reads through IsActionDown/Held/Up picks it up automatically.
///
/// Put one of these on a persistent bootstrap GameObject (DontDestroyOnLoad),
/// alongside KeyInventory. Wire up `defaultBindings` in the Inspector to
/// match the Chapter 1 starting loadout (spec Ver.0.2 §26 example:
/// MoveLeft->A, MoveRight->D, Jump->Space, Attack->F, all unlocked from the
/// start; Dash and RangedAttack left unassigned and locked).
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

    // ---- キーの一時的な予約 ----------------------------------------------
    // 掲示板の前など、「このキーでインタラクトする」場所にいる間、そのキーに割り当てた
    // 操作（攻撃など）を無効にする。複数の場所から同じキーを予約しても、すべて離れるまで続く。
    private readonly Dictionary<KeyCode, int> _reservedKeys = new Dictionary<KeyCode, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 重複した時は、このオブジェクトごとではなく、この部品だけを消す。
            // （同じオブジェクトにあるKeyConfigUIなど、そのシーンの画面の部品を巻き込まないため）
            Destroy(this);
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

    // ---- キーの予約（インタラクト用） -------------------------------------

    /// <summary>このキーを、ゲームの操作に使わせない（インタラクト用に確保する）。離れる時は、ReleaseKeyを呼ぶ。</summary>
    public void ReserveKey(KeyCode keyCode)
    {
        _reservedKeys.TryGetValue(keyCode, out int count);
        _reservedKeys[keyCode] = count + 1;
    }

    public void ReleaseKey(KeyCode keyCode)
    {
        if (!_reservedKeys.TryGetValue(keyCode, out int count))
            return;
        if (count <= 1) _reservedKeys.Remove(keyCode);
        else _reservedKeys[keyCode] = count - 1;
    }

    // ---- runtime queries: drop-in replacement for Input.GetKey* ---------

    // GamePause で止めている間（会話・キー入手ポップアップ中）は、操作入力をすべて無効にする。
    public bool IsActionDown(GameAction action) => !GamePause.IsPaused && TryGetKeyCode(action, out var kc) && Input.GetKeyDown(kc);
    public bool IsActionHeld(GameAction action) => !GamePause.IsPaused && TryGetKeyCode(action, out var kc) && Input.GetKey(kc);
    public bool IsActionUp(GameAction action) => !GamePause.IsPaused && TryGetKeyCode(action, out var kc) && Input.GetKeyUp(kc);

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
        // 掲示板の前などで予約されているキーは、ゲームの操作に使わせない。
        if (_reservedKeys.ContainsKey(key.keyCode))
            return false;
        keyCode = key.keyCode;
        return true;
    }

    /// <summary>性格仕様書：現在このアクションに割り当てられているキーの性格効果一式
    /// （統計ボーナスだけでなく、入力失敗率／操作性低下／最大HP減少／条件付き・
    /// 確率発動なども含む。中身はPersonalityEffect参照）。何も割り当てられて
    /// いなければPersonalityEffect.Noneを返す。</summary>
    public PersonalityEffect GetPersonalityEffect(GameAction action)
    {
        var key = GetBinding(action);
        return key == null ? PersonalityEffect.None : key.GetPersonalityEffect(action);
    }
}