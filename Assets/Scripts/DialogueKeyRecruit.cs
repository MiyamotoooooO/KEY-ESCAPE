using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// KEY ESCAPE - 会話で仲間にするキー用のスクリプト。
/// KeyPickup（触れるだけで入手）、Ally.keyDrop（倒すと入手）に続く、
/// 3つ目の入手方法。プレイヤーが近づくと1つの質問と2〜3択の選択肢を出し、
/// どれを選んでも（選択肢ごとに返事のセリフだけが変わり）最終的に必ず
/// 仲間になる。ステージ1のJキーのように、「まだAttackを何にも割り当てて
/// おらず戦えない」キャラクター向けの、戦闘を介さない入手ルート。
///
/// 正解・不正解の判定は無い（対話イベント寄りの仕様）。選択肢は演出用。
///
/// 会話の流れ:
///   1. プレイヤーが近づくと質問文が表示され、選択肢ボタンが上から順番に
///      （choiceRevealInterval秒おきに）1つずつ表示されていく。
///   2. 選択肢を1つ選ぶと、その場で（同じテキスト欄の中身を書き換えて）
///      返事のセリフに切り替わり、選択肢ボタンは消える。
///   3. その状態でSpaceキーを押すか、closeClickTarget（未設定ならdialoguePanel）を
///      クリックすると会話が終了し、その瞬間にkeyDefinitionが渡される。
///
/// 会話中もTime.timeScaleは変更しない（＝ゲームは止めない）。これはプレイヤーや
/// 相手キャラのアイドルアニメーションを会話中も再生し続けたいという理由による。
/// その代わり、会話が始まった瞬間にトリガーへ入ってきたPlayerのPlayerMovementを
/// 探して、PlayerMovement.SetInputLocked(true)でプレイヤーの入力だけを個別に
/// 止める（会話終了時にfalseへ戻す）。これによりプレイヤー自身は動かないまま、
/// アニメーションや他のオブジェクトは通常通り動き続ける。
///
/// セットアップ:
///  - Collider2Dを「Is Trigger」に設定したGameObjectに付ける。
///  - keyDefinitionに渡すKeyDefinitionアセット（例：Key_J）を割り当てる。
///  - question と choices（各選択肢のボタンラベル＋選んだ後に表示する返事）を
///    Inspectorで設定する。
///  - シーンに会話パネル一式（パネル本体・質問と返事を兼ねるテキスト1つ・
///    選択肢ボタン2〜3個）を作り、下記の参照を割り当てる。質問と返事は
///    同じdialogueTextを使い回すので、表示位置は1箇所で構わない。
///    選択肢ボタンの子にTMP_Textを1つ置いておけば、ラベルは自動で書き換わる。
///  - （任意）マウスが乗った選択肢の横に▲などのマークを出したい場合は、
///    その画像用のUI ImageのRectTransformをhoverCursorに割り当てる。
///    未設定のままなら何も表示しない今まで通りの見た目になる。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DialogueKeyRecruit : MonoBehaviour
{
    [System.Serializable]
    public class Choice
    {
        [Tooltip("選択肢ボタンに表示するテキスト。")]
        public string choiceLabel;

        [TextArea]
        [Tooltip("この選択肢を選んだ瞬間に、質問文と入れ替えて表示する返事のセリフ。")]
        public string response;
    }

    [Header("会話の中身")]
    [TextArea]
    [Tooltip("最初に表示する質問文。")]
    public string question;

    [Tooltip("2〜3個を想定。UI側のボタン数より少なくても、余ったボタンは自動で非表示になる。")]
    public Choice[] choices;

    [Tooltip("会話の結果、プレイヤーに渡されるキー。")]
    public KeyDefinition keyDefinition;

    [Header("UI参照（シーンに用意した会話パネル一式）")]
    public GameObject dialoguePanel;
    [Tooltip("質問と返事を両方表示する、共通の1つのテキスト。選択後はここの中身を返事に書き換える。")]
    public TMP_Text dialogueText;
    [Tooltip("シーン側に用意したボタン一式。choicesの数より多くてもよい（余りは自動で隠れる）。")]
    public Button[] choiceButtons;

    [Header("ホバーカーソル（任意）")]
    [Tooltip("マウスカーソルが乗っている選択肢の横に表示する▲などのマーク。未設定でも動作する（その場合は何も表示しない）。")]
    public RectTransform hoverCursor;
    [Tooltip("hoverCursorの位置合わせで、ボタンの高さ(Y)に合わせた後にさらに動かしたいズレ量。左右(X)にずらしたい場合などに使う。")]
    public Vector2 hoverCursorOffset = Vector2.zero;

    [Header("会話を閉じる操作")]
    [Tooltip("最後の返答が表示されている間、Spaceキーの代わりにクリックでも閉じられるようにするための受け皿。未設定ならdialoguePanelを使う。Image等のRaycast Targetが有効なGraphicが付いている必要がある。")]
    public GameObject closeClickTarget;

    [Tooltip("選択肢ボタンを上から1つずつ表示していく間隔（秒）。0にすると全部同時に表示される。")]
    public float choiceRevealInterval = 0.15f;

    [Header("返答クローズ時のフェード（任意）")]
    [Tooltip("最後の返答が表示されている状態でSpaceキーを押した後、消えるまでの秒数。0にすると今まで通り即座に消える。")]
    public float responseFadeDuration = 0.6f;
    [Tooltip("フェードアウトさせたい対象をdialogueTextより広い範囲にしたい場合（例：返答の吹き出し背景ごと）に使う。未設定ならdialogueText自体の文字の透明度だけをフェードする。")]
    public CanvasGroup dialogueFadeGroup;

    private bool _triggered;
    private bool _waitingForSpaceToClose;
    private Coroutine _revealCoroutine;
    private int _hoveredChoiceIndex = -1;
    private Vector3 _hoverCursorBasePosition;
    private bool _hoverCursorBaseCaptured;
    private PlayerMovement _playerMovement;

    /// <summary>
    /// choiceButtons[i]にマウスが乗った/離れたことをDialogueKeyRecruit本体に
    /// 中継するための小さなヘルパー。ボタン側にEventTriggerを手動で組む代わりに、
    /// BeginDialogue()側でAddComponentして自動で付ける。
    /// </summary>
    private class ChoiceHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public DialogueKeyRecruit owner;
        public int index;

        public void OnPointerEnter(PointerEventData eventData) => owner.OnChoiceHoverEnter(index);
        public void OnPointerExit(PointerEventData eventData) => owner.OnChoiceHoverExit(index);
    }

    /// <summary>
    /// closeClickTargetがクリックされたことをDialogueKeyRecruit本体に中継する。
    /// 会話開始のたびにBeginDialogue()側でownerを付け替えるので、複数の
    /// DialogueKeyRecruitが同じdialoguePanelを使い回していても、常に
    /// 「今開いている会話」に対してクリックが届く。
    /// </summary>
    private class CloseClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public DialogueKeyRecruit owner;

        public void OnPointerClick(PointerEventData eventData) => owner.RequestClose();
    }

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (!col.isTrigger)
            Debug.LogWarning($"[DialogueKeyRecruit] {name}: Collider2Dが 'Is Trigger' に設定されていません - 会話が始まりません。", this);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void Update()
    {
        // 返事を表示している間だけSpaceキーを監視する。
        if (_waitingForSpaceToClose && Input.GetKeyDown(KeyCode.Space))
            RequestClose();
    }

    /// <summary>
    /// Spaceキー、またはcloseClickTargetのクリックのどちらから来ても、
    /// ここに集約してフェードアウト→会話終了の処理に入る。
    /// </summary>
    private void RequestClose()
    {
        if (!_waitingForSpaceToClose)
            return;

        // 連打・連続クリックでフェードが何重にも始まらないよう、ここで即座にfalseにする。
        _waitingForSpaceToClose = false;
        StartCoroutine(FadeOutThenEndDialogue());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered || !other.CompareTag("Player"))
            return;

        _triggered = true;

        _playerMovement = other.GetComponent<PlayerMovement>();
        if (_playerMovement == null)
            _playerMovement = other.GetComponentInParent<PlayerMovement>();

        BeginDialogue();
    }

    private void BeginDialogue()
    {
        _waitingForSpaceToClose = false;

        // Time.timeScaleは変更しない：プレイヤー/相手キャラのアイドル
        // アニメーションを会話中も再生し続けたいため（要望により変更）。
        // その代わり、プレイヤーの入力だけを個別にロックしてその場から
        // 動けないようにする。
        if (_playerMovement != null)
            _playerMovement.SetInputLocked(true);
        else
            Debug.LogWarning($"[DialogueKeyRecruit] {name}: PlayerにPlayerMovementが見つからず、移動を止められませんでした。", this);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        // Spaceキーだけでなくクリックでも閉じられるようにする受け皿。
        // 会話ごとにownerを付け替えるので、同じdialoguePanelを複数の
        // DialogueKeyRecruitで使い回していても正しい相手に届く。
        var clickTarget = closeClickTarget != null ? closeClickTarget : dialoguePanel;
        if (clickTarget != null)
        {
            var closeRelay = clickTarget.GetComponent<CloseClickRelay>();
            if (closeRelay == null)
                closeRelay = clickTarget.AddComponent<CloseClickRelay>();
            closeRelay.owner = this;
        }

        // 同じdialogueText/dialogueFadeGroupを他のキーとの会話でも使い回す
        // 想定なので、前回フェードアウトした透明度が残っていたら1に戻す。
        if (dialogueText != null)
        {
            var c = dialogueText.color;
            c.a = 1f;
            dialogueText.color = c;
            dialogueText.text = question;
        }
        if (dialogueFadeGroup != null)
            dialogueFadeGroup.alpha = 1f;

        _hoveredChoiceIndex = -1;
        if (hoverCursor != null)
            hoverCursor.gameObject.SetActive(false);

        // ラベルとクリック処理は先に全部セットしておくが、表示自体は
        // RevealChoicesSequentially()側で1つずつ行うので、ここでは一旦
        // 全ボタンを非表示にする。
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null)
                continue;

            int idx = i; // クロージャで使うためのローカルコピー
            bool hasChoice = idx < choices.Length;

            choiceButtons[i].gameObject.SetActive(false);
            choiceButtons[i].onClick.RemoveAllListeners();

            if (hasChoice)
            {
                choiceButtons[i].onClick.AddListener(() => SelectChoice(idx));

                var label = choiceButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = choices[idx].choiceLabel;

                // マウスホバーの検知役。既に付いていれば使い回す。
                var relay = choiceButtons[i].GetComponent<ChoiceHoverRelay>();
                if (relay == null)
                    relay = choiceButtons[i].gameObject.AddComponent<ChoiceHoverRelay>();
                relay.owner = this;
                relay.index = idx;
            }
        }

        if (_revealCoroutine != null)
            StopCoroutine(_revealCoroutine);
        _revealCoroutine = StartCoroutine(RevealChoicesSequentially());
    }

    /// <summary>
    /// 選択肢ボタンを上（配列の先頭）から順に、choiceRevealInterval秒おきに
    /// 1つずつ表示していく。会話中でもTime.timeScaleは1のままなので
    /// WaitForSecondsRealtimeでなくても良いのだが、将来的にどこか別の場所で
    /// timeScaleが変更されるケースでも表示テンポが変わらないよう、あえて
    /// タイムスケールの影響を受けないRealtime版を使っている。
    /// </summary>
    private IEnumerator RevealChoicesSequentially()
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null || i >= choices.Length)
                continue;

            choiceButtons[i].gameObject.SetActive(true);

            if (choiceRevealInterval > 0f)
                yield return new WaitForSecondsRealtime(choiceRevealInterval);
        }

        _revealCoroutine = null;
    }

    /// <summary>ChoiceHoverRelay経由で、ボタンにマウスが乗った時に呼ばれる。</summary>
    private void OnChoiceHoverEnter(int index)
    {
        _hoveredChoiceIndex = index;
        UpdateHoverCursor();
    }

    /// <summary>
    /// ChoiceHoverRelay経由で、ボタンからマウスが離れた時に呼ばれる。
    /// 別のボタンに乗り移った場合、OnPointerExitとOnPointerEnterの発火順は
    /// 保証されていないため、「今離れたのが現在ホバー中のボタンと同じ番号の
    /// 時だけ」カーソルを消す（そうしないと、新しいボタンのEnterの直後に
    /// 古いボタンのExitが来てカーソルが消えてしまうことがある）。
    /// </summary>
    private void OnChoiceHoverExit(int index)
    {
        if (_hoveredChoiceIndex != index)
            return;

        _hoveredChoiceIndex = -1;
        UpdateHoverCursor();
    }

    private void UpdateHoverCursor()
    {
        if (hoverCursor == null)
            return;

        bool valid = _hoveredChoiceIndex >= 0
            && _hoveredChoiceIndex < choiceButtons.Length
            && choiceButtons[_hoveredChoiceIndex] != null;

        if (!valid)
        {
            hoverCursor.gameObject.SetActive(false);
            return;
        }

        var buttonRect = choiceButtons[_hoveredChoiceIndex].GetComponent<RectTransform>();
        if (buttonRect == null)
        {
            hoverCursor.gameObject.SetActive(false);
            return;
        }

        // 不具合修正：以前は「今のhoverCursor.position」に対してoffsetを
        // += していたため、選択肢を切り替えるたびにoffsetが毎回上乗せされ、
        // どんどん位置がずれていた。必ず「最初に配置されていた基準位置」
        // （_hoverCursorBasePosition、初回だけキャプチャ）を起点にして毎回
        // 計算し直すことで、切り替え回数に関係なく同じ位置に収まるようにする。
        if (!_hoverCursorBaseCaptured)
        {
            _hoverCursorBasePosition = hoverCursor.position;
            _hoverCursorBaseCaptured = true;
        }

        Vector3 pos = _hoverCursorBasePosition;
        pos.y = buttonRect.position.y;
        pos += (Vector3)hoverCursorOffset;
        hoverCursor.position = pos;

        hoverCursor.gameObject.SetActive(true);
    }

    /// <summary>
    /// 選択肢ボタンから呼ばれる。どれを選んでも結果（最終的に仲間になる）は
    /// 変わらない。押した瞬間に返事のセリフへ表示を切り替え、選択肢ボタンを
    /// 消して、Spaceキー待ちの状態にする。
    /// </summary>
    private void SelectChoice(int index)
    {
        if (index < 0 || index >= choices.Length)
            return;

        // まだ表示アニメーション中に選ばれた場合、残りのボタンが後から
        // 出てくるのを止める。
        if (_revealCoroutine != null)
        {
            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
        }

        if (dialogueText != null)
            dialogueText.text = choices[index].response;

        foreach (var btn in choiceButtons)
        {
            if (btn != null)
                btn.gameObject.SetActive(false);
        }

        _hoveredChoiceIndex = -1;
        if (hoverCursor != null)
            hoverCursor.gameObject.SetActive(false);

        _waitingForSpaceToClose = true;
    }

    /// <summary>
    /// Spaceキーまたはクリックで閉じる操作をした後、responseFadeDuration秒
    /// かけて相手のメッセージ（dialogueText、またはdialogueFadeGroupを
    /// 割り当てていればその範囲ごと）を徐々に透明にしてからEndDialogue()を呼ぶ。
    /// タイムスケールに影響されないようTime.unscaledDeltaTimeで経過時間を測る。
    /// </summary>
    private IEnumerator FadeOutThenEndDialogue()
    {
        if (responseFadeDuration > 0f && (dialogueFadeGroup != null || dialogueText != null))
        {
            float startAlpha = dialogueFadeGroup != null ? dialogueFadeGroup.alpha
                              : dialogueText.color.a;

            float t = 0f;
            while (t < responseFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / responseFadeDuration));

                if (dialogueFadeGroup != null)
                {
                    dialogueFadeGroup.alpha = alpha;
                }
                else
                {
                    var c = dialogueText.color;
                    c.a = alpha;
                    dialogueText.color = c;
                }

                yield return null;
            }
        }

        EndDialogue();
    }

    private void EndDialogue()
    {
        _waitingForSpaceToClose = false;

        if (_playerMovement != null)
            _playerMovement.SetInputLocked(false);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (keyDefinition == null)
            Debug.LogWarning($"[DialogueKeyRecruit] {name}: keyDefinitionが割り当てられていません - 何も渡せません。", this);
        else if (KeyInventory.Instance == null)
            Debug.LogWarning($"[DialogueKeyRecruit] {name}: シーンにKeyInventoryがありません（Bootstrap未配置？） - キーを入手できませんでした。", this);
        else
            KeyInventory.Instance.AddKey(keyDefinition);

        Destroy(gameObject);
    }
}
