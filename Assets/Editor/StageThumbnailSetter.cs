#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ステージ選択のカード（枠）に出す「一枚絵」を、StageCatalog に入れる道具。
///
/// 絵は Assets/Art/StageThumb/ に、「thumb_(ステージのid).png」の名前で置く。
///   例：thumb_tutorial.png / thumb_stage1.png ～ thumb_stage7.png（idは大文字小文字を区別しない）
/// メニュー「KEY ESCAPE / ステージの絵を、ステージ選択に入れる」を押すと、
///   ・絵を Sprite として読みこむ設定にして、
///   ・StageCatalog の、同じ id のステージの Thumbnail に入れる。
/// 絵を自分の絵に差し替えたいときは、同じ名前で上書きして、もう一度このメニューを押す
/// （または、StageCatalog の Thumbnail に直接ドラッグしてもよい）。
/// 絵のファイルが無いステージは、今のままにする。
/// </summary>
public static class StageThumbnailSetter
{
    private const string Folder = "Assets/Art/StageThumb";
    private const string CatalogPath = "Assets/Resources/StageCatalog.asset";

    [MenuItem("KEY ESCAPE/ステージの絵を、ステージ選択に入れる")]
    public static void Apply()
    {
        StageCatalog catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("ステージの絵を入れる", "StageCatalog が見つかりません。\n" + CatalogPath, "OK");
            return;
        }

        int set = 0;
        string missing = "";
        for (int i = 0; i < catalog.stages.Count; i++)
        {
            StageCatalog.Entry e = catalog.stages[i];
            if (e == null || string.IsNullOrEmpty(e.id))
                continue;

            string path = Folder + "/thumb_" + e.id.ToLowerInvariant() + ".png";
            if (!File.Exists(path))
            {
                missing += "・" + e.stageNumber + "（" + e.id + "）：" + path + "\n";
                continue;
            }

            SetupImporter(path);
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                missing += "・" + e.stageNumber + "（" + e.id + "）：絵を Sprite として読めませんでした\n";
                continue;
            }
            e.thumbnail = sp;
            set++;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = set + " 個のステージに、絵を入れました。\nCtrl+S は要りません（StageCatalog は保存済みです）。";
        if (missing.Length > 0)
            msg += "\n\n絵を入れなかったステージ：\n" + missing;
        EditorUtility.DisplayDialog("ステージの絵を入れる", msg, "OK");
    }

    private static void SetupImporter(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
            return;

        bool changed = false;
        if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
        if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
        if (ti.filterMode != FilterMode.Bilinear) { ti.filterMode = FilterMode.Bilinear; changed = true; }
        if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        if (ti.maxTextureSize < 1024) { ti.maxTextureSize = 1024; changed = true; }
        if (changed)
            ti.SaveAndReimport();
    }
}
#endif
