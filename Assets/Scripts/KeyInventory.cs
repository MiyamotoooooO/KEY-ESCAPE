using System;
using System.Collections.Generic;
using UnityEngine;

public class KeyInventory : MonoBehaviour
{
    public static KeyInventory Instance { get; private set; }

    public event Action<KeyDefinition> OnKeyAdded;

    [SerializeField] private List<KeyDefinition> startingKeys = new List<KeyDefinition>();

    [Header("Key Config画面用：ゲーム内に存在する全キー")]
    [Tooltip("Key Config画面の左一覧に表示する、ゲーム内の全KeyDefinitionをここに手動で登録する（所持済み・未入手を問わず、startingKeysの中身も含めて全部）。ここに登録されていないキーはKey Config画面の一覧に出てこない。")]
    [SerializeField] private List<KeyDefinition> allKeyDefinitions = new List<KeyDefinition>();

    private readonly List<KeyDefinition> _ownedKeys = new List<KeyDefinition>();
    public IReadOnlyList<KeyDefinition> OwnedKeys => _ownedKeys;

    public IReadOnlyList<KeyDefinition> AllKeyDefinitions => allKeyDefinitions;

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

    public void AddKey(KeyDefinition key)
    {
        if (key == null || _ownedKeys.Contains(key))
            return;

        _ownedKeys.Add(key);
        OnKeyAdded?.Invoke(key);
    }

    public void ResetForNewGame()
    {
        _ownedKeys.Clear();
        foreach (var key in startingKeys)
            AddKey(key);
    }
}