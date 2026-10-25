using UnityEditor;
using UnityEngine;

/// <summary>
/// 川のプレハブ（Assets/Prefab/Object/River.prefab）と、川の足場のプレハブ（Assets/Prefab/Object/RiverPlatform.prefab）を作る。
///
/// 組み上がる形：
///   River … BoxCollider2D（Is Trigger。川の範囲）・RiverCurrent（流す・酸素・川底・仮の水面を出す）
/// 置いた位置が、水面の高さ（川は、置いた位置から下へ広がる）。横は、置いた位置が中心。
/// 大きさは、プレハブの BoxCollider2D の Size で変える（初期値：幅12、高さ3）。
/// Layer は、「Ground」という名前の Layer があれば、それにする（川底が地面になって、川の中でも立てるため）。
/// なければ、Default のまま。その時は、自分で地面と同じ Layer にする。
///
/// メニュー：Tools > KEY ESCAPE > ステージ部品 > 川のプレハブを作る
/// メニュー：Tools > KEY ESCAPE > ステージ部品 > 川の足場のプレハブを作る
///   絵：Assets/Art/Sketch/sketch_platform_short.png。SpriteRenderer（Draw Mode：Sliced）・BoxCollider2D（絵の大きさに合う）。
///   置いた位置が、足場の上のふちの真ん中。Size の横幅を変えると、左右の端の線は伸びずに、足場が長くなる。
///   絵の設定（Sprite・Full Rect・左右の枠40）は、このメニューが自動で直す。
/// すでにプレハブがある時は、上書き（直した設定が消える）か、別の名前で作るかを選べる。
/// </summary>
public static class RiverBuilder
{
    private const string PrefabFolder = "Assets/Prefab/Object";
    private const string PrefabPath = "Assets/Prefab/Object/River.prefab";
    private const string PlatformPrefabPath = "Assets/Prefab/Object/RiverPlatform.prefab";
    private const string PlatformSpritePath = "Assets/Art/Sketch/sketch_platform_short.png";

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/川のプレハブを作る")]
    public static void Build()
    {
        string path = PrefabPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "川のプレハブ",
                "すでに " + PrefabPath + " があります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
                path = AssetDatabase.GenerateUniqueAssetPath(PrefabPath);
        }

        EnsureFolder("Assets", "Prefab");
        EnsureFolder("Assets/Prefab", "Object");

        GameObject go = new GameObject("River");

        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0)
            go.layer = ground;

        // RiverCurrent を付けると、BoxCollider2D も一緒に付く
        RiverCurrent river = go.AddComponent<RiverCurrent>();
        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null)
            box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(12f, 3f);
        box.offset = new Vector2(0f, -1.5f); // 上のふちが、置いた位置（水面の高さ）

        river.flowDirection = Vector2.right;
        river.flowSpeed = 4f;
        river.killAtDownstreamEnd = false; // 川の端では、やられない（酸素がなくなると、やられる）
        river.oxygenRefillCellsPerSecond = 1f; // 水から出たら、1秒に1マスずつ、ゆっくり回復する
        river.createRiverBed = true;
        river.showTempVisuals = true;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        string layerNote = ground >= 0 ? "Layer は Ground にしました。" : "Layer は Default のままです。自分で、地面と同じ Layer にしてください。";
        Debug.Log("[RiverBuilder] " + path + " を作りました。" + layerNote + " シーンに置いて、BoxCollider2D の Size で、川の大きさを変えられます。");
    }

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/川の足場のプレハブを作る")]
    public static void BuildPlatform()
    {
        if (!System.IO.File.Exists(PlatformSpritePath))
        {
            Debug.LogError("[RiverBuilder] " + PlatformSpritePath + " がありません。");
            return;
        }

        string path = PlatformPrefabPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPrefabPath) != null)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "川の足場のプレハブ",
                "すでに " + PlatformPrefabPath + " があります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
                path = AssetDatabase.GenerateUniqueAssetPath(PlatformPrefabPath);
        }

        EnsureFolder("Assets", "Prefab");
        EnsureFolder("Assets/Prefab", "Object");

        ConfigurePlatformSprite();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlatformSpritePath);
        if (sprite == null)
        {
            Debug.LogError("[RiverBuilder] " + PlatformSpritePath + " を、Sprite として読み込めませんでした。");
            return;
        }

        GameObject go = new GameObject("RiverPlatform");
        int ground = LayerMask.NameToLayer("Ground");
        if (ground >= 0)
            go.layer = ground;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(3f, sprite.bounds.size.y); // 幅3。高さは絵のまま

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.autoTiling = true; // 絵の大きさ（Size）に、当たり判定が合う
        box.size = sr.size;
        box.offset = new Vector2(0f, -sr.size.y * 0.5f); // 絵の上のふちが、置いた位置に来る

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        string layerNote = ground >= 0 ? "Layer は Ground にしました。" : "Layer は Default のままです。自分で、地面と同じ Layer にしてください。";
        Debug.Log("[RiverBuilder] " + path + " を作りました。" + layerNote + " SpriteRenderer の Size の横幅で、足場の長さを変えられます。");
    }

    /// <summary>足場の絵を、Sliced で伸ばせる設定にする（Sprite・Full Rect・左右の枠40・上のふちが基準）。</summary>
    private static void ConfigurePlatformSprite()
    {
        TextureImporter ti = AssetImporter.GetAtPath(PlatformSpritePath) as TextureImporter;
        if (ti == null)
            return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.spriteBorder = new Vector4(40f, 0f, 40f, 0f); // 左・下・右・上

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.TopCenter;
        ti.SetTextureSettings(s);

        ti.SaveAndReimport();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
