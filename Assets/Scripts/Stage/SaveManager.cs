using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>キーの割り当て1件分（アクション名とキー名）。</summary>
[Serializable]
public class BindingSave
{
    public string action;
    public string key;
}

/// <summary>セーブデータの中身。JSONで保存される。</summary>
[Serializable]
public class SaveData
{
    public int version = 2;

    /// <summary>クリア済みのステージのID（StageCatalogのid）。</summary>
    public List<string> clearedStages = new List<string>();

    // ---- キー関連（KeyStateSaverが書き込み・復元する） ----

    /// <summary>キーのデータを一度でも保存したか。falseなら、Bootstrapに設定した初期状態のままにする。</summary>
    public bool hasKeyData;
    /// <summary>所持しているキー（KeyDefinitionのアセット名）。</summary>
    public List<string> ownedKeys = new List<string>();
    /// <summary>解放済みのアクション（GameActionの名前）。</summary>
    public List<string> unlockedActions = new List<string>();
    /// <summary>アクションごとの、割り当てているキー。</summary>
    public List<BindingSave> bindings = new List<BindingSave>();
    /// <summary>特殊キー（Ctrl）を解放済みか。</summary>
    public bool modifierUnlocked;
    /// <summary>二段ジャンプを解放済みか（ステージクリアの報酬）。</summary>
    public bool doubleJumpUnlocked;

    /// <summary>ステージ選択画面で、一度カーソルを当てたステージのID（「新しい」の吹き出しを消すために使う）。</summary>
    public List<string> seenStages = new List<string>();

    // ---- ステージの挑戦中の記録（StageRunが書き込む） ----

    /// <summary>いま挑戦中（クリアも帰還もしていない）のステージのID。空なら、挑戦中ではない。</summary>
    public string runStageId = "";
    /// <summary>挑戦中に入手したキー（KeyDefinitionのアセット名）。拠点に帰ると、手持ちから外される。</summary>
    public List<string> runKeys = new List<string>();
}

/// <summary>
/// KEY ESCAPE - セーブデータを、ファイル（save.json）に保存・読み込みする。
/// ゲームを終了しても、シーンが変わっても、内容は残る。
/// 保存先：Application.persistentDataPath/save.json
/// </summary>
public static class SaveManager
{
    private const string FileName = "save.json";

    public static SaveData Data { get; private set; } = new SaveData();

    /// <summary>保存する直前に呼ばれる。ほかのスクリプトが、現在の状態をDataに書き込むのに使う。</summary>
    public static event Action BeforeSave;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        BeforeSave = null;
    }

    // 最初のシーンが読み込まれる前に、セーブデータを読み込んでおく。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Load();
    }

    public static void Load()
    {
        Data = new SaveData();
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                if (loaded != null)
                    Data = loaded;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveManager] セーブデータを読み込めませんでした。最初からにします。 " + e.Message);
            Data = new SaveData();
        }

        if (Data.clearedStages == null) Data.clearedStages = new List<string>();
        if (Data.ownedKeys == null) Data.ownedKeys = new List<string>();
        if (Data.unlockedActions == null) Data.unlockedActions = new List<string>();
        if (Data.bindings == null) Data.bindings = new List<BindingSave>();
        if (Data.seenStages == null) Data.seenStages = new List<string>();
        if (Data.runKeys == null) Data.runKeys = new List<string>();
        if (Data.runStageId == null) Data.runStageId = "";
    }

    public static void Save()
    {
        try
        {
            BeforeSave?.Invoke();

            // 書き込み途中でゲームが終了しても、セーブデータが壊れないよう、
            // いったん別のファイルに書いてから、置き換える。
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
            File.Copy(tmp, FilePath, true);
            File.Delete(tmp);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveManager] セーブできませんでした。 " + e.Message);
        }
    }

    /// <summary>セーブデータを削除して、最初の状態に戻す（テスト用）。</summary>
    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveManager] セーブデータを削除できませんでした。 " + e.Message);
        }
        Data = new SaveData();
    }
}