#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 拠点（SelectScene）に、「Tutorial をクリアして戻ってきたとき、キーがヒントを出す」ためのゾーンを作る。
///
/// 使い方：SelectScene を開いて、メニュー
///   Tools > KEY ESCAPE > ステージ部品 > 拠点に戻ったときのヒントを作る
/// を実行する。ゾーン（GimmickIntroZone）が1つできて、次の設定が入る。
///   ・Play On Scene Start … ON（範囲に入らなくても、拠点に戻ったら自動で出る）
///   ・Required Cleared Stage Id … Tutorial（Tutorial をクリア済みのときだけ）
///   ・Show Once … ON（最初の1回だけ）
///   ・セリフ … 「こんな風にしてみんなを集めよう!!」（Steps の Message で、あとから変えられる）
/// すでに作ってあるときは、それを選ぶだけ（重ねて作らない）。
/// </summary>
public static class TutorialClearHintBuilder
{
    private const string UiPath = "Assets/Prefab/GimmickIntroUI.prefab";
    private const string HintId = "TutorialClearHint";

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/拠点に戻ったときのヒントを作る")]
    public static void Build()
    {
        GimmickIntroZone[] zones = Object.FindObjectsOfType<GimmickIntroZone>(true);
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].introId == HintId)
            {
                Selection.activeGameObject = zones[i].gameObject;
                EditorGUIUtility.PingObject(zones[i].gameObject);
                Debug.Log("[TutorialClearHintBuilder] すでに「" + zones[i].name + "」があります。それを選びました。");
                return;
            }
        }

        GameObject go = new GameObject("TutorialClearHint");
        Undo.RegisterCreatedObjectUndo(go, "拠点に戻ったときのヒント");
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 1f);
        GimmickIntroZone zone = go.AddComponent<GimmickIntroZone>();

        zone.introId = HintId;
        zone.showOnce = true;
        zone.playOnSceneStart = true;
        zone.requiredClearedStageId = "Tutorial";
        zone.uiPrefab = AssetDatabase.LoadAssetAtPath<GimmickIntroUI>(UiPath);
        zone.steps = new GimmickIntroZone.Step[1];
        zone.steps[0] = new GimmickIntroZone.Step();
        zone.steps[0].message = "こんな風にしてみんなを集めよう!!";
        if (zone.uiPrefab == null)
            Debug.LogWarning("[TutorialClearHintBuilder] " + UiPath + " が見つかりません。Ui Prefab を、手動で入れてください。");

        EditorUtility.SetDirty(zone);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[TutorialClearHintBuilder] 「TutorialClearHint」を作りました。シーンを保存（Ctrl+S）してください。");
    }
}
#endif
