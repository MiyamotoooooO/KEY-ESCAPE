using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// KEY ESCAPE - 「新しいキーを獲得した！」ポップアップの演出。
/// KeyPickup／Ally.keyDrop／DialogueKeyRecruitなど、キーを入手する手段が
/// 増えても呼び出し側を個別に書き換えなくて済むよう、KeyInventory.OnKeyAdded
/// （新しいキーが追加された瞬間に発火するイベント）を直接購読して動く。
/// つまりこのスクリプトはシーンに1つ置いておくだけで良く、KeyPickup側などから
/// 明示的に呼び出す必要はない。
///
/// なお、KeyInventoryは起動時（Awake）にStarting Keysをこの同じOnKeyAddedで
/// 追加するが、このスクリプトの購読はStart()で行う。UnityのAwakeは全オブジェクト
/// 分が先に呼ばれ、Start()はその後なので、ゲーム開始時点で最初から持っている
/// キー分についてはこのポップアップは（何もしなくても）出ない。
///
/// 見た目のパーツは全部バラバラのオブジェクトとして扱う想定（Inspectorで
/// それぞれ割り当てる）。文字は1文字ずつスプライトを用意するのではなく、
/// keyLetterText（TMP_Text）にKeyDefinition.displayNameをそのまま流し込む。
///
/// 演出の流れ:
///   1. 背景（backgroundRect）が小さい状態(backgroundStartScale)から
///      等倍(1.0)へスケールアップする。
///   2. タイトル文言（titleTextObject）と、横のキラキラ（sparkleObjects）が
///      同時に表示される（この時点ではまだ点滅しない、ただ出ているだけ）。
///   3. キー本体（keyCapGroup＝キーの枠＋文字をまとめたCanvasGroup）が
///      フェードインする。
///   4. 下の線のスプライト（underlineGroup）がフェードインする。
///   5. 横のキラキラ（sparkleObjects）が点滅ループを開始する（点灯）。
///   6. 右下の▼（continueArrow）が表示される。
///   7. ▼も点滅ループを開始する（点灯）。
///   8. ここでSpaceキー、またはcloseClickTarget（未設定ならpanelRoot）への
///      クリックを受け付けるようになり、閉じられる。
///
/// Time.timeScaleは変更しない（DialogueKeyRecruitと同じ方針）。代わりに
/// lockPlayerWhileShowingがtrueなら、表示中だけPlayerMovement.SetInputLocked()
/// でプレイヤーの入力を個別にロックする。
/// </summary>
public class KeyAcquiredPopup : MonoBehaviour
{
    [Header("UI参照（パーツごとに別オブジェクト）")]
    [Tooltip("ポップアップ全体の親。最初は非アクティブにしておく。")]
    public GameObject panelRoot;
    [Tooltip("背景の紙のスプライト。小さい状態から等倍へスケールアップする対象。")]
    public RectTransform backgroundRect;
    [Tooltip("「新しいキーを獲得した！」の文言。常に同じ文なのでテキスト差し替えはしない。")]
    public GameObject titleTextObject;
    [Tooltip("獲得したキーの文字（例：\"C\"）。KeyDefinition.displayNameを流し込む。")]
    public TMP_Text keyLetterText;
    [Tooltip("キーの見た目（枠＋文字）をまとめてフェードインさせるためのCanvasGroup。keyLetterTextを含む親に付けておく。")]
    public CanvasGroup keyCapGroup;
    [Tooltip("キーの下にある線のスプライト用CanvasGroup。")]
    public CanvasGroup underlineGroup;
    [Tooltip("キーの横にあるキラキラのスプライト一式（複数可）。表示→点滅ループの2段階で使う。")]
    public GameObject[] sparkleObjects;
    [Tooltip("右下の▼（続きを促すマーク）。表示→点滅ループの2段階で使う。")]
    public GameObject continueArrow;

    [Header("閉じる操作")]
    [Tooltip("Spaceキーの代わりにクリックでも閉じられるようにするための受け皿。未設定ならpanelRootを使う。Image等のRaycast Targetが有効なGraphicが付いている必要がある。")]
    public GameObject closeClickTarget;

    [Header("演出タイミング（調整用）")]
    [Tooltip("背景が『小さい状態』のスケール（1が等倍）。")]
    [Range(0.1f, 1f)] public float backgroundStartScale = 0.6f;
    [Tooltip("背景が小→大にスケールするのにかかる秒数。")]
    public float backgroundScaleDuration = 0.25f;
    [Tooltip("キー本体がフェードインするのにかかる秒数。")]
    public float keyFadeInDuration = 0.3f;
    [Tooltip("下の線のスプライトがフェードインするのにかかる秒数。")]
    public float underlineFadeInDuration = 0.2f;
    [Tooltip("横のキラキラの、点灯している時間（秒）。")]
    public float sparkleOnDuration = 0.12f;
    [Tooltip("横のキラキラの、消えている時間（秒）。")]
    public float sparkleOffDuration = 0.12f;
    [Tooltip("右下の▼の、点灯している時間（秒）。")]
    public float arrowOnDuration = 0.4f;
    [Tooltip("右下の▼の、消えている時間（秒）。")]
    public float arrowOffDuration = 0.4f;

    [Header("表示中の操作ロック（任意）")]
    [Tooltip("ポップアップを表示している間、プレイヤーの入力をロックするかどうか。DialogueKeyRecruitと同じ方針でTime.timeScaleは変更しない。")]
    public bool lockPlayerWhileShowing = true;
    [Tooltip("ロック対象のPlayerMovement。未設定ならシーン内から自動で探す。")]
    public PlayerMovement playerMovement;

    private bool _waitingForClose;
    private Coroutine _sequenceCoroutine;
    private Coroutine _sparkleBlinkCoroutine;
    private Coroutine _arrowBlinkCoroutine;

    /// <summary>closeClickTargetのクリックを本体に中継するだけの小さなヘルパー。</summary>
    private class CloseClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public KeyAcquiredPopup owner;
        public void OnPointerClick(PointerEventData eventData) => owner.RequestClose();
    }

    private void Start()
    {
        if (KeyInventory.Instance != null)
            KeyInventory.Instance.OnKeyAdded += OnKeyAdded;
        else
            Debug.LogWarning($"[KeyAcquiredPopup] {name}: シーンにKeyInventoryが見つかりません（Bootstrap未配置？） - キー獲得ポップアップは動作しません。", this);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        var clickTarget = closeClickTarget != null ? closeClickTarget : panelRoot;
        if (clickTarget != null)
        {
            var relay = clickTarget.GetComponent<CloseClickRelay>();
            if (relay == null)
                relay = clickTarget.AddComponent<CloseClickRelay>();
            relay.owner = this;
        }
    }

    private void OnDestroy()
    {
        if (KeyInventory.Instance != null)
            KeyInventory.Instance.OnKeyAdded -= OnKeyAdded;
    }

    private void Update()
    {
        if (_waitingForClose && Input.GetKeyDown(KeyCode.Space))
            RequestClose();
    }

    private void OnKeyAdded(KeyDefinition key)
    {
        // ★調査用ログ（原因が分かったら削除してください）
        Debug.Log($"[KeyAcquiredPopup] OnKeyAdded呼び出し: key={(key != null ? key.displayName : "null")}");

        // 既に表示中に別のキーが追加された場合（同時取得など）は、
        // 前の演出を打ち切って新しい方を優先する。
        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);

        _sequenceCoroutine = StartCoroutine(PlaySequence(key));
    }

    private IEnumerator PlaySequence(KeyDefinition key)
    {
        _waitingForClose = false;
        StopSparkleBlink(keepVisible: false);
        StopArrowBlink(keepVisible: false);

        if (lockPlayerWhileShowing)
        {
            if (playerMovement == null)
                playerMovement = FindObjectOfType<PlayerMovement>();
            if (playerMovement != null)
                playerMovement.SetInputLocked(true);
        }

        if (keyLetterText != null)
            keyLetterText.text = key != null ? key.displayName : "?";

        // 全パーツを初期状態に戻す。
        if (panelRoot != null)
            panelRoot.SetActive(true);

        // ★調査用ログ（原因が分かったら削除してください）
        Debug.Log($"[KeyAcquiredPopup] panelRoot.SetActive(true)実行直後: " +
            $"panelRoot={(panelRoot != null ? panelRoot.name : "null")}, " +
            $"activeSelf={(panelRoot != null ? panelRoot.activeSelf.ToString() : "N/A")}, " +
            $"activeInHierarchy={(panelRoot != null ? panelRoot.activeInHierarchy.ToString() : "N/A")}");

        if (backgroundRect != null)
            backgroundRect.localScale = Vector3.one * backgroundStartScale;
        if (titleTextObject != null)
            titleTextObject.SetActive(false);
        foreach (var s in sparkleObjects)
            if (s != null) s.SetActive(false);
        if (keyCapGroup != null)
        {
            keyCapGroup.gameObject.SetActive(true);
            keyCapGroup.alpha = 0f;
        }
        if (underlineGroup != null)
        {
            underlineGroup.gameObject.SetActive(true);
            underlineGroup.alpha = 0f;
        }
        if (continueArrow != null)
            continueArrow.SetActive(false);

        // 1. 背景：小さい状態から等倍へ。
        yield return ScaleUp(backgroundRect, backgroundStartScale, 1f, backgroundScaleDuration);

        // 2. タイトル文言と横のキラキラを同時に表示（この時点ではまだ点滅しない）。
        if (titleTextObject != null)
            titleTextObject.SetActive(true);
        foreach (var s in sparkleObjects)
            if (s != null) s.SetActive(true);

        // 3. キー本体をフェードイン。
        yield return FadeCanvasGroup(keyCapGroup, 0f, 1f, keyFadeInDuration);

        // 4. 下の線のスプライトをフェードイン。
        yield return FadeCanvasGroup(underlineGroup, 0f, 1f, underlineFadeInDuration);

        // 5. 横のキラキラを点灯（点滅ループ開始）。
        StartSparkleBlink();

        // 6. 右下の▼を表示。
        if (continueArrow != null)
            continueArrow.SetActive(true);

        // 7. ▼も点灯（点滅ループ開始）。
        StartArrowBlink();

        _sequenceCoroutine = null;
        _waitingForClose = true;

        // ★調査用ログ（原因が分かったら削除してください）
        Debug.Log("[KeyAcquiredPopup] PlaySequence完了、_waitingForClose=true");
    }

    /// <summary>Spaceキー、またはcloseClickTargetのクリックのどちらから来ても、ここに集約する。</summary>
    private void RequestClose()
    {
        if (!_waitingForClose)
            return;

        _waitingForClose = false;

        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        StopSparkleBlink(keepVisible: false);
        StopArrowBlink(keepVisible: false);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (lockPlayerWhileShowing && playerMovement != null)
            playerMovement.SetInputLocked(false);
    }

    // ---- 演出の細かい部品 ----------------------------------------------

    private IEnumerator ScaleUp(RectTransform rt, float fromScale, float toScale, float duration)
    {
        if (rt == null)
            yield break;

        if (duration <= 0f)
        {
            rt.localScale = Vector3.one * toScale;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            rt.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, ratio);
            yield return null;
        }
        rt.localScale = Vector3.one * toScale;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = to;
    }

    private void StartSparkleBlink()
    {
        StopSparkleBlink(keepVisible: false);
        _sparkleBlinkCoroutine = StartCoroutine(BlinkLoop(sparkleObjects, sparkleOnDuration, sparkleOffDuration));
    }

    /// <summary>keepVisible: 止めた後、消灯状態のままにせず点灯状態に揃えるかどうか。</summary>
    private void StopSparkleBlink(bool keepVisible)
    {
        if (_sparkleBlinkCoroutine != null)
        {
            StopCoroutine(_sparkleBlinkCoroutine);
            _sparkleBlinkCoroutine = null;
        }

        if (keepVisible)
        {
            foreach (var s in sparkleObjects)
                if (s != null) s.SetActive(true);
        }
    }

    private void StartArrowBlink()
    {
        StopArrowBlink(keepVisible: false);
        if (continueArrow != null)
            _arrowBlinkCoroutine = StartCoroutine(BlinkLoop(new[] { continueArrow }, arrowOnDuration, arrowOffDuration));
    }

    private void StopArrowBlink(bool keepVisible)
    {
        if (_arrowBlinkCoroutine != null)
        {
            StopCoroutine(_arrowBlinkCoroutine);
            _arrowBlinkCoroutine = null;
        }

        if (keepVisible && continueArrow != null)
            continueArrow.SetActive(true);
    }

    /// <summary>渡されたオブジェクト群をまとめて、on/offを繰り返し表示し続ける（点滅）。</summary>
    private IEnumerator BlinkLoop(GameObject[] targets, float onDuration, float offDuration)
    {
        while (true)
        {
            foreach (var t in targets)
                if (t != null) t.SetActive(true);
            yield return new WaitForSecondsRealtime(onDuration);

            foreach (var t in targets)
                if (t != null) t.SetActive(false);
            yield return new WaitForSecondsRealtime(offDuration);
        }
    }
}
