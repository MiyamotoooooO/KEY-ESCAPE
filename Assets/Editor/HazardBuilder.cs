using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// レーザーと、落石のプレハブを作る。メニューは2つ。
///
/// 1) Tools > KEY ESCAPE > ステージ部品 > レーザーのプレハブを作る
///    Assets/Prefab/Object/LaserEmitter.prefab
///      LaserEmitter … LaserEmitter・BoxCollider2D（固い箱。上に乗れる）
///        └ Visual … SpriteRenderer（発射口の絵。レーザーは、右に出る）
///    絵：Assets/Art/Sketch/sketch_laser_emitter.png
///
/// 2) Tools > KEY ESCAPE > ステージ部品 > 落石のプレハブを作る
///    Assets/Prefab/Object/FallingStone.prefab
///      FallingStone … FallingStone・BoxCollider2D（Is Trigger。石の当たり判定）
///        └ Visual … SpriteRenderer（石の絵。ゆれる時は、この絵だけ動く）
///    絵：Assets/Art/Sketch/sketch_falling_stone.png
///
/// 絵の PNG は、Sprite（2D and UI）・Pixels Per Unit 100・Full Rect に、自動で設定する。
/// すでにプレハブがある時は、上書きするか、別の名前で作るかを選べる。
/// </summary>
public static class HazardBuilder
{
    private const string PrefabFolder = "Assets/Prefab/Object";
    private const string LaserSprite = "Assets/Art/Sketch/sketch_laser_emitter.png";
    private const string LaserPrefab = "Assets/Prefab/Object/LaserEmitter.prefab";
    private const string StoneSprite = "Assets/Art/Sketch/sketch_falling_stone.png";
    private const string StonePrefab = "Assets/Prefab/Object/FallingStone.prefab";
    private const float Ppu = 100f;

    // ---------- レーザー ----------

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/レーザーのプレハブを作る")]
    public static void BuildLaser()
    {
        string path = AskPath(LaserPrefab, "レーザーのプレハブ");
        if (path == null)
            return;
        Sprite sprite = SetupSprite(LaserSprite);
        if (sprite == null)
            return;

        GameObject root = new GameObject("LaserEmitter");

        // 本体：固い箱（上に乗れる）。発射口のぶん、右が少しせまい
        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.size = new Vector2(1.2f, 1.28f);
        box.offset = new Vector2(-0.06f, 0f);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 5;

        LaserEmitter laser = root.AddComponent<LaserEmitter>();
        laser.muzzleOffset = 0.6f;

        Save(root, path, "レーザー", "置いたら、Rotation の Z で向きを変えます（レーザーは、右に出ます。下は -90、上は 90、左は 180）。");
    }

    // ---------- 落石 ----------

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/落石のプレハブを作る")]
    public static void BuildStone()
    {
        string path = AskPath(StonePrefab, "落石のプレハブ");
        if (path == null)
            return;
        Sprite sprite = SetupSprite(StoneSprite);
        if (sprite == null)
            return;

        GameObject root = new GameObject("FallingStone");

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1.1f, 1.1f);
        box.offset = Vector2.zero;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 5;

        root.AddComponent<FallingStone>();

        Save(root, path, "落石", "天井の下など、落とす場所に置きます。石の真下の水色の枠が、落ちる範囲です（Detect Width で幅を変えられます）。");
    }

    // ---------- 共通 ----------

    private static string AskPath(string defaultPath, string title)
    {
        string path = defaultPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath) != null)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                title,
                "すでに " + defaultPath + " があります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return null;
            if (choice == 1)
                path = AssetDatabase.GenerateUniqueAssetPath(defaultPath);
        }
        EnsureFolder("Assets", "Prefab");
        EnsureFolder("Assets/Prefab", "Object");
        return path;
    }

    private static void Save(GameObject root, string path, string label, string hint)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
        Debug.Log("[HazardBuilder] " + path + " を作りました（" + label + "）。" + hint);
    }

    private static Sprite SetupSprite(string spritePath)
    {
        if (!File.Exists(spritePath))
        {
            Debug.LogError("[HazardBuilder] " + spritePath + " がありません。絵のファイルを、この場所に置いてください。");
            return null;
        }
        TextureImporter ti = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (ti == null)
            return null;

        bool dirty = false;
        if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
        if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (!Mathf.Approximately(ti.spritePixelsPerUnit, Ppu)) { ti.spritePixelsPerUnit = Ppu; dirty = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        if (s.spriteMeshType != SpriteMeshType.FullRect || s.spriteAlignment != (int)SpriteAlignment.Center)
        {
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);
            dirty = true;
        }
        if (dirty)
            ti.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
            Debug.LogError("[HazardBuilder] 絵を読みこめませんでした: " + spritePath);
        return sprite;
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
