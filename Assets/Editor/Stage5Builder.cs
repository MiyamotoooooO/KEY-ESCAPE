using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - ステージ5の、地面・足場・ギミックを、いま開いているシーンに自動で置く。
///
/// 使い方：
///   1) シーン「Stage5」を開く。
///   2) メニュー Tools > KEY ESCAPE > ステージ部品 > ステージ5を自動で組み立てる
///   3) Ctrl+S でシーンを保存する。
///
/// ・置いた物は、すべて「StageLayout」という親の下に入る。置いたあとは、自由に動かしてよい。
/// ・もう一度メニューを押すと、StageLayout だけを消して、作り直す（手で動かした分は、消える）。
/// ・プレイヤー、カメラ、UI、BGM、今ある地面、ゴールなどは、消さない。
///   ゴールだけは、新しい位置（ステージの右端）へ動かす。
/// ・テスト用に、シーンの直下へ置いてある「落石（FallingStone）」は、通路と重なるので、非表示にする。
///
/// 床の上面は y = -4.72（今ある地面と同じ高さ）。
/// </summary>
public static class Stage5Builder
{
    private const string PrefabFolder = "Assets/Prefab/Object/";
    private const string RootName = "StageLayout";
    private const string SceneName = "Stage5";

    // 床の上面の高さ
    private const float Floor = -4.72f;
    // 地面プレハブ（薄い板）の中心の高さ
    private const float GroundY = -4.79f;
    // 天井の中心の高さ
    private const float CeilY = 2.67f;
    // 天井の下面
    private const float CeilBottom = 2.6f;

    private static Transform _root;
    private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

    private static readonly string[] PrefabNames =
    {
        "Ground", "Tower", "Platform", "Spike", "Checkpoint", "Lever", "LeverBridge",
        "River", "RiverPlatform", "LaserEmitter", "FallingStone", "GimmickIntroZone",
    };

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/ステージ5を自動で組み立てる")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            bool go = EditorUtility.DisplayDialog("ステージ5を組み立てる",
                "いま開いているシーンは「" + scene.name + "」です。\nステージ5のシーンを開いてから、もう一度押してください。\n\nこのまま、いまのシーンに置きますか？",
                "このまま置く", "やめる");
            if (!go)
                return;
        }

        // プレハブが、ぜんぶあるか確かめる（足りなければ、何も置かずに終わる）
        _prefabs.Clear();
        var missing = new List<string>();
        foreach (string n in PrefabNames)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + n + ".prefab");
            if (p == null)
                missing.Add(n);
            else
                _prefabs[n] = p;
        }
        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("ステージ5を組み立てる",
                "次のプレハブが見つかりません。\n" + string.Join("\n", missing) + "\n\n" + PrefabFolder + " に置いてあるか、確かめてください。", "OK");
            return;
        }

        var existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("ステージ5を組み立てる",
                "すでに「" + RootName + "」があります。消して、作り直しますか？\n（手で動かした分は、消えます）", "作り直す", "やめる"))
                return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("ステージ5を組み立てる");

        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        var rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "StageLayout");
        _root = rootGo.transform;
        _root.position = Vector3.zero;

        Transform gGround = Group("01_地面と足場");
        Transform gStart = Group("02_スタートとレバー1");
        Transform gBridge = Group("03_跳ね橋");
        Transform gStone = Group("04_落石の通路");
        Transform gRiver = Group("05_川");
        Transform gEnd = Group("06_ゴール前");
        Transform gZone = Group("07_紹介ゾーン");
        Transform gCam = Group("08_カメラの目印");

        // ---------- 地面 ----------
        // 今ある地面（x -3.1 〜 17.5）は、そのまま使う
        Place("Ground", "Ground_B", 35.03f, GroundY, gGround);
        Place("Ground", "Ground_C", 55.0f, GroundY, gGround);
        // 天井（落石とレーザーを吊るす）
        Place("Ground", "Ceiling_Stone", 36.0f, CeilY, gGround);
        GameObject ceilEnd = Place("Ground", "Ceiling_End", 60.5f, CeilY, gGround);
        ceilEnd.transform.localScale = new Vector3(0.5f, 1f, 1f);

        // ---------- スタート〜レバー1 ----------
        Place("Tower", "Tower_Start", 6.4f, Floor + 1.045f, gStart);
        Place("Platform", "Platform_Lever1", 9.6f, -2.35f, gStart);
        GameObject lever1 = Place("Lever", "Lever_Bridge", 10.2f, -2.165f, gStart);

        // レーザー1（右向き。橋へ向かう道をふさぐ）
        GameObject laser1 = Place("LaserEmitter", "Laser_1", 13.8f, Floor + 0.64f, gStart);
        SetLaser(laser1, 2.5f, 0.8f, 1.0f, 0f, 7f);

        // ---------- 跳ね橋 ----------
        GameObject bridgeGo = Place("LeverBridge", "LeverBridge", 17.4f, Floor, gBridge);
        var bridge = bridgeGo.GetComponent<LeverBridge>();
        if (bridge != null)
        {
            bridge.length = 7.5f;
            bridge.extendRight = true;
            bridge.ApplyShape();
            RecordAll(bridgeGo);
            if (bridge.deck != null) RecordAll(bridge.deck.gameObject);
        }
        var lv1 = lever1.GetComponent<Lever>();
        if (lv1 != null && bridge != null)
        {
            lv1.targets = new LeverReceiver[] { bridge };
            lv1.zoomRatio = 1.6f;
            Record(lv1);
        }

        // ---------- 落石の通路 ----------
        Place("Checkpoint", "Checkpoint_1", 26.8f, Floor, gStone);
        SetCheckpointOrder("Checkpoint_1", 0);
        Place("Spike", "Spike_1", 33.3f, Floor, gStone);
        float stoneY = CeilBottom - 0.55f;
        SetStone(Place("FallingStone", "FallingStone_1", 30.4f, stoneY, gStone), 2.2f, 0.5f);
        SetStone(Place("FallingStone", "FallingStone_2", 33.3f, stoneY, gStone), 2.2f, 0.5f);
        SetStone(Place("FallingStone", "FallingStone_3", 36.2f, stoneY, gStone), 2.2f, 0.5f);

        // ---------- 川 ----------
        Place("Platform", "Platform_Step1", 38.6f, -3.15f, gRiver);
        Place("Platform", "Platform_Step2", 40.0f, -1.2f, gRiver);
        GameObject lever2 = Place("Lever", "Lever_River", 40.2f, -1.015f, gRiver);
        Place("Tower", "Tower_RiverL", 42.1f, Floor + 1.045f, gRiver);
        Place("Tower", "Tower_RiverR", 55.9f, Floor + 1.045f, gRiver);

        GameObject riverGo = Place("River", "River", 43.0f, -2.85f, gRiver);
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
        GameObject rp1 = Place("RiverPlatform", "RiverPlatform_1", 46.8f, -2.8f, gRiver);
        GameObject rp2 = Place("RiverPlatform", "RiverPlatform_2", 51.4f, -2.8f, gRiver);
        // 床の判定に入るよう、Default レイヤーにする（Ground レイヤーだと、足場の上で空中の動きになる）
        rp1.layer = 0;
        rp2.layer = 0;
        var lv2 = lever2.GetComponent<Lever>();
        if (lv2 != null && river != null)
        {
            lv2.targets = new LeverReceiver[] { river };
            lv2.zoomRatio = 1.3f;
            Record(lv2);
        }

        // ---------- ゴール前 ----------
        Place("Checkpoint", "Checkpoint_2", 57.2f, Floor, gEnd);
        SetCheckpointOrder("Checkpoint_2", 1);
        // 縦のレーザー（天井から下へ）
        GameObject laser2 = Place("LaserEmitter", "Laser_2", 58.6f, CeilBottom - 0.64f, gEnd);
        laser2.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        SetLaser(laser2, 2.5f, 0.8f, 1.0f, 1.0f, 9f);
        SetStone(Place("FallingStone", "FallingStone_4", 61.3f, stoneY, gEnd), 2.0f, 0.5f);

        // ---------- カメラの目印 ----------
        Transform camLaser = CamPoint("CP_Laser1", 16.5f, -3.5f, gCam);
        Transform camStone = CamPoint("CP_Stone", 33.3f, -0.5f, gCam);
        Transform camRiver = CamPoint("CP_River", 49.0f, -3.3f, gCam);

        // ---------- 紹介ゾーン ----------
        SetZone(Place("GimmickIntroZone", "IntroZone_Laser", 12.0f, Floor, gZone),
            camLaser, "あの機械、赤く光ったら何か出そう…。光が消えたすきに走り抜けよう", 1.2f);
        SetZone(Place("GimmickIntroZone", "IntroZone_Stone", 28.8f, Floor, gZone),
            camStone, "天井の石が落ちてきそう…。止まらずに走り抜けよう", 1.3f);
        SetZone(Place("GimmickIntroZone", "IntroZone_River", 39.5f, Floor, gZone),
            camRiver, "川の流れが強い…。高いところのレバーで、止められないかな？", 1.3f);

        // ---------- ゴールを右端へ ----------
        var goal = Object.FindObjectOfType<StageGoal>();
        if (goal != null)
        {
            Undo.RecordObject(goal.transform, "ゴールを動かす");
            goal.transform.position = new Vector3(63.4f, -4.74f, 0f);
            EditorUtility.SetDirty(goal.transform);
        }
        else
        {
            Debug.LogWarning("[Stage5Builder] StageGoal が見つかりません。ゴールは、手で右端（x=63付近）へ動かしてください。");
        }

        // ---------- テスト用の落石を非表示に ----------
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

        Debug.Log("[Stage5Builder] ステージ5を組み立てました（StageLayout）。テスト用の落石を非表示にした数：" + hidden
            + "。Ctrl+S でシーンを保存してください。");
        EditorUtility.DisplayDialog("ステージ5を組み立てる",
            "置き終わりました。\nCtrl+S でシーンを保存してください。\n\n"
            + "・置いた物は、Hierarchy の「StageLayout」の中にあります。\n"
            + "・ゴールは、x=63.4 へ動かしました。\n"
            + (hidden > 0 ? "・テスト用の落石を、" + hidden + "個、非表示にしました。\n" : ""),
            "OK");
    }

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/ステージ5の配置（StageLayout）を消す")]
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
