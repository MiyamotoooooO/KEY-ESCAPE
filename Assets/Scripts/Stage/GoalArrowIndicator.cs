using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - ゴールがどの方向にあるかを示す矢印。
///
/// ・ゴールが画面の外にいる間だけ、画面の縁に矢印が出る。ゴールが画面に映ったら消える。
///   例：ゴールが左下にあるときは、画面の左下の縁に「↙」向きの矢印が出る。
/// ・矢印の内側（画面の中心がわ）に、「GOAL」と書いたキーのアイコンと、「あと 25m」のような距離が並ぶ。
/// ・ゴールをクリアしたあと、一時停止中（ヒント・結果画面など）、ロード中、やられている最中は隠れる。
///
/// 使い方：
///   ・ステージのシーン（StageGoalがあるシーン）では、自動で作られる。シーンにオブジェクトを置く必要は無い。
///   ・見た目を変えたいときは、空のオブジェクトにこのスクリプトを付けると、その設定が使われる（自動作成はされない）。
///   ・矢印を出したくないシーンでは、このスクリプトを付けて「Show Arrow」をオフにする。
/// </summary>
[DisallowMultipleComponent]
public class GoalArrowIndicator : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("オフにすると、矢印を出さない。")]
    public bool showArrow = true;
    [Tooltip("シーンが始まってから、矢印を出し始めるまでの待ち時間（秒）。")]
    public float showDelay = 0.5f;
    [Tooltip("ゴールが、画面の縁からこの割合より内側に映っているときは「画面の中」とみなして、矢印を消す。")]
    [Range(0f, 0.3f)] public float onScreenInset = 0.04f;
    public string playerTag = "Player";
    [Tooltip("画面の重なり順。ヒント（500）より下、ゲームの画面より上。")]
    public int sortingOrder = 400;

    [Header("矢印")]
    [Tooltip("任意。矢印の画像（右向きで作る）。空なら、コードで作った矢印を使う。")]
    public Sprite arrowSprite;
    public Color arrowColor = new Color(0.9f, 0.1f, 0.1f, 1f);
    [Tooltip("矢印の大きさ（画面1920x1080のときの大きさ）。")]
    public float arrowSize = 90f;
    [Tooltip("矢印が、ゴールの向きに行ったり来たりする幅。0で動かない。")]
    public float bobAmount = 12f;
    public float bobSpeed = 6f;
    [Tooltip("矢印と画面の縁のあいだの、あき。")]
    public float edgePadding = 40f;

    [Header("アイコン（GOAL）")]
    [Tooltip("任意。矢印の横に出すアイコンの画像。空なら、コードで作ったキーの形を使う。")]
    public Sprite iconSprite;
    [Tooltip("キーの形に書く文字。半角の英数字にしてください（日本語は出ないことがあります）。")]
    public string iconLabel = "GOAL";
    public Color iconLabelColor = new Color(0.9f, 0.1f, 0.1f, 1f);
    public Color keyFaceColor = new Color(1f, 0.97f, 0.9f, 1f);
    public Color keySideColor = new Color(0.78f, 0.72f, 0.66f, 1f);
    public float iconSize = 110f;
    [Tooltip("矢印・アイコン・距離のあいだのあき。")]
    public float partGap = 14f;

    [Header("距離")]
    public bool showDistance = true;
    [Tooltip("{0}の部分に、ゴールまでの距離（m）が入る。")]
    public string distanceFormat = "あと {0}m";
    [Tooltip("任意。距離の文字のフォント。空なら、シーンにある日本語が出るフォントを探す。")]
    public TMP_FontAsset distanceFont;
    public int distanceFontSize = 40;
    public Color distanceColor = Color.white;
    [Tooltip("ゲームの長さ1つ（Unityの1ユニット）を、何mとして表示するか。")]
    public float unitsPerMeter = 1f;

    [Header("動き")]
    [Tooltip("矢印の向きが変わるときの、追いかける速さ。大きいほどすばやい。")]
    public float followSharpness = 14f;
    [Tooltip("出る・消えるときの速さ。")]
    public float fadeSpeed = 5f;

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private CanvasGroup _group;
    private RectTransform _arrowRt;
    private RectTransform _iconRt;
    private RectTransform _labelRt;
    private TextMeshProUGUI _label;
    private string _fmt;

    private Camera _cam;
    private Transform _player;
    private StageGoal[] _goals = new StageGoal[0];
    private StageGoalLetters[] _letters = new StageGoalLetters[0];
    private float _nextFind;
    private float _born;
    private float _alpha;
    private float _angle;
    private bool _hasAngle;
    private int _lastMeters = -1;
    private Vector2 _labelSize;

    private Texture2D _texArrow;
    private Texture2D _texKey;
    private Sprite _spArrow;
    private Sprite _spKey;

    // ----- 自動作成 -----

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSetup()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SpawnIfNeeded();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SpawnIfNeeded();
    }

    private static void SpawnIfNeeded()
    {
        if (FindObjectOfType<StageGoal>() == null)
            return;
        if (FindObjectOfType<GoalArrowIndicator>() != null)
            return;
        new GameObject("GoalArrowIndicator").AddComponent<GoalArrowIndicator>();
    }

    // ----- 部品づくり -----

    private void Awake()
    {
        _born = Time.unscaledTime;
        BuildUI();
    }

    private void Start()
    {
        FindTargets();
    }

    private void OnDestroy()
    {
        if (_spArrow != null) Destroy(_spArrow);
        if (_spKey != null) Destroy(_spKey);
        if (_texArrow != null) Destroy(_texArrow);
        if (_texKey != null) Destroy(_texKey);
    }

    private void BuildUI()
    {
        GameObject cg = new GameObject("GoalArrowCanvas");
        cg.transform.SetParent(transform, false);
        _canvas = cg.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = sortingOrder;
        CanvasScaler cs = cg.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        _group = cg.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _group.alpha = 0f;
        _canvasRect = cg.GetComponent<RectTransform>();

        // 矢印
        Sprite arrow = arrowSprite;
        if (arrow == null)
        {
            _texArrow = MakeArrowTexture();
            _spArrow = Sprite.Create(_texArrow, new Rect(0f, 0f, _texArrow.width, _texArrow.height), new Vector2(0.5f, 0.5f), 100f);
            arrow = _spArrow;
        }
        _arrowRt = NewImage("Arrow", _canvas.transform, arrow, arrowSize, arrowColor);

        // GOALのキー
        Sprite key = iconSprite;
        bool drawnKey = false;
        if (key == null)
        {
            _texKey = MakeKeyTexture(keyFaceColor, keySideColor);
            _spKey = Sprite.Create(_texKey, new Rect(0f, 0f, _texKey.width, _texKey.height), new Vector2(0.5f, 0.5f), 100f);
            key = _spKey;
            drawnKey = true;
        }
        _iconRt = NewImage("GoalIcon", _canvas.transform, key, iconSize, Color.white);
        if (drawnKey && !string.IsNullOrEmpty(iconLabel))
        {
            TextMeshProUGUI t = NewText("Label", _iconRt, null, 40, iconLabelColor);
            t.text = iconLabel;
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true;
            t.fontSizeMin = 12f;
            t.fontSizeMax = 40f;
            t.rectTransform.sizeDelta = new Vector2(iconSize * 0.74f, iconSize * 0.42f);
            t.rectTransform.anchoredPosition = new Vector2(0f, iconSize * 0.03f);
        }

        // 距離
        if (showDistance)
        {
            string fmt = (distanceFormat ?? "").Replace('　', ' ');
            TMP_FontAsset font = distanceFont != null ? distanceFont : ResolveFont(fmt);
            // 使えないフォントしか無いときは、日本語を使わない形にする。
            if (font != null)
            {
                for (int i = 0; i < fmt.Length; i++)
                {
                    if (fmt[i] >= '⺀' && !font.HasCharacter(fmt[i], true))
                    {
                        fmt = "{0}m";
                        break;
                    }
                }
            }
            _fmt = fmt;
            _label = NewText("Distance", _canvas.transform, font, distanceFontSize, distanceColor);
            _label.fontStyle = FontStyles.Bold;
            _label.outlineWidth = 0.3f;
            _label.outlineColor = new Color32(30, 10, 10, 255);
            _labelRt = _label.rectTransform;
        }

        _canvas.enabled = false;
    }

    private static RectTransform NewImage(string name, Transform parent, Sprite sprite, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        Image im = go.AddComponent<Image>();
        im.sprite = sprite;
        im.color = color;
        im.preserveAspect = true;
        im.raycastTarget = false;
        return rt;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, int size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>シーンにある文字から、この文の日本語が全部出るフォントを探す。無ければ、いちばん多く出るもの。</summary>
    private static TMP_FontAsset ResolveFont(string text)
    {
        TMP_Text[] all = FindObjectsOfType<TMP_Text>(true);
        TMP_FontAsset best = null;
        int bestCount = -1;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].font == null)
                continue;
            TMP_FontAsset f = all[i].font;
            if (f == best)
                continue;
            int count = 0;
            int need = 0;
            for (int k = 0; k < text.Length; k++)
            {
                if (text[k] < '⺀')
                    continue;
                need++;
                if (f.HasCharacter(text[k], true))
                    count++;
            }
            if (count == need)
                return f;
            if (count > bestCount)
            {
                bestCount = count;
                best = f;
            }
        }
        return best;
    }

    // ----- 毎フレーム -----

    private void FindTargets()
    {
        _goals = FindObjectsOfType<StageGoal>();
        _letters = FindObjectsOfType<StageGoalLetters>();
    }

    private void LateUpdate()
    {
        if (_canvas == null)
            return;

        float dt = Time.unscaledDeltaTime;
        float angle = 0f;
        float meters = 0f;
        bool visible = false;

        if (showArrow
            && Time.unscaledTime - _born >= showDelay
            && !GamePause.IsPaused
            && !LoadingScreen.IsLoading
            && !HitFeedback.IsDying)
        {
            visible = TryGetDirection(out angle, out meters);
        }

        _alpha = Mathf.MoveTowards(_alpha, visible ? 1f : 0f, dt * Mathf.Max(0.01f, fadeSpeed));
        _group.alpha = _alpha;
        bool show = _alpha > 0.001f;
        if (_canvas.enabled != show)
            _canvas.enabled = show;
        if (!show)
        {
            _hasAngle = false;
            return;
        }

        if (visible)
        {
            if (!_hasAngle)
            {
                _angle = angle;
                _hasAngle = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-followSharpness * dt);
                _angle = Mathf.LerpAngle(_angle, angle, k);
            }
            UpdateDistanceText(meters);
        }
        if (!_hasAngle)
            return;

        Layout();
    }

    /// <summary>ゴールが画面の外にあるときだけ true。向き（度）と、プレイヤーからゴールまでの距離（m）を返す。</summary>
    private bool TryGetDirection(out float angleDeg, out float meters)
    {
        angleDeg = 0f;
        meters = 0f;

        if (_cam == null)
            _cam = Camera.main;
        if (_cam == null)
            return false;
        if (_player == null && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + 0.5f;
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null)
                _player = p.transform;
        }
        if ((_goals == null || _goals.Length == 0) && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + 0.5f;
            FindTargets();
        }

        Vector3 origin = _player != null ? _player.position : _cam.transform.position;
        Vector3 target;
        if (!PickTarget(origin, out target))
            return false;

        Vector3 vp = _cam.WorldToViewportPoint(target);
        float inset = onScreenInset;
        if (vp.z > 0f && vp.x > inset && vp.x < 1f - inset && vp.y > inset && vp.y < 1f - inset)
            return false;

        float w = _canvasRect.rect.width;
        float h = _canvasRect.rect.height;
        if (w <= 1f || h <= 1f)
            return false;
        Vector2 d = new Vector2((vp.x - 0.5f) * w, (vp.y - 0.5f) * h);
        if (vp.z <= 0f)
            d = -d;
        if (d.sqrMagnitude < 1f)
            return false;

        angleDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        meters = Vector2.Distance(origin, target) / Mathf.Max(0.0001f, unitsPerMeter);
        return true;
    }

    /// <summary>まだクリアしていないゴールのうち、いちばん近いものの位置を返す。</summary>
    private bool PickTarget(Vector3 origin, out Vector3 pos)
    {
        pos = Vector3.zero;
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < _goals.Length; i++)
        {
            StageGoal g = _goals[i];
            if (g == null || g.IsDone || !g.isActiveAndEnabled)
                continue;
            Vector3 p = GoalPosition(g);
            float sq = (p - origin).sqrMagnitude;
            if (sq < best)
            {
                best = sq;
                pos = p;
                found = true;
            }
        }
        return found;
    }

    /// <summary>ゴールの場所。G O A L が並んでいれば、その真ん中。</summary>
    private Vector3 GoalPosition(StageGoal g)
    {
        for (int i = 0; i < _letters.Length; i++)
        {
            StageGoalLetters l = _letters[i];
            if (l == null || l.letters == null)
                continue;
            bool mine = l.goal == g || (l.goal == null && l.GetComponentInParent<StageGoal>() == g);
            if (!mine)
                continue;
            Vector3 sum = Vector3.zero;
            int c = 0;
            for (int k = 0; k < l.letters.Length; k++)
            {
                if (l.letters[k] == null)
                    continue;
                sum += l.letters[k].position;
                c++;
            }
            if (c > 0)
                return sum / c;
        }
        Collider2D col = g.GetComponent<Collider2D>();
        if (col != null)
            return col.bounds.center;
        return g.transform.position;
    }

    private void UpdateDistanceText(float meters)
    {
        if (_label == null)
            return;
        int m = Mathf.Max(0, Mathf.RoundToInt(meters));
        if (m == _lastMeters)
            return;
        _lastMeters = m;
        string text = _fmt.Replace("{0}", m.ToString());
        _label.text = text;
        _labelSize = _label.GetPreferredValues(text);
        _labelRt.sizeDelta = _labelSize;
    }

    private void Layout()
    {
        float w = _canvasRect.rect.width;
        float h = _canvasRect.rect.height;
        float rad = _angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // 画面の縁（少し内側）で、ゴールの向きにある点
        float pad = arrowSize * 0.5f + edgePadding;
        float hx = w * 0.5f - pad;
        float hy = h * 0.5f - pad;
        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        float tx = ax > 0.0001f ? hx / ax : float.MaxValue;
        float ty = ay > 0.0001f ? hy / ay : float.MaxValue;
        Vector2 p = dir * Mathf.Min(tx, ty);

        float t = Time.unscaledTime;
        float bob = Mathf.Sin(t * bobSpeed) * bobAmount;
        _arrowRt.anchoredPosition = p + dir * bob;
        _arrowRt.localRotation = Quaternion.Euler(0f, 0f, _angle);

        // 矢印の内側に、アイコン、そのまた内側に、距離を並べる。
        float arrowHalf = arrowSize * 0.45f;
        float iconHalf = HalfExtentAlong(dir, iconSize * 0.5f, iconSize * 0.5f);
        float iconDist = arrowHalf + iconHalf + partGap;
        _iconRt.anchoredPosition = p - dir * iconDist;
        float pulse = 1f + 0.06f * Mathf.Sin(t * bobSpeed);
        _iconRt.localScale = new Vector3(pulse, pulse, 1f);

        if (_labelRt != null)
        {
            float labelHalf = HalfExtentAlong(dir, _labelSize.x * 0.5f, _labelSize.y * 0.5f);
            float labelDist = iconDist + iconHalf + labelHalf + partGap * 0.5f;
            _labelRt.anchoredPosition = p - dir * labelDist;
        }
    }

    /// <summary>中心から、向きdirに進んだとき、横hx・縦hyの四角の外に出るまでの長さ。</summary>
    private static float HalfExtentAlong(Vector2 dir, float hx, float hy)
    {
        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        float tx = ax > 0.0001f ? hx / ax : float.MaxValue;
        float ty = ay > 0.0001f ? hy / ay : float.MaxValue;
        return Mathf.Min(tx, ty);
    }

    // ----- 画像をコードで作る（距離関数で、なめらかな縁にする） -----

    /// <summary>右向きの太い矢印（白＋黒いふち）。色はImageのColorで変わる。</summary>
    private static Texture2D MakeArrowTexture()
    {
        const int N = 128;
        const float outlineWidth = 6f;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color outline = new Color(0.12f, 0.03f, 0.03f, 1f);
        Vector2 ta = new Vector2(62f, 18f);
        Vector2 tb = new Vector2(62f, 110f);
        Vector2 tc = new Vector2(118f, 64f);
        Vector2 boxC = new Vector2(43f, 64f);
        Vector2 boxH = new Vector2(29f, 18f);
        Color[] px = new Color[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(SdBox(p, boxC, boxH), SdTri(p, ta, tb, tc));
                float fill = Mathf.Clamp01(0.5f - d);
                float edge = Mathf.Clamp01(0.5f - (d - outlineWidth));
                Color c = Color.Lerp(outline, Color.white, fill);
                c.a = edge;
                px[y * N + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>キーボードのキーのような、角丸の四角（ふち・横の面・上の面）。</summary>
    private static Texture2D MakeKeyTexture(Color face, Color side)
    {
        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color outline = new Color(0.18f, 0.06f, 0.06f, 1f);
        Color[] px = new Color[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Color c = new Color(0f, 0f, 0f, 0f);
                c = Over(c, outline, Mathf.Clamp01(0.5f - SdRoundBox(p, new Vector2(64f, 60f), new Vector2(60f, 56f), 16f)));
                c = Over(c, side, Mathf.Clamp01(0.5f - SdRoundBox(p, new Vector2(64f, 60f), new Vector2(56f, 52f), 14f)));
                c = Over(c, face, Mathf.Clamp01(0.5f - SdRoundBox(p, new Vector2(64f, 67f), new Vector2(50f, 41f), 13f)));
                px[y * N + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    private static Color Over(Color dst, Color src, float coverage)
    {
        float a = src.a * coverage;
        float outA = a + dst.a * (1f - a);
        if (outA <= 0f)
            return new Color(0f, 0f, 0f, 0f);
        Color o = (src * a + dst * dst.a * (1f - a)) / outA;
        o.a = outA;
        return o;
    }

    private static float SdBox(Vector2 p, Vector2 center, Vector2 half)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x) - half.x, Mathf.Abs(p.y - center.y) - half.y);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
    }

    private static float SdRoundBox(Vector2 p, Vector2 center, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x) - (half.x - radius), Mathf.Abs(p.y - center.y) - (half.y - radius));
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    private static float SdTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(DistSeg(p, a, b), Mathf.Min(DistSeg(p, b, c), DistSeg(p, c, a)));
        float s1 = Cross(b - a, p - a);
        float s2 = Cross(c - b, p - b);
        float s3 = Cross(a - c, p - c);
        bool inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
        return inside ? -d : d;
    }

    private static float Cross(Vector2 u, Vector2 v)
    {
        return u.x * v.y - u.y * v.x;
    }

    private static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a;
        Vector2 ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }
}
