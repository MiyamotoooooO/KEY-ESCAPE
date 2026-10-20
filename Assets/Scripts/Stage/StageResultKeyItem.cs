using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KEY ESCAPE - 結果画面の「回収したキー」の、1つ分の表示。
/// StageResultPanel の Key Item Template に付ける（Editor のメニューで作ったプレハブには、最初から付いている）。
///   Icon  … キーのキーキャップ風スプライト（KeyDefinition の Key Sprite）
///   Label … Key Sprite が無いキーのときに、代わりに出す文字（Display Name）
///   Stars … ランクの星（1〜3個目の順に並べる）
/// </summary>
public class StageResultKeyItem : MonoBehaviour
{
    public Image icon;
    public TMP_Text label;
    [Tooltip("ランクの星のImageを、1個目〜3個目の順に並べる。")]
    public Image[] stars;

    /// <summary>キーの見た目と、ランクの星（星の絵は、キーコンフィグ画面と同じもの）を設定する。</summary>
    public void Setup(KeyDefinition key, Sprite starFilled, Sprite starEmpty)
    {
        bool hasSprite = key != null && key.keySprite != null;

        if (icon != null)
        {
            icon.enabled = hasSprite;
            icon.sprite = hasSprite ? key.keySprite : null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        if (label != null)
        {
            label.gameObject.SetActive(!hasSprite);
            label.text = key != null ? key.displayName : "?";
        }

        int filled = key != null ? (int)key.rank : 0;
        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null)
                    continue;
                stars[i].sprite = (i < filled) ? starFilled : starEmpty;
                stars[i].color = Color.white;
                stars[i].preserveAspect = true;
                stars[i].raycastTarget = false;
            }
        }
    }
}
