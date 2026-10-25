using UnityEditor;
using UnityEngine;

/// <summary>
/// レバーのプレハブ（Lever.prefab）と、跳ね橋のプレハブ（LeverBridge.prefab）を作る。
///
/// メニュー：Tools > KEY ESCAPE > ステージ部品 > レバーのプレハブを作る
///   絵：Assets/Art/Sketch/sketch_lever.png。置いた位置が、レバーの足もと（絵の下のふちの真ん中）。
///   大きさは、プレハブの Scale（初期値 0.4）で変える。Is Trigger の当たり判定つき。
///   プレイヤーが攻撃すると、オン／オフが切り替わる。「Targets」に、動かしたい物（跳ね橋など）を入れる。
///
/// メニュー：Tools > KEY ESCAPE > ステージ部品 > 跳ね橋のプレハブを作る
///   絵：Assets/Art/Sketch/sketch_bridge.png（左右の端50ずつ。中の100ずつが、くり返される）。
///   置いた位置が、支点（橋が倒れた時の、上の面の高さ。手前のがけのふち）。
///   Length で長さを変える（整数にすると、板がきれいに並ぶ）。Layer は Default（主人公の「地面」の判定が、Default の物を地面とみなすため）。
///
/// 絵の設定（Sprite・Full Rect・橋の左右の枠50）は、このメニューが自動で直す。
/// すでにプレハブがある時は、上書き（直した設定が消える）か、別の名前で作るかを選べる。
/// </summary>
public static class LeverBuilder
{
    private const string PrefabFolder = "Assets/Prefab/Object";
    private const string LeverPrefabPath = "Assets/Prefab/Object/Lever.prefab";
    private const string BridgePrefabPath = "Assets/Prefab/Object/LeverBridge.prefab";
    private const string LeverSpritePath = "Assets/Art/Sketch/sketch_lever.png";
    private const string BridgeSpritePath = "Assets/Art/Sketch/sketch_bridge.png";

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/レバーのプレハブを作る")]
    public static void BuildLever()
    {
        if (!System.IO.File.Exists(LeverSpritePath))
        {
            Debug.LogError("[LeverBuilder] " + LeverSpritePath + " がありません。");
            return;
        }

        string path = ChoosePath(LeverPrefabPath, "レバーのプレハブ");
        if (path == null)
            return;

        ConfigureSprite(LeverSpritePath, Vector4.zero, false);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LeverSpritePath);
        if (sprite == null)
        {
            Debug.LogError("[LeverBuilder] " + LeverSpritePath + " を、Sprite として読み込めませんでした。");
            return;
        }

        EnsureFolder("Assets", "Prefab");
        EnsureFolder("Assets/Prefab", "Object");

        Vector2 size = sprite.bounds.size;

        GameObject go = new GameObject("Lever");
        go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

        // 絵は、子に置く（足もとが、置いた位置に来るように、絵の高さの半分だけ、上げる）
        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(go.transform, false);
        vis.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        SpriteRenderer sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        box.offset = new Vector2(0f, size.y * 0.5f);

        Lever lever = go.AddComponent<Lever>();
        lever.visual = sr;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
        Debug.Log("[LeverBuilder] " + path + " を作りました。シーンに置いて、Lever の「Targets」に、動かしたい物（跳ね橋など）を入れてください。");
    }

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/跳ね橋のプレハブを作る")]
    public static void BuildBridge()
    {
        if (!System.IO.File.Exists(BridgeSpritePath))
        {
            Debug.LogError("[LeverBuilder] " + BridgeSpritePath + " がありません。");
            return;
        }

        string path = ChoosePath(BridgePrefabPath, "跳ね橋のプレハブ");
        if (path == null)
            return;

        ConfigureSprite(BridgeSpritePath, new Vector4(50f, 0f, 50f, 0f), true);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BridgeSpritePath);
        if (sprite == null)
        {
            Debug.LogError("[LeverBuilder] " + BridgeSpritePath + " を、Sprite として読み込めませんでした。");
            return;
        }

        EnsureFolder("Assets", "Prefab");
        EnsureFolder("Assets/Prefab", "Object");

        // Layer は Default のままにする（主人公の「地面」の判定は、Default の物を地面とみなす。Ground レイヤーは含まない）
        GameObject go = new GameObject("LeverBridge");

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        GameObject deck = new GameObject("Deck");
        deck.transform.SetParent(go.transform, false);
        SpriteRenderer sr = deck.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        BoxCollider2D box = deck.AddComponent<BoxCollider2D>();

        LeverBridge bridge = go.AddComponent<LeverBridge>();
        bridge.deck = deck.transform;
        bridge.deckRenderer = sr;
        bridge.deckCollider = box;
        bridge.length = 6f;
        bridge.extendRight = true;
        bridge.ApplyShape();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        string layerNote = "Layer は Default にしました（主人公が、橋の上で地面に立っていると分かるように）。";
        Debug.Log("[LeverBuilder] " + path + " を作りました。" + layerNote + " 手前のがけのふち（橋の上の面の高さ）に置き、Length で長さを決めて、レバーの「Targets」に入れてください。ゲームを始めると、橋は立ち上がった状態になります。");
    }

    private static string ChoosePath(string path, string title)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            return path;

        int choice = EditorUtility.DisplayDialogComplex(
            title,
            "すでに " + path + " があります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
            "上書きする", "別の名前で作る", "やめる");
        if (choice == 2)
            return null;
        if (choice == 1)
            return AssetDatabase.GenerateUniqueAssetPath(path);
        return path;
    }

    /// <summary>絵を、Sprite として使える設定にする。橋は、左右の枠を決めて、中をくり返せるようにする。</summary>
    private static void ConfigureSprite(string assetPath, Vector4 border, bool fullRect)
    {
        TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null)
            return;

        bool changed = false;
        if (ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            changed = true;
        }
        if (ti.spriteImportMode != SpriteImportMode.Single)
        {
            ti.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
        if (fullRect)
        {
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.spriteBorder = border; // 左・下・右・上

            TextureImporterSettings s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);
            changed = true;
        }
        if (changed)
            ti.SaveAndReimport();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
