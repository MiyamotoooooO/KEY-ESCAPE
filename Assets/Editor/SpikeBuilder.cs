using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// トゲのプレハブ（Assets/Prefab/Object/Spike.prefab）を、仮の絵つきで作る。
///
/// 組み上がる形：
///   Spike … SpriteRenderer（横にタイル状に並ぶ）・BoxCollider2D（Is Trigger。大きさは絵に合わせて変わる）・Hazard
/// 仮の絵（灰色の三角のトゲ）は、Assets/sprite/Object/SpikePlaceholder.png に作る。
/// 自分の絵に替える時は、プレハブの SpriteRenderer の Sprite を差し替える
/// （Draw Mode が Tiled のままなら、絵が横に並ぶ。Size の横幅を変えると、トゲの列が長くなり、当たり判定も伸びる）。
/// 仮の絵のファイルがすでにある時は、作り直さない（自分で差し替えた絵を、消さないため）。
///
/// メニュー：Tools > KEY ESCAPE > ステージ部品 > トゲのプレハブを作る
/// すでにプレハブがある時は、上書き（直した設定が消える）か、別の名前で作るかを選べる。
/// </summary>
public static class SpikeBuilder
{
    private const string PrefabFolder = "Assets/Prefab/Object";
    private const string PrefabPath = "Assets/Prefab/Object/Spike.prefab";
    private const string SpriteFolder = "Assets/sprite/Object";
    private const string SpritePath = "Assets/sprite/Object/SpikePlaceholder.png";

    private const int PixelsPerUnit = 64; // 1ワールド単位あたりのピクセル数
    private const int TileW = 64;         // トゲ1本分の絵の幅（ピクセル）。ワールドでは1
    private const int TileH = 48;         // 高さ（ピクセル）。ワールドでは 0.75
    private const float Outline = 3f;     // 縁取りの太さ（ピクセル）

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/トゲのプレハブを作る")]
    public static void Build()
    {
        string path = PrefabPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "トゲのプレハブ",
                "すでに " + PrefabPath + " があります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
                path = AssetDatabase.GenerateUniqueAssetPath(PrefabPath);
        }

        EnsureFolder("Assets/Prefab", "Object");
        EnsureFolder("Assets/sprite", "Object");

        Sprite sprite = EnsureSprite();
        if (sprite == null)
        {
            Debug.LogError("[SpikeBuilder] 仮の絵を作れませんでした。");
            return;
        }

        GameObject go = new GameObject("Spike");

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(3f, TileH / (float)PixelsPerUnit); // 3本分

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.autoTiling = true; // 絵の大きさ（Size）に、当たり判定が合う
        box.size = sr.size;
        box.offset = new Vector2(0f, sr.size.y * 0.5f); // 絵の足元が、置いた位置に来る

        go.AddComponent<Hazard>();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        Debug.Log("[SpikeBuilder] " + path + " を作りました。シーンに置いて、SpriteRenderer の Size で長さを変えられます。");
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    // ---- 仮の絵 -------------------------------------------------------

    private static Sprite EnsureSprite()
    {
        if (!File.Exists(SpritePath))
            WritePng();

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    private static void WritePng()
    {
        Texture2D tex = new Texture2D(TileW, TileH, TextureFormat.RGBA32, false);
        Color32 ink = new Color32(30, 30, 34, 255);
        Color32 fill = new Color32(160, 162, 172, 255);

        var px = new Color32[TileW * TileH];
        const int N = 3; // 縁をなめらかにするための細かい分割
        for (int y = 0; y < TileH; y++)
        {
            for (int x = 0; x < TileW; x++)
            {
                float outer = 0f;
                float inner = 0f;
                for (int sy = 0; sy < N; sy++)
                {
                    for (int sx = 0; sx < N; sx++)
                    {
                        float fx = x + (sx + 0.5f) / N;
                        float fy = y + (sy + 0.5f) / N;
                        if (Inside(fx, fy, 0f)) outer += 1f;
                        if (Inside(fx, fy, Outline)) inner += 1f;
                    }
                }
                outer /= N * N;
                inner /= N * N;

                if (outer <= 0f)
                {
                    px[y * TileW + x] = new Color32(0, 0, 0, 0);
                    continue;
                }
                float edge = Mathf.Max(0f, outer - inner);
                float r = (ink.r * edge + fill.r * inner) / outer;
                float g = (ink.g * edge + fill.g * inner) / outer;
                float b = (ink.b * edge + fill.b * inner) / outer;
                px[y * TileW + x] = new Color32((byte)r, (byte)g, (byte)b, (byte)(outer * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    /// <summary>三角形の内側か。insetだけ内側に縮めた三角形で見る（縁取りのため）。底は下の端、頂点は上の端。</summary>
    private static bool Inside(float x, float y, float inset)
    {
        if (y < inset || y > TileH - 0.5f)
            return false;
        float half = (TileW * 0.5f) * (1f - y / TileH) - inset;
        return Mathf.Abs(x - TileW * 0.5f) <= half;
    }

    private static void ConfigureImporter()
    {
        TextureImporter ti = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (ti == null)
            return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PixelsPerUnit;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;

        // 横に並べる（Tiled）には、Mesh Type が Full Rect である必要がある。
        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        ti.SetTextureSettings(s);

        ti.SaveAndReimport();
    }
}
