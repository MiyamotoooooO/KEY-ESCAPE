using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ゲーム全体の状態をまとめて扱う。
/// タイトルへ戻る時に、シーンをまたいで残っているキーの仕組み（Bootstrap）をリセットする。
/// </summary>
public static class GameSession
{
    /// <summary>
    /// KeyInventory／KeyBindingManagerを消して、キーの状態を最初から作り直せるようにする。
    /// 次に拠点などのBootstrapが読み込まれた時に、セーブデータから復元される（KeyStateSaver）。
    /// </summary>
    public static void ResetKeySystem()
    {
        KeyInventory inv = KeyInventory.Instance;
        KeyBindingManager mgr = KeyBindingManager.Instance;

        // 消す前に、キーの変更の監視を外す。
        KeyStateSaver.Detach();

        var hosts = new HashSet<GameObject>();
        if (inv != null) hosts.Add(inv.gameObject);
        if (mgr != null) hosts.Add(mgr.gameObject);

        if (inv != null) Object.Destroy(inv);
        if (mgr != null) Object.Destroy(mgr);

        // この2つしか持っていなかったオブジェクトは、空になるので、オブジェクトごと消す。
        // ほかの部品（音の管理など）も付いているオブジェクトは、残す。
        foreach (GameObject go in hosts)
        {
            bool hasOthers = false;
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c is Transform || c is KeyInventory || c is KeyBindingManager)
                    continue;
                hasOthers = true;
                break;
            }
            if (!hasOthers)
                Object.Destroy(go);
        }
    }
}