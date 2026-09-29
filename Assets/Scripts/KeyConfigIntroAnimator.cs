using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - Key Config画面を開いたときの演出（新デザイン差し替え用）。
///
/// 演出の流れ（すべてTabキーで画面を開いた直後に再生）：
///   1. 左上の「Key Config」看板が、画面上から落ちてきて定位置で止まる。
///      止まる瞬間に少しバウンドする（行き過ぎてから戻る）。
///   2. 看板の演出が終わったら、画面を横切る線のスプライト（上下2本）が
///      左から右へ徐々に表示されていく。
///   3. 木・猫などの追加の装飾スプライト（decorativeSprites）は、看板や線とは
///      独立して並行で「ふわっと」フェード＋拡大しながら表示される
///      （それぞれ個別に遅延時間を設定できる）。
///   4. それ以外のUI（キー一覧・現在の設定・キー情報パネルなど）は
///      このスクリプトでは何もしない＝最初から表示されたまま。
///
/// Key Config画面はTime.timeScale = 0で開く（KeyConfigUI.pauseGameWhileOpen）
/// ため、このスクリプトのアニメーションはWaitForSecondsではなく
/// Time.unscaledDeltaTimeを使って、タイムスケールの影響を受けずに
/// 再生されるようにしている。
///
/// 使い方（看板・線）：
///   1. このスクリプトをKey Config画面のpanelRoot（か、その中の適当な
///      子オブジェクト）に付ける。
///   2. signboard に「Key Config」看板のRectTransformを割り当てる
///      （現在シーンに置いてある位置がそのまま「定位置」として使われる）。
///      ※ 親にVertical/Horizontal Layout Groupが付いている場合、看板の
///      GameObjectにLayout Elementを追加してIgnore Layoutにチェックを
///      入れないと、レイアウト側に位置を上書きされて動いて見えない。
///   3. topLine / bottomLine に、画面上下の線スプライトのImageコンポーネント
///      を割り当てる。それぞれのImageコンポーネントで
///        Image Type = Filled
///        Fill Method = Horizontal
///        Fill Origin = Left
///      にしておくこと（これで fillAmount を 0→1 に動かすと左から右へ
///      徐々に表示される。Image TypeがFilledでないとfillAmountは無効）。
///   4. KeyConfigUIのIntro Animatorフィールドにこのスクリプトを割り当てる
///      （KeyConfigUI.Open()から自動でPlayIntro()が呼ばれる）。
///
/// 使い方（木・猫などの追加スプライト）：
///   1. 表示したいスプライトのGameObjectに、Add Component →
///      Canvas Groupを追加する（フェード（半透明化）に必須）。
///   2. このスクリプトのDecorative SpritesリストにサイズをGameObjectの数だけ
///      増やし、各要素のCanvas Groupにそのオブジェクトを割り当てる。
///      拡大しながら表示したい場合はRect TransformにもRectTransformを割り当てる
///      （空のままでもフェードだけは動く）。
///   3. Start ScaleやDelay、Durationを調整する（デフォルトのままでも動く）。
///   4. これらも、看板と同じくLayout Groupの子になっている場合は
///      Layout Element → Ignore Layoutを付けておくこと。
/// </summary>
public class KeyConfigIntroAnimator : MonoBehaviour
{
    [Header("看板（左上のKey Config看板）")]
    [Tooltip("Key Config看板のRectTransform。定位置（=シーンに配置してある現在の位置）から真上に飛ばしてから、そこへ落としてバウンドさせる。")]
    public RectTransform signboard;
    [Tooltip("定位置からどれだけ上から落としてくるか（ピクセル）。")]
    public float signboardDropHeight = 260f;
    [Tooltip("落ちてくる時間（秒）。バウンドも含めた全体の長さ。")]
    public float signboardDropDuration = 0.45f;
    [Tooltip("落下～バウンドの動きのカーブ。横軸=時間の進み(0→1)、縦軸=どれだけ定位置に近づいたか(0=開始位置の上、1=定位置)。1を少し超えてから1へ戻るようにしてあるので、そのままでも軽いバウンドになる。Inspectorでカーブの形を自由に調整できる。")]
    public AnimationCurve signboardDropCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.7f, 1.08f),
        new Keyframe(0.85f, 0.97f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    [Header("線スプライト（画面上下2本）")]
    [Tooltip("看板の演出が終わってから、線の演出が始まるまでの間（秒）。0にすると看板が止まると同時に始まる。")]
    public float lineStartDelay = 0.05f;
    [Tooltip("線が左端から右端まで表示されるのにかかる時間（秒）。")]
    public float lineRevealDuration = 0.4f;
    [Tooltip("上側の線。Image TypeをFilled・Fill MethodをHorizontal・Fill OriginをLeftにしておくこと。")]
    public Image topLine;
    [Tooltip("下側の線。上側と同じ設定にしておくこと。")]
    public Image bottomLine;
    public AnimationCurve lineRevealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [System.Serializable]
    public struct DecorativeSpriteIntro
    {
        [Tooltip("フェード対象。表示したいスプライトのGameObjectにCanvas Groupを付けて、それを割り当てる。")]
        public CanvasGroup canvasGroup;
        [Tooltip("任意。拡大しながら表示したい場合にRectTransformを割り当てる。空ならフェードのみ。")]
        public RectTransform rectTransform;
        [Range(0.1f, 1f)]
        [Tooltip("開始時のスケール（元のスケールに対する倍率）。1にするとスケール演出なし（フェードのみ）。0.8前後だと、わずかに小さい状態から等倍へふわっと拡大しながら表示される。")]
        public float startScale;
        [Tooltip("Tabキーで画面を開いてから、このスプライトの演出が始まるまでの遅延（秒）。看板や線とは独立して並行で再生されるので、他の演出とタイミングをずらしたい時に使う。")]
        public float delay;
        [Tooltip("フェード＋拡大にかかる時間（秒）。")]
        public float duration;
    }

    [Header("追加の装飾スプライト（木・猫など、ふわっとフェード＋拡大で表示）")]
    public DecorativeSpriteIntro[] decorativeSprites;

    private Vector2 _signboardRestPosition;
    private bool _cachedRestPosition;
    private Vector3[] _decorativeRestScales;
    private Coroutine _playingRoutine;

    private void Awake()
    {
        CacheSignboardRestPosition();
        CacheDecorativeRestScales();
    }

    private void CacheSignboardRestPosition()
    {
        if (_cachedRestPosition || signboard == null)
            return;
        _signboardRestPosition = signboard.anchoredPosition;
        _cachedRestPosition = true;
    }

    private void CacheDecorativeRestScales()
    {
        if (decorativeSprites == null)
            return;
        if (_decorativeRestScales != null && _decorativeRestScales.Length == decorativeSprites.Length)
            return; // 既にキャッシュ済み

        _decorativeRestScales = new Vector3[decorativeSprites.Length];
        for (int i = 0; i < decorativeSprites.Length; i++)
        {
            var rt = decorativeSprites[i].rectTransform;
            _decorativeRestScales[i] = rt != null ? rt.localScale : Vector3.one;
        }
    }

    /// <summary>KeyConfigUI.Open()から呼ぶ。演出を最初から再生する。</summary>
    public void PlayIntro()
    {
        CacheSignboardRestPosition();
        CacheDecorativeRestScales();

        if (!gameObject.activeInHierarchy)
            return; // 非アクティブな間はコルーチンを開始できない

        if (_playingRoutine != null)
            StopCoroutine(_playingRoutine);
        _playingRoutine = StartCoroutine(PlayIntroRoutine());

        // 木・猫などの装飾スプライトは、看板や線の進行とは関係なく
        // それぞれ独自の遅延で並行に再生する。
        if (decorativeSprites != null)
        {
            for (int i = 0; i < decorativeSprites.Length; i++)
                StartCoroutine(FadeInDecorativeSprite(decorativeSprites[i], _decorativeRestScales[i]));
        }
    }

    private IEnumerator PlayIntroRoutine()
    {
        // ---- 開始状態にリセット ----
        if (signboard != null)
            signboard.anchoredPosition = _signboardRestPosition + new Vector2(0f, signboardDropHeight);
        if (topLine != null)
            topLine.fillAmount = 0f;
        if (bottomLine != null)
            bottomLine.fillAmount = 0f;

        // ---- 1. 看板が落ちてバウンドする ----
        yield return DropSignboard();

        // ---- 2. 線が左から右へ表示される ----
        if (lineStartDelay > 0f)
            yield return WaitRealtime(lineStartDelay);

        yield return RevealLines();

        _playingRoutine = null;
    }

    private IEnumerator DropSignboard()
    {
        if (signboard == null || signboardDropDuration <= 0f)
        {
            if (signboard != null)
                signboard.anchoredPosition = _signboardRestPosition;
            yield break;
        }

        Vector2 start = _signboardRestPosition + new Vector2(0f, signboardDropHeight);
        float t = 0f;
        while (t < signboardDropDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / signboardDropDuration);
            float curved = signboardDropCurve.Evaluate(p);
            signboard.anchoredPosition = Vector2.LerpUnclamped(start, _signboardRestPosition, curved);
            yield return null;
        }
        signboard.anchoredPosition = _signboardRestPosition;
    }

    private IEnumerator RevealLines()
    {
        if (topLine == null && bottomLine == null)
            yield break;

        if (lineRevealDuration <= 0f)
        {
            if (topLine != null) topLine.fillAmount = 1f;
            if (bottomLine != null) bottomLine.fillAmount = 1f;
            yield break;
        }

        float t = 0f;
        while (t < lineRevealDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / lineRevealDuration);
            float curved = lineRevealCurve.Evaluate(p);
            if (topLine != null) topLine.fillAmount = curved;
            if (bottomLine != null) bottomLine.fillAmount = curved;
            yield return null;
        }

        if (topLine != null) topLine.fillAmount = 1f;
        if (bottomLine != null) bottomLine.fillAmount = 1f;
    }

    /// <summary>
    /// 木・猫などの装飾スプライト1個分のフェード＋拡大演出。看板や線とは独立して
    /// 動くので、PlayIntro()からスプライトの数だけ並行に呼び出される。
    /// </summary>
    private IEnumerator FadeInDecorativeSprite(DecorativeSpriteIntro deco, Vector3 restScale)
    {
        if (deco.canvasGroup == null)
            yield break;

        float startScale = deco.startScale <= 0f ? 1f : deco.startScale;

        // ---- 開始状態にリセット ----
        deco.canvasGroup.alpha = 0f;
        if (deco.rectTransform != null)
            deco.rectTransform.localScale = restScale * startScale;

        if (deco.delay > 0f)
            yield return WaitRealtime(deco.delay);

        float duration = Mathf.Max(0.01f, deco.duration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            deco.canvasGroup.alpha = p;
            if (deco.rectTransform != null)
                deco.rectTransform.localScale = Vector3.LerpUnclamped(restScale * startScale, restScale, p);
            yield return null;
        }

        deco.canvasGroup.alpha = 1f;
        if (deco.rectTransform != null)
            deco.rectTransform.localScale = restScale;
    }

    private IEnumerator WaitRealtime(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
