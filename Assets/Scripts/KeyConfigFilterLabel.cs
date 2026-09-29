/// <summary>
/// KEY ESCAPE - KeyConfigFilterの日本語表示名。タブのラベルはここでまとめて管理する。
/// </summary>
public static class KeyConfigFilterLabel
{
    public static string Get(KeyConfigFilter filter)
    {
        switch (filter)
        {
            case KeyConfigFilter.All: return "すべて";
            case KeyConfigFilter.Serious: return "まじめ";
            case KeyConfigFilter.Clumsy: return "ぶきよう";
            case KeyConfigFilter.Ruthless: return "非情";
            case KeyConfigFilter.Rough: return "らんぼう";
            case KeyConfigFilter.Cowardly: return "おくびょう";
            case KeyConfigFilter.Fickle: return "きまぐれ";
            case KeyConfigFilter.Special: return "特殊";
            case KeyConfigFilter.Unowned: return "未入手";
            default: return filter.ToString();
        }
    }
}
