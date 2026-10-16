using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// チェックポイントのプレハブ（Assets/Prefab/Object/Checkpoint.prefab）を、旗の絵（hata.png）から組み直す。
///
/// 組み上がる形：
///   Checkpoint        … 触れる範囲（Is Trigger）と Checkpoint スクリプト
///   ├ Flag            … 赤い旗。最初からずっと表示。なびくアニメ付き
///   └ RespawnPoint    … 復活する位置
/// 旗の上のエモート（通る前／通った後）は、Checkpoint の Inspector にプレハブを入れて決める。
/// すでにあるプレハブに入れていたエモートなどの設定は、作り直しても引き継ぐ。
///
/// メニュー：Tools > KEY ESCAPE > チェックポイント > 旗のプレハブを作る
/// 何度実行しても同じ形に作り直される（プレハブの管理番号GUIDは変わらないので、置いた物の参照は外れない）。
/// </summary>
public static class CheckpointBuilder
{
    private const string PrefabPath = "Assets/Prefab/Object/Checkpoint.prefab";
    private const string SheetPath = "Assets/sprite/UI/hata.png";
    private const string ClipPath = "Assets/sprite/UI/hatacheckpoint.anim";
    private const string ControllerPath = "Assets/sprite/UI/CheckpointFlag.controller";

    // 旗の大きさ（元の絵は大きいので縮める）と、なびく速さ。
    private const float FlagScale = 0.2079f;
    private const float FramesPerSecond = 12f;

    // 旗の根元が、置いた位置の地面に来るようにずらす量（絵の中心が基準になっているため）。
    private const float FlagOffsetX = 1.02f;
    private const float FlagOffsetY = 1.10f;

    // 触れる範囲（縦に長い箱）と、復活する位置。
    private static readonly Vector2 TriggerSize = new Vector2(1.5f, 6f);
    private static readonly Vector2 TriggerOffset = new Vector2(0f, 3f);
    private static readonly Vector3 RespawnLocalPos = new Vector3(0f, 0.6f, 0f);

    [MenuItem("Tools/KEY ESCAPE/チェックポイント/旗のプレハブを作る")]
    public static void Build()
    {
        List<Sprite> frames = LoadFrames();
        if (frames.Count == 0)
        {
            Debug.LogError("[CheckpointBuilder] " + SheetPath + " から旗の絵が読み込めませんでした。");
            return;
        }

        AnimationClip clip = BuildClip(frames);
        AnimatorController controller = BuildController(clip);
        GameObject saved = BuildPrefab(frames[0], controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        Debug.Log("[CheckpointBuilder] " + PrefabPath + " を作りました（旗の絵 " + frames.Count + " コマ）。");
    }

    // ---- 絵 -----------------------------------------------------------

    /// <summary>hata.png を切り分けた絵（hata_0, hata_1 …）を、番号順に集める。</summary>
    private static List<Sprite> LoadFrames()
    {
        return AssetDatabase.LoadAllAssetsAtPath(SheetPath)
            .OfType<Sprite>()
            .OrderBy(s => NumberOf(s.name))
            .ToList();
    }

    private static int NumberOf(string spriteName)
    {
        int i = spriteName.LastIndexOf('_');
        int n;
        if (i >= 0 && int.TryParse(spriteName.Substring(i + 1), out n))
            return n;
        return int.MaxValue;
    }

    // ---- アニメーション -------------------------------------------------

    private static AnimationClip BuildClip(List<Sprite> frames)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        bool isNew = clip == null;
        if (isNew)
            clip = new AnimationClip { name = "hatacheckpoint" };

        clip.frameRate = FramesPerSecond;

        // 旗は Flag の SpriteRenderer の絵を、順番に切り替える。
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        float step = 1f / FramesPerSecond;
        var keys = new ObjectReferenceKeyframe[frames.Count + 1];
        for (int i = 0; i < frames.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * step, value = frames[i] };
        // 最後のコマも1コマ分の長さにするための印（ループの長さを決める）。
        keys[frames.Count] = new ObjectReferenceKeyframe { time = frames.Count * step, value = frames[frames.Count - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew)
            AssetDatabase.CreateAsset(clip, ClipPath);
        else
            EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController BuildController(AnimationClip clip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);

        // すでにあるなら、中身のアニメだけ差し替える。
        if (controller.layers.Length == 0)
            controller.AddLayer("Base Layer");
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState state = sm.defaultState;
        if (state == null)
            state = sm.AddState(clip.name);
        state.motion = clip;
        sm.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ---- プレハブ -------------------------------------------------------

    private static GameObject BuildPrefab(Sprite firstFrame, AnimatorController controller)
    {
        // 作り直す前に、いま入っている設定（順番・エモートなど）を控えておく。
        Checkpoint old = null;
        GameObject oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (oldPrefab != null)
            old = oldPrefab.GetComponent<Checkpoint>();
        bool hasOld = old != null;
        string oldTag = hasOld ? old.playerTag : null;
        int oldOrder = hasOld ? old.order : 0;
        GameObject oldBefore = hasOld ? old.emoteBeforePrefab : null;
        GameObject oldAfter = hasOld ? old.emoteAfterPrefab : null;
        Vector3 oldOffset = hasOld ? old.emoteOffset : Vector3.zero;
        float oldScale = hasOld ? old.emoteScale : 1f;
        float oldDuration = hasOld ? old.emoteAfterDuration : 0f;
        int oldBoost = hasOld ? old.emoteSortingBoost : 100;

        var root = new GameObject("Checkpoint");
        try
        {
            // 触れる範囲
            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = TriggerSize;
            box.offset = TriggerOffset;

            // 赤い旗（最初からずっと表示。なびくアニメ付き）
            GameObject flag = MakeFlag("Flag", root.transform, firstFrame, Color.white);
            var animator = flag.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // 復活位置
            var respawn = new GameObject("RespawnPoint");
            respawn.transform.SetParent(root.transform, false);
            respawn.transform.localPosition = RespawnLocalPos;

            // Checkpoint スクリプト
            var cp = root.AddComponent<Checkpoint>();
            cp.respawnPoint = respawn.transform;
            cp.inactiveVisual = null;
            cp.activeVisual = null;
            if (hasOld)
            {
                cp.playerTag = oldTag;
                cp.order = oldOrder;
                cp.emoteBeforePrefab = oldBefore;
                cp.emoteAfterPrefab = oldAfter;
                cp.emoteOffset = oldOffset;
                cp.emoteScale = oldScale;
                cp.emoteAfterDuration = oldDuration;
                cp.emoteSortingBoost = oldBoost;
            }

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject MakeFlag(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(FlagOffsetX, FlagOffsetY, 0f);
        go.transform.localScale = new Vector3(FlagScale, FlagScale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = -1; // プレイヤーより後ろに描く
        return go;
    }
}
