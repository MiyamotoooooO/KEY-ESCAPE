using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - ステージ7（ボス戦）を作る。メニューは5つ。
///
/// 1) Tools > KEY ESCAPE > ステージ部品 > Stage7のシーンを作る（Stage6をコピー）
///      ・Stage6.unity を Stage7.unity に複製して、開く（Stage6 が無ければ Stage5 を使う）。
///      ・複製した「StageLayout」は、消す。ゴールのステージIDを「Stage7」にする。
///      ・Build Settings と StageCatalog（Resources）に、Stage7 を足す（仮の名前「ボスの部屋」、番号 1-8）。
///
/// 2) Tools > KEY ESCAPE > ステージ部品 > Stage7を自動で組み立てる
///      ・シーン「Stage7」を開いた状態で押す。置いた物は、すべて「StageLayout」という親の下に入る。
///      ・道（約60メートル）と、倒れた空のキー、ひとこと話すキー、遠く離れた「ボスの部屋」、
///        ムービー（BossIntroMovie）を置く。
///      ・もう一度押すと、StageLayout だけを消して、作り直す（手で動かした分は、消える）。
///      ・プレイヤー、カメラ、UI、BGM、今ある地面は、消さない。ゴールは、ボスの部屋の右端へ動かす。
///
/// 3) Tools > KEY ESCAPE > ステージ部品 > Stage7の配置（StageLayout）を消す
///
/// 4) Tools > KEY ESCAPE > ステージ部品 > Stage7にボスを置く
///      ・ボスの部屋（BossSpawnPoint）に、ボス「BossA」を置く。「組み立てる」を押すと、これも一緒に置かれる。
///      ・ボスだけを置き直したいとき用。BossIntroMovie の設定（音など）は、消えない。
///      ・ムービーの終わり（On Movie End）に、BossA の BeginBattle をつなぐ。
///      ・（ゴールの見た目だけ消したいときは「Stage7のゴールの見た目を消す」。BossA の設定は消えない。）
///      ・ゴール（StageGoal）を「ボスを倒すとクリア」にして、ボスを割り当てる（G O A L の演出と、ゴールの矢印は消す）。
///
/// 床の上面は y = -4.72（今ある地面と同じ高さ）。置いたあとは、位置を自由に動かしてよい。
/// </summary>
public static class Stage7Builder
{
    private const string Stage5Path = "Assets/Scenes/Stage/Stage5.unity";
    private const string Stage6Path = "Assets/Scenes/Stage/Stage6.unity";
    private const string Stage7Path = "Assets/Scenes/Stage/Stage7.unity";
    private const string CatalogPath = "Assets/Resources/StageCatalog.asset";
    private const string RootName = "StageLayout";
    private const string SceneName = "Stage7";

    private const string ObjectFolder = "Assets/Prefab/Object/";
    private const string KeyNpcPath = "Assets/Prefab/Mob/Enemy/KeyNPC_Visual.prefab";
    private const string EmoteFolder = "Assets/Prefab/Emotes";
    private const string FallingStonePath = "Assets/Prefab/Object/FallingStone.prefab";
    private const string ChaserPath = "Assets/Prefab/Mob/Enemy/KeyEnemyRig_Chaser.prefab";
    private const string BossName = "BossA";
    private const string KeyAPath = "Assets/KeyDefinition/Key_A.asset";
    private const string HintUiPath = "Assets/Prefab/GimmickIntroUI.prefab";

    // ボス
    private const float BossScale = 3.2f;

    private const float Floor = -4.72f;
    private const float GroundY = -4.79f;

    // 道
    private const float RoadEndWallX = 57.2f;
    private const float SpeakerX = 49.0f;

    // ボスの部屋（道から遠く離れた所）
    private const float ArenaLeftWallX = 96.2f;
    private const float ArenaRightWallX = 135.8f;
    private const float ArenaPlayerX = 102.0f;
    private const float ArenaBossX = 126.0f;

    private static Transform _root;
    private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

    private static readonly string[] ObjectPrefabs = { "Ground", "Checkpoint" };

    // ====================================================================
    // 1) シーンを作る
    // ====================================================================

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/Stage7のシーンを作る（Stage6をコピー）")]
    public static void CreateScene()
    {
        string source = AssetDatabase.LoadAssetAtPath<SceneAsset>(Stage6Path) != null ? Stage6Path : Stage5Path;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null)
        {
            EditorUtility.DisplayDialog("Stage7のシーンを作る", "コピーもとのシーン（Stage6 / Stage5）が見つかりません。", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(Stage7Path) != null;
        if (exists)
        {
            int choice = EditorUtility.DisplayDialogComplex("Stage7のシーンを作る",
                Stage7Path + " は、すでにあります。\n上書きすると、Stage7で直した内容は、消えます。",
                "開くだけ", "コピーして上書き", "やめる");
            if (choice == 2)
                return;
            if (choice == 1)
            {
                if (!AssetDatabase.DeleteAsset(Stage7Path))
                {
                    EditorUtility.DisplayDialog("Stage7のシーンを作る", "既存の Stage7 を消せませんでした。", "OK");
                    return;
                }
                exists = false;
            }
        }

        if (!exists)
        {
            if (!AssetDatabase.CopyAsset(source, Stage7Path))
            {
                EditorUtility.DisplayDialog("Stage7のシーンを作る", "シーンをコピーできませんでした。Console を見てください。", "OK");
                return;
            }
            AssetDatabase.Refresh();
        }

        Scene scene = EditorSceneManager.OpenScene(Stage7Path, OpenSceneMode.Single);

        // コピーもとの配置は、いらないので消す（組み立てなおす）
        if (!exists)
        {
            var old = GameObject.Find(RootName);
            if (old != null)
                Object.DestroyImmediate(old);
        }

        // ゴールのステージIDを Stage7 に
        var goal = Object.FindObjectOfType<StageGoal>();
        if (goal != null)
        {
            Undo.RecordObject(goal, "ステージIDを変える");
            goal.stageId = "Stage7";
            EditorUtility.SetDirty(goal);
        }
        else
        {
            Debug.LogWarning("[Stage7Builder] StageGoal が見つかりません。ゴールのステージIDを、手で「Stage7」にしてください。");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        bool addedBuild = AddToBuildSettings();
        bool addedCatalog = AddToCatalog();

        EditorUtility.DisplayDialog("Stage7のシーンを作る",
            "Stage7 のシーンを作って、開きました。（コピーもと：" + source + "）\n"
            + "・ゴールのステージID：Stage7\n"
            + "・Build Settings：" + (addedBuild ? "足しました" : "すでにありました") + "\n"
            + "・StageCatalog：" + (addedCatalog ? "足しました（ボスの部屋 / 1-8）" : "すでにありました（または見つかりませんでした）") + "\n\n"
            + "次は、メニュー「Stage7を自動で組み立てる」を押してください。",
            "OK");
    }

    private static bool AddToBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].path == Stage7Path)
            {
                if (!list[i].enabled)
                {
                    list[i].enabled = true;
                    EditorBuildSettings.scenes = list.ToArray();
                }
                return false;
            }
        }
        list.Add(new EditorBuildSettingsScene(Stage7Path, true));
        EditorBuildSettings.scenes = list.ToArray();
        return true;
    }

    private static bool AddToCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("[Stage7Builder] " + CatalogPath + " が見つかりません。StageCatalog に、Stage7 を手で足してください。");
            return false;
        }
        if (catalog.IndexOf("Stage7") >= 0)
            return false;

        Undo.RecordObject(catalog, "StageCatalog に Stage7 を足す");
        var entry = new StageCatalog.Entry();
        entry.id = "Stage7";
        entry.displayName = "ボスの部屋";
        entry.stageNumber = "1-8";
        entry.thumbnail = null;
        entry.description = "";
        entry.sceneName = "Stage7";
        entry.rewards = new List<StageReward>();
        catalog.stages.Add(entry);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return true;
    }

    // ====================================================================
    // 2) 組み立てる
    // ====================================================================

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/Stage7を自動で組み立てる")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            EditorUtility.DisplayDialog("Stage7を組み立てる",
                "いま開いているシーンは「" + scene.name + "」です。\n先に、メニュー「Stage7のシーンを作る（Stage6をコピー）」を押すか、Stage7 のシーンを開いてください。",
                "OK");
            return;
        }

        _prefabs.Clear();
        var missing = new List<string>();
        foreach (string n in ObjectPrefabs)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(ObjectFolder + n + ".prefab");
            if (p == null)
                missing.Add(n);
            else
                _prefabs[n] = p;
        }
        var keyNpc = AssetDatabase.LoadAssetAtPath<GameObject>(KeyNpcPath);
        if (keyNpc == null)
            missing.Add("KeyNPC_Visual（" + KeyNpcPath + "）");
        else
            _prefabs["KeyNPC"] = keyNpc;

        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Stage7を組み立てる",
                "次のプレハブが見つかりません。\n" + string.Join("\n", missing), "OK");
            return;
        }

        var existing = GameObject.Find(RootName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Stage7を組み立てる",
                "すでに「" + RootName + "」があります。消して、作り直しますか？\n（手で動かした分は、消えます）", "作り直す", "やめる"))
                return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("ステージ7を組み立てる");

        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        var rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "StageLayout");
        _root = rootGo.transform;
        _root.position = Vector3.zero;

        Transform gMovie = Group("00_ムービー");
        Transform gGround = Group("01_地面");
        Transform gFallen = Group("02_倒れたキー");
        Transform gArena = Group("03_ボスの部屋");

        // ---------- 地面（道） ----------
        // 今ある地面（x -3.1 〜 17.5）の続き。約20ずつ
        Place("Ground", "Ground_Road1", 27.6f, GroundY, gGround);
        Place("Ground", "Ground_Road2", 47.6f, GroundY, gGround);
        // 道の行き止まり（ムービーを飛ばして歩いても、落ちないように）
        Wall("Wall_RoadEnd", RoadEndWallX, gGround);

        // ---------- 倒れたキー ----------
        float keyY = Floor;
        Vector3 npcScale = keyNpc.transform.localScale;
        var rnd = new System.Random(7);
        const int count = 24;
        int made = 0;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            // 右へ行くほど、キーの間がせまくなる
            float x = 5.5f + 50.5f * (1f - Mathf.Pow(1f - t, 1.5f));
            x += ((float)rnd.NextDouble() - 0.5f) * 1.2f;

            // 主人公とKキーが立つ場所と、話すキーのそばは、あける
            if (x > 42.6f && x < 46.8f)
                continue;
            if (Mathf.Abs(x - SpeakerX) < 1.0f)
                continue;

            made++;
            GameObject k = Place("KeyNPC", "FallenKey_" + made.ToString("00"), x, keyY, gFallen);
            float sc = 0.9f + (float)rnd.NextDouble() * 0.2f;
            float flip = rnd.NextDouble() < 0.5 ? -1f : 1f;
            k.transform.localScale = new Vector3(Mathf.Abs(npcScale.x) * sc * flip, npcScale.y * sc, npcScale.z);
            k.transform.rotation = Quaternion.Euler(0f, 0f, ((float)rnd.NextDouble() - 0.5f) * 16f);
        }
        // ひとこと話すキー（向きは、ムービーが始まると、主人公のほうになる）
        GameObject speaker = Place("KeyNPC", "FallenKey_Speaker", SpeakerX, keyY, gFallen);
        speaker.transform.localScale = new Vector3(Mathf.Abs(npcScale.x), npcScale.y, npcScale.z);

        // ---------- ボスの部屋 ----------
        Place("Ground", "Ground_Arena1", 106.0f, GroundY, gArena);
        Place("Ground", "Ground_Arena2", 126.0f, GroundY, gArena);
        Wall("Wall_ArenaLeft", ArenaLeftWallX, gArena);
        Wall("Wall_ArenaRight", ArenaRightWallX, gArena);
        GameObject ceil = Place("Ground", "Ceiling_Arena", 116.0f, 5.4f, gArena);
        ceil.transform.localScale = new Vector3(2.05f, 1f, 1f);

        // ボスの部屋の入口のチェックポイント（ここに着くと、やり直してもムービーは流れない）
        Place("Checkpoint", "Checkpoint_Arena", ArenaPlayerX, Floor, gArena);
        SetCheckpointOrder("Checkpoint_Arena", 0);

        Transform arenaPlayer = Marker("ArenaPlayerPoint", ArenaPlayerX, Floor, gArena);
        Transform bossSpawn = Marker("BossSpawnPoint", ArenaBossX, Floor, gArena);

        // ---------- ムービー ----------
        GameObject movieGo = new GameObject("BossIntroMovie");
        Undo.RegisterCreatedObjectUndo(movieGo, "BossIntroMovie");
        movieGo.transform.SetParent(gMovie, false);
        var movie = movieGo.AddComponent<BossIntroMovie>();
        movie.arenaPlayerPoint = arenaPlayer;
        movie.bossSpawnPoint = bossSpawn;
        movie.fallenKeysRoot = gFallen;
        movie.speakerKey = speaker.transform;
        movie.keyPrefab = keyNpc;
        movie.speakerStopDistance = 3.4f;

        // 日本語のフォントは、Kキーの文字のものを借りる
        TMP_Text tmp = keyNpc.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null && tmp.font != null)
            movie.font = tmp.font;
        else
            Debug.LogWarning("[Stage7Builder] フォントが見つかりませんでした。BossIntroMovie の Font に、日本語のフォントを入れてください。");

        // びっくりのエモート（名前に、それらしい言葉が入っているものを探す）
        movie.surpriseEmote = FindEmote();

        EditorUtility.SetDirty(movie);

        // ---------- ボス ----------
        string bossReport;
        BossA boss = CreateBoss(gArena, bossSpawn, movie, keyNpc, out bossReport);

        // ---------- ゴールをボスの部屋の右端へ ----------
        var goal = Object.FindObjectOfType<StageGoal>();
        if (goal != null)
        {
            Undo.RecordObject(goal.transform, "ゴールを動かす");
            goal.transform.position = new Vector3(133.0f, -4.74f, 0f);
            EditorUtility.SetDirty(goal.transform);
        }
        else
        {
            Debug.LogWarning("[Stage7Builder] StageGoal が見つかりません。ゴールは、手でボスの部屋（x=133付近）へ動かしてください。");
        }
        ConnectGoal(goal, boss);

        // ---------- 下の床（落ちたらやられる）を、ステージの真ん中へ ----------
        var kill = GameObject.Find("KillCollider");
        if (kill != null)
        {
            Undo.RecordObject(kill.transform, "KillColliderを動かす");
            Vector3 kp = kill.transform.position;
            kill.transform.position = new Vector3(90f, kp.y, kp.z);
            EditorUtility.SetDirty(kill.transform);
        }
        else
        {
            Debug.LogWarning("[Stage7Builder] KillCollider が見つかりません。");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = movieGo;

        Debug.Log("[Stage7Builder] ステージ7を組み立てました（StageLayout）。倒れたキー：" + (made + 1) + "体。Ctrl+S でシーンを保存してください。");
        EditorUtility.DisplayDialog("Stage7を組み立てる",
            "置き終わりました。\nCtrl+S でシーンを保存してください。\n\n"
            + "・ムービーは、Hierarchy の StageLayout > 00_ムービー > BossIntroMovie です。\n"
            + "・ステージに入ると、自動で始まります。作業中に飛ばしたいときは、Skip Movie をONに。\n"
            + "・ボスの部屋は、x=96〜136 にあります。ボスを倒すとクリアです（ゴールは部屋の右端、見えません）。\n"
            + "・びっくりのエモート：" + (movie.surpriseEmote != null ? movie.surpriseEmote.name : "見つかりませんでした（Surprise Emote に入れてください）") + "\n"
            + bossReport,
            "OK");
    }

    // ====================================================================
    // 4) ボスを置く
    // ====================================================================

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/Stage7にボスを置く")]
    public static void PlaceBoss()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            EditorUtility.DisplayDialog("Stage7にボスを置く",
                "いま開いているシーンは「" + scene.name + "」です。Stage7 のシーンを開いてから押してください。", "OK");
            return;
        }

        var spawnGo = GameObject.Find("BossSpawnPoint");
        if (spawnGo == null)
        {
            EditorUtility.DisplayDialog("Stage7にボスを置く",
                "BossSpawnPoint が見つかりません。先に、メニュー「Stage7を自動で組み立てる」を押してください。", "OK");
            return;
        }
        var keyNpc = AssetDatabase.LoadAssetAtPath<GameObject>(KeyNpcPath);
        if (keyNpc == null)
        {
            EditorUtility.DisplayDialog("Stage7にボスを置く", "KeyNPC_Visual（" + KeyNpcPath + "）が見つかりません。", "OK");
            return;
        }

        var existing = GameObject.Find(BossName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Stage7にボスを置く",
                "すでに「" + BossName + "」があります。消して、置き直しますか？\n（Inspector で直した数字は、消えます）", "置き直す", "やめる"))
                return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("ボスを置く");

        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        Transform arena = spawnGo.transform.parent != null ? spawnGo.transform.parent : spawnGo.transform;
        var movie = Object.FindObjectOfType<BossIntroMovie>();

        string report;
        BossA boss = CreateBoss(arena, spawnGo.transform, movie, keyNpc, out report);
        ConnectGoal(Object.FindObjectOfType<StageGoal>(), boss);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (boss != null)
            Selection.activeGameObject = boss.gameObject;

        EditorUtility.DisplayDialog("Stage7にボスを置く", "ボスを置きました。Ctrl+S でシーンを保存してください。\n\n" + report, "OK");
    }

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/Stage7のゴールの見た目を消す")]
    public static void HideGoalVisuals()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            EditorUtility.DisplayDialog("Stage7のゴールの見た目を消す",
                "いま開いているシーンは「" + scene.name + "」です。Stage7 のシーンを開いてから押してください。", "OK");
            return;
        }
        var boss = Object.FindObjectOfType<BossA>();
        var goal = Object.FindObjectOfType<StageGoal>();
        if (boss == null || goal == null)
        {
            EditorUtility.DisplayDialog("Stage7のゴールの見た目を消す",
                "BossA か StageGoal が見つかりません。先に「Stage7にボスを置く」を押してください。", "OK");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("ゴールの見た目を消す");
        ConnectGoal(goal, boss);
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Stage7のゴールの見た目を消す",
            "ゴールの見た目（G O A L のキャラ・吹き出し・花火）と、ゴールの矢印を消しました。\nCtrl+S でシーンを保存してください。\n（BossA の Inspector の設定は、そのままです）", "OK");
    }

    /// <summary>ボス（BossA）を作って、置く。</summary>
    private static BossA CreateBoss(Transform parent, Transform spawn, BossIntroMovie movie, GameObject keyNpc, out string report)
    {
        report = "";
        float rootX = spawn.position.x;

        GameObject bossGo = new GameObject(BossName);
        Undo.RegisterCreatedObjectUndo(bossGo, BossName);
        bossGo.transform.SetParent(parent, false);
        bossGo.transform.position = new Vector3(rootX, Floor, 0f);

        // 主人公の攻撃は「Enemy」タグのコライダーに当たる。層は、追ってくる敵と同じにする
        bossGo.tag = "Enemy";
        var chaser = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPath);
        if (chaser != null)
            bossGo.layer = chaser.layer;

        // 体の絵
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(keyNpc, bossGo.transform);
        Undo.RegisterCreatedObjectUndo(vis, "BossVisual");
        vis.name = "BossVisual";
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localRotation = Quaternion.identity;
        Vector3 ns = keyNpc.transform.localScale;
        vis.transform.localScale = new Vector3(Mathf.Abs(ns.x) * BossScale, ns.y * BossScale, ns.z);

        // 当たり判定（トリガー）：体の絵の大きさから決める
        var col = Undo.AddComponent<BoxCollider2D>(bossGo);
        col.isTrigger = true;
        Vector2 size = new Vector2(2.8f, 4.6f);
        Vector2 offset = new Vector2(0f, 2.3f);
        Transform body = FindDeep(vis.transform, "Body");
        SpriteRenderer bodySr = body != null ? body.GetComponent<SpriteRenderer>() : null;
        if (bodySr != null && bodySr.sprite != null)
        {
            Bounds b = bodySr.bounds;
            float h = Mathf.Max(2f, b.max.y - Floor);
            size = new Vector2(Mathf.Max(1.5f, b.size.x), h);
            offset = new Vector2(b.center.x - rootX, h * 0.5f);
        }
        col.size = size;
        col.offset = offset;

        // ボス本体
        var boss = Undo.AddComponent<BossA>(bossGo);
        boss.visual = vis.transform;
        boss.visualFacesRight = false;
        boss.leftLimit = ArenaLeftWallX + 1.3f;
        boss.rightLimit = ArenaRightWallX - 1.3f;
        boss.groundY = Floor;
        boss.ceilingY = 5.3f;

        TMP_Text tmp = keyNpc.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null && tmp.font != null)
            boss.font = tmp.font;
        else
            report += "・フォントが見つかりませんでした。BossA の Font に、日本語のフォントを入れてください。\n";

        // 倒したときのごほうび（Aキー）
        var keyA = AssetDatabase.LoadAssetAtPath<KeyDefinition>(KeyAPath);
        if (keyA != null)
            boss.keyDrop = keyA;
        else
            report += "・Key_A（" + KeyAPath + "）が見つかりません。BossA の Key Drop に入れてください（空でも、持っているキー一覧から「A」を探します）。\n";

        // Kキーのヒント（画面の下の説明パネル）
        var hintUi = AssetDatabase.LoadAssetAtPath<GimmickIntroUI>(HintUiPath);
        if (hintUi != null)
            boss.hintUiPrefab = hintUi;
        else
            report += "・ヒントの説明パネル（" + HintUiPath + "）が見つかりません。BossA の Hint Ui Prefab に入れてください（空でも、シーンの GimmickIntroZone のものを使います）。\n";

        var stone = AssetDatabase.LoadAssetAtPath<GameObject>(FallingStonePath);
        if (stone != null)
        {
            var ssr = stone.GetComponentInChildren<SpriteRenderer>(true);
            if (ssr != null)
                boss.rockSprite = ssr.sprite;
        }
        if (boss.rockSprite == null)
            report += "・落石の絵が見つかりませんでした（灰色の丸い石になります）。BossA の Rock Sprite に入れると、きれいになります。\n";

        EditorUtility.SetDirty(boss);

        // ムービーが終わったら、戦いが始まる
        if (movie != null)
        {
            Undo.RecordObject(movie, "ムービーの終わりにボスをつなぐ");
            for (int i = movie.onMovieEnd.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                Object t = movie.onMovieEnd.GetPersistentTarget(i);
                if (t == null || t is BossA)
                    UnityEventTools.RemovePersistentListener(movie.onMovieEnd, i);
            }
            UnityEventTools.AddPersistentListener(movie.onMovieEnd, boss.BeginBattle);
            EditorUtility.SetDirty(movie);
            report += "・ムービーの終わり（On Movie End）に、BossA の BeginBattle をつなぎました。\n";
        }
        else
        {
            report += "・BossIntroMovie が見つかりません。ムービーの On Movie End に、BossA > BeginBattle を手でつないでください。\n";
        }

        report += "・ボス：" + BossName + "（体力 " + boss.maxHp.ToString("0") + "、位置 x=" + rootX.ToString("0.0") + "）。数字は Inspector で直せます。\n"
            + "・効果音は、BossA の Inspector の「効果音」に入れると鳴ります（空なら鳴りません）。\n"
            + "・倒すと Key_A がもらえて、いつものステージクリアの演出（キーが横から走ってくる）に進みます。檻（キー封じ）は、BossA の「キー封じの檻」で調整できます。\n"
            + "・ムービーなしで試したいときは、BossA の Auto Start Distance に 30 などを入れると、近づくだけで戦いが始まります。\n";
        return boss;
    }

    /// <summary>
    /// ゴールを「ボスを倒すとクリア」にして、ボスを割り当てる。
    /// ボスの部屋では、ゴールの見た目（G O A L のキャラ、吹き出し、花火）と、ゴールの方向を示す矢印は、いらないので消す。
    /// </summary>
    private static void ConnectGoal(StageGoal goal, BossA boss)
    {
        if (goal == null)
        {
            Debug.LogWarning("[Stage7Builder] StageGoal が見つかりません。ゴールの Mode を BossDefeated にして、Boss に BossA を入れてください。");
            return;
        }
        if (boss == null)
            return;

        Undo.RecordObject(goal, "ゴールをボス撃破にする");
        goal.mode = StageGoal.GoalMode.BossDefeated;
        goal.boss = boss.gameObject;
        EditorUtility.SetDirty(goal);

        // G O A L の演出（近づくと喜ぶ・吹き出し・花火）を止める
        StageGoalLetters[] letters = goal.GetComponentsInChildren<StageGoalLetters>(true);
        for (int i = 0; i < letters.Length; i++)
        {
            Undo.RecordObject(letters[i], "ゴールの演出を止める");
            letters[i].enabled = false;
            EditorUtility.SetDirty(letters[i]);
        }

        // ゴールの子（キャラ・吹き出し・花火など）を、まとめて隠す
        Transform gt = goal.transform;
        for (int i = 0; i < gt.childCount; i++)
        {
            GameObject child = gt.GetChild(i).gameObject;
            if (!child.activeSelf)
                continue;
            Undo.RecordObject(child, "ゴールの見た目を隠す");
            child.SetActive(false);
            EditorUtility.SetDirty(child);
        }

        // 前の版で、絵だけを消していた場合は、元にもどす（子ごと隠したので、見えない）
        SpriteRenderer[] rs = goal.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i].enabled)
                continue;
            Undo.RecordObject(rs[i], "ゴールの絵をもどす");
            rs[i].enabled = true;
            EditorUtility.SetDirty(rs[i]);
        }

        // ゴールの方向を示す矢印は、出さない
        GoalArrowIndicator arrow = Object.FindObjectOfType<GoalArrowIndicator>();
        if (arrow == null)
        {
            GameObject ag = new GameObject("GoalArrowOff");
            Undo.RegisterCreatedObjectUndo(ag, "GoalArrowOff");
            GameObject layoutRoot = GameObject.Find(RootName);
            if (layoutRoot != null)
                ag.transform.SetParent(layoutRoot.transform, false);
            arrow = Undo.AddComponent<GoalArrowIndicator>(ag);
        }
        Undo.RecordObject(arrow, "ゴールの矢印を消す");
        arrow.showArrow = false;
        EditorUtility.SetDirty(arrow);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeep(root.GetChild(i), name);
            if (r != null)
                return r;
        }
        return null;
    }

    [MenuItem("Tools/KEY ESCAPE/ステージ部品/Stage7の配置（StageLayout）を消す")]
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

    /// <summary>床から立つ壁。Ground を横にして、細く立てる（高さ約10）。</summary>
    private static GameObject Wall(string instanceName, float x, Transform parent)
    {
        GameObject w = Place("Ground", instanceName, x, Floor + 5.05f, parent);
        w.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        w.transform.localScale = new Vector3(0.5f, 1f, 1f);
        return w;
    }

    private static Transform Marker(string name, float x, float y, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        return go.transform;
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
        EditorUtility.SetDirty(cp);
        if (PrefabUtility.IsPartOfPrefabInstance(cp))
            PrefabUtility.RecordPrefabInstancePropertyModifications(cp);
    }

    /// <summary>Assets/Prefab/Emotes の中から、びっくりのエモートらしいものを探す。</summary>
    private static GameObject FindEmote()
    {
        if (!AssetDatabase.IsValidFolder(EmoteFolder))
            return null;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EmoteFolder });
        string[] words = { "びっくり", "ビックリ", "驚", "Surprise", "surprise", "Exclam", "exclam", "Alert", "alert", "Notice", "notice", "!", "！" };
        foreach (string w in words)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.Contains(w))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
        return null;
    }
}
