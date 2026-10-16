using UnityEngine;

/// <summary>
/// KEY ESCAPE - 敵の頭の上に「吹き出し（エモート）」を出す部品。
///
/// どのプレハブをどの場面で出すかは、Inspectorの欄にプレハブを入れて決める（Assets/Prefab/Emotes など）。
/// 欄が空なら、その場面では何も出さない。
///
///   ・索敵していない状態 … 索敵するまでの間、一定間隔で出す（出す → 消す を繰り返す。Ally.IsAlerted が false の間）
///   ・索敵した瞬間       … 一定時間だけ出す
///   ・遠距離攻撃を外した … 一定時間だけ出す（ThrowableProjectile が、弾が壁などに当たったときに知らせる）
///   ・遠距離攻撃が当たった … 一定時間だけ出す（ThrowableProjectile が、弾がプレイヤーに当たったときに知らせる）
///   ・HPが半分以下になった … 一度だけ、一定時間出す
///
/// 一度に出る吹き出しは1つ。時間つきの吹き出しが出ると、いま出ているものと入れ替わる。
/// 「索敵していない」吹き出しは、時間つきの吹き出しが出ている間は止まり、消えたあとまた間隔を数える。
/// 敵が倒れたら、吹き出しも消える。
///
/// 吹き出しは、敵の子ではなく、敵を追いかける別のオブジェクトの子として出す。
/// → 敵が左右を向き直しても（scale.xの反転）、敵の拡大率が0.5でも、吹き出しは裏返らず、大きさも変わらない。
///
/// 使い方: 追従型（Ally）または巡回型（Enemy）のルートに Add Component する。
/// 索敵の吹き出しは Ally 専用（Enemy には索敵がないので、HPの吹き出しだけが使える）。
/// </summary>
[DisallowMultipleComponent]
public class EmoteBubble : MonoBehaviour
{
    [Header("吹き出し（プレハブを入れる。空欄ならその場面では何も出さない）")]
    [Tooltip("索敵していない状態。索敵するまでの間、一定間隔で出す（Ally専用）")]
    public GameObject unawarePrefab;
    [Tooltip("「索敵していない」吹き出しを出している時間（秒）")]
    public float unawareShowDuration = 1.5f;
    [Tooltip("「索敵していない」吹き出しが消えている時間（秒）")]
    public float unawareInterval = 4f;
    [Tooltip("間隔のばらつき（±秒）。複数の敵が同時に出さないようにする")]
    public float unawareIntervalJitter = 1f;

    [Tooltip("索敵した瞬間（Ally専用）")]
    public GameObject noticePrefab;
    public float noticeDuration = 1.2f;

    [Tooltip("遠距離攻撃を外したとき（弾がプレイヤー以外に当たって消えたとき）")]
    public GameObject rangedMissPrefab;
    public float rangedMissDuration = 1.2f;

    [Tooltip("遠距離攻撃が当たったとき（弾がプレイヤーに当たったとき）")]
    public GameObject rangedHitPrefab;
    public float rangedHitDuration = 1.2f;

    [Tooltip("HPが半分以下になったとき（一度だけ）")]
    public GameObject halfHpPrefab;
    public float halfHpDuration = 1.5f;
    [Tooltip("最大HPに対する割合。0.5 = 半分")]
    [Range(0.05f, 0.95f)] public float halfHpRatio = 0.5f;

    [Header("表示位置")]
    [Tooltip("敵の位置からの距離（ワールド単位）。頭の上に来るように調整する")]
    public Vector3 offset = new Vector3(0f, 0.9f, 0f);
    [Tooltip("吹き出し全体の拡大率")]
    public float bubbleScale = 1f;
    [Tooltip("吹き出しの描画順に足す値。敵や背景より手前に出すため")]
    public int sortingOrderBoost = 100;

    private enum Kind { None, Unaware, Timed }

    private Ally _ally;
    private Enemy _enemy;
    private GameObject _anchor;
    private GameObject _bubble;
    private Kind _kind = Kind.None;
    private float _timer;

    private bool _hasLife;
    private float _maxLife;
    private bool _halfShown;
    private bool _wasAlerted;
    private bool _dead;
    private float _unawareTimer;
    private bool _unawareCycleStarted;

    private void Awake()
    {
        _ally = GetComponentInParent<Ally>();
        _enemy = GetComponentInParent<Enemy>();
    }

    private void Start()
    {
        float life = CurrentLife();
        if (life > 0f)
        {
            _hasLife = true;
            _maxLife = life;
        }
    }

    private void Update()
    {
        if (_dead)
            return;

        if (_hasLife && CurrentLife() <= 0f)
        {
            _dead = true;
            ClearBubble();
            if (_anchor != null) Destroy(_anchor);
            return;
        }

        // ---- 索敵した瞬間 ----
        if (_ally != null)
        {
            bool alerted = _ally.IsAlerted;
            if (alerted != _wasAlerted)
            {
                _wasAlerted = alerted;
                if (alerted)
                {
                    if (_kind == Kind.Unaware)
                        ClearBubble();
                    ShowTimed(noticePrefab, noticeDuration);
                }
            }
        }

        // ---- HPが半分以下 ----
        if (_hasLife && !_halfShown && CurrentLife() <= _maxLife * halfHpRatio)
        {
            _halfShown = true;
            ShowTimed(halfHpPrefab, halfHpDuration);
        }

        // ---- 時間つきの吹き出しを消す ----
        if (_kind == Kind.Timed)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                ClearBubble();
        }

        // ---- 索敵していない状態（時間つきの吹き出しが出ていないときだけ）----
        // 「出す → 消す」を繰り返す。最初に出るまでの時間はランダム（敵どうしがそろわないように）。
        if (_kind != Kind.Timed)
        {
            bool wantUnaware = _ally != null && !_wasAlerted;
            if (!wantUnaware)
            {
                if (_kind == Kind.Unaware)
                    ClearBubble();
                _unawareCycleStarted = false;
            }
            else
            {
                if (!_unawareCycleStarted)
                {
                    _unawareCycleStarted = true;
                    _unawareTimer = Random.Range(0.2f, Mathf.Max(0.2f, unawareInterval));
                }

                _unawareTimer -= Time.deltaTime;
                if (_kind == Kind.Unaware)
                {
                    if (_unawareTimer <= 0f)
                    {
                        ClearBubble();
                        _unawareTimer = NextUnawareInterval();
                    }
                }
                else if (_unawareTimer <= 0f)
                {
                    ShowPersistent(unawarePrefab);
                    _unawareTimer = _kind == Kind.Unaware ? Mathf.Max(0.1f, unawareShowDuration) : NextUnawareInterval();
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (_anchor == null)
            return;
        _anchor.transform.position = transform.position + offset;
        _anchor.transform.localScale = Vector3.one * bubbleScale;
    }

    private void OnDestroy()
    {
        if (_anchor != null)
            Destroy(_anchor);
    }

    /// <summary>遠距離攻撃を外したとき。ThrowableProjectile から呼ばれる。</summary>
    public void NotifyRangedMiss()
    {
        if (_dead)
            return;
        ShowTimed(rangedMissPrefab, rangedMissDuration);
    }

    /// <summary>遠距離攻撃が当たったとき。ThrowableProjectile から呼ばれる。</summary>
    public void NotifyRangedHit()
    {
        if (_dead)
            return;
        ShowTimed(rangedHitPrefab, rangedHitDuration);
    }

    private float NextUnawareInterval()
    {
        return Mathf.Max(0.1f, unawareInterval + Random.Range(-unawareIntervalJitter, unawareIntervalJitter));
    }

    // ---- 吹き出しの出し入れ ----------------------------------------------

    private void ShowTimed(GameObject prefab, float duration)
    {
        if (prefab == null)
            return; // 欄が空なら、いま出している吹き出しをそのままにする
        if (!Spawn(prefab))
            return;
        _kind = Kind.Timed;
        _timer = Mathf.Max(0.05f, duration);
    }

    private void ShowPersistent(GameObject prefab)
    {
        if (prefab == null)
            return;
        if (!Spawn(prefab))
            return;
        _kind = Kind.Unaware;
    }

    private bool Spawn(GameObject prefab)
    {
        ClearBubble();

        if (_anchor == null)
            _anchor = new GameObject("EmoteAnchor");
        _anchor.transform.position = transform.position + offset;
        _anchor.transform.localScale = Vector3.one * bubbleScale;

        _bubble = Instantiate(prefab, _anchor.transform);
        _bubble.transform.localPosition = Vector3.zero; // プレハブに残っている位置は使わない
        _bubble.transform.localRotation = Quaternion.identity;

        if (sortingOrderBoost != 0)
        {
            SpriteRenderer[] renderers = _bubble.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder += sortingOrderBoost;
        }
        return true;
    }

    private void ClearBubble()
    {
        if (_bubble != null)
            Destroy(_bubble);
        _bubble = null;
        _kind = Kind.None;
    }

    private float CurrentLife()
    {
        if (_ally != null) return _ally.life;
        if (_enemy != null) return _enemy.life;
        return 0f;
    }
}
