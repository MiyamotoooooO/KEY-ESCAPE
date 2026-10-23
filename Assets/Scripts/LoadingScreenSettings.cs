using TMPro;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ロード画面の設定（ヒントの文章・背景の画像・待ち時間など）。
/// 「Assets/Resources/LoadingScreenSettings」という名前のアセットが、ロード画面から読み込まれる。
/// （Unityのメニュー KEY ESCAPE > ロード画面 > 設定アセットを選ぶ／作る、で作れる。）
/// </summary>
[CreateAssetMenu(menuName = "Key Escape/Loading Screen Settings", fileName = "LoadingScreenSettings")]
public class LoadingScreenSettings : ScriptableObject
{
    [Tooltip("OFFにすると、ロード画面を出さずに、これまでどおり、すぐにシーンを切り替える。")]
    public bool enabled = true;
    [Tooltip("ONなら、やられて最初からやり直すときも、ロード画面を出す。OFFなら、やり直しのときだけ、ロード画面を出さない。")]
    public bool showOnRetry = true;

    [Header("ヒント（ロード中に、ランダムで1つ出る）")]
    [Tooltip("ヒントの文章。好きなだけ追加できる。全角スペースと、「〜」は、フォントによって表示されないので、使わないこと。")]
    [TextArea(2, 4)]
    public string[] hints = new string[]
    {
        "Tabキーで、キーコンフィグを開けるよ。",
        "キーは、ドラッグして、好きな動作に割り当てられる。",
        "キーには、それぞれ性格がある。置く場所で、力の出かたが変わるよ。",
        "性格は、ぶきよう・非情・らんぼう・おくびょう・きまぐれ・まじめの6種類。",
        "Escキーで、設定画面を開けるよ。",
        "行きづまったら、キーの配置を見直してみよう。",
    };
    [Tooltip("ヒントの上に出す、小さな見出し。")]
    public string hintLabel = "ヒント";

    [Header("待ち時間")]
    [Tooltip("ロード画面を出す前の、暗くなっていく時間（秒）。")]
    public float fadeInSeconds = 0.3f;
    [Tooltip("ロード画面が消えていく時間（秒）。")]
    public float fadeOutSeconds = 0.4f;
    [Tooltip("Spaceを押さなかったとき、自動で進むまでの、最短の時間（秒）。")]
    public float autoMinSeconds = 2f;
    [Tooltip("Spaceを押さなかったとき、自動で進むまでの、最長の時間（秒）。")]
    public float autoMaxSeconds = 3f;
    [Tooltip("ヒントの文字数が多いほど、待ち時間を長くする。1文字につき足す時間（秒）。最短〜最長の間に収まる。0なら、いつも最短の時間。")]
    public float secondsPerChar = 0.04f;
    [Tooltip("ロード画面が出てから、Spaceで進めるようになるまでの時間（秒）。押し間違いで、すぐ飛ばないようにする。")]
    public float spaceEnableSeconds = 0.5f;

    [Header("見た目")]
    [Tooltip("背景の画像（任意）。画面いっぱいに広げて表示する。空なら、単色の背景。")]
    public Sprite background;
    public Color backgroundTint = Color.white;
    [Tooltip("単色の背景の色（背景の画像がないとき、画像の後ろにも敷く）。")]
    public Color baseColor = new Color(0.05f, 0.05f, 0.08f, 1f);
    [Tooltip("背景の画像の上に重ねる、黒い膜の濃さ（0〜1）。文字を読みやすくする。")]
    [Range(0f, 1f)]
    public float dimAlpha = 0.45f;
    [Tooltip("空なら、シーンにある日本語のフォントを自動で探して使う。")]
    public TMP_FontAsset font;
    public int hintFontSize = 40;
    public int labelFontSize = 28;
    public Color hintColor = Color.white;
    public Color labelColor = new Color(1f, 0.85f, 0.35f, 1f);

    [Header("右下の表示")]
    public string loadingLabel = "NOW LOADING";
    [Tooltip("Spaceで進めるようになったら出す文。")]
    public string skipLabel = "Spaceキーで次へ";
    public int smallFontSize = 26;
}
