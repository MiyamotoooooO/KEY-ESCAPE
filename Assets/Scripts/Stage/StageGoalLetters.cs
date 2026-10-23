using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ゴールの見た目。キーキャラクター4体で「G」「O」「A」「L」を作り、
/// ふだんは、左から順に、ぴょんぴょんと跳ねる（ウェーブ）。
/// プレイヤーが近づくと、4体とも両腕を上げて大喜びして、「お疲れ様!」の吹き出しが出る。
/// そのあと、少しして、いつものゴールの演出（StageGoal の Clear）に進む。
/// 「お疲れ様!」の吹き出しは、最初から出し続け、プレイヤーが見つけて近づくと消える。
/// ゴールしたあと（クリアになったあと）は、踊りを1つランダムに選んで、4体そろって結果画面までループする。
/// さらに、4体から、プレイヤーに向けて、花火のような粒を打ち上げ続ける。
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
    [Tooltip("ONなら、「お疲れ様!」の吹き出しを、最初から出し続ける（プレイヤーに見つけてもらうため）。OFFなら、近づいたときに出す。")]
    public bool bubbleFromStart = true;
    [Tooltip("ONなら、プレイヤーが近づいたら（見つけたら）、吹き出しを消す。OFFなら、クリアになるまで出し続ける。")]
    public bool hideBubbleOnApproach = true;
    [Tooltip("吹き出しが、ふわふわ上下する幅（ワールドの単位）。0で動かない。")]
    public float bubbleFloatAmount = 0.08f;
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

    [Header("ゴールしたあとの踊り（結果画面まで、ループする）")]
    [Tooltip("ONなら、ゴールしたとき、下の動きの中から1つをランダムに選んで、4体そろってループする。OFFなら、「喜ぶ」のまま。")]
    public bool randomDanceOnClear = true;
    [Tooltip("ランダムに選ぶ動きの一覧。出したくない動きは、一覧から外す。")]
    public LimbRig.KeyMotion[] danceMotions = new LimbRig.KeyMotion[]
    {
        LimbRig.KeyMotion.Cheer,
        LimbRig.KeyMotion.Sway,
        LimbRig.KeyMotion.Hop,
        LimbRig.KeyMotion.Wave,
        LimbRig.KeyMotion.Disco,
    };

    [Header("花火（ゴールしたあと、4体からプレイヤーに向けて打ち上げる）")]
    public bool fireworksEnabled = true;
    [Tooltip("ゴールしてから、最初の1発までの秒数。")]
    public float fireworkFirstDelay = 0.1f;
    [Tooltip("次の1発までの秒数。")]
    public float fireworkInterval = 0.45f;
    [Tooltip("打ち上げを続ける秒数。0なら、シーンが変わるまで（結果画面のあとまで）続ける。")]
    public float fireworkDuration = 0f;
    [Tooltip("打ち上げた玉が、プレイヤーのそばに届くまでの秒数。")]
    public float fireworkRocketSeconds = 0.8f;
    [Tooltip("玉が向かう位置。プレイヤーからのずれ（上が＋Y）。")]
    public Vector2 fireworkAimOffset = new Vector2(0f, 3.2f);
    [Tooltip("向かう位置の、左右のばらつき。")]
    public float fireworkAimSpread = 1.6f;
    [Tooltip("玉が開いて、飛び散る粒の数。")]
    public int fireworkSparkCount = 24;
    [Tooltip("粒が飛び散る速さ（ワールドの単位/秒）。")]
    public float fireworkSparkSpeed = 4.5f;
    [Tooltip("粒が消えるまでの秒数。")]
    public float fireworkSparkLife = 1.2f;
    [Tooltip("粒の大きさ（ワールドの単位）。")]
    public float fireworkSparkSize = 0.18f;
    [Tooltip("粒が落ちる強さ。")]
    public float fireworkGravity = 3f;
    [Tooltip("花火の描く順番。大きいほど手前。")]
    public int fireworkSortingOrder = 700;

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
    private Vector3 _bubbleBase;
    private bool _bubbleFloat;
    private bool _cheered;
    private bool _danced;

    private class Rocket
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector3 from;
        public Vector3 ctrl;
        public Vector3 to;
        public float t;
        public float dur;
        public float trailAcc;
        public Color color;
    }
    private class Spark
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float age;
        public float life;
        public float size;
        public float drag;
        public float gravityMul;
        public Color color;
    }
    private readonly System.Collections.Generic.List<Rocket> _rockets = new System.Collections.Generic.List<Rocket>();
    private readonly System.Collections.Generic.List<Spark> _sparks = new System.Collections.Generic.List<Spark>();
    private GameObject _fwRoot;
    private bool _fwActive;
    private float _fwTimer;
    private float _fwElapsed;
    private static Sprite s_dot;
    private const int MaxSparks = 420;

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

        // 「お疲れ様!」の吹き出し：最初から出し続ける。
        if (bubbleFromStart)
            ShowBubble(Center());

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
        // ゴールしたら（クリアになったら）、踊りを1つ選んで、結果画面までループする。花火も始める。
        if (!_danced && goal != null && goal.IsDone)
            StartClearDance();

        if (!_triggered && !_danced)
        {
            Wave();
            CheckApproach();
        }
        UpdateConfetti();
        UpdateFireworks();

        // 「お疲れ様!」の吹き出し：ふわふわ動かす。
        if (_bubble != null && _bubbleFloat)
        {
            _bubble.transform.position = _bubbleBase + new Vector3(0f, Mathf.Sin(Time.time * 3f) * bubbleFloatAmount, 0f);
        }

        // 「STAGE CLEAR!」が出るとき（クリアになったとき）、「お疲れ様!」の吹き出しは、消す。
        if (_bubble != null && goal != null && goal.IsDone)
        {
            Destroy(_bubble);
            _bubble = null;
        }
    }

    // ---- ゴールしたあとの踊り ---------------------------------------------------

    private void StartClearDance()
    {
        _danced = true;
        _triggered = true;   // 跳ねるのも、近づいたかの判定も、もう要らない

        // 跳ねるのをやめて、元の位置に戻す
        if (letters != null && _basePos != null && _basePos.Length == letters.Length)
        {
            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] != null)
                    letters[i].localPosition = _basePos[i];
            }
        }

        LimbRig.KeyMotion motion = LimbRig.KeyMotion.Cheer;
        if (randomDanceOnClear)
            motion = PickDance();
        if (_rigs != null)
        {
            for (int i = 0; i < _rigs.Length; i++)
            {
                if (_rigs[i] != null)
                    _rigs[i].SetMotion(motion);
            }
        }

        _fwActive = fireworksEnabled;
        _fwTimer = Mathf.Max(0f, fireworkFirstDelay);
        _fwElapsed = 0f;
    }

    /// <summary>踊りの一覧から、1つ選ぶ。近づいたときに「喜ぶ」をしていたら、できれば、それ以外にする。</summary>
    private LimbRig.KeyMotion PickDance()
    {
        System.Collections.Generic.List<LimbRig.KeyMotion> pool = new System.Collections.Generic.List<LimbRig.KeyMotion>();
        if (danceMotions != null)
        {
            for (int i = 0; i < danceMotions.Length; i++)
            {
                LimbRig.KeyMotion m = danceMotions[i];
                if (m == LimbRig.KeyMotion.None || m == LimbRig.KeyMotion.Talk || m == LimbRig.KeyMotion.Injured)
                    continue;
                pool.Add(m);
            }
        }
        if (pool.Count == 0)
            return LimbRig.KeyMotion.Cheer;
        if (_cheered && pool.Count > 1)
            pool.Remove(LimbRig.KeyMotion.Cheer);
        return pool[Random.Range(0, pool.Count)];
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
        if (_fwRoot != null)
            Destroy(_fwRoot);
    }

    // ---- 花火（ゴールしたあと、4体からプレイヤーに向けて打ち上げる） -------------

    private static readonly Color[] FireworkColors = new Color[]
    {
        new Color(1f, 0.35f, 0.30f, 1f),   // 赤
        new Color(1f, 0.85f, 0.30f, 1f),   // 黄
        new Color(0.40f, 0.85f, 1f, 1f),   // 水色
        new Color(0.50f, 1f, 0.50f, 1f),   // 緑
        new Color(1f, 0.55f, 0.85f, 1f),   // 桃
        new Color(1f, 0.60f, 0.20f, 1f),   // 橙
    };

    /// <summary>やわらかい丸い光の画像（中心が白く、まわりへいくほど透明）。</summary>
    private static Sprite DotSprite()
    {
        if (s_dot != null)
            return s_dot;
        const int N = 32;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f;
                float dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = Mathf.Pow(a, 0.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        s_dot = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), N);
        return s_dot;
    }

    private Transform FindPlayerTransform()
    {
        if (_player != null)
            return _player;
        // Playerタグが付いたもののうち、Rigidbody2Dを持つものが、本物のプレイヤー
        GameObject[] c = GameObject.FindGameObjectsWithTag(playerTag);
        for (int i = 0; i < c.Length; i++)
        {
            if (c[i].GetComponent<Rigidbody2D>() != null)
            {
                _player = c[i].transform;
                return _player;
            }
        }
        if (c.Length > 0)
            _player = c[0].transform;
        return _player;
    }

    private void UpdateFireworks()
    {
        if (!_fwActive && _rockets.Count == 0 && _sparks.Count == 0)
            return;

        // ゴールの演出でゲームが遅くなっても、花火は同じ速さで動かす（ポーズ中は止める）。
        float dt = GamePause.IsPaused ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (dt <= 0f)
            return;

        if (_fwActive)
        {
            _fwElapsed += dt;
            if (fireworkDuration > 0f && _fwElapsed >= fireworkDuration)
            {
                _fwActive = false;
            }
            else
            {
                _fwTimer -= dt;
                if (_fwTimer <= 0f)
                {
                    _fwTimer += Mathf.Max(0.05f, fireworkInterval);
                    LaunchRocket();
                }
            }
        }

        // 打ち上げた玉
        for (int i = _rockets.Count - 1; i >= 0; i--)
        {
            Rocket r = _rockets[i];
            if (r.tr == null)
            {
                _rockets.RemoveAt(i);
                continue;
            }
            r.t += dt / Mathf.Max(0.05f, r.dur);
            float k = Mathf.Clamp01(r.t);
            Vector3 pos = Bezier(r.from, r.ctrl, r.to, k);
            r.tr.position = pos;

            // 通ったあとに、小さな光を残す
            r.trailAcc += dt;
            while (r.trailAcc >= 0.03f)
            {
                r.trailAcc -= 0.03f;
                AddSpark(pos, Vector2.zero, r.color, fireworkSparkSize * 0.6f, 0.35f, 0f, 0f);
            }

            if (r.t >= 1f)
            {
                Burst(pos, r.color);
                Destroy(r.tr.gameObject);
                _rockets.RemoveAt(i);
            }
        }

        // 飛び散った粒
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            Spark s = _sparks[i];
            if (s.tr == null)
            {
                _sparks.RemoveAt(i);
                continue;
            }
            s.age += dt;
            if (s.age >= s.life)
            {
                Destroy(s.tr.gameObject);
                _sparks.RemoveAt(i);
                continue;
            }
            s.vel.y -= fireworkGravity * s.gravityMul * dt;
            s.vel *= Mathf.Max(0f, 1f - s.drag * dt);
            Vector3 p = s.tr.position;
            p.x += s.vel.x * dt;
            p.y += s.vel.y * dt;
            s.tr.position = p;

            float u = s.age / s.life;
            Color c = s.color;
            c.a = Mathf.Clamp01(1f - u * u);
            s.sr.color = c;
            float sz = s.size * Mathf.Lerp(1f, 0.4f, u);
            s.tr.localScale = new Vector3(sz, sz, 1f);
        }

        if (!_fwActive && _rockets.Count == 0 && _sparks.Count == 0 && _fwRoot != null)
        {
            Destroy(_fwRoot);
            _fwRoot = null;
        }
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private Transform FwRoot()
    {
        if (_fwRoot == null)
            _fwRoot = new GameObject("GoalFireworks");
        return _fwRoot.transform;
    }

    /// <summary>4体のどれかから、プレイヤーの上あたりに向けて、玉を1つ打ち上げる。</summary>
    private void LaunchRocket()
    {
        if (letters == null || letters.Length == 0)
            return;

        // 打ち上げる文字を、ランダムに選ぶ（左から順にしないほうが、にぎやか）
        Transform src = null;
        for (int tries = 0; tries < 8 && src == null; tries++)
            src = letters[Random.Range(0, letters.Length)];
        if (src == null)
            return;

        Vector3 from = src.position + new Vector3(0f, 0.9f, 0f);
        Transform pl = FindPlayerTransform();
        Vector3 target = pl != null ? pl.position : Center();
        Vector3 to = target + new Vector3(
            fireworkAimOffset.x + Random.Range(-fireworkAimSpread, fireworkAimSpread),
            fireworkAimOffset.y + Random.Range(-0.6f, 0.8f),
            0f);
        to.z = from.z;
        Vector3 ctrl = (from + to) * 0.5f + new Vector3(0f, Random.Range(1.5f, 3.2f), 0f);

        Color col = FireworkColors[Random.Range(0, FireworkColors.Length)];

        GameObject go = new GameObject("Rocket");
        go.transform.SetParent(FwRoot(), false);
        go.transform.position = from;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DotSprite();
        sr.sortingOrder = fireworkSortingOrder + 1;
        sr.color = Color.Lerp(col, Color.white, 0.5f);
        float sz = fireworkSparkSize * 1.8f;
        go.transform.localScale = new Vector3(sz, sz, 1f);

        Rocket r = new Rocket();
        r.tr = go.transform;
        r.sr = sr;
        r.from = from;
        r.ctrl = ctrl;
        r.to = to;
        r.t = 0f;
        r.dur = fireworkRocketSeconds * Random.Range(0.85f, 1.15f);
        r.color = col;
        _rockets.Add(r);
    }

    /// <summary>玉が開いて、粒が丸く飛び散る。</summary>
    private void Burst(Vector3 pos, Color col)
    {
        int n = Mathf.Max(6, fireworkSparkCount);
        float offset = Random.Range(0f, 6.2832f);
        for (int i = 0; i < n; i++)
        {
            float ang = offset + 6.2832f * i / n + Random.Range(-0.08f, 0.08f);
            float sp = fireworkSparkSpeed * Random.Range(0.7f, 1.05f);
            Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
            AddSpark(pos, v, col, fireworkSparkSize, fireworkSparkLife * Random.Range(0.85f, 1.1f), 1.4f, 1f);
        }
        // 中心に、白い小さな粒（きらっと光る）
        int core = Mathf.Max(3, n / 4);
        for (int i = 0; i < core; i++)
        {
            float ang = Random.Range(0f, 6.2832f);
            float sp = fireworkSparkSpeed * Random.Range(0.1f, 0.4f);
            Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp;
            AddSpark(pos, v, Color.Lerp(col, Color.white, 0.8f), fireworkSparkSize * 0.8f, fireworkSparkLife * 0.6f, 2f, 0.6f);
        }
    }

    private void AddSpark(Vector3 pos, Vector2 vel, Color col, float size, float life, float drag, float gravityMul)
    {
        if (_sparks.Count >= MaxSparks)
            return;
        GameObject go = new GameObject("Spark");
        go.transform.SetParent(FwRoot(), false);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DotSprite();
        sr.sortingOrder = fireworkSortingOrder;
        sr.color = col;
        go.transform.localScale = new Vector3(size, size, 1f);

        Spark s = new Spark();
        s.tr = go.transform;
        s.sr = sr;
        s.vel = vel;
        s.age = 0f;
        s.life = Mathf.Max(0.1f, life);
        s.size = size;
        s.drag = drag;
        s.gravityMul = gravityMul;
        s.color = col;
        _sparks.Add(s);
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
            _cheered = true;
            for (int i = 0; i < _rigs.Length; i++)
            {
                if (_rigs[i] != null)
                    _rigs[i].SetMotion(LimbRig.KeyMotion.Cheer);
            }
        }

        // 吹き出し：最初から出していたら、見つけてもらったので、消す（設定でそのまま残せる）。
        // 最初は出していなかったときは、ここで出す。
        if (_bubble == null && !bubbleFromStart)
            ShowBubble(center);
        else if (_bubble != null && hideBubbleOnApproach)
            StartCoroutine(HideBubble());

        yield return new WaitForSeconds(Mathf.Max(0f, clearDelay));

        if (triggerClear && goal != null)
        {
            if (goal.mode == StageGoal.GoalMode.Touch)
                goal.Clear();
        }
    }

    /// <summary>吹き出しを作って、ポンと出す。</summary>
    private void ShowBubble(Vector3 center)
    {
        _bubble = BuildBubble(center);
        if (_bubble == null)
            return;
        _bubbleBase = _bubble.transform.position;
        _bubbleFloat = false;
        StartCoroutine(PopBubble(_bubble.transform));
    }

    /// <summary>吹き出しを、ぽんと小さくして、消す。</summary>
    private IEnumerator HideBubble()
    {
        GameObject b = _bubble;
        _bubbleFloat = false;
        if (b == null)
            yield break;
        Transform t = b.transform;
        float dur = 0.2f;
        float time = 0f;
        while (time < dur && t != null)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / dur);
            t.localScale = Vector3.one * Mathf.Lerp(1f, 0f, k * k);
            yield return null;
        }
        if (b != null)
            Destroy(b);
        if (_bubble == b)
            _bubble = null;
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
        {
            t.localScale = Vector3.one;
            if (_bubble != null && _bubble.transform == t && !_triggered)
                _bubbleFloat = true;
        }
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
