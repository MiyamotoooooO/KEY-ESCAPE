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
///   ・ダメージを受けた       … 一定時間だけ出す（巡回型 Enemy が ApplyDamage で知らせる）
///   ・会話型（DialogueKeyRecruit と同じオブジェクトに付けた場合）
///        話しかける前 … 話しかけるまで、ずっと出し続ける
///        質問のとき   … 会話が始まった時に出す（Keep Dialogue Bubble Until Next がONなら、選択肢を選ぶまで出し続ける）
///        選択したとき … 選択肢を選んだ時に出す（ONなら、会話が閉じるまで出し続ける。OFFなら一定時間だけ）
///
/// 「索敵していない」吹き出しは、1つ目と2つ目の欄のうち、入っているものからランダムに選ぶ。
/// 巡回型（Enemy）は索敵しないので、生きている間ずっとこの状態として、一定間隔で出す。
/// 会話の吹き出しは、会話中にゲームが止まっていても進むよう、実時間で時間を数える。
///
/// 一度に出る吹き出しは1つ。時間つきの吹き出しが出ると、いま出ているものと入れ替わる。
/// 「索敵していない」吹き出しは、時間つきの吹き出しが出ている間は止まり、消えたあとまた間隔を数える。
/// 敵が倒れたら、吹き出しも消える。
///
/// 吹き出しは、敵の子ではなく、敵を追いかける別のオブジェクトの子として出す。
/// → 敵が左右を向き直しても（scale.xの反転）、敵の拡大率が0.5でも、吹き出しは裏返らず、大きさも変わらない。
///
/// 使い方: 追従型（Ally）・巡回型（Enemy）のルート、または会話型（DialogueKeyRecruit）と同じオブジェクトに Add Component する。
/// 「索敵した瞬間」は Ally 専用。
/// </summary>
[DisallowMultipleComponent]
public class EmoteBubble : MonoBehaviour
{
    [Header("吹き出し（プレハブを入れる。空欄ならその場面では何も出さない）")]
    [Tooltip("索敵していない状態。索敵するまでの間、一定間隔で出す。巡回型は生きている間ずっとこの状態")]
    public GameObject unawarePrefab;
    [Tooltip("索敵していない状態の2つ目（任意）。入れると、1つ目と2つ目からランダムに出す")]
    public GameObject unawarePrefab2;
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

    [Tooltip("ダメージを受けたとき（巡回型 Enemy 用。固定の1つ）")]
    public GameObject damagedPrefab;
    public float damagedDuration = 1.2f;

    [Header("会話型（DialogueKeyRecruit と同じオブジェクトに付けたとき）")]
    [Tooltip("話しかける前。話しかけるまで、ずっと出し続ける")]
    public GameObject dialogueWaitingPrefab;
    [Tooltip("質問のとき（会話が始まった時）")]
    public GameObject dialogueQuestionPrefab;
    [Tooltip("質問の吹き出しを出している時間（秒）")]
    public float dialogueQuestionDuration = 1.5f;
    [Tooltip("選択肢を選んだとき")]
    public GameObject dialogueChoicePrefab;
    [Tooltip("選択の吹き出しを出している時間（秒）")]
    public float dialogueChoiceDuration = 1.5f;
    [Tooltip("ONなら、質問の吹き出しは選択肢を選ぶまで、選択の吹き出しは会話が閉じるまで、出し続ける（上の「時間」は使わない）。OFFなら、上の「時間」が過ぎたら消える。")]
    public bool keepDialogueBubbleUntilNext = true;

    [Header("表示位置")]
    [Tooltip("敵の位置からの距離（ワールド単位）。頭の上に来るように調整する")]
    public Vector3 offset = new Vector3(0f, 0.9f, 0f);
    [Tooltip("吹き出し全体の拡大率")]
    public float bubbleScale = 1f;
    [Tooltip("吹き出しの描画順に足す値。敵や背景より手前に出すため")]
    public int sortingOrderBoost = 100;

    private enum Kind { None, Unaware, Persistent, Timed }

    private Ally _ally;
    private Enemy _enemy;
    private DialogueKeyRecruit _dialogue;
    private bool _dialogueStarted;
    private GameObject _currentPrefab;
    private bool _timedUnscaled;
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
        _dialogue = GetComponent<DialogueKeyRecruit>();
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

        // ---- 会話型：話しかける前は、ずっと出し続ける ----
        if (_dialogue != null && !_dialogueStarted && _kind == Kind.None && dialogueWaitingPrefab != null)
        {
            Spawn(dialogueWaitingPrefab);
            _kind = Kind.Persistent;
        }

        // ---- 時間つきの吹き出しを消す ----
        if (_kind == Kind.Timed)
        {
            // 会話の吹き出しは、ゲームが止まっている間も進める
            _timer -= _timedUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_timer <= 0f)
                ClearBubble();
        }

        // ---- 索敵していない状態（時間つきの吹き出しが出ていないときだけ）----
        // 「出す → 消す」を繰り返す。最初に出るまでの時間はランダム（敵どうしがそろわないように）。
        if (_kind != Kind.Timed)
        {
            // 追従型は索敵するまで。巡回型（Enemy）は索敵しないので、生きている間ずっと。
            bool wantUnaware = (_ally != null && !_wasAlerted) || (_ally == null && _enemy != null);
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
                    ShowUnaware(PickUnawarePrefab());
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

    /// <summary>ダメージを受けたとき。巡回型 Enemy.ApplyDamage から呼ばれる。</summary>
    public void NotifyDamaged()
    {
        if (_dead)
            return;
        ShowTimed(damagedPrefab, damagedDuration);
    }

    /// <summary>会話が始まった（質問が出た）とき。DialogueKeyRecruit から呼ばれる。</summary>
    public void NotifyDialogueStart()
    {
        if (_dead)
            return;
        _dialogueStarted = true;
        ClearBubble(); // 話しかける前の吹き出しを消す
        if (keepDialogueBubbleUntilNext)
            ShowPersistent(dialogueQuestionPrefab);
        else
            ShowTimed(dialogueQuestionPrefab, dialogueQuestionDuration, true);
    }

    /// <summary>選択肢を選んだとき。DialogueKeyRecruit から呼ばれる。</summary>
    public void NotifyDialogueChoice()
    {
        if (_dead)
            return;
        ClearBubble(); // 質問の吹き出しが残っていれば消す
        if (keepDialogueBubbleUntilNext)
            ShowPersistent(dialogueChoicePrefab);
        else
            ShowTimed(dialogueChoicePrefab, dialogueChoiceDuration, true);
    }

    /// <summary>会話が閉じられたとき（Spaceキーやクリックで次へ進んだとき）。DialogueKeyRecruit から呼ばれる。</summary>
    public void NotifyDialogueEnd()
    {
        if (_dead)
            return;
        ClearBubble();
    }

    /// <summary>「索敵していない」吹き出しを、入っているものからランダムに選ぶ。</summary>
    private GameObject PickUnawarePrefab()
    {
        if (unawarePrefab != null && unawarePrefab2 != null)
            return Random.value < 0.5f ? unawarePrefab : unawarePrefab2;
        return unawarePrefab != null ? unawarePrefab : unawarePrefab2;
    }

    private float NextUnawareInterval()
    {
        return Mathf.Max(0.1f, unawareInterval + Random.Range(-unawareIntervalJitter, unawareIntervalJitter));
    }

    // ---- 吹き出しの出し入れ ----------------------------------------------

    private void ShowTimed(GameObject prefab, float duration, bool unscaled = false)
    {
        if (prefab == null)
            return; // 欄が空なら、いま出している吹き出しをそのままにする

        // 同じ吹き出しがすでに出ているときは、出し直さず、時間だけ延ばす（連続ダメージでチラつかないように）
        if (_kind == Kind.Timed && _currentPrefab == prefab && _bubble != null)
        {
            _timer = Mathf.Max(0.05f, duration);
            _timedUnscaled = unscaled;
            return;
        }

        if (!Spawn(prefab))
            return;
        _kind = Kind.Timed;
        _timer = Mathf.Max(0.05f, duration);
        _timedUnscaled = unscaled;
    }

    /// <summary>消すまで出し続ける吹き出し（会話用）。ゲームが止まっていてもアニメーションが動く。</summary>
    private void ShowPersistent(GameObject prefab)
    {
        if (prefab == null)
            return;
        if (!Spawn(prefab, true))
            return;
        _kind = Kind.Persistent;
    }

    private void ShowUnaware(GameObject prefab)
    {
        if (prefab == null)
            return;
        if (!Spawn(prefab))
            return;
        _kind = Kind.Unaware;
    }

    private bool Spawn(GameObject prefab, bool unscaledAnimators = false)
    {
        ClearBubble();

        if (_anchor == null)
            _anchor = new GameObject("EmoteAnchor");
        _anchor.transform.position = transform.position + offset;
        _anchor.transform.localScale = Vector3.one * bubbleScale;

        _bubble = Instantiate(prefab, _anchor.transform);
        _bubble.transform.localPosition = Vector3.zero; // プレハブに残っている位置は使わない
        _bubble.transform.localRotation = Quaternion.identity;
        _currentPrefab = prefab;

        // ゲームを止めている間に出した吹き出しも、アニメーションが動くようにする
        if (GamePause.IsPaused || unscaledAnimators)
        {
            Animator[] animators = _bubble.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
                animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;
        }

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
        _currentPrefab = null;
        _kind = Kind.None;
    }

    private float CurrentLife()
    {
        if (_ally != null) return _ally.life;
        if (_enemy != null) return _enemy.life;
        return 0f;
    }
}
