using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ギミック紹介のプレハブを2つ作る。
///   Assets/Prefab/GimmickIntroUI.prefab … 画面の下の説明パネルと、点滅するSpaceキーの案内
///   Assets/Prefab/Object/GimmickIntroZone.prefab … ステージに置く範囲（GimmickIntroZone。Ui Prefab は自動で入る）
///
/// 使い方：GimmickIntroZone をステージに置き、範囲（BoxCollider2D の Size）を広げて、
/// Steps に手順（カメラが映す場所・説明・ズーム）を入れる。
/// Spaceキーの絵は、プレハブ GimmickIntroUI の SpaceKey（Image）の Sprite に、自分の絵を入れて差し替えられる。
///
/// メニュー：Tools > KEY ESCAPE > ステージ部品 > ギミック紹介のプレハブを作る
/// すでにプレハブがある時は、上書き（直した設定が消える）か、別の名前で作るかを選べる。
/// </summary>
public static class GimmickIntroBuilder
{
    private const string UiPath = "Assets/Prefab/GimmickIntroUI.prefab";
    private const string ZonePath = "Assets/Prefab/Object/GimmickIntroZone.prefab";
    private const string FontPath = "Assets/Font/ドットフォント.asset";

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/ギミック紹介のプレハブを作る")]
    public static void Build()
    {
        string uiPath = UiPath;
        string zonePath = ZonePath;
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(UiPath) != null
                      || AssetDatabase.LoadAssetAtPath<GameObject>(ZonePath) != null;
        if (exists)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "ギミック紹介のプレハブ",
                "すでに GimmickIntroUI / GimmickIntroZone のプレハブがあります。\n上書きすると、Unity で直した設定は、初期の状態に戻ります。",
                "上書きする", "別の名前で作る", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
            {
                uiPath = AssetDatabase.GenerateUniqueAssetPath(UiPath);
                zonePath = AssetDatabase.GenerateUniqueAssetPath(ZonePath);
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            AssetDatabase.CreateFolder("Assets", "Prefab");
        if (!AssetDatabase.IsValidFolder("Assets/Prefab/Object"))
            AssetDatabase.CreateFolder("Assets/Prefab", "Object");

        TMP_FontAsset font = FindFont();

        GameObject uiPrefab = BuildUi(uiPath, font);
        GameObject zonePrefab = BuildZone(zonePath, uiPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = zonePrefab;
        EditorGUIUtility.PingObject(zonePrefab);
        Debug.Log("[GimmickIntroBuilder] " + uiPath + " と " + zonePath + " を作りました（フォント："
                  + (font != null ? font.name : "なし") + "）。GimmickIntroZone をステージに置いて、Steps を設定してください。");
    }

    // ---- 説明パネル ----------------------------------------------------

    private static GameObject BuildUi(string path, TMP_FontAsset font)
    {
        GameObject root = new GameObject("GimmickIntroUI", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Sprite box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // パネル（画面の下の帯）
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 60f);
        prt.sizeDelta = new Vector2(1500f, 230f);
        Image bg = panel.AddComponent<Image>();
        bg.sprite = box;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);
        bg.raycastTarget = false;
        CanvasGroup group = panel.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // 説明の文字
        GameObject textObj = new GameObject("Message", typeof(RectTransform));
        textObj.transform.SetParent(panel.transform, false);
        RectTransform trt = textObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(60f, 50f);
        trt.offsetMax = new Vector2(-60f, -30f);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = "ここに説明が出ます";
        text.fontSize = 44f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        // Spaceキーの案内（右下）
        GameObject key = new GameObject("SpaceKey", typeof(RectTransform));
        key.transform.SetParent(panel.transform, false);
        RectTransform krt = key.GetComponent<RectTransform>();
        krt.anchorMin = new Vector2(1f, 0f);
        krt.anchorMax = new Vector2(1f, 0f);
        krt.pivot = new Vector2(1f, 0f);
        krt.anchoredPosition = new Vector2(-40f, 24f);
        krt.sizeDelta = new Vector2(190f, 64f);
        Image keyImg = key.AddComponent<Image>();
        keyImg.sprite = box;
        keyImg.type = Image.Type.Sliced;
        keyImg.color = new Color(0.95f, 0.95f, 0.9f, 1f);
        keyImg.raycastTarget = false;
        key.AddComponent<BlinkingPrompt>();

        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(key.transform, false);
        RectTransform lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        TextMeshProUGUI keyText = label.AddComponent<TextMeshProUGUI>();
        if (font != null)
            keyText.font = font;
        keyText.text = "SPACE";
        keyText.fontSize = 36f;
        keyText.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        keyText.alignment = TextAlignmentOptions.Center;
        keyText.raycastTarget = false;

        // 部品
        GimmickIntroUI ui = root.AddComponent<GimmickIntroUI>();
        ui.panel = panel;
        ui.messageText = text;
        ui.spacePrompt = key;
        ui.fadeGroup = group;

        panel.SetActive(false);
        key.SetActive(false);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    // ---- 範囲 ----------------------------------------------------------

    private static GameObject BuildZone(string path, GameObject uiPrefab)
    {
        GameObject root = new GameObject("GimmickIntroZone");

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(3f, 4f);
        box.offset = new Vector2(0f, 2f); // 足元が、置いた位置に来る

        GimmickIntroZone zone = root.AddComponent<GimmickIntroZone>();
        zone.uiPrefab = uiPrefab != null ? uiPrefab.GetComponent<GimmickIntroUI>() : null;
        zone.steps = new GimmickIntroZone.Step[1];
        zone.steps[0] = new GimmickIntroZone.Step { message = "ここに説明を入れます。" };

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
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
