using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - ステージ9を作る（シーンのコピー＋地面・足場・ギミックの自動配置）。メニューは2つ。
///
/// 1) Tools > KEY ESCAPE > ステージ部品 > Stage9のシーンを作る（Stage5をコピー）
///      ・Stage5.unity を Stage9.unity に複製して、開く。
///      ・ゴールのステージIDを「Stage9」にする。
///      ・Build Settings と StageCatalog（Resources）に、Stage9 を足す。
///
/// 2) Tools > KEY ESCAPE > ステージ部品 > Stage9を自動で組み立てる
///      ・シーン「Stage9」を開いた状態で押す。置いた物は、すべて「StageLayout」という親の下に入る。
///      ・もう一度押すと、StageLayout だけを消して、作り直す（手で動かした分は、消える）。
///      ・プレイヤー、カメラ、UI、BGM、今ある地面は、消さない。ゴールは、右端へ動かす。
///      ・先に、「壊せる石のプレハブを作る」のメニューを、押しておくこと。
///
/// 床の上面は y = -4.72（今ある地面と同じ高さ）。置いたあとは、位置を自由に動かしてよい。
/// </summary>
public static class Stage9Builder
{
    private const string Stage5Path = "Assets/Scenes/Stage/Stage5.unity";
    private const string Stage9Path = "Assets/Scenes/Stage/Stage9.unity";
    private const string CatalogPath = "Assets/Resources/StageCatalog.asset";
    private const string RootName = "StageLayout";
    private const string SceneName = "Stage9";

    private const string ObjectFolder = "Assets/Prefab/Object/";
    private const string ChaserPath = "Assets/Prefab/Mob/Enemy/KeyEnemyRig_Chaser.prefab";

    private const float Floor = -4.72f;
    private const float GroundY = -4.79f;
    private const float CeilY = 2.67f;
    private const float CeilBottom = 2.6f;

    private static Transform _root;
    private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

    private static readonly string[] ObjectPrefabs =
    {
        "Ground", "Tower", "Platform", "Spike", "Checkpoint", "Lever", "LeverBridge",
        "River", "RiverPlatform", "LaserEmitter", "FallingStone", "GimmickIntroZone", "BreakableStone",
    };

    // ====================================================================
    // 1) シーンを作る
    // ====================================================================

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/Stage9のシーンを作る（Stage5をコピー）")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Stage5Path) == null)
        {
            EditorUtility.DisplayDialog("Stage9のシーンを作る", Stage5Path + " が見つかりません。", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(Stage9Path) != null;
        if (exists)
        {
            int choice = EditorUtility.DisplayDialogComplex("Stage9のシーンを作る",
                Stage9Path + " は、すでにあります。\n上書きすると、Stage9で直した内容は、消えます。",
                "開くだけ", "Stage5をコピーして上書き", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
            {
                if (!AssetDatabase.DeleteAsset(Stage9Path))
                {
                    EditorUtility.DisplayDialog("Stage9のシーンを作る", "既存の Stage9 を消せませんでした。", "OK");
                    return;
                }
                exists = false;
            }
        }

        if (!exists)
        {
            if (!AssetDatabase.CopyAsset(Stage5Path, Stage9Path))
            {
                EditorUtility.DisplayDialog("Stage9のシーンを作る", "シーンをコピーできませんでした。Console を見てください。", "OK");
                return;
            }
            AssetDatabase.Refresh();
        }

        Scene scene = EditorSceneManager.OpenScene(Stage9Path, OpenSceneMode.Single);

        // ゴールのステージIDを Stage9 に
        var goal = Object.FindObjectOfType<StageGoal>();
        if (goal != null)
        {
            Undo.RecordObject(goal, "ステージIDを変える");
            goal.stageId = "Stage9";
            EditorUtility.SetDirty(goal);
        }
        else
        {
            Debug.LogWarning("[Stage9Builder] StageGoal が見つかりません。ゴールのステージIDを、手で「Stage9」にしてください。");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        bool addedBuild = AddToBuildSettings();
        bool addedCatalog = AddToCatalog();

        EditorUtility.DisplayDialog("Stage9のシーンを作る",
            "Stage9 のシーンを作って、開きました。\n"
            + "・ゴールのステージID：Stage9\n"
            + "・Build Settings：" + (addedBuild ? "足しました" : "すでにありました") + "\n"
            + "・StageCatalog：" + (addedCatalog ? "足しました（きしむ回廊 / 1-10）" : "すでにありました（または見つかりませんでした）") + "\n\n"
            + "次は、メニュー「Stage9を自動で組み立てる」を押してください。",
            "OK");
    }

    private static bool AddToBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].path == Stage9Path)
            {
                if (!list[i].enabled)
                {
                    list[i].enabled = true;
                    EditorBuildSettings.scenes = list.ToArray();
                }
                return false;
            }
        }
        list.Add(new EditorBuildSettingsScene(Stage9Path, true));
        EditorBuildSettings.scenes = list.ToArray();
        return true;
    }

    private static bool AddToCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("[Stage9Builder] " + CatalogPath + " が見つかりません。StageCatalog に、Stage9 を手で足してください。");
            return false;
        }
        if (catalog.IndexOf("Stage9") >= 0)
            return false;

        Undo.RecordObject(catalog, "StageCatalog に Stage9 を足す");
        var entry = new StageCatalog.Entry();
        entry.id = "Stage9";
        entry.displayName = "きしむ回廊";
        entry.stageNumber = "1-10";
        entry.thumbnail = null;
        entry.description = "";
        entry.sceneName = "Stage9";
        entry.rewards = new List<StageReward>();
        catalog.stages.Add(entry);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return true;
    }

    // ====================================================================
    // 2) 組み立てる
    // ====================================================================

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/Stage9を自動で組み立てる")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            EditorUtility.DisplayDialog("Stage9を組み立てる",
                "いま開いているシーンは「" + scene.name + "」です。\n先に、メニュー「Stage9のシーンを作る（Stage5をコピー）」を押すか、Stage9 のシーンを開いてください。",
                "OK");
            return;
        }

        _prefabs.Clear();
        var missing = new List<string>();
        foreach (string n in ObjectPrefabs)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(ObjectFolder + n + ".prefab");
            if (p == null)
                missing.Add(n + (n == "BreakableStone" ? "（先に、メニュー「壊せる石のプレハブを作る」を押してください）" : ""));
            else
                _prefabs[n] = p;
        }
        var chaser = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPath);
        if (chaser == null)
            missing.Add("KeyEnemyRig_Chaser（" + ChaserPath + "）");
        else
            _prefabs["Chaser"] = chaser;

        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Stage9を組み立てる",
                "次のプレハブが見つかりません。\n" + string.Join("\n", missing), "OK");
            return;
        }

        var existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Stage9を組み立てる",
                "すでに「" + RootName + "」があります。消して、作り直しますか？\n（手で動かした分は、消えます）", "作り直す", "やめる"))
                return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("ステージ9を組み立てる");

        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        var rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "StageLayout");
        _root = rootGo.transform;
        _root.position = Vector3.zero;

        Transform gGround = Group("01_地面");
        Transform gStart = Group("02_スタートと追従キー1");
        Transform gStoneA = Group("03_壊せる石Aと落石の通路");
        Transform gBridge1 = Group("04_跳ね橋1と縦レーザー2つ");
        Transform gRiver = Group("05_川");
        Transform gMid = Group("06_石Bと追従キー2");
        Transform gGauntlet = Group("07_レーザーと落石の通路と追従キー3");
        Transform gBridge2 = Group("08_跳ね橋2と縦レーザー2つ");
        Transform gEnd = Group("09_石Cとゴール前");
        Transform gZone = Group("10_紹介ゾーン");
        Transform gCam = Group("11_カメラの目印");

        float stoneY = CeilBottom - 0.55f;

        // ---------- 地面 ----------
        // 今ある地面（x -3.1 〜 17.5）は、そのまま使う。1枚の長さは、約20.6
        Place("Ground", "Ground_B", 27.8f, GroundY, gGround);     // 17.5 〜 38.1
        Place("Ground", "Ground_C", 55.5f, GroundY, gGround);     // 45.2 〜 65.8
        GameObject groundD = Place("Ground", "Ground_D", 74.2f, GroundY, gGround);   // 65.4 〜 83.0
        groundD.transform.localScale = new Vector3(0.85f, 1f, 1f);
        Place("Ground", "Ground_E", 92.9f, GroundY, gGround);     // 82.6 〜 103.2
        Place("Ground", "Ground_F", 121.2f, GroundY, gGround);    // 110.9 〜 131.5

        // 天井（落石と縦レーザーを吊るす）
        GameObject ceil1 = Place("Ground", "Ceiling_StoneA", 28.6f, CeilY, gGround);
        ceil1.transform.localScale = new Vector3(0.45f, 1f, 1f);
        GameObject ceil2 = Place("Ground", "Ceiling_Bridge1", 41.8f, CeilY, gGround);
        ceil2.transform.localScale = new Vector3(0.42f, 1f, 1f);
        GameObject ceil3 = Place("Ground", "Ceiling_Gauntlet", 91.6f, CeilY, gGround);
        ceil3.transform.localScale = new Vector3(0.45f, 1f, 1f);
        GameObject ceil4 = Place("Ground", "Ceiling_Bridge2", 106.6f, CeilY, gGround);
        ceil4.transform.localScale = new Vector3(0.38f, 1f, 1f);
        GameObject ceil5 = Place("Ground", "Ceiling_Goal", 120.0f, CeilY, gGround);
        ceil5.transform.localScale = new Vector3(0.3f, 1f, 1f);

        // ---------- スタート：追従キー1 ----------
        SetChaser(Place("Chaser", "KeyEnemy_Chaser1", 11.0f, Floor + 0.95f, gStart), 4.92f, 1.4f);

        // ---------- 壊せる石A → チェックポイント → 落石の通路（トゲつき） ----------
        SetBreakable(Place("BreakableStone", "BreakableStone_A", 20.5f, Floor, gStoneA), new Vector2(1.8f, 3.6f));
        Place("Checkpoint", "Checkpoint_1", 23.0f, Floor, gStoneA);
        SetCheckpointOrder("Checkpoint_1", 0);
        Place("Spike", "Spike_1", 28.6f, Floor, gStoneA);
        SetStone(Place("FallingStone", "FallingStone_1", 26.0f, stoneY, gStoneA), 2.0f, 0.45f);
        SetStone(Place("FallingStone", "FallingStone_2", 28.6f, stoneY, gStoneA), 2.0f, 0.45f);
        SetStone(Place("FallingStone", "FallingStone_3", 31.2f, stoneY, gStoneA), 2.0f, 0.45f);

        // ---------- 跳ね橋1・縦レーザー2つ ----------
        PlaceBridgeSet(gBridge1, "1", 33.7f, 37.9f, 7.6f);
        GameObject laser1 = Place("LaserEmitter", "Laser_Bridge1A", 40.0f, CeilBottom - 0.64f, gBridge1);
        laser1.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        SetLaser(laser1, 1.8f, 0.7f, 1.0f, 0.0f, 9f);
        GameObject laser2 = Place("LaserEmitter", "Laser_Bridge1B", 43.6f, CeilBottom - 0.64f, gBridge1);
        laser2.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        SetLaser(laser2, 1.8f, 0.7f, 1.0f, 1.1f, 9f);

        // ---------- 川 ----------
        Place("Platform", "Platform_Step1", 46.9f, -3.15f, gRiver);
        Place("Platform", "Platform_Step2", 48.3f, -1.2f, gRiver);
        GameObject lever2 = Place("Lever", "Lever_River", 48.5f, -1.015f, gRiver);
        Place("Tower", "Tower_RiverL", 49.6f, Floor + 1.045f, gRiver);
        Place("Tower", "Tower_RiverR", 63.4f, Floor + 1.045f, gRiver);

        GameObject riverGo = Place("River", "River", 50.5f, -2.85f, gRiver);
        var river = riverGo.GetComponent<RiverCurrent>();
        var riverCol = riverGo.GetComponent<BoxCollider2D>();
        if (riverCol != null)
        {
            riverCol.size = new Vector2(12f, 1.87f);
            riverCol.offset = new Vector2(6f, -0.935f);
            Record(riverCol);
        }
        if (river != null)
        {
            river.flowDirection = new Vector2(-1f, 0f);
            river.flowSpeed = 12f;
            river.stoppedWhenLeverOn = true;
            Record(river);
        }
        GameObject rp1 = Place("RiverPlatform", "RiverPlatform_1", 54.2f, -2.8f, gRiver);
        GameObject rp2 = Place("RiverPlatform", "RiverPlatform_2", 58.8f, -2.8f, gRiver);
        // 足場の上で空中の動きにならないよう、Default レイヤーにする
        rp1.layer = 0;
        rp2.layer = 0;
        var lv2 = lever2.GetComponent<Lever>();
        if (lv2 != null && river != null)
        {
            lv2.targets = new LeverReceiver[] { river };
            lv2.zoomRatio = 1.3f;
            Record(lv2);
        }

        // ---------- 壊せる石B・チェックポイント・追従キー2 ----------
        // 石Bは、川の右の塔から飛び越えられないよう、高くする
        SetBreakable(Place("BreakableStone", "BreakableStone_B", 67.8f, Floor, gMid), new Vector2(2.0f, 5.6f));
        Place("Checkpoint", "Checkpoint_2", 69.8f, Floor, gMid);
        SetCheckpointOrder("Checkpoint_2", 1);
        SetChaser(Place("Chaser", "KeyEnemy_Chaser2", 72.5f, Floor + 0.95f, gMid), 6f, 3f);

        // ---------- レーザーと落石の通路・追従キー3 ----------
        GameObject laser3 = Place("LaserEmitter", "Laser_Floor1", 86.5f, Floor + 0.64f, gGauntlet);
        SetLaser(laser3, 2.0f, 0.8f, 1.0f, 0.0f, 4.5f);
        SetChaser(Place("Chaser", "KeyEnemy_Chaser3", 83.8f, Floor + 0.95f, gGauntlet), 5f, 2f);
        Place("Spike", "Spike_2", 91.6f, Floor, gGauntlet);
        SetStone(Place("FallingStone", "FallingStone_4", 89.0f, stoneY, gGauntlet), 2.0f, 0.4f);
        SetStone(Place("FallingStone", "FallingStone_5", 91.6f, stoneY, gGauntlet), 2.0f, 0.4f);
        SetStone(Place("FallingStone", "FallingStone_6", 94.2f, stoneY, gGauntlet), 2.0f, 0.4f);
        Place("Checkpoint", "Checkpoint_3", 96.2f, Floor, gGauntlet);
        SetCheckpointOrder("Checkpoint_3", 2);

        // ---------- 跳ね橋2・縦レーザー2つ ----------
        PlaceBridgeSet(gBridge2, "2", 98.8f, 103.0f, 8.0f);
        GameObject laser4 = Place("LaserEmitter", "Laser_Bridge2A", 105.2f, CeilBottom - 0.64f, gBridge2);
        laser4.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        SetLaser(laser4, 1.6f, 0.7f, 1.0f, 0.3f, 9f);
        GameObject laser5 = Place("LaserEmitter", "Laser_Bridge2B", 108.4f, CeilBottom - 0.64f, gBridge2);
        laser5.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        SetLaser(laser5, 1.6f, 0.7f, 1.0f, 1.2f, 9f);

        // ---------- 壊せる石C・ゴール前の落石とレーザー ----------
        Place("Checkpoint", "Checkpoint_4", 112.6f, Floor, gEnd);
        SetCheckpointOrder("Checkpoint_4", 3);
        SetBreakable(Place("BreakableStone", "BreakableStone_C", 115.2f, Floor, gEnd), new Vector2(2.0f, 4.2f));
        GameObject laser6 = Place("LaserEmitter", "Laser_Goal", 117.6f, Floor + 0.64f, gEnd);
        SetLaser(laser6, 2.0f, 0.8f, 1.0f, 0.0f, 4.5f);
        SetStone(Place("FallingStone", "FallingStone_7", 120.0f, stoneY, gEnd), 2.0f, 0.4f);
        SetStone(Place("FallingStone", "FallingStone_8", 121.8f, stoneY, gEnd), 2.0f, 0.4f);

        // ---------- カメラの目印 ----------
        Transform camChaser = CamPoint("CP_Chaser", 11.0f, -3.2f, gCam);
        Transform camGauntlet = CamPoint("CP_Gauntlet", 90.0f, -1.5f, gCam);
        Transform camBridge2 = CamPoint("CP_Bridge2", 106.6f, -1.5f, gCam);

        // ---------- 紹介ゾーン（はじめて出会う仕掛けではないので、少なめ） ----------
        SetZone(Place("GimmickIntroZone", "IntroZone_Start", 6.5f, Floor, gZone),
            camChaser, "この回廊は、長い…。これまで出会った仕掛けが、ぜんぶ待っているみたいだ", 1.2f);
        SetZone(Place("GimmickIntroZone", "IntroZone_Gauntlet", 81.0f, Floor, gZone),
            camGauntlet, "レーザーと落石が、いっしょに来る…。タイミングをよく見て、進もう", 1.3f);
        SetZone(Place("GimmickIntroZone", "IntroZone_Bridge2", 96.0f, Floor, gZone),
            camBridge2, "もう一つ、橋が上がっている…。橋の上には、また光る機械が見える", 1.4f);

        // ---------- ゴールを右端へ ----------
        var goal = Object.FindObjectOfType<StageGoal>();
        if (goal != null)
        {
            Undo.RecordObject(goal.transform, "ゴールを動かす");
            goal.transform.position = new Vector3(125.0f, -4.74f, 0f);
            EditorUtility.SetDirty(goal.transform);
        }
        else
        {
            Debug.LogWarning("[Stage9Builder] StageGoal が見つかりません。ゴールは、手で右端（x=125付近）へ動かしてください。");
        }

        // ---------- 下の床（落ちたらやられる）を、ステージの真ん中へ ----------
        var kill = GameObject.Find("KillCollider");
        if (kill != null)
        {
            Undo.RecordObject(kill.transform, "KillColliderを動かす");
            Vector3 kp = kill.transform.position;
            kill.transform.position = new Vector3(62f, kp.y, kp.z);
            kill.transform.localScale = new Vector3(1.4f, kill.transform.localScale.y, kill.transform.localScale.z);
            EditorUtility.SetDirty(kill.transform);
        }
        else
        {
            Debug.LogWarning("[Stage9Builder] KillCollider が見つかりません。橋の下に落ちたとき、やられない可能性があります。");
        }

        // ---------- シーンに残っているテスト用の落石を非表示に ----------
        int hidden = 0;
        foreach (var fs in Object.FindObjectsOfType<FallingStone>())
        {
            if (fs.transform.IsChildOf(_root))
                continue;
            if (fs.transform.parent != null)
                continue;
            Undo.RecordObject(fs.gameObject, "テスト用の落石を非表示");
            fs.gameObject.SetActive(false);
            hidden++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = rootGo;

        Debug.Log("[Stage9Builder] ステージ9を組み立てました（StageLayout）。テスト用の落石を非表示にした数：" + hidden + "。Ctrl+S でシーンを保存してください。");
        EditorUtility.DisplayDialog("Stage9を組み立てる",
            "置き終わりました。\nCtrl+S でシーンを保存してください。\n\n"
            + "・置いた物は、Hierarchy の「StageLayout」の中にあります。\n"
            + "・ゴールは、x=125 へ動かしました。\n"
            + "・追従型キーが落とすキーは、空です。Inspector の Key Drop で決めてください。\n"
            + (hidden > 0 ? "・テスト用の落石を、" + hidden + "個、非表示にしました。\n" : ""),
            "OK");
    }

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/Stage9の配置（StageLayout）を消す")]
    public static void Clear()
    {
        var existing = GameObject.Find(RootName);
        if (existing == null)
        {
            EditorUtility.DisplayDialog("配置を消す", "「" + RootName + "」は、見つかりませんでした。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("配置を消す", "「" + RootName + "」を消します。よろしいですか？\n（Ctrl+Z で戻せます）", "消す", "やめる"))
            return;
        Undo.DestroyObjectImmediate(existing);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ---------- 部品を置く ----------

    private static Transform Group(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(_root, false);
        return go.transform;
    }

    private static GameObject Place(string prefabName, string instanceName, float x, float y, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_prefabs[prefabName], parent);
        Undo.RegisterCreatedObjectUndo(go, instanceName);
        go.name = instanceName;
        go.transform.position = new Vector3(x, y, 0f);
        go.transform.rotation = Quaternion.identity;
        return go;
    }

    private static Transform CamPoint(string name, float x, float y, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        return go.transform;
    }

    private static void PlaceBridgeSet(Transform parent, string suffix, float towerX, float bridgeX, float length)
    {
        Place("Tower", "Tower_Lever" + suffix, towerX, Floor + 1.045f, parent);
        Place("Platform", "Platform_Lever" + suffix, towerX + 2.7f, -2.35f, parent);
        GameObject lever = Place("Lever", "Lever_Bridge" + suffix, towerX + 3.2f, -2.165f, parent);

        GameObject bridgeGo = Place("LeverBridge", "LeverBridge" + suffix, bridgeX, Floor, parent);
        var bridge = bridgeGo.GetComponent<LeverBridge>();
        if (bridge != null)
        {
            bridge.length = length;
            bridge.extendRight = true;
            bridge.ApplyShape();
            RecordAll(bridgeGo);
            if (bridge.deck != null) RecordAll(bridge.deck.gameObject);
        }
        var lv = lever.GetComponent<Lever>();
        if (lv != null && bridge != null)
        {
            lv.targets = new LeverReceiver[] { bridge };
            lv.zoomRatio = 1.6f;
            Record(lv);
        }
    }

    // ---------- 設定 ----------

    private static void SetLaser(GameObject go, float off, float warn, float fire, float delay, float maxLen)
    {
        var l = go.GetComponent<LaserEmitter>();
        if (l == null)
            return;
        l.offSeconds = off;
        l.warnSeconds = warn;
        l.fireSeconds = fire;
        l.startDelay = delay;
        l.maxLength = maxLen;
        Record(l);
    }

    private static void SetStone(GameObject go, float detectWidth, float shakeSeconds)
    {
        var s = go.GetComponent<FallingStone>();
        if (s == null)
            return;
        s.detectWidth = detectWidth;
        s.shakeSeconds = shakeSeconds;
        Record(s);
    }

    private static void SetBreakable(GameObject go, Vector2 size)
    {
        var s = go.GetComponent<BreakableStone>();
        if (s == null)
            return;
        s.size = size;
        s.ApplyShape();
        RecordAll(go);
        if (s.visual != null)
        {
            Record(s.visual.transform);
            Record(s.visual);
        }
        var col = go.GetComponent<BoxCollider2D>();
        if (col != null)
            Record(col);
    }

    private static void SetChaser(GameObject go, float detectX, float detectY)
    {
        var a = go.GetComponent<Ally>();
        if (a == null)
            return;
        a.keyDrop = null;   // 落とすキーは、あとで Inspector で決める
        a.detectRangeX = detectX;
        a.detectRangeY = detectY;
        Record(a);
    }

    private static void SetCheckpointOrder(string instanceName, int order)
    {
        var go = GameObject.Find(instanceName);
        if (go == null)
            return;
        var cp = go.GetComponent<Checkpoint>();
        if (cp == null)
            return;
        cp.order = order;
        Record(cp);
    }

    private static void SetZone(GameObject go, Transform cameraPoint, string message, float zoom)
    {
        var z = go.GetComponent<GimmickIntroZone>();
        if (z == null)
            return;
        var step = new GimmickIntroZone.Step();
        step.cameraPoint = cameraPoint;
        step.message = message;
        step.zoomRatio = zoom;
        z.steps = new GimmickIntroZone.Step[] { step };
        Record(z);
    }

    private static void Record(Object o)
    {
        EditorUtility.SetDirty(o);
        if (PrefabUtility.IsPartOfPrefabInstance(o))
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    private static void RecordAll(GameObject go)
    {
        foreach (var c in go.GetComponents<Component>())
        {
            if (c != null)
                Record(c);
        }
    }
}
