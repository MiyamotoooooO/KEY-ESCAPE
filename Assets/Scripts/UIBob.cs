using UnityEngine;

/// <summary>
/// KEY ESCAPE - UIを、ゆっくり上下にふわふわ動かす部品（ステージ選択の「STAGE SELECT」の看板など）。
///
/// 使い方：動かしたいUI（RectTransform）に、このスクリプトを付ける。
/// 動かす量は「いまの位置からのずれ」として足し引きするので、位置を決めた後でも動かせる。
/// ゲームを止めている間（Time.timeScale = 0）も動く。
///
/// 看板が落ちてくる演出（KeyConfigIntroAnimator）と一緒に使う時は、「Start Delay」を、
/// 落ちてくる時間より長くしておく（演出が終わってから、ふわふわを始める）。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIBob : MonoBehaviour
{
    [Tooltip("上下に動く幅（ピクセル）。この値の分だけ、上と下に動く。")]
    public float amplitude = 10f;
    [Tooltip("1往復にかかる秒数。大きいほどゆっくり。")]
    public float period = 3f;
    [Tooltip("任意。左右に傾ける角度（度）。0なら傾けない。")]
    public float rotationAmplitude = 0f;
    [Tooltip("表示されてから、動き始めるまでの待ち（秒）。")]
    public float startDelay = 0.6f;
    [Tooltip("動き始めてから、最大の幅になるまでの時間（秒）。")]
    public float rampTime = 0.4f;

    private const float MaxDt = 0.05f;

    private RectTransform _rt;
    private Vector2 _lastOffset;
    private float _lastAngle;
    private float _time;

    private void Awake()
    {
        _rt = (RectTransform)transform;
    }

    private void OnEnable()
    {
        _time = 0f;
        _lastOffset = Vector2.zero;
        _lastAngle = 0f;
    }

    private void OnDisable()
    {
        // 足していたずれを戻して、元の位置に返す。
        if (_rt != null)
        {
            _rt.anchoredPosition -= _lastOffset;
            Vector3 e = _rt.localEulerAngles;
            e.z -= _lastAngle;
            _rt.localEulerAngles = e;
        }
        _lastOffset = Vector2.zero;
        _lastAngle = 0f;
    }

    private void Update()
    {
        _time += Mathf.Min(Time.unscaledDeltaTime, MaxDt);

        float k = Mathf.Clamp01((_time - startDelay) / Mathf.Max(0.01f, rampTime));
        float phase = _time * 2f * Mathf.PI / Mathf.Max(0.1f, period);
        Vector2 offset = new Vector2(0f, Mathf.Sin(phase) * amplitude * k);
        float angle = Mathf.Sin(phase * 0.7f) * rotationAmplitude * k;

        _rt.anchoredPosition += offset - _lastOffset;
        Vector3 euler = _rt.localEulerAngles;
        euler.z += angle - _lastAngle;
        _rt.localEulerAngles = euler;

        _lastOffset = offset;
        _lastAngle = angle;
    }
}
