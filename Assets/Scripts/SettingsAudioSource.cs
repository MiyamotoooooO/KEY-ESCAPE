using UnityEngine;

/// <summary>
/// KEY ESCAPE - 既存のAudioSourceを、設定画面の音量（GameSettings）に追従させる。
/// AudioSourceと同じオブジェクトに付けるだけでよい。鳴らしているスクリプトは書き換え不要。
///
/// 仕組み：付けた時点のAudioSourceのVolumeを「基本音量」として覚え、
/// 実際のVolume = 基本音量 × 設定の音量 にする。
/// 設定画面でスライダーを動かすと、すぐに反映される。
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class SettingsAudioSource : MonoBehaviour
{
    public enum Kind { SE, BGM }

    [Tooltip("効果音ならSE、BGMならBGM。")]
    public Kind kind = Kind.SE;

    private AudioSource _source;
    private float _baseVolume = 1f;

    /// <summary>設定を掛ける前の、この音源自体の音量。スクリプトから音量を変えたい時は、AudioSource.volumeではなくこちらを変える。</summary>
    public float BaseVolume
    {
        get => _baseVolume;
        set { _baseVolume = Mathf.Clamp01(value); Apply(); }
    }

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _baseVolume = _source.volume; // Inspectorで決めた音量を、基本音量として覚える
        Apply();
    }

    private void OnEnable()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= Apply;
    }

    private void Apply()
    {
        if (_source == null)
            return;
        float setting = kind == Kind.SE ? GameSettings.SeVolume : GameSettings.BgmVolume;
        _source.volume = _baseVolume * setting;
    }
}