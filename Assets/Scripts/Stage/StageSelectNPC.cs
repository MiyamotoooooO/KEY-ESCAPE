using UnityEngine;

/// <summary>
/// KEY ESCAPE - 拠点にいる、ステージ選択の案内役（馬、バッグなど）。
/// プレイヤーが近づくと「話す」の表示が出て、interactKeyを押すと、ステージ選択画面が開く。
///
/// 付け方：馬（バッグ）のオブジェクトに、このスクリプトと、Is Triggerをオンにした Collider2D を付ける。
/// 話しかけられる範囲は、Collider2Dの大きさで決まる（絵より少し大きくしておく）。
/// </summary>
public class StageSelectNPC : MonoBehaviour
{
    [Tooltip("開くステージ選択画面（StageSelectController）。空なら、シーン内から自動で探す。")]
    public StageSelectUI stageSelectUI;
    [Tooltip("近づいた時にだけ表示する「話す」の案内（子オブジェクトなど）。")]
    public GameObject prompt;
    [Tooltip("話しかけるキー。注意：集めたキー（文字キーなど）は、操作に割り当てて使うので、重ならないキーにすること。")]
    public KeyCode interactKey = KeyCode.Return;
    public string playerTag = "Player";

    private bool _inRange;
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
        if (other.CompareTag(playerTag))
            _inRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            _inRange = false;
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