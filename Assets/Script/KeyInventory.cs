using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which KeyDefinitions the player has actually picked up in the
/// world so far (separate from which action a key is bound to - that's
/// KeyBindingManager's job). Put this on a persistent bootstrap GameObject
/// (DontDestroyOnLoad) alongside KeyBindingManager.
///
/// Drives the "NEW KEY!" pickup popup and the list of keys offered in the
/// Key Config screen (spec Ver.0.1 §51, §49).
/// </summary>
public class KeyInventory : MonoBehaviour
{
    public static KeyInventory Instance { get; private set; }

    /// <summary>Fired right after a new key is added - hook the pickup popup UI here.</summary>
    public event Action<KeyDefinition> OnKeyAdded;

    [SerializeField] private List<KeyDefinition> startingKeys = new List<KeyDefinition>();

    private readonly List<KeyDefinition> _ownedKeys = new List<KeyDefinition>();
    public IReadOnlyList<KeyDefinition> OwnedKeys => _ownedKeys;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var key in startingKeys)
            AddKey(key);
    }

    public bool Owns(KeyDefinition key) => key != null && _ownedKeys.Contains(key);

    /// <summary>Call this from a key pickup / chest / boss-drop when the player collects a key.</summary>
    public void AddKey(KeyDefinition key)
    {
        if (key == null || _ownedKeys.Contains(key))
            return;

        _ownedKeys.Add(key);
        OnKeyAdded?.Invoke(key);
    }

    /// <summary>Call from the title screen / New Game flow, not mid-run.</summary>
    public void ResetForNewGame()
    {
        _ownedKeys.Clear();
        foreach (var key in startingKeys)
            AddKey(key);
    }
}
