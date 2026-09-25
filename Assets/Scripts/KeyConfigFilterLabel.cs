public static class KeyConfigFilterLabel
{
    public static string Get(KeyConfigFilter filter)
    {
        switch (filter)
        {
            case KeyConfigFilter.All: return "‚·‚×‚Ä";
            case KeyConfigFilter.Movement: return "ˆÚ“®";
            case KeyConfigFilter.Attack: return "ƒAƒNƒVƒ‡ƒ“";
            case KeyConfigFilter.Special: return "“ÁŽê";
            case KeyConfigFilter.Unowned: return "–¢“üŽè";
            default: return filter.ToString();
        }
    }
}