using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ステージクリア後の結果画面のプレハブ（Assets/Prefab/StageResultPanel.prefab）を、初期の見た目で作る。
///
/// 配置は、イメージ図（左上に STAGE CLEAR!、中央にクリアタイム、右上に死亡シーン、下にキー、最下段にボタン）に合わせてある。
/// 作ったあとは、Unity でプレハブを開いて、絵・フォント・色・位置・大きさを自由に直してよい。
///   ・Paper … 紙の背景。Image の Source Image に絵を入れると、背景が変わる。
///   ・各パネル（TimePanel / DeathPanel / KeysPanel）の Image の Source Image に枠の絵を入れると、手書き風になる。
///   ・SignAnchor … STAGE CLEAR! の看板が移ってくる位置と、大きさ（幅）と、傾き。
///   ・PlayerArt … 左側のプレイヤーのイラスト。Source Image に絵を入れると出る。
///   ・Crown … 王冠。Source Image に絵を入れると、記録更新の時に出る。
///   ・一言コメントのリストは、ルートの StageResultPanel の Inspector で直す。
///
/// メニュー：Tools > KEY ESCAPE > リザルト > 結果画面のプレハブを作る
/// すでにプレハブがある時は、上書き（見た目の調整が消える）か、別の名前で作るかを選べる。
/// 作ったら、シーンの StageClearDirector の Result Panel Prefab に、このプレハブを入れる。
/// </summary>
public static class StageResultBuilder
{
    private const string PrefabFolder = "Assets/Prefab";
    private const string PrefabPath = "Assets/Prefab/StageResultPanel.prefab";
    private const string FontPath = "Assets/Font/ドットフォント.asset"; // 設定画面などと同じフォント

    private static readonly Color Ink = new Color(0.10f, 0.10f, 0.10f, 1f);
    private static readonly Color PaperColor = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color PanelFill = new Color(0.96f, 0.96f, 0.95f, 1f);
    private static readonly Color TabFill = new Color(0.98f, 0.98f, 0.97f, 1f);
    private static readonly Color Red = new Color(0.93f, 0.30f, 0.38f, 1f);
    private static readonly Color KeyBack = new Color(0.88f, 0.88f, 0.87f, 1f);

    [MenuItem("Tools/KEY ESCAPE/リザルト/結果画面のプレハブを作る")]
    public static void Build()
    {
        string path = PrefabPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "結果画面のプレハブ",
                "すでに " + PrefabPath + " があります。\n上書きすると、Unity で直した見た目は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
                path = AssetDatabase.GenerateUniqueAssetPath(PrefabPath);
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefab");

        TMP_FontAsset font = FindFont();
        GameObject root = BuildHierarchy(font);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        Debug.Log("[StageResultBuilder] " + path + " を作りました（フォント：" + (font != null ? font.name : "なし") + "）。"
                  + "シーンの StageClearDirector の Result Panel Prefab に入れてください。");
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
            if (p.Contains("Liberation"))
                continue;
            f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            if (f != null)
                return f;
        }
        return null;
    }

    // ================= 組み立て =================

    private static GameObject BuildHierarchy(TMP_FontAsset font)
    {
        GameObject rootObj = new GameObject("StageResultPanel", typeof(RectTransform));
        RectTransform root = (RectTransform)rootObj.transform;
        Stretch(root);
        StageResultPanel panel = rootObj.AddComponent<StageResultPanel>();
        panel.font = font;

        // 紙の背景
        RectTransform paperRt = NewRect("Paper", root);
        Stretch(paperRt);
        Image paperImg = paperRt.gameObject.AddComponent<Image>();
        paperImg.color = PaperColor;
        panel.paper = paperImg;

        // 看板の移り先（絵は無い。位置・幅・傾きだけを決める）
        RectTransform signAnchor = NewRect("SignAnchor", root);
        Place(signAnchor, new Vector2(-422f, 356f), new Vector2(830f, 263f));
        signAnchor.localRotation = Quaternion.Euler(0f, 0f, 4f);
        panel.signAnchor = signAnchor;

        // プレイヤーのイラスト（絵が入るまで出ない）
        RectTransform artRt = NewRect("PlayerArt", root);
        Place(artRt, new Vector2(-752f, 33f), new Vector2(300f, 360f));
        Image art = artRt.gameObject.AddComponent<Image>();
        art.preserveAspect = true;
        art.raycastTarget = false;
        panel.playerArt = art;

        BuildTimePanel(root, panel, font);
        BuildDeathPanel(root, panel, font);
        BuildKeysPanel(root, panel, font);
        BuildButton(root, panel, font);

        return rootObj;
    }

    private static void BuildTimePanel(RectTransform root, StageResultPanel panel, TMP_FontAsset font)
    {
        RectTransform box = Box("TimePanel", root, new Vector2(-286f, 43f), new Vector2(506f, 222f), PanelFill);
        panel.timePanel = box;

        Tab(box, "クリアタイム", new Vector2(0f, 100f), new Vector2(250f, 60f), font);

        TextMeshProUGUI time = Text("TimeText", box, "01:23.45", 96f, TextAlignmentOptions.Center,
                                    new Vector2(0f, -5f), new Vector2(470f, 110f), font, Ink);
        panel.timeText = time;

        TextMeshProUGUI best = Text("BestTimeText", box, "ベスト 01:20.00", 28f, TextAlignmentOptions.Center,
                                    new Vector2(-90f, -66f), new Vector2(250f, 36f), font, Ink);
        panel.bestTimeText = best;

        RectTransform badge = Box("NewRecord", box, new Vector2(121f, -66f), new Vector2(190f, 46f), Red);
        badge.localRotation = Quaternion.Euler(0f, 0f, 4f);
        Text("Label", badge, "NEW RECORD!", 26f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(190f, 46f), font, Color.white);
        panel.newRecordBadge = badge.gameObject;

        RectTransform crownRt = NewRect("Crown", box);
        Place(crownRt, new Vector2(-41f, 170f), new Vector2(100f, 80f));
        Image crown = crownRt.gameObject.AddComponent<Image>();
        crown.preserveAspect = true;
        crown.raycastTarget = false;
        panel.crown = crown;
    }

    private static void BuildDeathPanel(RectTransform root, StageResultPanel panel, TMP_FontAsset font)
    {
        RectTransform box = Box("DeathPanel", root, new Vector2(469f, 134f), new Vector2(919f, 409f), PanelFill);
        panel.deathPanel = box;

        Tab(box, "今回の死亡シーン", new Vector2(-13f, 204f), new Vector2(395f, 70f), font);

        panel.deathCountText = Text("DeathCountText", box, "死亡 3 回", 30f, TextAlignmentOptions.Right,
                                    new Vector2(310f, 160f), new Vector2(260f, 40f), font, Ink);

        // 写真の並び（使わない枠は、非表示になる。残った枠は、中央にそろう）
        RectTransform row = NewRect("PhotoRow", box);
        Place(row, new Vector2(0f, -21f), new Vector2(860f, 309f));
        HorizontalLayoutGroup hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 22f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var slots = new StageResultPanel.PhotoSlot[3];
        for (int i = 0; i < slots.Length; i++)
        {
            RectTransform slotRt = Box("PhotoSlot" + (i + 1), row, Vector2.zero, new Vector2(261f, 309f), TabFill);

            RectTransform photoRt = NewRect("Photo", slotRt);
            photoRt.anchorMin = new Vector2(0f, 1f);
            photoRt.anchorMax = new Vector2(1f, 1f);
            photoRt.pivot = new Vector2(0.5f, 1f);
            photoRt.sizeDelta = new Vector2(-12f, 222f);
            photoRt.anchoredPosition = new Vector2(0f, -6f);
            RawImage raw = photoRt.gameObject.AddComponent<RawImage>();
            raw.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            raw.raycastTarget = false;

            RectTransform commentRt = NewRect("Comment", slotRt);
            commentRt.anchorMin = new Vector2(0f, 0f);
            commentRt.anchorMax = new Vector2(1f, 0f);
            commentRt.pivot = new Vector2(0.5f, 0f);
            commentRt.sizeDelta = new Vector2(-16f, 74f);
            commentRt.anchoredPosition = new Vector2(0f, 4f);
            TextMeshProUGUI comment = commentRt.gameObject.AddComponent<TextMeshProUGUI>();
            Setup(comment, "ここで死ぬ？？？", 28f, TextAlignmentOptions.Center, font, Ink);
            comment.enableWordWrapping = true;
            comment.enableAutoSizing = true;
            comment.fontSizeMin = 16f;
            comment.fontSizeMax = 28f;

            slots[i] = new StageResultPanel.PhotoSlot { root = slotRt.gameObject, photo = raw, comment = comment };
        }
        panel.photoSlots = slots;

        TextMeshProUGUI noDeath = Text("NoDeath", box, "NO DEATH!", 96f, TextAlignmentOptions.Center,
                                       new Vector2(0f, -20f), new Vector2(760f, 140f), font, Ink);
        panel.noDeathObject = noDeath.gameObject;
    }

    private static void BuildKeysPanel(RectTransform root, StageResultPanel panel, TMP_FontAsset font)
    {
        RectTransform box = Box("KeysPanel", root, new Vector2(9f, -254f), new Vector2(1488f, 211f), PanelFill);
        panel.keysPanel = box;

        Tab(box, "回収したキー", new Vector2(-511f, 113f), new Vector2(330f, 64f), font);

        RectTransform container = NewRect("KeysContainer", box);
        Place(container, new Vector2(0f, -14f), new Vector2(1420f, 150f));
        panel.keysContainer = container;

        // キー1つ分の見本（実行中は、これを複製して並べる）
        RectTransform item = NewRect("KeyItemTemplate", container);
        Place(item, Vector2.zero, new Vector2(124f, 140f));
        Image back = item.gameObject.AddComponent<Image>();
        back.color = KeyBack;
        back.raycastTarget = false;

        RectTransform iconRt = NewRect("Icon", item);
        Place(iconRt, new Vector2(0f, 22f), new Vector2(92f, 76f));
        Image icon = iconRt.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI label = Text("Label", item, "W", 44f, TextAlignmentOptions.Center,
                                     new Vector2(0f, 22f), new Vector2(100f, 70f), font, Ink);

        var stars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform starRt = NewRect("Star" + (i + 1), item);
            Place(starRt, new Vector2((i - 1) * 30f, -48f), new Vector2(28f, 28f));
            Image star = starRt.gameObject.AddComponent<Image>();
            star.color = new Color(1f, 0.8f, 0.2f, 1f);
            star.preserveAspect = true;
            star.raycastTarget = false;
            stars[i] = star;
        }

        StageResultKeyItem keyItem = item.gameObject.AddComponent<StageResultKeyItem>();
        keyItem.icon = icon;
        keyItem.label = label;
        keyItem.stars = stars;
        panel.keyItemTemplate = keyItem;
    }

    private static void BuildButton(RectTransform root, StageResultPanel panel, TMP_FontAsset font)
    {
        RectTransform rt = Box("ReturnButton", root, new Vector2(0f, -448f), new Vector2(460f, 92f), PanelFill);
        Image img = rt.GetComponent<Image>();
        img.raycastTarget = true;

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.72f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.85f);
        btn.colors = colors;

        Text("Label", rt, "拠点に戻る", 44f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(440f, 80f), font, Ink);

        rt.gameObject.AddComponent<UIButtonSound>(); // 他のボタンと同じ、乗せた音・クリックの音
        panel.returnButton = btn;
    }

    // ================= 部品 =================

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>画面の中央を基準にした位置と大きさ。</summary>
    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>黒い縁つきの四角。</summary>
    private static RectTransform Box(string name, Transform parent, Vector2 pos, Vector2 size, Color fill)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = fill;
        img.raycastTarget = false;
        Outline outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = Ink;
        outline.effectDistance = new Vector2(3f, -3f);
        return rt;
    }

    /// <summary>パネルの上の縁にまたがる、見出しの札。</summary>
    private static void Tab(RectTransform parent, string label, Vector2 pos, Vector2 size, TMP_FontAsset font)
    {
        RectTransform tab = Box("Tab", parent, pos, size, TabFill);
        Text("Label", tab, label, 36f, TextAlignmentOptions.Center, Vector2.zero, size, font, Ink);
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string text, float fontSize,
                                        TextAlignmentOptions align, Vector2 pos, Vector2 size, TMP_FontAsset font, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Place(rt, pos, size);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        Setup(tmp, text, fontSize, align, font, color);
        return tmp;
    }

    private static void Setup(TextMeshProUGUI tmp, string text, float fontSize, TextAlignmentOptions align, TMP_FontAsset font, Color color)
    {
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
    }
}
