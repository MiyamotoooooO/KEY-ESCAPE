using UnityEngine;

/// <summary>
/// KEY ESCAPE - この場所（シーン／ステージ）で流すBGMを決める。
/// タイトルシーンや各ステージのシーンに、空のオブジェクトを作って付ける。
///
/// 前のシーンと同じ曲なら、途切れずに続けて鳴る。曲が違えば、クロスフェードで切り替わる。
/// Clipを空にしておくと、BGMには触らない（前のシーンのBGMがそのまま続く）。
/// 無音にしたい時は、Stop()を呼ぶ。
/// </summary>
public class SceneBGM : MonoBehaviour
{
    public enum PlayTiming
    {
        [Tooltip("シーンが始まった時に鳴らす。（タイトル、ステージごとにシーンが分かれている場合）")]
        SceneStart,
        [Tooltip("このオブジェクトが有効になった時に鳴らす。（1つのシーン内で、ステージの親オブジェクトを切り替える場合）")]
        WhenEnabled,
        [Tooltip("自動では鳴らさない。Play()を、ほかのスクリプトやUnityEventから呼ぶ。")]
        Manual
    }

    [Tooltip("このシーン（ステージ）のBGM。空なら、BGMには触らない。")]
    public AudioClip clip;
    [Tooltip("曲を切り替える時のクロスフェードの秒数。")]
    public float fadeTime = 1f;
    public bool loop = true;
    public PlayTiming playTiming = PlayTiming.SceneStart;

    private void Start()
    {
        if (playTiming == PlayTiming.SceneStart)
            Play();
    }

    private void OnEnable()
    {
        if (playTiming == PlayTiming.WhenEnabled)
            Play();
    }

    public void Play()
    {
        if (clip == null)
            return;
        AudioManager.PlayBGM(clip, fadeTime, loop);
    }

    public void Stop()
    {
        AudioManager.StopBGM(fadeTime);
    }
}