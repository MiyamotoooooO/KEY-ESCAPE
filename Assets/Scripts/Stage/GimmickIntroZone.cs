using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - ギミックやチェックポイントの紹介。
/// プレイヤーがこの範囲に入ると、ゲームが止まり、カメラが指定した場所（トゲなど）へ動いて、
/// 画面の下に説明が出る（文字は1文字ずつ出て、キーのキャラクターが話す）。
/// 文字が出ている間にSpaceキーを押すと全文が出て、もう一度押すと次の手順へ進む。最後まで進むとカメラが戻ってゲームが再開する。
///
/// ・手順（Steps）は、いくつでも並べられる。手順ごとに「カメラが映す場所」「説明」「ズーム」を決める。
///   カメラが映す場所を空にすると、カメラはプレイヤーのまま（チェックポイントの説明など、文だけを出したいとき）。
/// ・ゲームを止めている間は、クリアタイムに入らない。
/// ・Show Once がONなら、一度最後まで見たらセーブデータに記録して、二度と出さない。
///   セーブデータを消すと、また出る（テスト用）。
/// ・Intro Id が空なら、シーンの名前＋このオブジェクトの名前が、自動でIDになる。
///
/// 付け方：Is Triggerをオンにした Collider2D と、このスクリプトを付ける（プレハブ GimmickIntroZone を置くのが簡単）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GimmickIntroZone : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        [Tooltip("カメラが映す場所（シーンの中のオブジェクト。トゲそのものでもよい）。空ならカメラはプレイヤーのまま。")]
        public Transform cameraPoint;
        [TextArea(2, 5)]
        [Tooltip("画面の下に出す説明。")]
        public string message = "";
        [Tooltip("ズームの倍率。0なら変えない。0.7なら画面の広さが7割（拡大）、1.3なら広く映す。")]
        public float zoomRatio = 0f;
        [Tooltip("空なら、出さない。入れると、紹介がぜんぶ終わったあと、画面の左上に「・（この文）」が出る。「カメラが映す場所」にプレイヤーが近づくと、消える（カメラが映す場所が空だと、消えない）。")]
        public string objectiveText = "";
        [Tooltip("「カメラが映す場所」から、この距離以内に入ると、左上の文が消える（ワールドの単位）。")]
        public float objectiveRadius = 1.5f;
        [Tooltip("None（初期値）なら、いつもどおり Space で次へ進む。キーを選ぶと、そのキーを押さないと次へ進めない（Spaceを含め、ほかのキーは受け付けない）。例：掲示板の前で、F。")]
        public KeyCode requiredKey = KeyCode.None;
    }

    [Header("基本")]
    [Tooltip("この紹介のID。空なら、シーン名＋オブジェクト名が自動で入る。")]
    public string introId = "";
    [Tooltip("ONなら、一度最後まで見たら、二度と出さない（セーブデータに記録する）。")]
    public bool showOnce = true;
    public string playerTag = "Player";

    [Header("手順")]
    public Step[] steps = new Step[1];

    [Header("表示")]
    [Tooltip("説明パネルのプレハブ（Assets/Prefab/GimmickIntroUI）。")]
    public GimmickIntroUI uiPrefab;

    [Header("シーンが始まったとき、自動で出す（拠点に戻ったときなど）")]
    [Tooltip("ONなら、プレイヤーが範囲に入らなくても、シーンが始まって（ロード画面が消えて）少ししたら、自動で出す。この場合、このゾーンの範囲（コライダー）は使わない（自動でOFFになる）。")]
    public bool playOnSceneStart = false;
    [Tooltip("空でなければ、このステージ（StageCatalogのid。例：Tutorial）をクリア済みのときだけ出す。Show Once がONなら、最初に出したあとは出ない。")]
    public string requiredClearedStageId = "";
    [Tooltip("ロード画面が消えてから、出すまでの秒数。")]
    public float autoStartDelay = 0.6f;

    [Header("止まる位置（掲示板の前などで、確実に止めたいとき）")]
    [Tooltip("入れると、範囲に入ったあと、プレイヤーが自動でこの場所まで歩いて止まり、そこで会話が始まる（範囲の端で止まらない）。この場所より右にいたときは、左へ戻る。空なら、範囲に入ったその場で止まる。")]
    public Transform standPoint;
    [Tooltip("止まる位置まで歩く速さ。")]
    public float standWalkSpeed = 4f;
    [Tooltip("止まる位置から、これだけ離れていたら、まだ歩く（ワールドの単位）。")]
    public float standTolerance = 0.1f;
    [Tooltip("止まる位置まで歩くのを待つ、最長の秒数（引っかかって止まったときに、先へ進むため）。")]
    public float standWalkMaxSeconds = 6f;

    [Header("終わったあと")]
    [Tooltip("ONなら、紹介がぜんぶ終わったあとに、掲示板（ステージ選択）を開く。なお、最後の手順の Required Key が F のときは、ここがOFFでも、自動で掲示板を開く（Fキーは、掲示板のキーなので）。")]
    public bool openStageSelectAtEnd = false;

    [Header("掲示板を開いたあと（ステージ選択の画面で、キーが話す）")]
    [TextArea(2, 4)]
    [Tooltip("掲示板（ステージ選択）が開いたあと、画面の下に、キーの説明パネルでこの文を出す。Spaceで消える。空なら、出さない。")]
    public string stageSelectMessage = "";
    [Tooltip("ONなら、掲示板を開いたあと、ステージを選んで出発するまで、戻れなくする（戻るボタンも、Escキーも効かない）。「挑戦しますか？」の「いいえ」は効くので、選びなおせる。")]
    public bool lockStageSelectBack = true;

    [Header("左上のヒント（手順の Objective Text を入れたとき）")]
    [Tooltip("ONなら、横（X）の距離だけで、着いたかを決める（高さは気にしない）。OFFなら、縦も含めた距離。")]
    public bool objectiveHorizontalOnly = true;
    public ObjectiveHintStyle objectiveStyle = new ObjectiveHintStyle();
    [Tooltip("任意。左上の文字のフォント。空なら、シーンにある日本語のフォントを自動で探す。")]
    public TMP_FontAsset objectiveFont;

    [Header("調整")]
    [Tooltip("カメラが目的地に着いたとみなす距離。")]
    public float cameraArriveDistance = 0.15f;
    [Tooltip("カメラが目的地に着くのを待つ最長の秒数。")]
    public float cameraMoveMaxTime = 2.5f;
    [Tooltip("説明が出てから、Spaceキーを受け付けるまでの秒数（ジャンプのSpaceで、うっかり飛ばさないため）。Spaceキーの案内も、この秒数がたつまで出ない。")]
    public float inputDelay = 0.5f;
    [Tooltip("ズームにかける秒数。")]
    public float zoomTime = 0.6f;

    private bool _running;
    private bool _done;
    private PlayerMovement _player;
    private CameraFollow _cam;
    private Transform _originalTarget;
    private GimmickIntroUI _ui;

    // 左上に出している、まだ着いていないヒント（手順の番号）
    private readonly System.Collections.Generic.List<int> _activeObjectives = new System.Collections.Generic.List<int>();
    private Transform _objectivePlayer;

    private string Id
    {
        get
        {
            if (!string.IsNullOrEmpty(introId))
                return introId;
            return SceneManager.GetActiveScene().name + "/" + gameObject.name;
        }
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Start()
    {
        if (showOnce && SaveManager.Data != null && SaveManager.Data.seenIntros != null
            && SaveManager.Data.seenIntros.Contains(Id))
        {
            _done = true;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.enabled = false;

            // 紹介は見たけれど、左上のヒントの場所に、まだ着いていないとき：もう一度、左上に出す
            ActivateObjectives();
        }
        else if (playOnSceneStart)
        {
            // 範囲に入ったときではなく、シーンが始まったときに出す（範囲は使わない）
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.enabled = false;
            StartCoroutine(AutoStart());
        }
    }

    /// <summary>シーンが始まったら、条件（クリア済みのステージ）を見て、ロード画面が消えるのを待ってから、紹介を始める。</summary>
    private IEnumerator AutoStart()
    {
        if (steps == null || steps.Length == 0)
            yield break;
        if (!string.IsNullOrEmpty(requiredClearedStageId) && !StageProgress.IsCleared(requiredClearedStageId))
            yield break;

        yield return null;
        while (LoadingScreen.IsLoading)
            yield return null;
        yield return new WaitForSeconds(Mathf.Max(0f, autoStartDelay));

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

        if (_running || _done)
            yield break;
        StartCoroutine(Run(pm));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_running || _done || !other.CompareTag(playerTag))
            return;
        if (steps == null || steps.Length == 0)
            return;

        PlayerMovement pm = other.GetComponent<PlayerMovement>();
        if (pm == null)
            pm = other.GetComponentInParent<PlayerMovement>();
        StartCoroutine(Run(pm));
    }

    private IEnumerator Run(PlayerMovement player)
    {
        _running = true;
        _player = player;

        // 止まる位置が決まっているときは、先にそこまで歩かせる（ゲームを止める前に。止めると、歩けないため）
        if (standPoint != null && _player != null)
        {
            _player.SetInputLocked(true);
            yield return WalkToStandPoint();
        }

        GamePause.Acquire(this);
        if (_player != null)
            _player.SetInputLocked(true);

        _cam = FindObjectOfType<CameraFollow>();
        _originalTarget = _cam != null ? _cam.Target : null;
        if (_cam != null)
            _cam.followUnscaled = true;

        if (uiPrefab != null)
            _ui = Instantiate(uiPrefab);
        else
            Debug.LogWarning("[GimmickIntroZone] " + name + ": Ui Prefabが空です。説明の文字が出ません（Spaceキーで進めます）。", this);

        SoundEffects.Play(SEId.DialogueOpen);

        bool zoomed = false;
        for (int i = 0; i < steps.Length; i++)
        {
            Step s = steps[i];
            if (s == null)
                continue;

            // --- カメラを動かす ---
            float minWait = 0f;
            if (_cam != null)
            {
                _cam.Target = s.cameraPoint != null ? s.cameraPoint : _originalTarget;
                if (s.zoomRatio > 0f)
                {
                    _cam.ZoomTo(s.zoomRatio, zoomTime);
                    zoomed = true;
                    minWait = zoomTime;
                }
                else if (zoomed)
                {
                    _cam.ZoomTo(1f, zoomTime);
                    zoomed = false;
                    minWait = zoomTime;
                }
            }
            yield return WaitCamera(minWait);

            // --- 説明を出して、Spaceキーを待つ ---
            if (_ui != null)
                _ui.Show(s.message);

            if (s.requiredKey != KeyCode.None)
            {
                // 指定のキーだけで進む。文字は、最後まで出てから。Spaceを含め、ほかのキーは受け付けない
                string keyLabel = KeyLabel(s.requiredKey);
                float tk = 0f;
                while (true)
                {
                    tk += Time.unscaledDeltaTime;
                    bool typingK = _ui != null && _ui.IsTyping;
                    bool ready = !typingK && tk >= inputDelay;
                    if (_ui != null)
                    {
                        _ui.SetPromptVisible(false);
                        _ui.SetKeyPromptVisible(ready, keyLabel);
                    }
                    if (ready && Input.GetKeyDown(s.requiredKey))
                        break;
                    yield return null;
                }
            }
            else
            {
                // 文字が出ている間にSpaceを押すと、全文をすぐ表示する。出終わってからSpaceを押すと、次へ進む。
                float t = 0f;
                while (true)
                {
                    t += Time.unscaledDeltaTime;
                    bool typing = _ui != null && _ui.IsTyping;
                    if (_ui != null)
                        _ui.SetPromptVisible(!typing && t >= inputDelay);

                    if (t >= inputDelay && Input.GetKeyDown(KeyCode.Space))
                    {
                        if (typing)
                            _ui.CompleteText();
                        else
                            break;
                    }
                    yield return null;
                }
            }

            SoundEffects.Play(SEId.DialogueAdvance);
            if (_ui != null)
                _ui.Hide();
        }

        // --- カメラをプレイヤーに戻す ---
        SoundEffects.Play(SEId.DialogueClose);
        float returnWait = 0f;
        if (_cam != null)
        {
            _cam.Target = _originalTarget;
            if (zoomed)
            {
                _cam.ZoomTo(1f, zoomTime);
                returnWait = zoomTime;
            }
        }
        yield return WaitCamera(returnWait);

        Finish(true);

        // 「Fキーを押してみて」のあとは、掲示板（ステージ選択）を開く
        if (ShouldOpenStageSelect())
            OpenStageSelect();
    }

    /// <summary>終わったあと、掲示板を開くか。チェックがONか、最後の手順が「Fキーを押して」のとき。</summary>
    private bool ShouldOpenStageSelect()
    {
        if (openStageSelectAtEnd)
            return true;
        if (steps == null)
            return false;
        for (int i = steps.Length - 1; i >= 0; i--)
        {
            if (steps[i] == null)
                continue;
            return steps[i].requiredKey == KeyCode.F;
        }
        return false;
    }

    private void OpenStageSelect()
    {
        StageSelectUI board = FindObjectOfType<StageSelectUI>(true);
        if (board != null)
        {
            Debug.Log("[GimmickIntroZone] " + name + "：掲示板（ステージ選択）を開きます");
            board.Open();

            // 開いたあと：戻れなくして、キーが話す
            if (StageSelectUI.IsOpen)
            {
                if (lockStageSelectBack && !string.IsNullOrEmpty(stageSelectMessage))
                    StageSelectUI.BackLocked = true;
                if (!string.IsNullOrEmpty(stageSelectMessage))
                    StartCoroutine(StageSelectGuide());
            }
        }
        else
            Debug.LogWarning("[GimmickIntroZone] " + name + "：StageSelectUI（掲示板の画面）がシーンに無いので、開けませんでした。", this);
    }

    /// <summary>ステージ選択の画面で、キーが話す（Spaceで消える）。画面が閉じられたら、途中でもやめる。</summary>
    private IEnumerator StageSelectGuide()
    {
        if (uiPrefab == null)
            yield break;
        yield return null;   // 開いたのと同じフレームのキーは、受け付けない

        GimmickIntroUI ui = Instantiate(uiPrefab);
        SoundEffects.Play(SEId.DialogueOpen);
        ui.Show(stageSelectMessage);

        float t = 0f;
        while (StageSelectUI.IsOpen)
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
        Destroy(ui.gameObject);
    }

    /// <summary>案内に出すキーの名前（Alpha1 → 1、それ以外は、そのままの名前）。</summary>
    private static string KeyLabel(KeyCode k)
    {
        string n = k.ToString();
        if (n.StartsWith("Alpha"))
            return n.Substring(5);
        return n;
    }

    /// <summary>プレイヤーを、止まる位置まで、自動で歩かせる（操作ではなく、位置を動かす）。</summary>
    private IEnumerator WalkToStandPoint()
    {
        float targetX = standPoint.position.x;
        if (Mathf.Abs(targetX - _player.transform.position.x) <= standTolerance)
            yield break;

        Rigidbody2D rb = _player.GetComponent<Rigidbody2D>();
        Animator an = _player.animator;
        float dir = targetX > _player.transform.position.x ? 1f : -1f;

        // 歩く向きを向く
        CharacterController2D cc = _player.GetComponent<CharacterController2D>();
        if (cc != null)
            cc.Move(0.01f * dir, false, false);

        if (an != null)
            an.SetFloat("Speed", 40f);

        float waited = 0f;
        while (waited < standWalkMaxSeconds && Mathf.Abs(targetX - _player.transform.position.x) > standTolerance)
        {
            yield return new WaitForFixedUpdate();
            waited += Time.fixedDeltaTime;
            float remaining = targetX - _player.transform.position.x;
            float step = Mathf.Sign(remaining) * Mathf.Min(standWalkSpeed * Time.fixedDeltaTime, Mathf.Abs(remaining));
            if (rb != null)
                rb.position = rb.position + new Vector2(step, 0f);
            else
                _player.transform.position += new Vector3(step, 0f, 0f);
        }

        if (an != null)
            an.SetFloat("Speed", 0f);
    }

    /// <summary>カメラが目的地に着くまで（またはminWait秒、最長でcameraMoveMaxTime秒）待つ。</summary>
    private IEnumerator WaitCamera(float minWait)
    {
        float t = 0f;
        while (t < Mathf.Max(cameraMoveMaxTime, minWait))
        {
            t += Time.unscaledDeltaTime;
            if (t >= minWait && (_cam == null || _cam.Target == null || IsCameraNear(_cam.Target.position)))
                break;
            yield return null;
        }
    }

    private bool IsCameraNear(Vector3 point)
    {
        Vector2 d = (Vector2)_cam.transform.position - (Vector2)point;
        return d.magnitude < cameraArriveDistance;
    }

    private void Finish(bool completed)
    {
        if (!_running)
            return;
        _running = false;

        if (_cam != null)
        {
            _cam.followUnscaled = false;
            if (!completed)
                _cam.Target = _originalTarget;
        }
        if (_player != null)
            _player.SetInputLocked(false);
        if (_ui != null)
            Destroy(_ui.gameObject);
        _ui = null;
        GamePause.Release(this);

        if (completed)
        {
            _done = true;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
                col.enabled = false;

            if (showOnce && SaveManager.Data != null && SaveManager.Data.seenIntros != null
                && !SaveManager.Data.seenIntros.Contains(Id))
            {
                SaveManager.Data.seenIntros.Add(Id);
                SaveManager.Save();
            }

            // 紹介が終わったので、左上のヒントを出す
            ActivateObjectives();
        }
    }

    // ---- 左上のヒント ------------------------------------------------------

    private string ObjectiveId(int step)
    {
        return Id + "#goal" + step;
    }

    private bool ObjectiveCompleted(int step)
    {
        return showOnce && SaveManager.Data != null && SaveManager.Data.seenIntros != null
            && SaveManager.Data.seenIntros.Contains(ObjectiveId(step));
    }

    /// <summary>Objective Text のある手順を、左上に出す（着いたことのあるものは、出さない）。</summary>
    private void ActivateObjectives()
    {
        if (steps == null)
            return;
        for (int i = 0; i < steps.Length; i++)
        {
            Step s = steps[i];
            if (s == null || string.IsNullOrEmpty(s.objectiveText))
                continue;
            if (_activeObjectives.Contains(i) || ObjectiveCompleted(i))
                continue;
            if (s.cameraPoint == null)
                Debug.LogWarning("[GimmickIntroZone] " + name + "：手順" + i + "は、Camera Point が空なので、左上のヒントは消えません（着く場所が無いため）。", this);
            _activeObjectives.Add(i);
            ObjectiveHintUI.Show(ObjectiveId(i), s.objectiveText, objectiveStyle, objectiveFont);
        }
    }

    private Transform ObjectivePlayerTransform()
    {
        if (_player != null)
            return _player.transform;
        if (_objectivePlayer == null)
        {
            GameObject g = NewGameIntro.FindMainPlayerObject();
            if (g != null)
                _objectivePlayer = g.transform;
        }
        return _objectivePlayer;
    }

    private void Update()
    {
        if (_running || _activeObjectives.Count == 0)
            return;
        Transform pl = ObjectivePlayerTransform();
        if (pl == null)
            return;

        for (int n = _activeObjectives.Count - 1; n >= 0; n--)
        {
            int i = _activeObjectives[n];
            Step s = (steps != null && i < steps.Length) ? steps[i] : null;
            if (s == null || s.cameraPoint == null)
                continue;
            Vector3 d = pl.position - s.cameraPoint.position;
            float dist = objectiveHorizontalOnly ? Mathf.Abs(d.x) : new Vector2(d.x, d.y).magnitude;
            if (dist <= Mathf.Max(0.1f, s.objectiveRadius))
                CompleteObjective(i);
        }
    }

    /// <summary>着いたので、左上のヒントを消して、記録する。</summary>
    private void CompleteObjective(int step)
    {
        _activeObjectives.Remove(step);
        ObjectiveHintUI.Hide(ObjectiveId(step));
        if (showOnce && SaveManager.Data != null && SaveManager.Data.seenIntros != null
            && !SaveManager.Data.seenIntros.Contains(ObjectiveId(step)))
        {
            SaveManager.Data.seenIntros.Add(ObjectiveId(step));
            SaveManager.Save();
        }
    }

    private void OnDestroy()
    {
        // 途中で消えても、止めたままにならないように後始末をする。
        Finish(false);

        // 左上のヒントも、出しっぱなしにしない（シーンを移るとき。着いていなければ、次に来たときに、また出る）
        for (int n = 0; n < _activeObjectives.Count; n++)
            ObjectiveHintUI.Hide(ObjectiveId(_activeObjectives[n]));
        _activeObjectives.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        // 止まる位置（赤）
        if (standPoint != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 1f);
            Vector3 sp = standPoint.position;
            Gizmos.DrawLine(transform.position, sp);
            Gizmos.DrawWireCube(sp + Vector3.up * 0.5f, new Vector3(0.9f, 1f, 0.1f));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(sp + Vector3.up * 1.35f, "止まる位置");
#endif
        }

        if (steps == null)
            return;

        Camera c = Camera.main;
        float h = (c != null && c.orthographic) ? c.orthographicSize : 5f;
        float w = h * 1.7778f;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] == null || steps[i].cameraPoint == null)
                continue;
            Vector3 p = steps[i].cameraPoint.position;
            Gizmos.DrawLine(transform.position, p);
            Gizmos.DrawWireSphere(p, 0.35f);
            float ratio = steps[i].zoomRatio > 0f ? steps[i].zoomRatio : 1f;
            Gizmos.DrawWireCube(p, new Vector3(w * 2f * ratio, h * 2f * ratio, 0f));

            // 左上のヒントが消える範囲（水色）
            if (!string.IsNullOrEmpty(steps[i].objectiveText))
            {
                Gizmos.color = new Color(0.3f, 0.9f, 1f, 1f);
                Gizmos.DrawWireSphere(p, Mathf.Max(0.1f, steps[i].objectiveRadius));
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
            }
        }
    }
}
