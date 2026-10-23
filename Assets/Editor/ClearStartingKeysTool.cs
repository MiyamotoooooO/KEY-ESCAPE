using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 最初に持っているキー（デバッグ用に入れてあったもの）を、プロジェクトの全シーンから消す。
///
/// メニュー Tools > KEY ESCAPE > 最初に持っているキーを全部消す
///   ・KeyInventory の「Starting Keys」を、空にする。
///   ・KeyBindingManager の「Default Bindings」に入っているキーも、外す（持っていないキーに、割り当てが残らないように）。
///   ・変更があったシーンは、自動で保存する。実行前に、いま開いているシーンの保存を聞かれる。
///   ・何回実行しても、同じ結果になる。
/// </summary>
public static class ClearStartingKeysTool
{
    [MenuItem("Tools/KEY ESCAPE/最初に持っているキーを全部消す")]
    public static void Run()
    {
        if (!EditorUtility.DisplayDialog("最初に持っているキーを全部消す",
            "全シーンの KeyInventory の Starting Keys と、KeyBindingManager の Default Bindings のキーを、空にします。\n変更のあったシーンは、自動で保存されます。\n\n（GitHubへのバックアップは、済んでいますか？）",
            "実行する", "やめる"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string prevScene = SceneManager.GetActiveScene().path;
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        List<string> report = new List<string>();
        int totalKeys = 0;
        int totalBindings = 0;

        for (int g = 0; g < guids.Length; g++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[g]);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;
            int keys = 0;
            int binds = 0;

            KeyInventory inv = Object.FindObjectOfType<KeyInventory>(true);
            if (inv != null)
            {
                SerializedObject so = new SerializedObject(inv);
                SerializedProperty list = so.FindProperty("startingKeys");
                if (list != null && list.arraySize > 0)
                {
                    keys = list.arraySize;
                    list.ClearArray();
                    so.ApplyModifiedProperties();
                    changed = true;
                }
            }

            KeyBindingManager mgr = Object.FindObjectOfType<KeyBindingManager>(true);
            if (mgr != null)
            {
                SerializedObject so = new SerializedObject(mgr);
                SerializedProperty arr = so.FindProperty("defaultBindings");
                if (arr != null)
                {
                    for (int i = 0; i < arr.arraySize; i++)
                    {
                        SerializedProperty k = arr.GetArrayElementAtIndex(i).FindPropertyRelative("key");
                        if (k != null && k.objectReferenceValue != null)
                        {
                            k.objectReferenceValue = null;
                            binds++;
                            changed = true;
                        }
                    }
                    if (binds > 0)
                        so.ApplyModifiedProperties();
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add("・" + scene.name + "：Starting Keys " + keys + "個、Default Bindings " + binds + "個を消しました");
                totalKeys += keys;
                totalBindings += binds;
            }
        }

        if (!string.IsNullOrEmpty(prevScene))
            EditorSceneManager.OpenScene(prevScene, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("最初に持っているキーを全部消す",
            report.Count > 0
                ? "消しました。\n\n" + string.Join("\n", report) + "\n\n（Starting Keys 合計 " + totalKeys + "個、Default Bindings 合計 " + totalBindings + "個）"
                : "消すものは、ありませんでした（すでに空です）。",
            "OK");
    }
}
