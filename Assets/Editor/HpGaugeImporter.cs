using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Resources/HPGauge/ に入れたHPゲージ用PNG（Frame / Red / White）の取り込み設定を自動で整える。
///
///   ・Sprite(Single) / Filter = Bilinear（手描きの線をなめらかに）/ 圧縮なし / Mipmapなし
///
/// 新しくPNGを入れたり、中身を差し替えたときは自動で適用される。
/// すでに入っている素材には、メニュー Tools > KEY ESCAPE > HPゲージ > 素材のインポート設定を適用 で適用する。
/// 画像を差し替えるときは、PNGを削除して入れ直さず、同じ名前で上書きすること
/// （入れ直すと、画像の管理番号（GUID）が変わって、参照が外れることがある）。
/// </summary>
public class HpGaugeImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/HPGauge";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder + "/"))
            return;

        TextureImporter ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
    }

    [MenuItem("Tools/KEY ESCAPE/HPゲージ/素材のインポート設定を適用")]
    public static void ApplyImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.Refresh();
        Debug.Log("[HpGauge] 素材のインポート設定を適用しました（" + guids.Length + "枚）");
    }
}
