using UnityEngine;

/// <summary>
/// KEY ESCAPE - UIの効果音の「標準の音」をまとめて持つ設定アセット。
/// Resourcesフォルダに「UISoundDefaults」という名前で1つだけ置く。
/// ボタンごとに音を割り当てなくても、ここで決めた音が全ボタンで鳴る。
/// </summary>
[CreateAssetMenu(fileName = "UISoundDefaults", menuName = "KEY ESCAPE/UI Sound Defaults")]
public class UISoundDefaults : ScriptableObject
{
    [Header("音")]
    [Tooltip("マウスを乗せた時の音。空なら鳴らさない。")]
    public AudioClip hoverClip;
    [Tooltip("クリックした時の音。")]
    public AudioClip clickClip;
    [Tooltip("「もどる」などのボタンをクリックした時の音。空なら、クリックの音を使う。")]
    public AudioClip backClip;

    [Header("音量（効果音の設定音量に、さらに掛かる）")]
    [Range(0f, 1f)] public float hoverVolume = 0.6f;
    [Range(0f, 1f)] public float clickVolume = 1f;

    private static UISoundDefaults _instance;

    /// <summary>Resourcesフォルダの「UISoundDefaults」を読み込んで返す。無ければnull。</summary>
    public static UISoundDefaults Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<UISoundDefaults>("UISoundDefaults");
            return _instance;
        }
    }
}