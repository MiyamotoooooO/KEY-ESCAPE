using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// アクション解放の演出（ActionLockBreakSequence：キーキャラクターがロックを壊しに来る）の部品を、シーンに付ける。
///
/// 開いているシーン（プレハブを開いているときは、そのプレハブの中）にある KeyConfigUnlockSequence、
/// なければ KeyAcquiredPopup、それもなければ KeyConfigUI のあるオブジェクトに、ActionLockBreakSequence を付けて、
/// キーキャラクターのプレハブ（KeyNPC_Visual）と「SPACEでスキップ」の案内を割り当てる。
/// すでに付いていれば、空の欄だけを埋める（調整した値は、そのまま）。
///
/// このメニューを使わなくても、ステージをクリアしたときに自動で作られて動く（その場合の調整は、このメニューで付けたあとに行う）。
///
/// メニュー：Tools > KEY ESCAPE > ロック解除演出 > シーンに追加する
/// </summary>
public static class ActionLockBreakSetup
{
    private const string KeyPrefabPath = "Assets/Prefab/Mob/Enemy/KeyNPC_Visual.prefab";

    [MenuItem("Tools/KEY ESCAPE/ロック解除演出/シーンに追加する")]
    public static void Setup()
    {
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        GameObject root = prefabStage != null ? prefabStage.prefabContentsRoot : null;

        Component target = Find<KeyConfigUnlockSequence>(root);
        if (target == null)
            target = Find<KeyAcquiredPopup>(root);
        if (target == null)
            target = Find<KeyConfigUI>(root);

        if (target == null)
        {
            EditorUtility.DisplayDialog("ロック解除演出",
                "開いているシーン（またはプレハブ）に、KeyConfigUnlockSequence・KeyAcquiredPopup・KeyConfigUI のどれも見つかりませんでした。\nキーコンフィグ画面のあるシーンを開いて、もう一度実行してください。", "OK");
            return;
        }

        GameObject host = target.gameObject;
        ActionLockBreakSequence seq = host.GetComponent<ActionLockBreakSequence>();
        if (seq == null)
            seq = Undo.AddComponent<ActionLockBreakSequence>(host);

        Undo.RecordObject(seq, "ロック解除演出の設定");
        GameObject keyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefabPath);
        if (seq.keyCharacterPrefab == null)
        {
            KeyConfigUnlockSequence other = Find<KeyConfigUnlockSequence>(root);
            if (other != null && other.keyCharacterPrefab != null)
                seq.keyCharacterPrefab = other.keyCharacterPrefab;
            else
                seq.keyCharacterPrefab = keyPrefab;
        }
        if (seq.keyCharacterPrefab == null)
            Debug.LogWarning("[ActionLockBreakSetup] " + KeyPrefabPath + " が見つかりません。Key Character Prefab を、手動で入れてください。");

        if (seq.skipPrompt == null)
        {
            KeyConfigUnlockSequence other = Find<KeyConfigUnlockSequence>(root);
            if (other != null)
                seq.skipPrompt = other.skipPrompt;
        }

        EditorUtility.SetDirty(seq);
        EditorSceneManager.MarkSceneDirty(host.scene);
        Debug.Log("[ActionLockBreakSetup] 「" + host.name + "」に、ロック解除演出（ActionLockBreakSequence）を付けました。シーンを保存（Ctrl+S）してください。");
    }

    private static T Find<T>(GameObject prefabRoot) where T : Component
    {
        T[] all = prefabRoot != null
            ? prefabRoot.GetComponentsInChildren<T>(true)
            : Object.FindObjectsOfType<T>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene.IsValid())
                return all[i];
        }
        return null;
    }
}
