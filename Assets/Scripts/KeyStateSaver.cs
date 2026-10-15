using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - キーの所持・割り当て・解放状態を、セーブデータに保存し、ゲーム開始時に復元する。
/// KeyInventory／KeyBindingManagerは書き換えずに、それらの公開機能だけを使う。
/// どこにも付けなくてよい（ゲーム開始時に自動で動く）。
///
/// 流れ：
///   ・Bootstrapが最初に現れた時に、保存してあるキーの状態を復元する（1回だけ）。
///   ・その後、キーを拾ったり、割り当てが変わるたびに、自動で保存する。
///   ・シーンが変わっても、Bootstrapは残り続けるので、状態は引き継がれる。
/// </summary>
public static class KeyStateSaver
{
    private static bool _attached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _attached = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SaveManager.BeforeSave -= Capture;
        SaveManager.BeforeSave += Capture;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        TryAttach();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAttach();
    }

    // Bootstrapが現れるまで（最初のシーンにBootstrapが無い場合など）、シーンが読み込まれるたびに試す。
    private static void TryAttach()
    {
        if (_attached)
            return;

        KeyInventory inv = KeyInventory.Instance;
        KeyBindingManager mgr = KeyBindingManager.Instance;
        if (inv == null || mgr == null)
            return;

        Restore(inv, mgr);

        // 復元が終わってから、変更の監視を始める（復元中に保存が走らないように）。
        inv.OnKeyAdded += OnKeyAdded;
        mgr.OnBindingsChanged += OnBindingsChanged;
        _attached = true;
    }

    private static void OnKeyAdded(KeyDefinition key) => SaveManager.Save();
    private static void OnBindingsChanged() => SaveManager.Save();

    // ---- 保存：現在の状態をSaveManager.Dataに書き込む ----------------------

    private static void Capture()
    {
        KeyInventory inv = KeyInventory.Instance;
        KeyBindingManager mgr = KeyBindingManager.Instance;
        if (inv == null || mgr == null)
            return; // Bootstrapが無いシーンでは、前回の保存内容を変えない。

        SaveData d = SaveManager.Data;
        d.hasKeyData = true;

        d.ownedKeys.Clear();
        foreach (KeyDefinition k in inv.OwnedKeys)
        {
            if (k != null)
                d.ownedKeys.Add(k.name);
        }

        d.unlockedActions.Clear();
        d.bindings.Clear();
        foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
        {
            if (mgr.IsActionUnlocked(action))
                d.unlockedActions.Add(action.ToString());

            KeyDefinition bound = mgr.GetBinding(action);
            if (bound != null)
                d.bindings.Add(new BindingSave { action = action.ToString(), key = bound.name });
        }

        d.modifierUnlocked = mgr.IsModifierUnlocked;
    }

    // ---- 復元：保存してある状態を、実際のゲームに反映する -------------------------

    private static void Restore(KeyInventory inv, KeyBindingManager mgr)
    {
        SaveData d = SaveManager.Data;
        if (!d.hasKeyData)
            return; // 一度も保存していないなら、Bootstrapに設定した初期状態のまま。

        // 所持しているキー
        foreach (string id in d.ownedKeys)
        {
            KeyDefinition key = FindKey(inv, id);
            if (key != null)
                inv.AddKey(key);
            else
                Debug.LogWarning("[KeyStateSaver] セーブデータのキー「" + id + "」が、KeyInventoryのAll Key Definitionsに見つかりません。");
        }

        // 解放済みのアクションと、特殊キー
        foreach (string a in d.unlockedActions)
        {
            if (Enum.TryParse(a, out GameAction action))
                mgr.UnlockAction(action);
        }
        if (d.modifierUnlocked)
            mgr.UnlockModifierKey();

        // 割り当て：Bootstrapの初期の割り当てを一度外して、保存してあるものに置き換える。
        mgr.ResetAllBindings();
        foreach (BindingSave b in d.bindings)
        {
            if (!Enum.TryParse(b.action, out GameAction action))
                continue;
            KeyDefinition key = FindKey(inv, b.key);
            if (key != null)
                mgr.TryBindKey(action, key);
        }
    }

    private static KeyDefinition FindKey(KeyInventory inv, string assetName)
    {
        foreach (KeyDefinition k in inv.AllKeyDefinitions)
        {
            if (k != null && k.name == assetName)
                return k;
        }
        return null;
    }
}