using System.Collections;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - 拠点（SelectScene）に出てくる、「バトルの塔」。
///
/// ボス戦（Required Cleared Stage Id）をクリアして拠点に戻ると、
///   1) カメラが塔の場所へ動く
///   2) 画面がゆれて、塔が地面からせり上がる
///   3) Kキーが「あの塔は一体…？とりあえず行ってみよう」と話す
///   4) カメラがプレイヤーに戻る
/// の順に演出が流れる。演出は、一度見たらセーブデータに記録して、二度と出さない。
/// 見終わったあとは、塔の前で F キーを押すと、掲示板と同じステージ選択（クエスト）が開く。
///
/// ・ボス戦をクリアするまでは、塔は出ない（絵もゾーンも無効）。
/// ・塔の場所は、このオブジェクト（BattleTower）を動かして決める。足もとは、自動で地面に合わせる。
/// ・セーブデータを消すと、演出がもう一度出る。
///
/// 付け方：メニュー Tools > KEY ESCAPE > ステージ部品 > 拠点に「バトルの塔」を作る を押すと、必要な物がそろって置かれる。
/// </summary>
public class BattleTower : MonoBehaviour
{
    [Header("出す条件")]
    [Tooltip("このステージ（StageCatalogのid）をクリア済みのときに、塔が出る。")]
    public string requiredClearedStageId = "Stage7";
    [Tooltip("演出を見たかの記録に使うID。後から変えない。")]
    public string appearId = "BattleTowerAppear";

    [Header("部品")]
    [Tooltip("塔の絵。")]
    public SpriteRenderer towerSprite;
    [Tooltip("塔の前で話しかけたときに、ステージ選択を開く部品。")]
    public StageSelectNPC npc;
    [Tooltip("話しかけられる範囲（Is TriggerのCollider2D）。")]
    public Collider2D talkArea;
    [Tooltip("Kキーの説明パネルのプレハブ（Assets/Prefab/GimmickIntroUI）。")]
    public GimmickIntroUI uiPrefab;

    [Header("地面")]
    [Tooltip("ONなら、このオブジェクトの真下にある地面に、塔の足もとを合わせる。")]
    public bool snapToGround = true;
    [Tooltip("地面を探す、いちばん下までの距離。")]
    public float groundSearchDistance = 20f;
    [Tooltip("塔を地面にどれだけうめるか（正の数で、下がる）。")]
    public float sinkAmount = 0.05f;

    [Header("セリフ")]
    [TextArea(2, 4)]
    public string message = "あの塔は一体…？とりあえず行ってみよう";

    [Header("演出")]
    [Tooltip("ロード画面が消えてから、演出が始まるまでの秒数。")]
    public float startDelay = 0.8f;
    [Tooltip("カメラが塔へ動く秒数。")]
    public float cameraMoveTime = 1.4f;
    [Tooltip("カメラが映す高さ（地面から）。")]
    public float focusHeight = 2.6f;
    [Tooltip("画面の広さの倍率。1.15なら、少し広く映す。0なら、変えない。")]
    public float zoomRatio = 1.15f;
    [Tooltip("塔がせり上がる秒数。")]
    public float riseSeconds = 3.0f;
    [Tooltip("せり上がる間の、画面のゆれの強さ。")]
    public float shakeAmount = 0.12f;
    [Tooltip("塔が出きってから、セリフが出るまでの秒数。")]
    public float afterRiseWait = 0.7f;
    [Tooltip("カメラがプレイヤーに戻る秒数。")]
    public float cameraReturnTime = 1.2f;
    [Tooltip("セリフが出てから、Spaceを受け付けるまでの秒数。")]
    public float inputDelay = 0.5f;

    [Header("テスト用")]
    [Tooltip("ONなら、ボス戦をクリアしていなくても、拠点に入るたびに演出が出る（記録は残さない）。リリース前に、必ずOFFにする。")]
    public bool testAlwaysPlay = false;

    private Transform _camPoint;
    private float _groundY;
    private float _finalSpriteY;
    private bool _running;

    private void Start()
    {
        if (towerSprite == null)
            towerSprite = GetComponentInChildren<SpriteRenderer>(true);
        if (npc == null)
            npc = GetComponent<StageSelectNPC>();
        if (talkArea == null)
            talkArea = GetComponent<Collider2D>();

        PlaceOnGround();

        bool cleared = testAlwaysPlay || StageProgress.IsCleared(requiredClearedStageId);
        bool seen = !testAlwaysPlay && HasSeen();

        if (cleared && seen)
        {
            SetUsable(true);
            return;
        }

        SetUsable(false);
        if (towerSprite != null)
            towerSprite.enabled = false;

        if (cleared)
            StartCoroutine(AppearRoutine());
    }

    /// <summary>塔の足もとを、地面に合わせる。カメラが映す点も、ここで決める。</summary>
    private void PlaceOnGround()
    {
        _groundY = transform.position.y;
        if (snapToGround)
        {
            Vector2 origin = new Vector2(transform.position.x, transform.position.y + 0.5f);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, groundSearchDistance + 0.5f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D c = hits[i].collider;
                if (c == null || c.isTrigger || c.transform.IsChildOf(transform) || c.CompareTag("Player"))
                    continue;
                _groundY = hits[i].point.y;
                break;
            }
        }

        if (towerSprite != null)
        {
            // 絵の下の端が、地面の高さ（少しうめる）に来るように、絵を動かす
            float bottom = towerSprite.bounds.min.y;
            Vector3 p = towerSprite.transform.position;
            p.y += (_groundY - sinkAmount) - bottom;
            towerSprite.transform.position = p;
            _finalSpriteY = p.y;
        }

        if (_camPoint == null)
        {
            GameObject cp = new GameObject("CameraPoint");
            _camPoint = cp.transform;
            _camPoint.SetParent(transform, true);
        }
        _camPoint.position = new Vector3(transform.position.x, _groundY + focusHeight, 0f);
    }

    private bool HasSeen()
    {
        return SaveManager.Data != null && SaveManager.Data.seenIntros != null
               && SaveManager.Data.seenIntros.Contains(appearId);
    }

    private void MarkSeen()
    {
        if (testAlwaysPlay)
            return;
        if (SaveManager.Data != null && SaveManager.Data.seenIntros != null
            && !SaveManager.Data.seenIntros.Contains(appearId))
        {
            SaveManager.Data.seenIntros.Add(appearId);
            SaveManager.Save();
        }
    }

    /// <summary>塔が使える状態か（絵が見えて、話しかけられる）。</summary>
    private void SetUsable(bool usable)
    {
        if (towerSprite != null)
            towerSprite.enabled = usable;
        if (npc != null)
            npc.enabled = usable;
        if (talkArea != null)
            talkArea.enabled = usable;
    }

    private IEnumerator AppearRoutine()
    {
        if (_running)
            yield break;
        _running = true;

        // ロード画面が消えるのを待つ
        yield return null;
        while (LoadingScreen.IsLoading)
            yield return null;
        yield return new WaitForSeconds(Mathf.Max(0f, startDelay));

        // プレイヤーを探す（キーコンフィグのプレビュー用は除く）
        PlayerMovement pm = null;
        float waited = 0f;
        while (pm == null && waited < 3f)
        {
            GameObject g = NewGameIntro.FindMainPlayerObject();
            if (g != null)
                pm = g.GetComponent<PlayerMovement>();
            if (pm == null)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        GamePause.Acquire(this);
        if (pm != null)
            pm.SetInputLocked(true);

        CameraFollow cam = FindObjectOfType<CameraFollow>();
        Transform originalTarget = cam != null ? cam.Target : null;
        if (cam != null)
            cam.followUnscaled = true;

        // --- 1) カメラが塔の場所へ ---
        if (cam != null)
        {
            cam.MoveTo(_camPoint, cameraMoveTime);
            if (zoomRatio > 0f)
                cam.ZoomTo(zoomRatio, 0.8f);
        }
        yield return WaitUnscaled(cameraMoveTime + 0.4f);

        // --- 2) ゆれて、塔がせり上がる ---
        yield return RiseRoutine(cam);
        yield return WaitUnscaled(afterRiseWait);

        // --- 3) Kキーが話す ---
        GimmickIntroUI ui = null;
        if (uiPrefab != null)
            ui = Instantiate(uiPrefab);
        else
            Debug.LogWarning("[BattleTower] Ui Prefabが空です。セリフが出ません。", this);

        if (ui != null)
        {
            SoundEffects.Play(SEId.DialogueOpen);
            ui.Show(message);

            float t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                bool typing = ui.IsTyping;
                ui.SetPromptVisible(!typing && t >= inputDelay);
                if (t >= inputDelay && Input.GetKeyDown(KeyCode.Space))
                {
                    if (typing)
                        ui.CompleteText();
                    else
                        break;
                }
                yield return null;
            }
            SoundEffects.Play(SEId.DialogueAdvance);
            ui.Hide();
            SoundEffects.Play(SEId.DialogueClose);
        }

        // --- 4) カメラをプレイヤーに戻す ---
        if (cam != null)
        {
            cam.MoveTo(originalTarget, cameraReturnTime);
            if (zoomRatio > 0f)
                cam.ZoomTo(1f, 0.8f);
        }
        yield return WaitUnscaled(Mathf.Max(cameraReturnTime, 0.8f) + 0.2f);

        if (cam != null)
            cam.followUnscaled = false;
        if (ui != null)
            Destroy(ui.gameObject);
        if (pm != null)
            pm.SetInputLocked(false);
        GamePause.Release(this);

        SetUsable(true);
        MarkSeen();
        _running = false;
    }

    /// <summary>塔を、地面（のマスク）の下から、上へせり上げる。</summary>
    private IEnumerator RiseRoutine(CameraFollow cam)
    {
        if (towerSprite == null)
            yield break;

        float height = towerSprite.bounds.size.y;
        float width = towerSprite.bounds.size.x;
        Transform tr = towerSprite.transform;
        float startY = _finalSpriteY - height - 0.1f;
        float baseX = tr.position.x;

        // 地面より下は見えないように、マスクを置く（地面の高さから上だけ見える）
        GameObject maskGo = new GameObject("RiseMask");
        maskGo.transform.SetParent(transform, true);
        maskGo.transform.position = new Vector3(baseX, _groundY, 0f);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Color[] px = { Color.white, Color.white, Color.white, Color.white };
        tex.SetPixels(px);
        tex.Apply();
        Sprite maskSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 2f);
        SpriteMask mask = maskGo.AddComponent<SpriteMask>();
        mask.sprite = maskSprite;
        maskGo.transform.localScale = new Vector3(width + 4f, height + 6f, 1f);

        towerSprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        tr.position = new Vector3(baseX, startY, tr.position.z);
        towerSprite.enabled = true;

        if (cam != null)
            cam.ShakeCamera(riseSeconds + 0.4f, shakeAmount);

        float t = 0f;
        float dur = Mathf.Max(0.1f, riseSeconds);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k);   // はじめ速く、だんだんゆっくり
            float jitter = (1f - k) * 0.05f * Mathf.Sin(t * 70f);
            tr.position = new Vector3(baseX + jitter, Mathf.Lerp(startY, _finalSpriteY, e), tr.position.z);
            yield return null;
        }

        tr.position = new Vector3(baseX, _finalSpriteY, tr.position.z);
        towerSprite.maskInteraction = SpriteMaskInteraction.None;
        Destroy(maskGo);
        Destroy(maskSprite);
        Destroy(tex);
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        // 途中で消えても、止めたままにならないようにする
        if (_running)
        {
            GamePause.Release(this);
            CameraFollow cam = FindObjectOfType<CameraFollow>();
            if (cam != null)
                cam.followUnscaled = false;
        }
    }
}
