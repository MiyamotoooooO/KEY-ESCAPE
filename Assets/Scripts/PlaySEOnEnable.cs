using System.Collections;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - このオブジェクトが有効になった（表示された）時に、効果音を鳴らす。
/// キー取得のポップアップや、会話画面などに付けるだけで、スクリプトを書き換えずに音を付けられる。
/// 音は、SoundEffects（Resources）のIdで選ぶ。
/// </summary>
public class PlaySEOnEnable : MonoBehaviour
{
    [Tooltip("鳴らす音。SoundEffectsアセットに割り当てた音が鳴る。")]
    public SEId id = SEId.KeyPickup;
    [Tooltip("表示されてから、鳴るまでの待ち時間（秒）。")]
    public float delay = 0f;
    [Tooltip("シーンが始まった直後に、最初から表示されているオブジェクトで鳴ってしまうのを防ぐ。")]
    public bool ignoreAtSceneStart = true;

    private void OnEnable()
    {
        if (ignoreAtSceneStart && Time.timeSinceLevelLoad < 0.2f)
            return;

        if (delay <= 0f)
            SoundEffects.Play(id);
        else
            StartCoroutine(PlayAfterDelay());
    }

    private IEnumerator PlayAfterDelay()
    {
        // ゲームを止めている間も進むよう、Realtimeを使う。
        yield return new WaitForSecondsRealtime(delay);
        SoundEffects.Play(id);
    }
}