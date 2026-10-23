#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ゴールのオブジェクトの子に、キーキャラクター4体（G・O・A・L）を並べて、StageGoalLetters を付ける。
///
/// 使い方：Hierarchyで、StageGoal の付いたゴールのオブジェクトを選んで、メニュー
///   Tools > KEY ESCAPE > ゴール > 選んだゴールに G O A L を並べる
/// を実行する。すでに並べてあるときは、並べ直す（子の「GoalLetter_」は、作り直される）。
/// 並べたあと、位置・間隔は、子のオブジェクトを動かして調整してよい。
/// </summary>
public static class StageGoalLettersBuilder
{
    private const string KeyPrefabPath = "Assets/Prefab/Mob/Enemy/KeyNPC_Visual.prefab";
    private const float Spacing = 1.4f;
    private static readonly string[] Letters = { "G", "O", "A", "L" };

    [MenuItem("Tools/KEY ESCAPE/ゴール/選んだゴールに G O A L を並べる")]
    public static void Build()
    {
        GameObject goal = Selection.activeGameObject;
        if (goal == null || !goal.scene.IsValid())
        {
            EditorUtility.DisplayDialog("ゴールに G O A L を並べる",
                "Hierarchyで、StageGoal の付いたゴールのオブジェクトを選んでから、もう一度実行してください。", "OK");
            return;
        }
        if (goal.GetComponent<StageGoal>() == null)
        {
            if (!EditorUtility.DisplayDialog("ゴールに G O A L を並べる",
                "選んだオブジェクト「" + goal.name + "」には、StageGoal が付いていません。\nこのまま並べますか？（近づいたあとの自動クリアは、StageGoal を付けるまで動きません）",
                "並べる", "やめる"))
                return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("ゴールに G O A L を並べる",
                KeyPrefabPath + " が見つかりません。", "OK");
            return;
        }

        // 前に並べた文字を、片づける
        for (int i = goal.transform.childCount - 1; i >= 0; i--)
        {
            Transform c = goal.transform.GetChild(i);
            if (c.name.StartsWith("GoalLetter_"))
                Undo.DestroyObjectImmediate(c.gameObject);
        }

        StageGoalLetters comp = goal.GetComponent<StageGoalLetters>();
        if (comp == null)
            comp = Undo.AddComponent<StageGoalLetters>(goal);

        Transform[] list = new Transform[Letters.Length];
        for (int i = 0; i < Letters.Length; i++)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, goal.transform);
            Undo.RegisterCreatedObjectUndo(go, "G O A L を並べる");
            go.name = "GoalLetter_" + Letters[i];
            go.transform.localPosition = new Vector3((i - (Letters.Length - 1) * 0.5f) * Spacing, 0f, 0f);

            // 文字を、そのキャラクターのキートップに入れる
            KeyLabel[] labels = go.GetComponentsInChildren<KeyLabel>(true);
            for (int k = 0; k < labels.Length; k++)
            {
                Undo.RecordObject(labels[k], "G O A L");
                labels[k].textOverride = Letters[i];
                labels[k].keyOverride = null;
                TMPro.TextMeshPro tmp = labels[k].GetComponent<TMPro.TextMeshPro>();
                if (tmp != null)
                {
                    Undo.RecordObject(tmp, "G O A L");
                    tmp.text = Letters[i];
                    if (comp.overrideLetterColor)
                        tmp.color = comp.letterColor;
                }
                EditorUtility.SetDirty(labels[k]);
            }
            list[i] = go.transform;
        }

        Undo.RecordObject(comp, "G O A L を並べる");
        comp.letters = list;
        if (comp.goal == null)
            comp.goal = goal.GetComponent<StageGoal>();
        EditorUtility.SetDirty(comp);
        EditorSceneManager.MarkSceneDirty(goal.scene);
        Selection.activeGameObject = goal;
        Debug.Log("[StageGoalLettersBuilder] 「" + goal.name + "」に、G O A L の4体を並べました。シーンを保存（Ctrl+S）してください。");
    }
}
#endif
