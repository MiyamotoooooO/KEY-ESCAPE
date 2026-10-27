using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - 「キー封じ」の状態を覚えておく（ボスの檻に捕まったキー）。
///
/// ・封じられたキーは、時間が来るまで使えない（KeyBindingManager が、そのキーを無いものとして扱う）。
/// ・封じられたキーは、キーコンフィグでつかめない（KeyGridButtonUI / KeyConfigUI）。
/// ・時間は、ゲームの時間（Time.deltaTime）で数える。ゲームを止めている間（キーコンフィグ・設定・会話・ポップアップ）は、進まない。
/// ・シーンが変わると（やられてやり直す、拠点に帰る）、全部解ける。ボスを倒したときも、BossA が全部解く。
/// ・封じたとき、画面の上に檻が吊られる（KeySealHud。自動で作られる）。
///
/// 使い方：
///   KeySeal.Seal(key, 12f);        // 12秒のあいだ、封じる
///   KeySeal.IsSealed(key);         // 封じられているか
///   KeySeal.ReleaseAll();          // 全部解く
/// </summary>
public static class KeySeal
{
    public class Entry
    {
        public KeyDefinition key;
        public float total;
        public float remaining;
    }

    private static readonly List<Entry> s_entries = new List<Entry>();
    private static GameObject s_runner;

    /// <summary>封じられた／解けたときに呼ばれる（キーコンフィグの表示を直すのに使う）。</summary>
    public static event Action Changed;

    /// <summary>時間が来て解けたときに呼ばれる（画面の檻が開く演出に使う）。</summary>
    public static event Action<KeyDefinition> Released;

    /// <summary>画面の上の檻に出す文字のフォント。BossA が入れる。空なら、TextMeshPro の標準のフォント。</summary>
    public static TMP_FontAsset HudFont;

    public static IReadOnlyList<Entry> Entries { get { return s_entries; } }
    public static int Count { get { return s_entries.Count; } }

    // Enter Play Mode の設定で、ドメインのリロードを切っていても、前のPlayの状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        s_entries.Clear();
        s_runner = null;
        Changed = null;
        Released = null;
        HudFont = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // シーンが変わったら、全部解く（やり直しても、封じられたままにならないように）
        ReleaseAll();
    }

    public static bool IsSealed(KeyDefinition key)
    {
        if (key == null)
            return false;
        for (int i = 0; i < s_entries.Count; i++)
        {
            if (s_entries[i].key == key)
                return true;
        }
        return false;
    }

    /// <summary>封じられているキーの、のこりの秒数。封じられていなければ 0。</summary>
    public static float Remaining(KeyDefinition key)
    {
        for (int i = 0; i < s_entries.Count; i++)
        {
            if (s_entries[i].key == key)
                return s_entries[i].remaining;
        }
        return 0f;
    }

    /// <summary>封じられているキーの、全体の秒数（のこりの割合を出すのに使う）。</summary>
    public static float Total(KeyDefinition key)
    {
        for (int i = 0; i < s_entries.Count; i++)
        {
            if (s_entries[i].key == key)
                return s_entries[i].total;
        }
        return 0f;
    }

    /// <summary>キーを封じる。すでに封じられているなら、時間を延ばす。</summary>
    public static void Seal(KeyDefinition key, float seconds)
    {
        if (key == null)
            return;
        seconds = Mathf.Max(0.5f, seconds);

        for (int i = 0; i < s_entries.Count; i++)
        {
            if (s_entries[i].key == key)
            {
                s_entries[i].total = Mathf.Max(s_entries[i].total, seconds);
                s_entries[i].remaining = seconds;
                if (Changed != null)
                    Changed();
                return;
            }
        }

        s_entries.Add(new Entry { key = key, total = seconds, remaining = seconds });
        EnsureRunner();
        if (Changed != null)
            Changed();
    }

    /// <summary>1つ解く。byTimer が true なら、檻が開く演出を出す。</summary>
    public static void Release(KeyDefinition key, bool byTimer)
    {
        for (int i = 0; i < s_entries.Count; i++)
        {
            if (s_entries[i].key != key)
                continue;
            s_entries.RemoveAt(i);
            if (byTimer && Released != null)
                Released(key);
            if (Changed != null)
                Changed();
            return;
        }
    }

    /// <summary>全部、すぐに解く（演出なし）。</summary>
    public static void ReleaseAll()
    {
        if (s_entries.Count == 0)
            return;
        s_entries.Clear();
        if (Changed != null)
            Changed();
    }

    /// <summary>時間を進める。KeySealRunner が毎フレーム呼ぶ。</summary>
    public static void Tick(float dt)
    {
        if (dt <= 0f || s_entries.Count == 0)
            return;

        List<KeyDefinition> done = null;
        for (int i = 0; i < s_entries.Count; i++)
        {
            s_entries[i].remaining -= dt;
            if (s_entries[i].remaining <= 0f)
            {
                if (done == null)
                    done = new List<KeyDefinition>();
                done.Add(s_entries[i].key);
            }
        }
        if (done != null)
        {
            for (int i = 0; i < done.Count; i++)
                Release(done[i], true);
        }
    }

    /// <summary>
    /// 封じられるキーの候補：いま、アクションに割り当てられていて、まだ封じられていないキー。
    /// （使っていないキーを封じても、困らないので、候補に入れない）
    /// </summary>
    public static List<KeyDefinition> PickableKeys()
    {
        var list = new List<KeyDefinition>();
        KeyBindingManager mgr = KeyBindingManager.Instance;
        if (mgr == null)
            return list;

        foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
        {
            if (!mgr.IsActionUnlocked(a))
                continue;
            KeyDefinition k = mgr.GetBinding(a);
            if (k == null || IsSealed(k) || list.Contains(k))
                continue;
            list.Add(k);
        }
        return list;
    }

    /// <summary>
    /// 持っているキーのうち、いまアクションに割り当てられていなくて、封じられてもいないキーの数。
    /// 封じられたとき、代わりに設定できるキーがあるかどうかを調べるのに使う。
    /// </summary>
    public static int SpareKeyCount()
    {
        KeyInventory inv = KeyInventory.Instance;
        KeyBindingManager mgr = KeyBindingManager.Instance;
        if (inv == null || mgr == null)
            return 0;

        int n = 0;
        IReadOnlyList<KeyDefinition> owned = inv.OwnedKeys;
        for (int i = 0; i < owned.Count; i++)
        {
            KeyDefinition k = owned[i];
            if (k == null || k.isSpecialKey || IsSealed(k))
                continue;
            GameAction a;
            if (mgr.TryGetActionForKey(k, out a))
                continue;
            n++;
        }
        return n;
    }

    private static void EnsureRunner()
    {
        if (s_runner != null)
            return;
        s_runner = new GameObject("KeySealRunner");
        UnityEngine.Object.DontDestroyOnLoad(s_runner);
        s_runner.AddComponent<KeySealRunner>();
        s_runner.AddComponent<KeySealHud>();
    }
}

/// <summary>封じられたキーの時間を進める（KeySeal が自動で作る）。</summary>
public class KeySealRunner : MonoBehaviour
{
    private void Update()
    {
        // ゲームの時間で数える。キーコンフィグ・設定画面・会話で止まっている間は、進まない。
        KeySeal.Tick(Time.deltaTime);
    }
}

/// <summary>
/// KEY ESCAPE - キーコンフィグ・設定・ステージ選択が開いているか。
/// ボスの体力ゲージや、封じられたキーの檻（画面の上）は、これが true の間は隠す
/// （画面の手前に出たまま、キーコンフィグの上に重ならないように）。
/// </summary>
public static class GameOverlayState
{
    private static KeyConfigUI s_config;
    private static float s_nextFind;

    public static bool IsBlocking
    {
        get
        {
            if (SettingsUI.IsOpen || StageSelectUI.IsOpen)
                return true;
            if (s_config == null && Time.unscaledTime >= s_nextFind)
            {
                s_config = UnityEngine.Object.FindObjectOfType<KeyConfigUI>(true);
                s_nextFind = Time.unscaledTime + 1f;
            }
            return s_config != null && s_config.IsOpen;
        }
    }
}
