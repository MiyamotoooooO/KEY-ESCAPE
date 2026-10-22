using System.Collections;
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
        }
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
        }
    }

    private void OnDestroy()
    {
        // 途中で消えても、止めたままにならないように後始末をする。
        Finish(false);
    }

    private void OnDrawGizmosSelected()
    {
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
        }
    }
}
