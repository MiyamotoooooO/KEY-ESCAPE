#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 拠点（SelectScene）に、「バトルの塔」を作る。
///
/// 使い方：SelectScene を開いて、メニュー
///   Tools > KEY ESCAPE > ステージ部品 > 拠点に「バトルの塔」を作る
/// を押す。次の物ができる（BattleTower）。
///   ・塔の絵（Assets/Art/Sketch/sketch_tower_battle.png）
///   ・話しかける範囲と、ステージ選択を開く部品（掲示板と同じ StageSelectNPC）
///   ・F キーの案内（掲示板のものを、コピー）
/// ボス戦（Stage7）をクリアして拠点に戻ると、塔がせり上がり、Kキーが話す。
/// 置く場所は、BattleTower を動かして決める（足もとは、自動で地面に合う）。
/// すでにあるときは、それを選ぶだけ（重ねて作らない）。
///
/// 前から飾りとして置いてある、絵だけの「sketch_tower_battle」は、重なって見えないように、非表示にする。
/// </summary>
public static class BattleTowerSetup
{
    private const string SpritePath = "Assets/Art/Sketch/sketch_tower_battle.png";
    private const string UiPath = "Assets/Prefab/GimmickIntroUI.prefab";
    private const float DefaultX = 8f;
    private const float DefaultGroundY = -4.56f;

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/拠点に「バトルの塔」を作る")]
    public static void Build()
    {
        BattleTower existing = Object.FindObjectOfType<BattleTower>(true);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("[BattleTowerSetup] すでに「" + existing.name + "」があります。それを選びました。");
            return;
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("バトルの塔を作る", SpritePath + " が見つかりません。", "OK");
            return;
        }

        // 掲示板（StageSelectNPC）を探す
        StageSelectNPC board = null;
        foreach (StageSelectNPC n in Object.FindObjectsOfType<StageSelectNPC>(true))
        {
            board = n;
            break;
        }
        if (board == null)
        {
            EditorUtility.DisplayDialog("バトルの塔を作る",
                "掲示板（StageSelectNPC）が、シーンに見つかりません。SelectScene を開いているか、確認してください。", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("バトルの塔を作る");

        GameObject root = new GameObject("BattleTower");
        Undo.RegisterCreatedObjectUndo(root, "BattleTower");
        root.transform.position = new Vector3(DefaultX, DefaultGroundY, 0f);

        // 絵（足もとが、BattleTower の位置に来る）
        GameObject visual = new GameObject("TowerSprite");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -1;
        visual.transform.localPosition = new Vector3(0f, sprite.bounds.extents.y, 0f);

        // 話しかける範囲
        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(Mathf.Max(5f, sprite.bounds.size.x + 1.2f), Mathf.Max(6f, sprite.bounds.size.y + 0.4f));
        col.offset = new Vector2(0f, col.size.y * 0.5f);

        // F キーの案内（掲示板のをコピー）
        GameObject promptClone = null;
        if (board.prompt != null)
        {
            promptClone = Object.Instantiate(board.prompt, root.transform);
            promptClone.name = "Prompt";
            promptClone.transform.position = new Vector3(DefaultX, DefaultGroundY + 3.2f, 0f);
            promptClone.transform.localScale = board.prompt.transform.lossyScale;
            promptClone.SetActive(false);
            Undo.RegisterCreatedObjectUndo(promptClone, "Prompt");
        }
        else
        {
            Debug.LogWarning("[BattleTowerSetup] 掲示板に Prompt（Fキーの案内）が入っていないので、塔には、案内が出ません。");
        }

        // ステージ選択を開く部品
        StageSelectNPC npc = root.AddComponent<StageSelectNPC>();
        npc.stageSelectUI = board.stageSelectUI;
        npc.interactKey = board.interactKey;
        npc.playerTag = board.playerTag;
        npc.prompt = promptClone;

        // 演出
        BattleTower tower = root.AddComponent<BattleTower>();
        tower.towerSprite = sr;
        tower.npc = npc;
        tower.talkArea = col;
        tower.uiPrefab = AssetDatabase.LoadAssetAtPath<GimmickIntroUI>(UiPath);
        if (tower.uiPrefab == null)
            Debug.LogWarning("[BattleTowerSetup] " + UiPath + " が見つかりません。BattleTower の Ui Prefab を、手動で入れてください。");

        // 飾りの塔を、非表示に
        int hidden = 0;
        foreach (SpriteRenderer r in Object.FindObjectsOfType<SpriteRenderer>(true))
        {
            if (r.gameObject.name == "sketch_tower_battle" && !r.transform.IsChildOf(root.transform) && r.gameObject.activeSelf)
            {
                Undo.RecordObject(r.gameObject, "飾りの塔を非表示");
                r.gameObject.SetActive(false);
                hidden++;
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Debug.Log("[BattleTowerSetup] 「BattleTower」を作りました。飾りの塔を非表示にした数：" + hidden + "。Ctrl+S でシーンを保存してください。");
        EditorUtility.DisplayDialog("バトルの塔を作る",
            "BattleTower を、x=" + DefaultX + " に置きました。\n"
            + "・場所は、Hierarchy の BattleTower を動かして決めてください（足もとは、地面に自動で合います）。\n"
            + "・ボス戦（Stage7）をクリアして拠点に戻ると、塔がせり上がります。\n"
            + "・すぐ試したいときは、BattleTower の「Test Always Play」をONにして再生（リリース前にOFF）。\n"
            + (hidden > 0 ? "・飾りの塔（sketch_tower_battle）を、" + hidden + "個、非表示にしました。\n" : "")
            + "\nCtrl+S でシーンを保存してください。",
            "OK");
    }
}
#endif
