/// <summary>
/// KEY ESCAPE - ステージの進行状況（クリア済みか、解放済みか）を扱う。
/// データはSaveManagerに保存されるので、ゲームを終了しても残る。
///
/// 解放のルール：1つ目のステージは最初から解放。2つ目以降は、直前のステージをクリアすると解放される。
/// クリア済みのステージには、いつでも何度でも入り直せる。
/// </summary>
public static class StageProgress
{
    public static bool IsCleared(string id)
    {
        return !string.IsNullOrEmpty(id) && SaveManager.Data.clearedStages.Contains(id);
    }

    /// <summary>ステージをクリア済みにして、すぐ保存する。</summary>
    public static void MarkCleared(string id)
    {
        if (string.IsNullOrEmpty(id) || IsCleared(id))
            return;
        SaveManager.Data.clearedStages.Add(id);
        SaveManager.Save();
    }

    public static bool IsUnlocked(string id)
    {
        StageCatalog catalog = StageCatalog.Instance;
        if (catalog == null)
            return false;

        int index = catalog.IndexOf(id);
        if (index < 0)
            return false;
        if (index == 0)
            return true;
        return IsCleared(catalog.stages[index - 1].id);
    }
}