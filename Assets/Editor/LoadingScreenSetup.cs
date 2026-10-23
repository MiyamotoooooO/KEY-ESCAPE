#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ロード画面の設定アセット（Assets/Resources/LoadingScreenSettings.asset）を、無ければ自動で作る。
/// メニュー KEY ESCAPE > ロード画面 > 設定アセットを選ぶ でも、選べる（無ければ作る）。
/// </summary>
public static class LoadingScreenSetup
{
    private const string Folder = "Assets/Resources";
    private const string Path = "Assets/Resources/LoadingScreenSettings.asset";

    [InitializeOnLoadMethod]
    private static void AutoCreate()
    {
        EditorApplication.delayCall += () =>
        {
            if (Resources.Load<LoadingScreenSettings>("LoadingScreenSettings") == null)
                CreateAsset();
        };
    }

    [MenuItem("KEY ESCAPE/ロード画面/設定アセットを選ぶ")]
    public static void SelectAsset()
    {
        LoadingScreenSettings st = Resources.Load<LoadingScreenSettings>("LoadingScreenSettings");
        if (st == null)
            st = CreateAsset();
        Selection.activeObject = st;
        EditorGUIUtility.PingObject(st);
    }

    private static LoadingScreenSettings CreateAsset()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Resources");
        LoadingScreenSettings st = ScriptableObject.CreateInstance<LoadingScreenSettings>();
        AssetDatabase.CreateAsset(st, Path);
        AssetDatabase.SaveAssets();
        Debug.Log("[LoadingScreen] 設定アセットを作りました：" + Path);
        return st;
    }
}
#endif
