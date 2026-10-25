using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 洞窟のプレハブを作る。メニューは2つ。
///
/// 1) Tools > KEY ESCAPE > ステージ部品 > 洞窟（岩山）のプレハブを作る
///    小・中・大の、3つのプレハブを作る（Assets/Prefab/Object/CaveRockS.prefab、CaveRockM.prefab、CaveRockL.prefab）。
///      CaveRockX … BoxCollider2D（Is Trigger。洞窟の中の範囲。最初は入口の大きさ）・CaveCover
///        └ Cover … SpriteRenderer（岩山の絵。足もとが、置いた位置に来る）
///    絵は、Assets/Art/Sketch/sketch_cave_rock_s.png / _m.png / _l.png。
///
/// 2) Tools > KEY ESCAPE > ステージ部品 > 洞窟の四角いおおいのプレハブを作る
///    ひび割れの岩の四角いおおい（Assets/Prefab/Object/CaveCover.prefab）。BoxCollider2D の大きさに、絵が合う。
///      ふち（sketch_cave_cover.png。四隅の枠は、そのまま）の上に、ひび割れ（sketch_cave_cracks.png。くり返し並ぶ）が重なる。
///
/// 絵の PNG は、Sprite（2D and UI）・Pixels Per Unit 100 に、自動で設定する。
/// すでにプレハブがある時は、上書きするか、別の名前で作るかを選べる。
/// </summary>
public static class CaveBuilder
{
    private const string PrefabFolder = "Assets/Prefab/Object";
    private const string PlainPrefabPath = "Assets/Prefab/Object/CaveCover.prefab";
    private const string PlainSpritePath = "Assets/Art/Sketch/sketch_cave_cover.png";
    private const string CracksSpritePath = "Assets/Art/Sketch/sketch_cave_cracks.png";
    private const int CoverOrder = 1000;

    // 岩山の絵の大きさ（ピクセル）と、入口の大きさ。絵（cave_rock.py）と同じ数にしておく
    private const float Ppu = 100f;
    private const float BaseMargin = 6f; // 絵の下はしから、底の線まで（ピクセル）

    private struct Rock
    {
        public string suffix, sprite;
        public int w, h, mouthW, mouthH;
        public Rock(string suffix, int w, int h, int mouthW, int mouthH)
        {
            this.suffix = suffix; this.w = w; this.h = h; this.mouthW = mouthW; this.mouthH = mouthH;
            sprite = "Assets/Art/Sketch/sketch_cave_rock_" + suffix.ToLower() + ".png";
        }
    }

    private static readonly Rock[] Rocks =
    {
        new Rock("S", 640, 480, 190, 270),
        new Rock("M", 900, 640, 260, 350),
        new Rock("L", 1200, 840, 340, 460),
    };

    // ---------- 岩山（小・中・大） ----------

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/洞窟（岩山）のプレハブを作る")]
    public static void BuildRocks()
    {
        for (int i = 0; i < Rocks.Length; i++)
        {
            if (!File.Exists(Rocks[i].sprite))
            {
                Debug.LogError("[CaveBuilder] " + Rocks[i].sprite + " がありません。絵のファイルを、この場所に置いてください。");
                return;
            }
        }

        bool exists = false;
        for (int i = 0; i < Rocks.Length; i++)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabPath(Rocks[i])) != null)
                exists = true;

        bool separate = false;
        if (exists)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "洞窟（岩山）のプレハブ",
                "すでに、岩山のプレハブがあります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return;
            separate = choice == 1;
        }

        EnsureFolder("Assets", "Prefab");
        EnsureFolder("Assets/Prefab", "Object");

        GameObject last = null;
        for (int i = 0; i < Rocks.Length; i++)
        {
            Rock r = Rocks[i];
            Sprite sprite = SetupSprite(r.sprite, false);
            if (sprite == null)
            {
                Debug.LogError("[CaveBuilder] 絵を読みこめませんでした: " + r.sprite);
                continue;
            }

            string path = RockPrefabPath(r);
            if (separate)
                path = AssetDatabase.GenerateUniqueAssetPath(path);

            GameObject root = new GameObject("CaveRock" + r.suffix);

            // 洞窟の中の範囲：最初は入口の大きさ。中を広く作ったら、Size と Offset を広げる
            BoxCollider2D box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(r.mouthW / Ppu, r.mouthH / Ppu);
            box.offset = new Vector2(0f, r.mouthH / Ppu * 0.5f);

            GameObject child = new GameObject("Cover");
            child.transform.SetParent(root.transform, false);
            SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = CoverOrder;
            // 絵の底の線が、置いた位置（地面）に来るようにする
            child.transform.localPosition = new Vector3(0f, (r.h * 0.5f - BaseMargin) / Ppu, 0f);

            CaveCover cc = root.AddComponent<CaveCover>();
            cc.cover = sr;
            cc.fitToZone = false;

            last = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (last != null)
        {
            Selection.activeObject = last;
            EditorGUIUtility.PingObject(last);
        }

        Debug.Log("[CaveBuilder] 岩山の洞窟（小・中・大）を作りました（" + PrefabFolder + "）。地面に置いて、BoxCollider2D の枠を、洞窟の中の広さに合わせてください。");
    }

    private static string RockPrefabPath(Rock r)
    {
        return PrefabFolder + "/CaveRock" + r.suffix + ".prefab";
    }

    // ---------- 四角いおおい ----------

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/洞窟の四角いおおいのプレハブを作る")]
    public static void BuildPlain()
    {
        if (!File.Exists(PlainSpritePath) || !File.Exists(CracksSpritePath))
        {
            Debug.LogError("[CaveBuilder] " + PlainSpritePath + " か、" + CracksSpritePath + " がありません。絵のファイルを、この場所に置いてください。");
            return;
        }

        string path = PlainPrefabPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlainPrefabPath) != null)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "洞窟の四角いおおいのプレハブ",
                "すでに " + PlainPrefabPath + " があります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
                path = AssetDatabase.GenerateUniqueAssetPath(PlainPrefabPath);
        }

        Sprite sprite = SetupSprite(PlainSpritePath, true);
        Sprite crackSprite = SetupSprite(CracksSpritePath, false);
        if (sprite == null || crackSprite == null)
        {
            Debug.LogError("[CaveBuilder] 絵を読みこめませんでした: " + PlainSpritePath + " / " + CracksSpritePath);
            return;
        }

        EnsureFolder("Assets", "Prefab");
        EnsureFolder("Assets/Prefab", "Object");

        GameObject root = new GameObject("CaveCover");
        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(12f, 6f);
        box.offset = new Vector2(0f, 3f); // 足元が、置いた位置に来る

        GameObject child = new GameObject("Cover");
        child.transform.SetParent(root.transform, false);
        SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = box.size;
        child.transform.localPosition = new Vector3(box.offset.x, box.offset.y, 0f);
        sr.sortingOrder = CoverOrder;

        GameObject crackObj = new GameObject("Cracks");
        crackObj.transform.SetParent(child.transform, false);
        SpriteRenderer crackSr = crackObj.AddComponent<SpriteRenderer>();
        crackSr.sprite = crackSprite;
        crackSr.drawMode = SpriteDrawMode.Tiled;
        crackSr.tileMode = SpriteTileMode.Continuous;
        crackSr.size = new Vector2(box.size.x - 0.2f, box.size.y - 0.2f);
        crackSr.sortingOrder = CoverOrder + 1;

        CaveCover cc = root.AddComponent<CaveCover>();
        cc.cover = sr;
        cc.cracks = crackSr;
        cc.fitToZone = true;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        Debug.Log("[CaveBuilder] " + path + " を作りました。シーンに置いて、BoxCollider2D の Size と Offset で、洞窟の中をおおってください。");
    }

    // ---------- 共通 ----------

    private static Sprite SetupSprite(string spritePath, bool sliced)
    {
        TextureImporter ti = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (ti == null)
            return null;

        bool dirty = false;
        if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
        if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (!Mathf.Approximately(ti.spritePixelsPerUnit, Ppu)) { ti.spritePixelsPerUnit = Ppu; dirty = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
        if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; dirty = true; } // 大きい絵が、縮まないように

        Vector4 border = sliced ? new Vector4(48, 48, 48, 48) : Vector4.zero;
        if (ti.spriteBorder != border) { ti.spriteBorder = border; dirty = true; }

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

        return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
