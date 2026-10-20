using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - ステージの「挑戦」の記録。ステージの途中で拠点に帰った時に、
/// そのステージで入手したキーを手持ちから外して、次に入った時にもう一度出現させるために使う。
///
/// 流れ：
///   ・ステージのシーン（StageCatalogのScene Name）に入ると、挑戦を始める。入手したキーを記録する。
///   ・やられて、同じステージが読み直された時は、挑戦を続ける（キーはそのまま）。
///   ・クリアすると（StageGoal）、記録を確定する。キーは、そのまま残る。
///   ・拠点（StageCatalogのHub Scene Name）に入ると、記録が残っていれば、そのキーを手持ちから外す。
///     外すキーが操作に割り当て済みなら、割り当ても外す。
///   記録はセーブにも書き込むので、途中でゲームを閉じた場合も、次に拠点に入った時に外される。
/// どこにも付けなくてよい（KeyStateSaverから呼ばれる）。
/// </summary>
public static class StageRun
{
    /// <summary>シーンが読み込まれた時に、KeyStateSaverから呼ばれる（キーの復元が終わった後）。</summary>
    public static void OnSceneLoaded(Scene scene)
    {
        StageCatalog catalog = StageCatalog.Instance;
        if (catalog == null)
            return;

        if (scene.name == catalog.hubSceneName)
        {
            Rollback();
            return;
        }

        for (int i = 0; i < catalog.stages.Count; i++)
        {
            StageCatalog.Entry entry = catalog.stages[i];
            if (entry != null && !string.IsNullOrEmpty(entry.sceneName) && entry.sceneName == scene.name)
            {
                Begin(entry.id);
                return;
            }
        }
    }

    private static void Begin(string stageId)
    {
        SaveData d = SaveManager.Data;
        if (d.runStageId == stageId)
            return; // やられて、同じステージが読み直された。挑戦を続ける。

        // 別のステージの記録が残っていれば、先に取り消す。
        if (!string.IsNullOrEmpty(d.runStageId))
            Rollback();

        d.runStageId = stageId;
        SaveManager.Save();
    }

    /// <summary>キーを入手した時に、KeyStateSaverから呼ばれる。挑戦中なら、記録する。</summary>
    public static void RecordKey(KeyDefinition key)
    {
        SaveData d = SaveManager.Data;
        if (key == null || string.IsNullOrEmpty(d.runStageId))
            return;
        if (!d.runKeys.Contains(key.name))
            d.runKeys.Add(key.name);
    }

    /// <summary>ステージをクリアした時に、StageGoalから呼ばれる。記録を確定して、キーを残す。</summary>
    public static void Complete(string stageId)
    {
        SaveData d = SaveManager.Data;
        if (d.runStageId != stageId)
            return;

        d.runKeys.Clear();
        d.runStageId = "";
        SaveManager.Save();
    }

    /// <summary>挑戦中に入手したキーを、手持ちから外す（割り当ても外す）。</summary>
    private static void Rollback()
    {
        SaveData d = SaveManager.Data;
        if (string.IsNullOrEmpty(d.runStageId) && d.runKeys.Count == 0)
            return;

        KeyInventory inv = KeyInventory.Instance;
        if (inv == null)
            return; // キーの仕組み（Bootstrap）が無い。記録は残しておいて、次の機会に外す。

        KeyBindingManager mgr = KeyBindingManager.Instance;
        var ids = new List<string>(d.runKeys);
        foreach (string id in ids)
        {
            KeyDefinition key = FindKey(inv, id);
            if (key == null)
            {
                Debug.LogWarning("[StageRun] 外すキー「" + id + "」が、KeyInventoryのAll Key Definitionsに見つかりません。");
                continue;
            }

            if (mgr != null && mgr.TryGetActionForKey(key, out GameAction action))
                mgr.UnbindAction(action);
            inv.RemoveKey(key);
        }

        d.runKeys.Clear();
        d.runStageId = "";
        SaveManager.Save();
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
