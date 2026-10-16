using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - 「ゲームを止める」ための共通の仕組み。
///
/// 使い方:  GamePause.Acquire(this);  で止める / GamePause.Release(this);  で解除する。
/// 止めている理由（呼び出し元）を数えていて、全員がReleaseしたときだけ再開する。
/// だから「会話 → キー入手ポップアップ」のように理由が連続しても、間で一瞬再開したりしない。
///
/// 止める内容:
///   ・Time.timeScale を 0 にする（移動・物理・敵のAI・弾・カメラなどが止まる）。
///   ・シーン内のすべてのAnimator（非アクティブ含む）を Unscaled Time にする。
///     → 時間が止まっていても、アニメーションだけは流れ続ける（走っていたらその場で走りのループ）。
///     解除したら、各Animatorの元の Update Mode に戻す。
///
/// ・止めている間は IsPaused が true。KeyBindingManager の操作入力や設定画面の開閉は、これを見て無効になる。
/// ・シーンが切り替わったら、止めた状態は必ず解除して元に戻す（止まったまま次のシーンに入らないように）。
/// </summary>
public static class GamePause
{
    private static readonly HashSet<object> _holders = new HashSet<object>();
    private static readonly Dictionary<Animator, AnimatorUpdateMode> _savedModes = new Dictionary<Animator, AnimatorUpdateMode>();
    private static float _savedTimeScale = 1f;
    private static bool _applied;

    /// <summary>誰かが止めているあいだ true。</summary>
    public static bool IsPaused
    {
        get { return _holders.Count > 0; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _holders.Clear();
        _savedModes.Clear();
        _savedTimeScale = 1f;
        _applied = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>ゲームを止める。同じ owner が何度呼んでも1回として数える。</summary>
    public static void Acquire(object owner)
    {
        if (owner == null)
            return;
        if (!_holders.Add(owner))
            return;
        if (_holders.Count == 1)
            Apply();
    }

    /// <summary>止めるのをやめる。最後の1人がReleaseしたときに再開する。</summary>
    public static void Release(object owner)
    {
        if (owner == null)
            return;
        if (!_holders.Remove(owner))
            return;
        if (_holders.Count == 0)
            Revert();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_holders.Count == 0 && !_applied)
            return;
        _holders.Clear();
        Revert();
    }

    private static void Apply()
    {
        _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        _applied = true;

        _savedModes.Clear();
        Animator[] all = Object.FindObjectsOfType<Animator>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Animator a = all[i];
            if (a == null)
                continue;
            if (!_savedModes.ContainsKey(a))
                _savedModes[a] = a.updateMode;
            a.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    private static void Revert()
    {
        foreach (KeyValuePair<Animator, AnimatorUpdateMode> kv in _savedModes)
        {
            if (kv.Key != null)
                kv.Key.updateMode = kv.Value;
        }
        _savedModes.Clear();

        if (_applied)
        {
            Time.timeScale = _savedTimeScale;
            _applied = false;
        }
    }
}
