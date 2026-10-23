using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 「はじめから」のあとの会話で、怪我をしたKキーが座る場所の目印を、SelectScene に置く。
///
/// メニュー Tools > KEY ESCAPE > はじめからの会話 > Kキーの位置の目印を置く
///   ・SelectScene（拠点）を開いた状態で実行する。
///   ・「NewGameIntro_KeyPoint」という空のオブジェクトを、主人公の右に作る（すでにあれば、それを選ぶだけ）。
///   ・Sceneビューで、家の前に動かして、Ctrl+S で保存する。
/// </summary>
public static class NewGameIntroSetup
{
    [MenuItem("Tools/KEY ESCAPE/はじめからの会話/Kキーの位置の目印を置く")]
    public static void PlaceKeyPoint()
    {
        NewGameIntroKeyPoint existing = Object.FindObjectOfType<NewGameIntroKeyPoint>(true);
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            EditorUtility.DisplayDialog("Kキーの位置", "目印は、すでにあります（選択しました）。\nSceneビューで、家の前に動かしてください。", "OK");
            return;
        }

        GameObject player = null;
        try { player = GameObject.FindGameObjectWithTag("Player"); } catch { }
        Vector3 pos = player != null ? player.transform.position + new Vector3(4f, 0f, 0f) : Vector3.zero;

        GameObject go = new GameObject("NewGameIntro_KeyPoint");
        go.transform.position = pos;
        go.AddComponent<NewGameIntroKeyPoint>();
        Undo.RegisterCreatedObjectUndo(go, "Kキーの位置の目印を置く");
        Selection.activeObject = go;
        EditorGUIUtility.PingObject(go);
        EditorSceneManager.MarkSceneDirty(go.scene);

        EditorUtility.DisplayDialog("Kキーの位置",
            "「NewGameIntro_KeyPoint」を置きました。\n\nSceneビューで、家の前（Kキーを座らせたい場所）に動かして、Ctrl+S でシーンを保存してください。\n黄色い枠が、Kキーの立つ場所です（ゲーム中は見えません）。", "OK");
    }
}
