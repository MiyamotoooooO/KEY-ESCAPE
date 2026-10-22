using UnityEngine;

/// <summary>
/// KEY ESCAPE - 「Spaceキーを押してね」などの案内を、ゆっくり点滅させる部品。
///
/// 点滅させたい案内のオブジェクト（Spaceキーの絵のImageなど）に付ける。
/// 子のImage・文字もまとめて点滅する（CanvasGroup を自動で付けて、透明度を動かす）。
/// ゲームを止めている間（Time.timeScale = 0）でも、実時間で点滅する。
/// 表示されるたびに、いちばん明るいところから始まる。
///
/// DialogueKeyRecruit の Space Prompt に入れたオブジェクトに付けると、
/// 返事が出ている間だけ表示されて点滅する。
/// </summary>
[DisallowMultipleComponent]
public class BlinkingPrompt : MonoBehaviour
{
    [Tooltip("1秒間に何回点滅するか。")]
    public float blinksPerSecond = 1.2f;
    [Tooltip("いちばん暗いときの透明度（0〜1）。")]
    [Range(0f, 1f)] public float minAlpha = 0.2f;
    [Tooltip("いちばん明るいときの透明度（0〜1）。")]
    [Range(0f, 1f)] public float maxAlpha = 1f;
    [Tooltip("明るいときに、少し大きくする割合（0なら大きさは変えない）。")]
    [Range(0f, 0.3f)] public float scalePulse = 0.04f;

    private CanvasGroup _group;
    private Vector3 _baseScale = Vector3.one;
    private bool _baseCaptured;
    private float _time;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();
        // 案内は見るだけ。クリックを邪魔しない。
        _group.interactable = false;
        _group.blocksRaycasts = false;

        _baseScale = transform.localScale;
        _baseCaptured = true;
    }

    private void OnEnable()
    {
        _time = 0f;
        Apply(1f);
    }

    private void OnDisable()
    {
        if (_baseCaptured)
            transform.localScale = _baseScale;
    }

    private void Update()
    {
        _time += Time.unscaledDeltaTime;
        // 1から始まり、0.5周期でいちばん暗くなる、なめらかな波（0〜1）。
        float wave = 0.5f + 0.5f * Mathf.Cos(_time * blinksPerSecond * Mathf.PI * 2f);
        Apply(wave);
    }

    private void Apply(float wave)
    {
        if (_group != null)
            _group.alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
        if (_baseCaptured)
            transform.localScale = _baseScale * (1f + scalePulse * wave);
    }
}
