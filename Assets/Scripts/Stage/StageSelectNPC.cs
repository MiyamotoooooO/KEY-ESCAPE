using UnityEngine;

/// <summary>
/// KEY ESCAPE - 拠点にいる、ステージ選択の案内役（掲示板、馬、バッグなど）。
/// プレイヤーが近づくと「話す」の表示が出て、interactKeyを押すと、ステージ選択画面が開く。
///
/// 付け方：掲示板などのオブジェクトに、このスクリプトと、Is Triggerをオンにした Collider2D を付ける。
/// 話しかけられる範囲は、Collider2Dの大きさで決まる（絵より少し大きくしておく）。
///
/// 範囲に入っている間、interactKeyに割り当てられた操作（攻撃など）は無効になる
/// （KeyBindingManager.ReserveKey）。同時にその操作が出てしまうのを防ぐため。
/// </summary>
public class StageSelectNPC : MonoBehaviour
{
    [Tooltip("開くステージ選択画面（StageSelectController）。空なら、シーン内から自動で探す。")]
    public StageSelectUI stageSelectUI;
    [Tooltip("近づいた時にだけ表示する「話す」の案内（Fキーの絵など）。")]
    public GameObject prompt;
    [Tooltip("話しかけるキー。範囲に入っている間、このキーに割り当てた操作は無効になる。")]
    public KeyCode interactKey = KeyCode.F;
    public string playerTag = "Player";

    private bool _inRange;
    private bool _keyReserved;
    private KeyConfigUI _keyConfigUI;

    private void Start()
    {
        if (stageSelectUI == null)
            stageSelectUI = FindObjectOfType<StageSelectUI>(true);
        _keyConfigUI = FindObjectOfType<KeyConfigUI>(true);

        if (prompt != null)
            prompt.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;
        _inRange = true;
        ReserveInteractKey();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;
        _inRange = false;
        ReleaseInteractKey();
    }

    // シーンが切り替わる時や、無効にされた時にも、確保したキーを必ず解放する。
    private void OnDisable()
    {
        _inRange = false;
        ReleaseInteractKey();
    }

    private void ReserveInteractKey()
    {
        if (_keyReserved || KeyBindingManager.Instance == null)
            return;
        KeyBindingManager.Instance.ReserveKey(interactKey);
        _keyReserved = true;
    }

    private void ReleaseInteractKey()
    {
        if (!_keyReserved)
            return;
        _keyReserved = false;
        if (KeyBindingManager.Instance != null)
            KeyBindingManager.Instance.ReleaseKey(interactKey);
    }

    private void Update()
    {
        bool blocked = StageSelectUI.IsOpen
                       || SettingsUI.IsOpen
                       || (_keyConfigUI != null && _keyConfigUI.IsOpen);

        bool canTalk = _inRange && !blocked;

        if (prompt != null && prompt.activeSelf != canTalk)
            prompt.SetActive(canTalk);

        if (canTalk && stageSelectUI != null && Input.GetKeyDown(interactKey))
            stageSelectUI.Open();
    }
}