using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 画面の左上に、プレイヤーのHPゲージを出す。
///
/// 枠（HPGauge_Frame）の中に、マスが並ぶ。各マスは 白（HPGauge_White：失ったHP）の上に
/// 赤（HPGauge_Red：残っているHP）を重ねたもの。HPが減ると、右のマスから赤が白に変わる。
/// ダメージの量がマスの境目に合わないときは、途中のマスの赤を、左から割合だけ見せる。
///
/// 画像は Assets/Resources/HPGauge/ に置く（名前を変えないこと）。
/// シーンやプレハブには何も置かなくてよい。ゲーム開始時に自分で画面用のキャンバスを作り、
/// シーンにプレイヤー（CharacterController2D）がいる間だけ表示する。
/// 最大HPは、プレイヤーを見つけた時のHPとする（いまは10）。
///
/// 見た目の数値は、すぐ下の「調整用」の定数を書き換える。
/// </summary>
public class HpGaugeUI : MonoBehaviour
{
    // ================= 調整用の数値 =================

    const int CellCount = 5;               // マスの数。接触ダメージ2・最大HP10なら、5マスで1回の被弾ごとに1マス減る

    // 位置と大きさ（基準解像度 1920×1080 での値）
    const float FrameWidth = 640f;         // 枠の幅（高さは画像の縦横比から決まる）
    const float MarginLeft = 30f;          // 画面の左端からの距離
    const float MarginTop = 30f;           // 画面の上端からの距離

    // マスの並べ方（枠の幅に対する割合）
    const float CellPadX = 0.010f;         // 枠の内側の左右の余白
    const float CellGap = 0.012f;          // マスとマスの間のすき間
    const float CellHeightRatio = 0.76f;   // 枠の内側の高さに対する、マスの高さ

    // 動き
    const float GaugeSpeed = 0.8f;         // 1秒あたりに動く割合（0.8 なら、1マス = 0.2 を約0.25秒で動かす）

    const int SortingOrder = -100;         // 描く順。マイナスにして、設定画面・キーコンフィグ・会話などの画面の後ろに回す（死亡の暗転・赤い縁は、3000で一番手前）

    // 枠画像（HPGauge_Frame.png 2034×362）の「内側の透明な部分」の範囲。画像を差し替えたら合わせ直す。
    const float InnerLeft = 34f / 2034f;
    const float InnerRight = 1972f / 2034f;
    const float InnerTop = 49f / 362f;     // 上端から測る
    const float InnerBottom = 323f / 362f; // 上端から測る

    const string ResourcePath = "HPGauge/";

    // ==============================================================================

    private static HpGaugeUI _instance;

    private RectTransform _root;
    private Image[] _red;
    private CharacterController2D _player;
    private float _maxLife = 1f;
    private float _shown = 1f;
    private float _retryTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
            return;
        GameObject go = new GameObject("HpGaugeUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<HpGaugeUI>();
    }

    private void Awake()
    {
        Sprite frame = Resources.Load<Sprite>(ResourcePath + "HPGauge_Frame");
        Sprite red = Resources.Load<Sprite>(ResourcePath + "HPGauge_Red");
        Sprite white = Resources.Load<Sprite>(ResourcePath + "HPGauge_White");
        if (frame == null || red == null || white == null)
        {
            Debug.LogWarning("[HpGaugeUI] Assets/Resources/HPGauge/ に HPGauge_Frame / HPGauge_Red / HPGauge_White（Sprite）が見つからないため、HPゲージを出しません。");
            enabled = false;
            return;
        }

        BuildUI(frame, red, white);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this)
            _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        // 新しいシーンのプレイヤーを探し直す
        _player = null;
        _retryTimer = 0f;
        SetVisible(false);
    }

    // ================= 画面の部品を作る =================

    private void BuildUI(Sprite frame, Sprite red, Sprite white)
    {
        GameObject canvasObj = new GameObject("HpGaugeCanvas");
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // 枠と同じ大きさの入れ物（ここを左上に置く）
        float aspect = frame.rect.height / frame.rect.width;
        GameObject rootObj = new GameObject("Gauge", typeof(RectTransform));
        rootObj.transform.SetParent(canvasObj.transform, false);
        _root = (RectTransform)rootObj.transform;
        _root.anchorMin = new Vector2(0f, 1f);
        _root.anchorMax = new Vector2(0f, 1f);
        _root.pivot = new Vector2(0f, 1f);
        _root.anchoredPosition = new Vector2(MarginLeft, -MarginTop);
        _root.sizeDelta = new Vector2(FrameWidth, FrameWidth * aspect);

        // マスの位置（入れ物に対する割合）
        float innerW = InnerRight - InnerLeft;
        float cellW = (innerW - 2f * CellPadX - (CellCount - 1) * CellGap) / CellCount;
        float innerH = InnerBottom - InnerTop;
        float cellH = innerH * CellHeightRatio;
        float centerFromTop = (InnerTop + InnerBottom) * 0.5f;
        float yMin = 1f - (centerFromTop + cellH * 0.5f);
        float yMax = 1f - (centerFromTop - cellH * 0.5f);

        _red = new Image[CellCount];
        for (int i = 0; i < CellCount; i++)
        {
            float x0 = InnerLeft + CellPadX + i * (cellW + CellGap);
            Vector2 min = new Vector2(x0, yMin);
            Vector2 max = new Vector2(x0 + cellW, yMax);

            MakeImage("Cell" + i + "_White", _root, white, min, max);
            Image r = MakeImage("Cell" + i + "_Red", _root, red, min, max);
            r.type = Image.Type.Filled;
            r.fillMethod = Image.FillMethod.Horizontal;
            r.fillOrigin = (int)Image.OriginHorizontal.Left;
            r.fillAmount = 1f;
            _red[i] = r;
        }

        // 枠は一番手前（マスの上）に重ねる
        MakeImage("Frame", _root, frame, Vector2.zero, Vector2.one);
    }

    private static Image MakeImage(string name, RectTransform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false; // ボタンなどの操作をじゃましない
        return img;
    }

    // ================= 毎フレーム =================

    private void Update()
    {
        if (_player == null)
        {
            // プレイヤーがまだいない（読み込み中・タイトルなど）。少しずつ探す。
            _retryTimer -= Time.unscaledDeltaTime;
            if (_retryTimer <= 0f)
            {
                _retryTimer = 0.25f;
                TryBind();
            }
            return;
        }

        float life = _player.life;
        if (life > _maxLife)
            _maxLife = life;

        float target = Mathf.Clamp01(life / _maxLife);
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        _shown = Mathf.MoveTowards(_shown, target, GaugeSpeed * dt);
        Apply(_shown);
    }

    private void TryBind()
    {
        // Rigidbody2Dを持つものが、本物のプレイヤー（キーコンフィグのプレビュー用キャラなどは除く）。
        CharacterController2D p = null;
        CharacterController2D[] all = FindObjectsOfType<CharacterController2D>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].GetComponent<Rigidbody2D>() != null)
            {
                p = all[i];
                break;
            }
        }
        if (p == null)
            return;

        _player = p;
        _maxLife = Mathf.Max(0.0001f, p.life);
        _shown = Mathf.Clamp01(p.life / _maxLife);
        Apply(_shown);
        SetVisible(true);
    }

    /// <summary>ratio（0〜1）に合わせて、各マスの赤を、左から何割見せるか決める。</summary>
    private void Apply(float ratio)
    {
        for (int i = 0; i < CellCount; i++)
        {
            float fill = Mathf.Clamp01(ratio * CellCount - i);
            _red[i].fillAmount = fill;
            _red[i].enabled = fill > 0.001f;
        }
    }

    private void SetVisible(bool visible)
    {
        if (_root != null && _root.gameObject.activeSelf != visible)
            _root.gameObject.SetActive(visible);
    }
}
