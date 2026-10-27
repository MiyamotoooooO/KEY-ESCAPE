using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 壊せる石のプレハブを作る。
///
/// メニュー：Tools > KEY ESCAPE > ステージ部品 > 壊せる石のプレハブを作る
///   Assets/Prefab/Object/BreakableStone.prefab
///     BreakableStone … BreakableStone・BoxCollider2D（固い箱）
///       └ Visual … 石の絵
///           ├ Crack1 … 1回目のあとに出るひび
///           └ Crack2 … 2回目のあとに出るひび
///
/// 絵：Assets/Art/Sketch/sketch_breakable_stone.png・_crack1.png・_crack2.png
///     （Sprite・Pixels Per Unit 100・Full Rect に、自動で設定する）
///
/// すでにプレハブがある時は、上書きするか、別の名前で作るかを選べる。
/// </summary>
public static class BreakableStoneBuilder
{
    private const string PrefabPath = "Assets/Prefab/Object/BreakableStone.prefab";
    private const string BodySprite = "Assets/Art/Sketch/sketch_breakable_stone.png";
    private const string Crack1Sprite = "Assets/Art/Sketch/sketch_breakable_stone_crack1.png";
    private const string Crack2Sprite = "Assets/Art/Sketch/sketch_breakable_stone_crack2.png";
    private const float Ppu = 100f;

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/壊せる石のプレハブを作る")]
    public static void Build()
    {
        string path = AskPath(PrefabPath, "壊せる石のプレハブ");
        if (path == null)
            return;

        Sprite body = SetupSprite(BodySprite);
        Sprite c1 = SetupSprite(Crack1Sprite);
        Sprite c2 = SetupSprite(Crack2Sprite);
        if (body == null || c1 == null || c2 == null)
            return;

        GameObject root = new GameObject("BreakableStone");

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = false;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer vr = visual.AddComponent<SpriteRenderer>();
        vr.sprite = body;
        vr.sortingOrder = 5;

        SpriteRenderer cr1 = MakeCrack("Crack1", visual.transform, c1);
        SpriteRenderer cr2 = MakeCrack("Crack2", visual.transform, c2);

        BreakableStone stone = root.AddComponent<BreakableStone>();
        stone.hitsToBreak = 3;
        stone.size = new Vector2(1.6f, 2.2f);
        stone.visual = vr;
        stone.crackLayers = new SpriteRenderer[] { cr1, cr2 };

        // 大きさに合わせて、絵と当たり判定を整える
        box.size = stone.size;
        box.offset = new Vector2(0f, stone.size.y * 0.5f);
        Vector2 spriteSize = body.bounds.size;
        visual.transform.localPosition = new Vector3(0f, stone.size.y * 0.5f, 0f);
        visual.transform.localScale = new Vector3(stone.size.x / spriteSize.x, stone.size.y / spriteSize.y, 1f);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
        Debug.Log("[BreakableStoneBuilder] " + path + " を作りました。道をふさぐ場所に置き、Size で大きさを、Hits To Break で壊れるまでの回数を決めます。置く位置は、石の足もと（下の真ん中）です。");
    }

    private static SpriteRenderer MakeCrack(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 6;
        sr.enabled = false;
        return sr;
    }

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

    private static Sprite SetupSprite(string spritePath)
    {
        if (!File.Exists(spritePath))
        {
            Debug.LogError("[BreakableStoneBuilder] " + spritePath + " がありません。絵のファイルを、この場所に置いてください。");
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
            Debug.LogError("[BreakableStoneBuilder] 絵を読みこめませんでした: " + spritePath);
        return sprite;
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
