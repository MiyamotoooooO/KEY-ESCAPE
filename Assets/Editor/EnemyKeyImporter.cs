using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Art/EnemyKey/ に入れた敵キー用PNG（KeyBody / Arm / Leg...）の取り込み設定を自動で整える。
///
///   ・Sprite(Single) / Pixels Per Unit = 500 / Filter = Bilinear（線画をなめらかに）/ 圧縮なし
///   ・回転の軸（Pivot）: 体=キーの見た目の中心、腕=付け根（左上の端）、脚=付け根（上の端）
///
/// 新しくPNGを入れたときは自動で適用される。すでに入っている素材には、メニュー
/// Tools > KEY ESCAPE > 敵キー > 素材のインポート設定を適用 で適用する。
/// 数値を変えたいときは、下の定数を書き換えてからメニューを実行する。
/// </summary>
public class EnemyKeyImporter : AssetPostprocessor
{
    public const string ArtFolder = "Assets/Art/EnemyKey";

    /// <summary>1ユニットあたりのピクセル数。小さくするとキャラが大きくなる。</summary>
    public const float PixelsPerUnit = 500f;

    // Pivot は画像の左下が(0,0)、右上が(1,1)
    private static readonly Vector2 BodyPivot = new Vector2(0.53f, 0.5f);   // 画像左の余白ぶん補正した、キーの見た目の中心
    private static readonly Vector2 ArmPivot = new Vector2(0.05f, 0.94f);   // 腕の付け根（左上の端）
    private static readonly Vector2 LegPivot = new Vector2(0.88f, 0.98f);   // 脚の付け根（右上の端）

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtFolder + "/"))
            return;

        Vector2 pivot;
        if (!TryGetPivot(Path.GetFileNameWithoutExtension(assetPath), out pivot))
            return;

        TextureImporter ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PixelsPerUnit;
        ti.filterMode = FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        s.spritePixelsPerUnit = PixelsPerUnit;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
    }

    private static bool TryGetPivot(string fileName, out Vector2 pivot)
    {
        string n = fileName.ToLowerInvariant();
        if (n.StartsWith("keybody")) { pivot = BodyPivot; return true; }
        if (n.StartsWith("arm")) { pivot = ArmPivot; return true; }
        if (n.StartsWith("leg")) { pivot = LegPivot; return true; }
        pivot = Vector2.zero;
        return false;
    }

    [MenuItem("Tools/KEY ESCAPE/敵キー/素材のインポート設定を適用")]
    public static void ApplyImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.Refresh();
        Debug.Log("[EnemyKey] 素材のインポート設定を適用しました（" + guids.Length + "枚）");
    }
}
