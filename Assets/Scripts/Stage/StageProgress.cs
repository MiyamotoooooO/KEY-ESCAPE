using UnityEngine;

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

    /// <summary>ステージ選択画面で、一度でもカーソルを当てたか（「新しい」の吹き出しを出すかどうかに使う）。</summary>
    public static bool IsSeen(string id)
    {
        return !string.IsNullOrEmpty(id) && SaveManager.Data.seenStages.Contains(id);
    }

    /// <summary>ステージを「見た」ことにして、すぐ保存する。</summary>
    public static void MarkSeen(string id)
    {
        if (string.IsNullOrEmpty(id) || IsSeen(id))
            return;
        SaveManager.Data.seenStages.Add(id);
        SaveManager.Save();
    }

    /// <summary>ステージのベストタイム（秒）。記録が無ければ、false。</summary>
    public static bool TryGetBestTime(string id, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrEmpty(id) || SaveManager.Data.bestTimes == null)
            return false;
        for (int i = 0; i < SaveManager.Data.bestTimes.Count; i++)
        {
            StageTimeSave s = SaveManager.Data.bestTimes[i];
            if (s != null && s.stageId == id)
            {
                seconds = s.seconds;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// クリアタイムを記録する。記録が無い（初クリア）か、いままでのベストより短ければ、更新して保存し、trueを返す。
    /// </summary>
    public static bool TrySetBestTime(string id, float seconds)
    {
        if (string.IsNullOrEmpty(id) || seconds <= 0f)
            return false;

        float best;
        if (TryGetBestTime(id, out best))
        {
            if (seconds >= best)
                return false;
            for (int i = 0; i < SaveManager.Data.bestTimes.Count; i++)
            {
                StageTimeSave s = SaveManager.Data.bestTimes[i];
                if (s != null && s.stageId == id)
                    s.seconds = seconds;
            }
        }
        else
        {
            SaveManager.Data.bestTimes.Add(new StageTimeSave { stageId = id, seconds = seconds });
        }
        SaveManager.Save();
        return true;
    }

    /// <summary>秒数を「01:23.45」の形（分:秒.1/100秒）にする。</summary>
    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int total = Mathf.Min(Mathf.FloorToInt(seconds * 100f), 99 * 6000 + 5999);
        int cs = total % 100;
        int s = (total / 100) % 60;
        int m = total / 6000;
        return m.ToString("00") + ":" + s.ToString("00") + "." + cs.ToString("00");
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