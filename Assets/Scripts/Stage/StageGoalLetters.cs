using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ゴールの見た目。キーキャラクター4体で「G」「O」「A」「L」を作り、
/// ふだんは、左から順に、ぴょんぴょんと跳ねる（ウェーブ）。
/// プレイヤーが近づくと、4体とも両腕を上げて大喜びして、「お疲れ様!」の吹き出しが出る。
/// そのあと、少しして、いつものゴールの演出（StageGoal の Clear）に進む。
///
/// 付け方：StageGoal の付いたゴールのオブジェクトを選んで、メニュー
///   Tools > KEY ESCAPE > ゴール > 選んだゴールに G O A L を並べる
/// を実行する（このスクリプトと、4体のキーキャラクターが、そのオブジェクトの子として付く）。
/// 位置・間隔・ジャンプの高さ・吹き出しは、このスクリプトの Inspector で調整する。
///
/// 近づいて自動でクリアになるのは、StageGoal の Mode が Touch のとき。BossDefeated のときは、ボスを倒すまでクリアにならない
/// （そのときも、近づけば、喜んで、吹き出しは出る）。
/// </summary>
[DisallowMultipleComponent]
public class StageGoalLetters : MonoBehaviour
{
    [Header("文字のキャラクター（左から G, O, A, L）")]
    [Tooltip("文字になるキーキャラクター。メニューで並べると、自動で入る。左から順に、ウェーブで跳ねる。")]
    public Transform[] letters = new Transform[0];
    [Tooltip("近づいたあとにクリアにする、ゴール。空なら、同じオブジェクトの StageGoal を使う。")]
    public StageGoal goal;

    [Header("キートップの文字の色")]
    [Tooltip("ONなら、4体のキートップの文字（G・O・A・L）を、下の Letter Color の色にする。OFFなら、キャラクターのまま。")]
    public bool overrideLetterColor = true;
    public Color letterColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    [Header("ウェーブ（順番にジャンプ）")]
    [Tooltip("跳ねる高さ（ワールドの単位）。")]
    public float jumpHeight = 0.6f;
    [Tooltip("1回跳ねるのにかかる秒数。")]
    public float jumpSeconds = 0.5f;
    [Tooltip("となりのキャラが跳ね始めるまでの秒数（小さいほど、波が速く進む）。")]
    public float delayBetween = 0.2f;
    [Tooltip("4体とも跳ねたあと、次の波が始まるまでの休みの秒数。")]
    public float restSeconds = 0.8f;

    [Header("プレイヤーが近づいたとき")]
    public string playerTag = "Player";
    [Tooltip("4体の真ん中から、左右にこの距離（ワールドの単位）に入ると、近づいたことになる。")]
    public float approachRadius = 4f;
    [Tooltip("上下には、この距離まで。")]
    public float approachHeight = 4f;
    [Tooltip("ONなら、近づいたとき、4体とも両腕を上げて大喜びする。")]
    public bool cheerOnApproach = true;
    [Tooltip("ONなら、近づいてから少しして、自動でゴールの演出（クリア）に進む。StageGoal の Mode が Touch のときだけ。")]
    public bool triggerClear = true;
    [Tooltip("近づいてから、クリアの演出に進むまでの秒数（吹き出しが出てから、少しの間）。")]
    public float clearDelay = 1f;

    [Header("吹き出し")]
    public string bubbleText = "お疲れ様!";
    [Tooltip("吹き出しの位置。4体の真ん中の足元からのずれ（上が＋Y）。吹き出しのしっぽの先が、ここに来る。")]
    public Vector2 bubbleOffset = new Vector2(0f, 2.2f);
    [Tooltip("吹き出しの文字の大きさ（ワールドの単位の、およそ10倍。4.7で、キーの文字と同じ）。")]
    public float bubbleFontSize = 5f;
    public Color bubbleTextColor = new Color(0.1f, 0.1f, 0.12f, 1f);
    public Color bubbleFillColor = Color.white;
    public Color bubbleOutlineColor = new Color(0.1f, 0.1f, 0.12f, 1f);
    [Tooltip("文字のまわりの余白（横, 縦）。")]
    public Vector2 bubblePadding = new Vector2(0.5f, 0.3f);
    [Tooltip("空なら、シーンにある日本語のフォントを自動で探して使う。")]
    public TMP_FontAsset bubbleFont;
    [Tooltip("吹き出しが、ポンと出る秒数。")]
    public float bubblePopSeconds = 0.25f;
    [Tooltip("吹き出しの描く順番。大きいほど手前。")]
    public int bubbleSortingOrder = 600;

    [Header("紙吹雪（ゴールの上に、ずっと降らせる。ゴールすると止まる）")]
    [Tooltip("ONなら、ゴールの上から、紙吹雪を降らせ続ける。ゴールした（クリアになった）ら、止まる。")]
    public bool confettiEnabled = true;
    [Tooltip("1秒に降らせる枚数。")]
    public float confettiPerSecond = 12f;
    [Tooltip("紙吹雪を出す位置。4体の真ん中の足元からのずれ（上が＋Y）。")]
    public Vector2 confettiOffset = new Vector2(0f, 4.5f);
    [Tooltip("紙吹雪を出す、横の幅。")]
    public float confettiWidth = 5f;
    [Tooltip("紙吹雪1枚の高さ（ワールドの単位）。大きさは、少しずつばらつく。")]
    public float confettiSize = 0.25f;
    [Tooltip("落ちる速さ（ワールドの単位/秒）。")]
    public float confettiFallSpeed = 1.6f;
    [Tooltip("1枚が消えるまでの秒数。落ちる速さ×この秒数が、落ちる距離になる。")]
    public float confettiLifeSeconds = 3f;
    [Tooltip("紙吹雪の画像（色違いを、いくつか入れる）。空なら、赤・黄・青・緑・桃色の四角を、コードで作って降らせる。")]
    public Sprite[] confettiSprites;
    [Tooltip("紙吹雪の描く順番。大きいほど手前。")]
    public int confettiSortingOrder = 100;
    [Tooltip("ゴールしたとき、降っている紙吹雪が、消えるまでの秒数。")]
    public float confettiStopFadeSeconds = 0.6f;

    private Vector3[] _basePos;
    private LimbRig[] _rigs;
    private Transform _player;
    private float _findTimer;
    private bool _triggered;
    private GameObject _bubble;

    private class ConfettiPiece
    {
        public Transform tr;
        public SpriteRenderer sr;
        public float vy;
        public float sway;
        public float swayAmp;
        public float swaySpeed;
        public float spin;
        public float age;
        public Color color;
    }
    private readonly System.Collections.Generic.List<ConfettiPiece> _pieces = new System.Collections.Generic.List<ConfettiPiece>();
    private GameObject _confettiRoot;
    private float _emitAcc;
    private bool _confettiStopped;
    private float _stopTime;
    private static Sprite s_white;

    private void Reset()
    {
        goal = GetComponent<StageGoal>();
    }

    private void Start()
    {
        if (goal == null)
            goal = GetComponent<StageGoal>();
        ApplyLetterColor(false);

        int n = letters != null ? letters.Length : 0;
        _basePos = new Vector3[n];
        _rigs = new LimbRig[n];
        for (int i = 0; i < n; i++)
        {
            if (letters[i] == null)
                continue;
            _basePos[i] = letters[i].localPosition;
            _rigs[i] = letters[i].GetComponentInChildren<LimbRig>(true);
        }

        // 紙吹雪：すでに降っているところから、始まるようにする。
        if (confettiEnabled && confettiPerSecond > 0f && confettiLifeSeconds > 0f)
        {
            int count = Mathf.RoundToInt(confettiPerSecond * confettiLifeSeconds);
            for (int i = 0; i < count; i++)
                SpawnPiece(Random.Range(0f, confettiLifeSeconds));
        }
    }

    /// <summary>4体のキートップの文字の色を、Letter Color にする。</summary>
    private void ApplyLetterColor(bool recordUndo)
    {
        if (!overrideLetterColor || letters == null)
            return;
        for (int i = 0; i < letters.Length; i++)
        {
            if (letters[i] == null)
                continue;
            TextMeshPro[] tmps = letters[i].GetComponentsInChildren<TextMeshPro>(true);
            for (int k = 0; k < tmps.Length; k++)
            {
                if (tmps[k] == null || tmps[k].color == letterColor)
                    continue;
#if UNITY_EDITOR
                if (recordUndo)
                    UnityEditor.Undo.RecordObject(tmps[k], "G O A L の文字の色");
#endif
                tmps[k].color = letterColor;
#if UNITY_EDITOR
                if (recordUndo)
                    UnityEditor.EditorUtility.SetDirty(tmps[k]);
#endif
            }
        }
    }

#if UNITY_EDITOR
    // Inspectorで色を変えたら、エディタ上の4体にも、すぐ反映する。
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                ApplyLetterColor(true);
        };
    }
#endif

    private void Update()
    {
        if (!_triggered)
        {
            Wave();
            CheckApproach();
        }
        UpdateConfetti();

        // 「STAGE CLEAR!」が出るとき（クリアになったとき）、「お疲れ様!」の吹き出しは、消す。
        if (_bubble != null && goal != null && goal.IsDone)
        {
            Destroy(_bubble);
            _bubble = null;
        }
    }

    // ---- 紙吹雪 -----------------------------------------------------------------

    private static Sprite WhiteSprite()
    {
        if (s_white == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            s_white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
        return s_white;
    }

    private static Color PickFallbackColor()
    {
        switch (Random.Range(0, 5))
        {
            case 0: return new Color(0.92f, 0.28f, 0.25f, 1f); // 赤
            case 1: return new Color(0.98f, 0.82f, 0.25f, 1f); // 黄
            case 2: return new Color(0.28f, 0.45f, 0.90f, 1f); // 青
            case 3: return new Color(0.30f, 0.75f, 0.40f, 1f); // 緑
            default: return new Color(1f, 0.55f, 0.75f, 1f);   // 桃
        }
    }

    private Sprite PickConfettiSprite()
    {
        if (confettiSprites == null || confettiSprites.Length == 0)
            return null;
        for (int tries = 0; tries < 4; tries++)
        {
            Sprite s = confettiSprites[Random.Range(0, confettiSprites.Length)];
            if (s != null)
                return s;
        }
        return null;
    }

    /// <summary>紙吹雪を1枚、ゴールの上に作る。age が0より大きいときは、その秒数ぶん、すでに落ちた位置に作る（最初から降っているように見せる）。</summary>
    private void SpawnPiece(float age)
    {
        if (_confettiRoot == null)
            _confettiRoot = new GameObject("GoalConfetti");

        Sprite sprite = PickConfettiSprite();
        bool fallback = sprite == null;
        if (fallback)
            sprite = WhiteSprite();

        GameObject go = new GameObject("Piece");
        go.transform.SetParent(_confettiRoot.transform, false);

        float vy = confettiFallSpeed * Random.Range(0.7f, 1.3f);
        Vector3 c = Center();
        go.transform.position = c + new Vector3(
            confettiOffset.x + Random.Range(-confettiWidth * 0.5f, confettiWidth * 0.5f),
            confettiOffset.y - vy * age,
            0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = confettiSortingOrder;
        Color col = fallback ? PickFallbackColor() : Color.white;
        sr.color = col;

        float s = confettiSize / Mathf.Max(0.0001f, sprite.bounds.size.y) * Random.Range(0.8f, 1.25f);
        go.transform.localScale = new Vector3(fallback ? s * 0.6f : s, s, 1f);

        ConfettiPiece p = new ConfettiPiece();
        p.tr = go.transform;
        p.sr = sr;
        p.vy = vy;
        p.sway = Random.Range(0f, 6.28f);
        p.swayAmp = Random.Range(0.3f, 0.9f);
        p.swaySpeed = Random.Range(2f, 5f);
        p.spin = Random.Range(-260f, 260f);
        p.age = age;
        p.color = col;
        _pieces.Add(p);
    }

    private void UpdateConfetti()
    {
        // ゴールしたら、新しく降らせるのをやめて、降っているものを消していく。
        if (!_confettiStopped && goal != null && goal.IsDone)
        {
            _confettiStopped = true;
            _stopTime = 0f;
        }

        float dt = Time.deltaTime;

        if (!_confettiStopped && confettiEnabled && confettiPerSecond > 0f && confettiLifeSeconds > 0f)
        {
            _emitAcc += confettiPerSecond * dt;
            while (_emitAcc >= 1f)
            {
                _emitAcc -= 1f;
                SpawnPiece(0f);
            }
        }

        float stopMul = 1f;
        if (_confettiStopped)
        {
            _stopTime += Time.unscaledDeltaTime;   // ゴールの演出でゲームが遅くなっても、決まった秒数で消す
            stopMul = 1f - Mathf.Clamp01(_stopTime / Mathf.Max(0.01f, confettiStopFadeSeconds));
        }

        const float FadeTime = 0.5f;
        for (int i = _pieces.Count - 1; i >= 0; i--)
        {
            ConfettiPiece p = _pieces[i];
            if (p.tr == null)
            {
                _pieces.RemoveAt(i);
                continue;
            }
            p.age += dt;
            if (p.age >= confettiLifeSeconds || (_confettiStopped && stopMul <= 0f))
            {
                Destroy(p.tr.gameObject);
                _pieces.RemoveAt(i);
                continue;
            }

            p.sway += p.swaySpeed * dt;
            Vector3 pos = p.tr.position;
            pos.y -= p.vy * dt;
            pos.x += Mathf.Sin(p.sway) * p.swayAmp * dt;
            p.tr.position = pos;
            p.tr.Rotate(0f, 0f, p.spin * dt);

            Color c = p.color;
            c.a = Mathf.Clamp01((confettiLifeSeconds - p.age) / FadeTime) * stopMul;
            p.sr.color = c;
        }

        if (_confettiStopped && _pieces.Count == 0 && _confettiRoot != null)
        {
            Destroy(_confettiRoot);
            _confettiRoot = null;
        }
    }

    private void OnDestroy()
    {
        if (_confettiRoot != null)
            Destroy(_confettiRoot);
    }

    // ---- ウェーブ -------------------------------------------------------------

    private void Wave()
    {
        int n = letters != null ? letters.Length : 0;
        if (n == 0 || _basePos == null || _basePos.Length != n)
            return;

        float jump = Mathf.Max(0.05f, jumpSeconds);
        float cycle = (n - 1) * Mathf.Max(0f, delayBetween) + jump + Mathf.Max(0f, restSeconds);
        float t = Mathf.Repeat(Time.time, cycle);
        for (int i = 0; i < n; i++)
        {
            if (letters[i] == null)
                continue;
            float lt = t - i * Mathf.Max(0f, delayBetween);
            float y = (lt >= 0f && lt < jump) ? jumpHeight * Mathf.Sin(Mathf.PI * lt / jump) : 0f;
            letters[i].localPosition = _basePos[i] + new Vector3(0f, y, 0f);
        }
    }

    // ---- 近づいた ---------------------------------------------------------------

    private Vector3 Center()
    {
        Vector3 sum = Vector3.zero;
        int c = 0;
        for (int i = 0; i < letters.Length; i++)
        {
            if (letters[i] == null)
                continue;
            // 跳ねている最中でも、ぶれないように、足元の基準位置で数える。
            Transform p = letters[i].parent;
            Vector3 w = p != null ? p.TransformPoint(_basePos[i]) : _basePos[i];
            sum += w;
            c++;
        }
        return c > 0 ? sum / c : transform.position;
    }

    private void CheckApproach()
    {
        if (_player == null)
        {
            _findTimer -= Time.deltaTime;
            if (_findTimer > 0f)
                return;
            _findTimer = 0.5f;
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null)
                _player = p.transform;
            if (_player == null)
                return;
        }

        Vector3 c = Center();
        Vector3 pp = _player.position;
        if (Mathf.Abs(pp.x - c.x) <= approachRadius && Mathf.Abs(pp.y - c.y) <= approachHeight)
            StartCoroutine(Celebrate(c));
    }

    private IEnumerator Celebrate(Vector3 center)
    {
        _triggered = true;

        // 跳ねるのをやめて、元の位置に戻す
        for (int i = 0; i < letters.Length; i++)
        {
            if (letters[i] != null)
                letters[i].localPosition = _basePos[i];
        }

        if (cheerOnApproach)
        {
            for (int i = 0; i < _rigs.Length; i++)
            {
                if (_rigs[i] != null)
                    _rigs[i].SetMotion(LimbRig.KeyMotion.Cheer);
            }
        }

        _bubble = BuildBubble(center);
        if (_bubble != null)
            StartCoroutine(PopBubble(_bubble.transform));

        yield return new WaitForSeconds(Mathf.Max(0f, clearDelay));

        if (triggerClear && goal != null)
        {
            if (goal.mode == StageGoal.GoalMode.Touch)
                goal.Clear();
        }
    }

    private IEnumerator PopBubble(Transform t)
    {
        float dur = Mathf.Max(0.01f, bubblePopSeconds);
        float time = 0f;
        while (time < dur && t != null)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / dur);
            // 少し大きくなってから、1に落ち着く
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.2f, k / 0.7f) : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f);
            t.localScale = Vector3.one * s;
            yield return null;
        }
        if (t != null)
            t.localScale = Vector3.one;
    }

    // ---- 吹き出し ---------------------------------------------------------------

    private GameObject BuildBubble(Vector3 center)
    {
        if (string.IsNullOrEmpty(bubbleText))
            return null;

        Sprite frameSprite = MakeFrameSprite(bubbleFillColor, bubbleOutlineColor);
        Sprite tailSprite = MakeTailSprite(bubbleFillColor, bubbleOutlineColor);

        GameObject root = new GameObject("GoalBubble");
        root.transform.SetParent(transform, true);
        root.transform.position = center + new Vector3(bubbleOffset.x, bubbleOffset.y, 0f);
        root.transform.localScale = Vector3.zero;

        // 文字
        GameObject tg = new GameObject("Text");
        tg.transform.SetParent(root.transform, false);
        TextMeshPro tmp = tg.AddComponent<TextMeshPro>();
        string text = bubbleText.Replace('　', ' ');
        tmp.font = bubbleFont != null ? bubbleFont : ResolveFont(text);
        tmp.text = text;
        tmp.fontSize = bubbleFontSize;
        tmp.color = bubbleTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.sortingOrder = bubbleSortingOrder + 2;
        Vector2 pref = tmp.GetPreferredValues(text);
        tmp.rectTransform.sizeDelta = pref;

        // しっぽ（下）と、枠
        const float tailH = 0.25f;
        const float tailW = 0.4f;
        Vector2 size = pref + bubblePadding * 2f;
        size.x = Mathf.Max(size.x, 1f);
        size.y = Mathf.Max(size.y, 0.6f);

        GameObject tail = new GameObject("Tail");
        tail.transform.SetParent(root.transform, false);
        SpriteRenderer tsr = tail.AddComponent<SpriteRenderer>();
        tsr.sprite = tailSprite;
        tsr.sortingOrder = bubbleSortingOrder + 1;   // 枠の下のふちを、しっぽで隠す
        tail.transform.localPosition = new Vector3(0f, tailH * 0.5f, 0f);
        tail.transform.localScale = new Vector3(tailW / (tailSprite.rect.width / tailSprite.pixelsPerUnit), tailH / (tailSprite.rect.height / tailSprite.pixelsPerUnit), 1f);

        GameObject fg = new GameObject("Frame");
        fg.transform.SetParent(root.transform, false);
        SpriteRenderer fsr = fg.AddComponent<SpriteRenderer>();
        fsr.sprite = frameSprite;
        fsr.drawMode = SpriteDrawMode.Sliced;
        fsr.size = size;
        fsr.sortingOrder = bubbleSortingOrder;
        float frameY = tailH - 0.08f + size.y * 0.5f;   // しっぽと、少し重ねる
        fg.transform.localPosition = new Vector3(0f, frameY, 0f);
        tg.transform.localPosition = new Vector3(0f, frameY, 0f);
        return root;
    }

    /// <summary>角丸の四角（ふち付き）。9分割で、好きな大きさに伸ばせる。</summary>
    private static Sprite MakeFrameSprite(Color fill, Color outline)
    {
        const int N = 24;
        const float radius = 8f;
        const float border = 2f;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                // 角丸の四角の内側までの距離（負なら、中）
                float cx = Mathf.Min(x + 0.5f, N - x - 0.5f);
                float cy = Mathf.Min(y + 0.5f, N - y - 0.5f);
                float d;
                if (cx < radius && cy < radius)
                    d = Mathf.Sqrt((radius - cx) * (radius - cx) + (radius - cy) * (radius - cy)) - radius;
                else
                    d = -Mathf.Min(cx, cy);
                Color c;
                if (d > 0f) c = clear;
                else if (d > -border) c = outline;
                else c = fill;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0, SpriteMeshType.FullRect, new Vector4(9, 9, 9, 9));
    }

    /// <summary>しっぽ（下向きの三角形、ふち付き）。</summary>
    private static Sprite MakeTailSprite(Color fill, Color outline)
    {
        const int W = 16;
        const int H = 10;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < H; y++)
        {
            // 上（y=H-1）が広く、下（y=0）がとがる
            float half = (W * 0.5f) * (y + 1f) / H;
            for (int x = 0; x < W; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - W * 0.5f);
                Color c = clear;
                if (dx <= half)
                    c = (dx > half - 2f || y == 0) ? outline : fill;
                // 上のほうは、枠とつながるように、ふちを描かない
                if (y >= H - 3 && dx <= half)
                    c = fill;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W, 0, SpriteMeshType.FullRect);
    }

    /// <summary>シーンにある文字から、この文の文字が、全部表示できる（日本語が出る）フォントを探す。1つも無いときは、いちばん多く表示できるものを使う。</summary>
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

    private void OnDrawGizmosSelected()
    {
        Vector3 c = transform.position;
        if (letters != null && letters.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] == null)
                    continue;
                sum += letters[i].position;
                n++;
            }
            if (n > 0)
                c = sum / n;
        }
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(c, new Vector3(approachRadius * 2f, approachHeight * 2f, 0.1f));
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(c + (Vector3)bubbleOffset, 0.15f);
    }
}
