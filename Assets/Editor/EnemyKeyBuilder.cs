using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 手足の生えた敵キーのプレハブを新しく作る（既存のプレハブ・シーンは触らない）。
///
/// メニュー Tools > KEY ESCAPE > 敵キー から、次のどちらかを作る:
///   ・巡回タイプ（Enemy.cs）      → Assets/Prefab/Mob/Enemy/KeyEnemyRig_Patrol.prefab
///   ・追跡・攻撃タイプ（Ally.cs）  → Assets/Prefab/Mob/Enemy/KeyEnemyRig_Chaser.prefab
///   ・会話・待機のみ（見た目だけ） → Assets/Prefab/Mob/Enemy/KeyNPC_Visual.prefab
///       DialogueKeyRecruit / KeyPickup のオブジェクトの子として置く。物理や敵のスクリプトは付かない。
///       既存の KeyEnemy.prefab があれば、その Ally の設定（keyDrop・弾・攻撃距離など）を引き継ぐ。
///
/// 階層:
///   KeyEnemyRig（Enemy/Ally・Rigidbody2D・Collider・Animator・LimbRig）
///    ├ FallCheck / WallCheck（Enemyのみ） / AttackCheck（Allyのみ）
///    └ Visual（原点は足元）
///        ├ LegL / LegR / ArmL / ArmR
///        └ Body ─ Label（TextMeshPro + KeyLabel：キーの文字）
/// </summary>
public static class EnemyKeyBuilder
{
    private const string ArtFolder = "Assets/Art/EnemyKey";
    private const string PrefabFolder = "Assets/Prefab/Mob/Enemy";
    private const string ControllerPath = ArtFolder + "/KeyEnemyRig.controller";
    private const string OldAllyPrefabPath = PrefabFolder + "/KeyEnemy.prefab";
    private const string SlipperyMaterialGuid = "73035f974857c94489896440d1e2b9e8";

    /// <summary>ルートの拡大率。Enemy.cs/Ally.cs が死亡時に決め打ちで変えるコライダー(size 1x0.25, offset 0,-0.8)の大きさに合わせてある。</summary>
    private const float RootScale = 0.5f;

    /// <summary>足元のローカルY。死亡時コライダーの下端(-0.8-0.125)と一致させ、倒れても浮かない。</summary>
    private const float FeetY = -0.925f;

    [MenuItem("Tools/KEY ESCAPE/敵キー/プレハブを作成（巡回タイプ : Enemy）")]
    public static void BuildPatrol()
    {
        Build(false);
    }

    [MenuItem("Tools/KEY ESCAPE/敵キー/プレハブを作成（追跡・攻撃タイプ : Ally）")]
    public static void BuildChaser()
    {
        Build(true);
    }

    [MenuItem("Tools/KEY ESCAPE/敵キー/プレハブを作成（会話・待機のみ）")]
    public static void BuildNpc()
    {
        EnemyKeyImporter.ApplyImportSettings();

        Sprite body = LoadSprite("keybody");
        Sprite arm = LoadSprite("arm");
        Sprite leg = LoadSprite("leg");
        if (body == null || arm == null || leg == null)
        {
            EditorUtility.DisplayDialog("敵キー", "素材が見つかりません。\n" + ArtFolder + " に KeyBody / Arm / Leg（LegVer2 など）のPNGを入れてください。", "OK");
            return;
        }

        string prefabPath = PrefabFolder + "/KeyNPC_Visual.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog("上書き確認", prefabPath + " はすでにあります。作り直して上書きしますか？", "上書きする", "キャンセル"))
                return;
        }

        // ---- 寸法（ローカル単位）。原点は足元 ----
        Vector2 bs = body.bounds.size;
        float legH = leg.bounds.size.y;
        float hipY = legH - 0.10f;
        float bodyY = hipY + bs.y * 0.5f - 0.06f;
        float armX = bs.x * 0.35f;
        float armY = bodyY + bs.y * 0.02f;
        float legX = bs.x * 0.20f;

        GameObject root = new GameObject("KeyNPC_Visual");
        root.transform.localScale = Vector3.one * RootScale;

        GameObject visual = MakeChild(root.transform, "Visual", Vector3.zero);

        GameObject legL = MakeChild(visual.transform, "LegL", new Vector3(-legX, hipY, 0f));
        AddSprite(legL, leg, 1, false);
        GameObject legR = MakeChild(visual.transform, "LegR", new Vector3(legX, hipY, 0f));
        AddSprite(legR, leg, 1, true);  // 右脚は反転（ハ字立ち）

        GameObject armL = MakeChild(visual.transform, "ArmL", new Vector3(-armX, armY, 0f));
        AddSprite(armL, arm, 1, true);
        GameObject armR = MakeChild(visual.transform, "ArmR", new Vector3(armX, armY, 0f));
        AddSprite(armR, arm, 1, false);

        GameObject bodyGo = MakeChild(visual.transform, "Body", new Vector3(0f, bodyY, 0f));
        AddSprite(bodyGo, body, 2, false);

        GameObject labelGo = MakeChild(bodyGo.transform, "Label", new Vector3(-bs.x * 0.015f, bs.y * 0.12f, 0f));
        TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.text = "A";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.5f;
        tmp.fontSizeMax = 8f;
        tmp.enableWordWrapping = false;
        tmp.rectTransform.sizeDelta = new Vector2(bs.x * 0.55f, bs.y * 0.5f);
        MeshRenderer labelRenderer = labelGo.GetComponent<MeshRenderer>();
        if (labelRenderer != null) labelRenderer.sortingOrder = 3;
        labelGo.AddComponent<KeyLabel>();

        LimbRig rig = root.AddComponent<LimbRig>();
        rig.visual = visual.transform;
        rig.armL = armL.transform;
        rig.armR = armR.transform;
        rig.legL = legL.transform;
        rig.legR = legR.transform;
        rig.spriteFacesRight = false;
        rig.idleOnly = true;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        EditorUtility.DisplayDialog("敵キー", "作成しました:\n" + prefabPath + "\n\n会話キー（DialogueKeyRecruit）のオブジェクトの子としてシーンに置き、元のSpriteRendererは外してください。\n文字は、親の keyDefinition から自動で入ります。", "OK");
    }

    private static void Build(bool chaser)
    {
        EnemyKeyImporter.ApplyImportSettings();

        Sprite body = LoadSprite("keybody");
        Sprite arm = LoadSprite("arm");
        Sprite leg = LoadSprite("leg");
        if (body == null || arm == null || leg == null)
        {
            EditorUtility.DisplayDialog("敵キー", "素材が見つかりません。\n" + ArtFolder + " に KeyBody / Arm / Leg（LegVer2 など）のPNGを入れてください。", "OK");
            return;
        }

        string prefabPath = PrefabFolder + (chaser ? "/KeyEnemyRig_Chaser.prefab" : "/KeyEnemyRig_Patrol.prefab");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog("上書き確認", prefabPath + " はすでにあります。作り直して上書きしますか？\n（シーンに置いたものは、プレハブ側の変更が反映されます）", "上書きする", "キャンセル"))
                return;
        }

        AnimatorController controller = EnsureController();
        GameObject old = AssetDatabase.LoadAssetAtPath<GameObject>(OldAllyPrefabPath);

        // ---- 寸法（ローカル単位）----
        Vector2 bs = body.bounds.size;
        float legH = leg.bounds.size.y;
        float hipY = legH - 0.10f;                    // 脚の付け根（体の下に少し隠れる）
        float bodyY = hipY + bs.y * 0.5f - 0.06f;     // 体の中心
        float armX = bs.x * 0.35f;
        float armY = bodyY + bs.y * 0.02f;
        float legX = bs.x * 0.20f;
        float totalH = bodyY + bs.y * 0.5f;           // 足元からキー上端まで
        float face = chaser ? 1f : -1f;               // +1 = 右向き, -1 = 左向き（scale.x が +1 のとき）

        // ---- ルート ----
        GameObject root = new GameObject(chaser ? "KeyEnemyRig_Chaser" : "KeyEnemyRig_Patrol");
        root.transform.localScale = Vector3.one * RootScale;
        root.layer = old != null ? old.layer : 9;
        TrySetTag(root, old != null ? old.tag : "Enemy");

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 5f;
        rb.mass = 1f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        string matPath = AssetDatabase.GUIDToAssetPath(SlipperyMaterialGuid);
        if (!string.IsNullOrEmpty(matPath))
            rb.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(matPath);

        CapsuleCollider2D capsule = root.AddComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.size = new Vector2(bs.x * 0.8f, totalH);
        capsule.offset = new Vector2(0f, FeetY + totalH * 0.5f);

        Animator anim = root.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        // ---- チェック用の目印（Enemy / Ally が名前で探す）----
        if (!chaser)
        {
            MakeChild(root.transform, "FallCheck", new Vector3(face * 1.1f, FeetY - 0.1f, 0f));
            MakeChild(root.transform, "WallCheck", new Vector3(face * (bs.x * 0.5f + 0.1f), FeetY + totalH * 0.5f, 0f));
        }
        else
        {
            MakeChild(root.transform, "AttackCheck", new Vector3(face * 1.0f, FeetY + totalH * 0.45f, 0f));
        }

        // ---- 見た目 ----
        GameObject visual = MakeChild(root.transform, "Visual", new Vector3(0f, FeetY, 0f));

        // 脚の絵は右上の付け根から左下へ流れる弧。左脚はそのまま、右脚を反転して左右対称のハ字立ちにする
        GameObject legL = MakeChild(visual.transform, "LegL", new Vector3(-legX, hipY, 0f));
        AddSprite(legL, leg, 1, false);
        GameObject legR = MakeChild(visual.transform, "LegR", new Vector3(legX, hipY, 0f));
        AddSprite(legR, leg, 1, true);

        GameObject armL = MakeChild(visual.transform, "ArmL", new Vector3(-armX, armY, 0f));
        AddSprite(armL, arm, 1, true);  // 腕は右腕向きの絵。左腕は左右反転
        GameObject armR = MakeChild(visual.transform, "ArmR", new Vector3(armX, armY, 0f));
        AddSprite(armR, arm, 1, false);

        GameObject bodyGo = MakeChild(visual.transform, "Body", new Vector3(0f, bodyY, 0f));
        AddSprite(bodyGo, body, 2, false);

        // ---- キートップの文字 ----
        GameObject labelGo = MakeChild(bodyGo.transform, "Label", new Vector3(-bs.x * 0.015f, bs.y * 0.12f, 0f));
        TextMeshPro tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.text = "A";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.5f;
        tmp.fontSizeMax = 8f;
        tmp.enableWordWrapping = false;
        tmp.rectTransform.sizeDelta = new Vector2(bs.x * 0.55f, bs.y * 0.5f);
        MeshRenderer labelRenderer = labelGo.GetComponent<MeshRenderer>();
        if (labelRenderer != null) labelRenderer.sortingOrder = 3;
        labelGo.AddComponent<KeyLabel>();

        // ---- 動かすスクリプト ----
        LimbRig rig = root.AddComponent<LimbRig>();
        rig.visual = visual.transform;
        rig.armL = armL.transform;
        rig.armR = armR.transform;
        rig.legL = legL.transform;
        rig.legR = legR.transform;
        rig.spriteFacesRight = chaser;

        // ---- Enemy / Ally 本体 ----
        if (!chaser)
        {
            Enemy enemy = root.AddComponent<Enemy>();
            enemy.life = 15f;
            enemy.speed = 5f;
            enemy.turnLayerMask = LayerMask.GetMask("Default");
        }
        else
        {
            Ally ally = root.AddComponent<Ally>();
            Ally oldAlly = old != null ? old.GetComponent<Ally>() : null;
            if (oldAlly != null)
                EditorUtility.CopySerialized(oldAlly, ally);
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);

        string extra = chaser
            ? "既存の KeyEnemy の Ally 設定を引き継ぎました。keyDrop が入っているか確認してください。"
            : "keyDrop（倒すと手に入るキー）を設定すると、その文字がキートップに出ます。";
        EditorUtility.DisplayDialog("敵キー", "作成しました:\n" + prefabPath + "\n\n" + extra, "OK");
    }

    private static AnimatorController EnsureController()
    {
        AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (c != null)
            return c;

        c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        string[] names = { "IsDead", "Hit", "IsWaiting", "IsJumping", "IsDashing", "Attack" };
        for (int i = 0; i < names.Length; i++)
            c.AddParameter(names[i], AnimatorControllerParameterType.Bool);
        c.layers[0].stateMachine.AddState("Idle");
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        return c;
    }

    private static Sprite LoadSprite(string namePrefix)
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { ArtFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path).ToLowerInvariant().StartsWith(namePrefix))
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return null;
    }

    private static GameObject MakeChild(Transform parent, string childName, Vector3 localPos)
    {
        GameObject g = new GameObject(childName);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        return g;
    }

    private static void AddSprite(GameObject g, Sprite s, int order, bool flipX)
    {
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        sr.flipX = flipX;
    }

    private static void TrySetTag(GameObject g, string tag)
    {
        try { g.tag = tag; }
        catch (UnityException) { Debug.LogWarning("[EnemyKey] タグ '" + tag + "' が無いので設定しませんでした"); }
    }
}
