using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - 会話型のキー（DialogueKeyRecruit）が無いステージで、会話の画面が出っぱなしになるのを防ぐ。
///
/// 会話の画面（DialoguePanel）を隠す処理は、DialogueKeyRecruit の中にしか無い。
/// そのため、ステージに会話型のキーを置かないと、シーンに置いたままの会話の画面（質問の枠・ボタン・カーソル）が、ずっと出てしまう。
/// このスクリプトは、ステージのシーン（StageGoal があるシーン）が読み込まれたとき、
/// 有効な DialogueKeyRecruit が1つも無ければ、「DialoguePanel」という名前の画面を、非表示にする。
///
/// ・どこにも付けなくてよい（自動で動く）。
/// ・会話型のキーがあるシーンでは、何もしない（キーが自分で隠したり、出したりする）。
/// ・拠点やタイトルなど、StageGoal が無いシーンでも、何もしない。
/// ・会話の画面の名前を変えたときは、下の PanelName も同じ名前にする。
/// </summary>
public static class DialoguePanelAutoHide
{
    /// <summary>隠す画面の名前。</summary>
    public const string PanelName = "DialoguePanel";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Setup()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        HideIfNeeded();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideIfNeeded();
    }

    private static void HideIfNeeded()
    {
        // ステージのシーンだけ。
        if (Object.FindObjectOfType<StageGoal>() == null)
            return;
        // 会話型のキーがあれば、キーが自分で扱う。
        if (Object.FindObjectOfType<DialogueKeyRecruit>() != null)
            return;

        Transform[] all = Object.FindObjectsOfType<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == PanelName && all[i].gameObject.activeSelf)
                all[i].gameObject.SetActive(false);
        }
    }
}
