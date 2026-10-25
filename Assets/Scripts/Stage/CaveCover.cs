using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 洞窟のおおい。
/// 洞窟の中を、外から見えないように、絵でおおいかくす。
/// プレイヤーが洞窟に入る（この部品の BoxCollider2D の範囲に入る）と、おおいがふわっと消えて、中が見える。
/// 洞窟から出ると、またふわっと、おおいがもどる。
///
/// 使い方：
///   1. メニュー「Tools > KEY ESCAPE > ステージ部品 > 洞窟（岩山）のプレハブを作る」で、小・中・大の3つのプレハブを作る。
///   2. 好きな大きさのものをシーンに置く。足もと（地面）に、岩山の下の線を合わせる。
///   3. 洞窟の中の地面・壁・足場は、いつもどおりに、岩山の後ろに置く。岩山が、その上にかぶさる。
///   4. BoxCollider2D（水色の枠）が、洞窟の中の範囲。最初は入口の大きさ。中を広く作ったら、枠も広げる。
///      主人公が枠に入ると、岩山がふわっと消える。枠から出ると、もどる。
/// 四角いおおい（Fit To Zone を ON）にして、BoxCollider2D の大きさに、絵を合わせることもできる。
/// その場合の絵は、ふち（Cover。四隅の枠は、そのまま）と、くり返し並ぶひび割れ（Cracks）の2枚。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CaveCover : MonoBehaviour
{
    [Header("おおい")]
    [Tooltip("おおいの絵（SpriteRenderer）。空なら、子オブジェクトから自動で探す。BoxCollider2D の大きさに合わせて広がる")]
    public SpriteRenderer cover;

    [Tooltip("ひび割れの絵（SpriteRenderer）。四角いおおいの上に、くり返し並ぶ。なければ、ひび割れなし。Cover の子オブジェクトに置く")]
    public SpriteRenderer cracks;

    [Tooltip("Fit To Zone が ON のとき、ひび割れを、おおいのふちから内側に引っこめる量（ワールド単位）。ふちの線の下で、ひび割れが途切れるのを隠す")]
    public float cracksInset = 0.1f;

    [Tooltip("おおいの色（絵に、この色がかかる）。白なら、絵のまま")]
    public Color coverColor = Color.white;

    [Tooltip("ON：おおいの絵を、BoxCollider2D の大きさに合わせて伸ばす（四角いおおい用）。OFF：絵は、そのままの大きさ（岩山用）")]
    public bool fitToZone = false;

    [Tooltip("Fit To Zone が ON のとき、おおいの外側に、はみ出させる量（ワールド単位）")]
    public float margin = 0f;

    [Header("入る・出る")]
    [Tooltip("外にいるとき、枠のふちから、この距離（ワールド単位）だけ中に入ってから、おおいが消える。入口の少し手前で消えるのを、防ぐ")]
    public float enterInset = 0.2f;

    [Header("ふわっと消える・もどる")]
    [Tooltip("洞窟に入ったとき、おおいが消えるまでの秒数")]
    public float revealSeconds = 0.4f;

    [Tooltip("洞窟から出たとき、おおいがもどるまでの秒数")]
    public float coverSeconds = 0.5f;

    [Tooltip("出てから、おおいがもどり始めるまでの待ち時間（秒）。入口で行ったり来たりしても、ちらつかない")]
    public float coverDelay = 0.1f;

    [Header("描く順番")]
    [Tooltip("Sorting Layer の名前。空なら、変えない。ステージの絵が別の Sorting Layer にあるときは、それと同じ名前にする")]
    public string sortingLayerName = "";

    [Tooltip("Order in Layer。大きいほど手前。吹き出し・エモート（敵の描く順 + 100 など）より大きくして、洞窟の中のものが、おおいを突き抜けて見えないようにする")]
    public int coverSortingOrder = 1000;

    [Header("効果音（なくてもよい）")]
    public AudioClip revealClip;
    public AudioClip coverClip;
    [Range(0f, 1f)] public float seVolume = 0.8f;

    [Header("確認用")]
    [Tooltip("ONにすると、Console に、入った・出たを出す")]
    public bool debugLog = false;

    private BoxCollider2D _zone;
    private SpriteRenderer[] _all;
    private GameObject _player;
    private Collider2D[] _playerCols;

    private float _alpha = 1f;       // いまの濃さ（1 = おおっている、0 = 消えた）
    private bool _inside;            // プレイヤーが、中にいるか
    private float _outsideTimer;     // 出てからの経過
    private bool _started;

    private void Reset()
    {
        _zone = GetComponent<BoxCollider2D>();
        if (_zone != null)
            _zone.isTrigger = true;
    }

    private void Awake()
    {
        _zone = GetComponent<BoxCollider2D>();
        if (_zone != null)
            _zone.isTrigger = true;
    }

    private void Start()
    {
        Collect();
        ApplyShape();
        ApplySorting();

        // 主人公が、すでに中にいる（チェックポイントから始めたときなど）なら、おおいなしで始める
        FindPlayer();
        _inside = IsPlayerInside();
        _alpha = _inside ? 0f : 1f;
        ApplyAlpha();
        _started = true;
    }

    private void Collect()
    {
        if (cover == null)
            cover = GetComponentInChildren<SpriteRenderer>(true);
        if (cracks == null && cover != null && fitToZone)
        {
            // おおいの子オブジェクトに、絵があれば、それをひび割れとして使う
            SpriteRenderer[] kids = cover.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < kids.Length; i++)
                if (kids[i] != cover) { cracks = kids[i]; break; }
        }
        _all = GetComponentsInChildren<SpriteRenderer>(true);
    }

    /// <summary>おおいの大きさと位置を、BoxCollider2D に合わせる（値が変わるときだけ書く）。</summary>
    private void ApplyShape()
    {
        if (!fitToZone || cover == null || _zone == null)
            return;

        if (cover.drawMode == SpriteDrawMode.Simple)
            cover.drawMode = SpriteDrawMode.Sliced;

        Vector2 size = _zone.size + new Vector2(margin * 2f, margin * 2f);
        if (cover.size != size)
            cover.size = size;

        // おおいを、BoxCollider2D の中心に置く（おおいが子オブジェクトのとき）
        if (cover.transform != transform)
        {
            Vector3 p = new Vector3(_zone.offset.x, _zone.offset.y, cover.transform.localPosition.z);
            if (cover.transform.localPosition != p)
                cover.transform.localPosition = p;
            if (cover.transform.localRotation != Quaternion.identity)
                cover.transform.localRotation = Quaternion.identity;
            if (cover.transform.localScale != Vector3.one)
                cover.transform.localScale = Vector3.one;
        }

        // ひび割れ：おおいの中に、少し引っこめて、くり返し並べる
        if (cracks != null && cracks != cover)
        {
            if (cracks.drawMode != SpriteDrawMode.Tiled)
                cracks.drawMode = SpriteDrawMode.Tiled;
            if (cracks.tileMode != SpriteTileMode.Continuous)
                cracks.tileMode = SpriteTileMode.Continuous;
            Vector2 cs = size - new Vector2(cracksInset * 2f, cracksInset * 2f);
            if (cs.x < 0.1f) cs.x = 0.1f;
            if (cs.y < 0.1f) cs.y = 0.1f;
            if (cracks.size != cs)
                cracks.size = cs;
            if (cracks.transform.parent == cover.transform && cracks.transform.localPosition != Vector3.zero)
                cracks.transform.localPosition = Vector3.zero;
        }
    }

    private void ApplySorting()
    {
        if (_all == null)
            return;
        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i] == null)
                continue;
            if (!string.IsNullOrEmpty(sortingLayerName))
                _all[i].sortingLayerName = sortingLayerName;
            // ひび割れは、おおいの絵の、すぐ手前に
            _all[i].sortingOrder = coverSortingOrder + (cracks != null && cracks != cover && _all[i] == cracks ? 1 : 0);
        }
    }

    private void FindPlayer()
    {
        _player = NewGameIntro.FindMainPlayerObject();
        _playerCols = _player != null ? _player.GetComponentsInChildren<Collider2D>() : null;
    }

    /// <summary>主人公の当たり判定が、洞窟の範囲に重なっているか。</summary>
    private bool IsPlayerInside()
    {
        if (_zone == null)
            return false;
        if (_player == null)
        {
            FindPlayer();
            if (_player == null)
                return false;
        }

        // 主人公の当たり判定のうち、トリガーでないものの範囲を使う（攻撃判定などは数えない）
        Bounds zb = _zone.bounds;
        if (!_inside && enterInset > 0f)
        {
            // 入るときだけ、枠を少し小さくする（出るときは、元の大きさ）
            float ix = Mathf.Min(enterInset, zb.extents.x * 0.9f);
            float iy = Mathf.Min(enterInset, zb.extents.y * 0.9f);
            zb.Expand(new Vector3(-ix * 2f, -iy * 2f, 0f));
        }
        bool any = false;
        if (_playerCols != null)
        {
            for (int i = 0; i < _playerCols.Length; i++)
            {
                Collider2D c = _playerCols[i];
                if (c == null || !c.enabled || c.isTrigger)
                    continue;
                any = true;
                if (Overlap2D(zb, c.bounds))
                    return true;
            }
        }
        if (!any)
            return Overlap2D(zb, new Bounds(_player.transform.position, Vector3.zero));
        return false;
    }

    /// <summary>横と縦だけで、重なっているか調べる（奥行きの z の違いは、気にしない）。</summary>
    private static bool Overlap2D(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.y <= b.max.y && a.max.y >= b.min.y;
    }

    private void FixedUpdate()
    {
        if (!_started)
            return;

        bool now = IsPlayerInside();
        if (now != _inside)
        {
            _inside = now;
            _outsideTimer = 0f;
            if (debugLog)
                Debug.Log("[CaveCover] " + name + (now ? " に入った" : " から出た"));

            if (now)
                PlayClip(revealClip);
            else if (_alpha < 0.5f)
                PlayClip(coverClip);
        }
        else if (!now)
        {
            _outsideTimer += Time.fixedDeltaTime;
        }
    }

    private void Update()
    {
        if (!_started)
            return;

        float target;
        float seconds;
        if (_inside)
        {
            target = 0f;
            seconds = revealSeconds;
        }
        else
        {
            // 出てから、少し待って、もどし始める
            target = _outsideTimer >= coverDelay ? 1f : _alpha;
            seconds = coverSeconds;
        }

        if (!Mathf.Approximately(_alpha, target))
        {
            float step = seconds <= 0.01f ? 1f : Time.deltaTime / seconds;
            _alpha = Mathf.MoveTowards(_alpha, target, step);
            ApplyAlpha();
        }
    }

    private void ApplyAlpha()
    {
        if (_all == null)
            return;

        // 動きをなめらかに見せるため、直線の変化に、ゆるいカーブをかける
        float a = Mathf.SmoothStep(0f, 1f, _alpha);

        for (int i = 0; i < _all.Length; i++)
        {
            SpriteRenderer sr = _all[i];
            if (sr == null)
                continue;
            Color c = coverColor;
            c.a = coverColor.a * a;
            sr.color = c;
            sr.enabled = a > 0.001f; // 完全に消えたら、描かない
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null)
            AudioManager.PlaySE(clip, seVolume);
    }

    private void OnDisable()
    {
        // 無効になるときは、次に有効になったときのために、おおいをもどしておく
        _started = false;
    }

    // ---------- エディタ ----------

#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate の中では、SpriteRenderer.size を書き換えられない（警告が出る）ので、あとで行う
        EditorApplication.delayCall += () =>
        {
            if (this == null)
                return;
            _zone = GetComponent<BoxCollider2D>();
            if (cover == null)
                cover = GetComponentInChildren<SpriteRenderer>(true);
            if (!Application.isPlaying)
                ApplyShape();
        };
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D z = _zone != null ? _zone : GetComponent<BoxCollider2D>();
        if (z == null)
            return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(z.offset, z.size);
        Gizmos.matrix = old;
    }
#endif
}
