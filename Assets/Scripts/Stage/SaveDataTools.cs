#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SaveDataTools
{
    [MenuItem("Tools/KEY ESCAPE/セーブデータを削除")]
    private static void DeleteSave()
    {
        if (EditorUtility.DisplayDialog("確認", "セーブデータ（ステージのクリア状況など）を削除します。よろしいですか？", "削除", "キャンセル"))
        {
            SaveManager.DeleteSave();
            Debug.Log("[SaveDataTools] セーブデータを削除しました。");
        }
    }

    [MenuItem("Tools/KEY ESCAPE/セーブデータの場所を開く")]
    private static void RevealSave()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
#endif