using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - ステージ7（ボス戦）を作る。メニューは3つ。
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
            + "・ボスの部屋は、x=96〜136 にあります。ゴールは、仮に部屋の右端（x=133）です。\n"
            + "・びっくりのエモート：" + (movie.surpriseEmote != null ? movie.surpriseEmote.name : "見つかりませんでした（Surprise Emote に入れてください）") + "\n",
            "OK");
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
