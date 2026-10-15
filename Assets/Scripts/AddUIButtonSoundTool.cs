#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - すべてのButtonに、UIButtonSoundを付けるエディタ用ツール。
/// 対象：Assets内のすべてのプレハブ ＋ 今開いているシーン。
/// すでに付いているものは飛ばすので、何度実行しても重複しない。
/// </summary>
public static class AddUIButtonSoundTool
{
    [MenuItem("Tools/KEY ESCAPE/全ButtonにUIButtonSoundを付ける")]
    private static void Run()
    {
        if (!EditorUtility.DisplayDialog(
                "確認",
                "Assets内の全プレハブと、今開いているシーンのButtonに、UIButtonSoundを付けます。\n" +
                "実行前に、Gitでコミットしておくことをおすすめします。続けますか？",
                "実行", "キャンセル"))
            return;

        int prefabAdded = 0;
        int sceneAdded = 0;

        // ---- プレハブ ----
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (Button b in root.GetComponentsInChildren<Button>(true))
                {
                    if (b.GetComponent<UIButtonSound>() != null)
                        continue;
                    b.gameObject.AddComponent<UIButtonSound>();
                    changed = true;
                    prefabAdded++;
                }
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---- 今開いているシーン ----
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            bool changed = false;
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                foreach (Button b in rootObj.GetComponentsInChildren<Button>(true))
                {
                    if (b.GetComponent<UIButtonSound>() != null)
                        continue;
                    Undo.AddComponent<UIButtonSound>(b.gameObject);
                    changed = true;
                    sceneAdded++;
                }
            }
            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[UIButtonSound] 付けた数：プレハブ " + prefabAdded + " 個、シーン " + sceneAdded + " 個。" +
                  "シーンは Ctrl+S で保存してください。");
    }
}
#endif
