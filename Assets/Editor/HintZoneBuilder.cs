using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ヒントゾーンのプレハブ（Assets/Prefab/Object/HintZone.prefab）を作る。
///
/// 組み上がる形：
///   HintZone … BoxCollider2D（Is Trigger。プレイヤーが入ると反応する範囲）・ActionHintZone
///     Hint（最初は非表示）… 吹き出しの背景（SpriteRenderer）と、文字（TextMeshPro）
///       Bubble … 背景
///       Text   … メッセージの文字（ActionHintZone の Message が入る）
///
/// 使い方：ステージのシーンにドラッグして置き、範囲（BoxCollider2D の Size）を広げ、
/// Required Action（どの操作のヒントか）と Message（表示する文）を変える。
/// ヒントは「その操作が解放済みなのに、キーがまだ割り当てられていない」間、範囲の中にいるときだけ出る。
/// 吹き出しの位置は、子の Hint を動かして調整する。
///
/// メニュー：Tools > KEY ESCAPE > ステージ部品 > ヒントゾーンのプレハブを作る
/// すでにプレハブがある時は、上書き（直した設定が消える）か、別の名前で作るかを選べる。
/// </summary>
public static class HintZoneBuilder
{
    private const string PrefabFolder = "Assets/Prefab/Object";
    private const string PrefabPath = "Assets/Prefab/Object/HintZone.prefab";
    private const string FontPath = "Assets/Font/ドットフォント.asset";

    private const int BubbleOrder = 300; // 背景。敵や背景より手前に出す
    private const int TextOrder = 301;   // 文字は背景より手前

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/ヒントゾーンのプレハブを作る")]
    public static void Build()
    {
        string path = PrefabPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "ヒントゾーンのプレハブ",
                "すでに " + PrefabPath + " があります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
                path = AssetDatabase.GenerateUniqueAssetPath(PrefabPath);
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            AssetDatabase.CreateFolder("Assets", "Prefab");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefab", "Object");

        TMP_FontAsset font = FindFont();

        // ---- 本体：範囲 ----
        GameObject root = new GameObject("HintZone");

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(5f, 4f);
        box.offset = new Vector2(0f, 2f); // 足元が、置いた位置に来る

        // ---- ヒントの表示（最初は非表示）----
        GameObject hint = new GameObject("Hint");
        hint.transform.SetParent(root.transform, false);
        hint.transform.localPosition = new Vector3(0f, 3.4f, 0f);

        GameObject bubble = new GameObject("Bubble");
        bubble.transform.SetParent(hint.transform, false);
        SpriteRenderer sr = bubble.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(9f, 1.3f);
        sr.color = new Color(0.08f, 0.08f, 0.1f, 0.88f);
        sr.sortingOrder = BubbleOrder;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(hint.transform, false);
        TextMeshPro text = textObj.AddComponent<TextMeshPro>();
        if (font != null)
            text.font = font;
        text.text = "ジャンプのキーを割り当てよう（Tabキー）";
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 2f;
        text.fontSizeMax = 4f;
        text.fontSize = 4f;
        text.enableWordWrapping = true;
        text.color = Color.white;
        text.sortingOrder = TextOrder;
        RectTransform rt = text.rectTransform;
        rt.sizeDelta = new Vector2(8.4f, 1.1f);

        hint.SetActive(false);

        // ---- ActionHintZone ----
        ActionHintZone zone = root.AddComponent<ActionHintZone>();
        zone.requiredAction = GameAction.Jump;
        zone.hintObject = hint;
        zone.hintText = text;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        Debug.Log("[HintZoneBuilder] " + path + " を作りました（フォント：" + (font != null ? font.name : "なし")
                  + "）。ステージに置いて、Required Action と Message を変えてください。");
    }

    private static TMP_FontAsset FindFont()
    {
        TMP_FontAsset f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (f != null)
            return f;
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            if (f != null && f.name.Contains("ドット"))
                return f;
        }
        return null;
    }
}
