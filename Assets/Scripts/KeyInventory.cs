using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーがこれまでにワールド内で実際に拾ったKeyDefinitionを管理する
/// （どのアクションにキーが割り当てられているかとは別の話 - そちらは
/// KeyBindingManagerの担当）。KeyBindingManagerと同じ、常駐するBootstrap用の
/// GameObject（DontDestroyOnLoad）に付けること。
///
/// 「NEW KEY！」のピックアップポップアップと、Key Config画面で選べるキーの
/// 一覧を駆動する（仕様書Ver.0.1 §51、§49）。
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
