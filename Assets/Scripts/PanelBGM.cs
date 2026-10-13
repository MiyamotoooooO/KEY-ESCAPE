using System.Collections;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - 画面（パネル）が開いている間だけ、別のBGMを流す。
/// キーコンフィグ画面（KeyConfigPanel）や設定画面（SettingsPanel）に付ける。
///
/// 開いた時：今のBGMと再生位置を覚えて、このパネルのBGMに切り替える。
/// 閉じた時：覚えておいたBGMを、続きから再開する。
/// Clipを空にしておくと、BGMは切り替えず、ステージのBGMがそのまま鳴り続ける。
///
/// 注意：SettingsUIと違い、このスクリプトはパネル自体に付ける（パネルが有効になった時に動くため）。
/// </summary>
public class PanelBGM : MonoBehaviour
{
    [Tooltip("このパネルが開いている間のBGM。空なら、BGMは切り替えない。")]
    public AudioClip clip;
    [Tooltip("開く時・閉じる時のクロスフェードの秒数。")]
    public float fadeTime = 0.5f;
    public bool loop = true;

    private bool _pushed;

    private void OnEnable()
    {
        // シーン開始時に、一瞬だけ有効になって、すぐ無効にされるパネル（SettingsUIなど）で、
        // BGMが乱れないように、1フレーム待ってから切り替える。
        StartCoroutine(PushNextFrame());
    }

    private IEnumerator PushNextFrame()
    {
        yield return null;
        if (_pushed)
            yield break;
        _pushed = true;
        AudioManager.PushBGM(clip, fadeTime, loop);
    }

    private void OnDisable()
    {
        if (!_pushed)
            return;
        _pushed = false;
        AudioManager.PopBGM(fadeTime);
    }
}