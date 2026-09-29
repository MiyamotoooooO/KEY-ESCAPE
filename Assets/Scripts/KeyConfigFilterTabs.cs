using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - Key Config画面左上の絞り込みタブ（すべて／まじめ／ぶきよう／
/// 非情／らんぼう／おくびょう／きまぐれ／特殊／未入手の9個）をまとめて管理
/// する小さなスクリプト。
///
/// タブの切り替えロジック自体はKeyConfigUI.SetFilterXxx()が持っている
/// （そちらを各ボタンのOnClickに直接繋ぐ）。このスクリプトの役目は見た目だけ：
/// 現在選択中のタブの背景色を変えて、どれが選ばれているか分かるようにする。
///
/// 使い方：
///   1. タブ用のButtonを9つ作る（すべて／まじめ／ぶきよう／非情／らんぼう／
///      おくびょう／きまぐれ／特殊／未入手）。
///   2. 各ButtonのOnClickに、KeyConfigUIの対応するSetFilterXxx()を繋ぐ
///      （例：「ぶきよう」ボタン→KeyConfigUI.SetFilterClumsy）。
///   3. このスクリプトをタブ行（親オブジェクト）に付けて、tabsに9つとも
///      登録する（どのFilterに対応するボタンか、背景Image）。
///   4. KeyConfigUIのFilter Tabsフィールドに、このスクリプトを割り当てる。
/// これでKeyConfigUI.SetFilter()が呼ばれるたびに自動でハイライトが更新される。
/// </summary>
public class KeyConfigFilterTabs : MonoBehaviour
{
    [System.Serializable]
    public struct Tab
    {
        public KeyConfigFilter filter;
        [Tooltip("このタブの背景Image（色を変える対象）。ButtonのTarget Graphicと同じでよい。")]
        public Image background;
    }

    public Tab[] tabs;

    [Header("ハイライト色")]
    public Color activeColor = new Color(1f, 0.706f, 0.33f);      // アクセントカラー（選択中）
    public Color inactiveColor = new Color(0.141f, 0.161f, 0.216f); // 非選択時

    /// <summary>KeyConfigUI.SetFilter()から呼ばれる。現在のフィルターに合わせて色を塗り直す。</summary>
    public void Refresh(KeyConfigFilter current)
    {
        if (tabs == null)
            return;

        foreach (var tab in tabs)
        {
            if (tab.background != null)
                tab.background.color = (tab.filter == current) ? activeColor : inactiveColor;
        }
    }
}
