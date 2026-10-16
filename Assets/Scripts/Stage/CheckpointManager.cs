using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KEY ESCAPE - チェックポイントの記録と、再開位置への移動。
///
/// 倒れてシーンが読み直されると、プレイヤーを最後のチェックポイントの位置へ移す。
/// カメラも、同じ位置へすぐに移す（CameraFollowはゆっくり追うので、そのままだと
/// スタート地点から動き出してしまう）。
/// 記録は、同じステージのシーンが読み直される間だけ残る。
/// 別のシーン（拠点、タイトルなど）が読み込まれると、自動で消える。
/// どこにも付けなくてよい（ゲーム開始時に自動で動く）。
/// </summary>
public static class CheckpointManager
{
    private static bool _has;
    private static string _sceneName;
    private static Vector3 _position;
    private static int _order;

    /// <summary>今のシーンに、記録したチェックポイントがあるか。</summary>
    public static bool HasCheckpoint => _has && _sceneName == SceneManager.GetActiveScene().name;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// チェックポイントを記録する。より手前（orderが小さい）のチェックポイントには、戻らない。
    /// 記録できたらtrue。
    /// </summary>
    public static bool Set(Vector3 position, int order)
    {
        string scene = SceneManager.GetActiveScene().name;
        if (_has && _sceneName == scene && order < _order)
            return false;

        _has = true;
        _sceneName = scene;
        _position = position;
        _order = order;
        return true;
    }

    /// <summary>この位置が、今の再開位置か（シーンを読み直した後に、旗を点灯させておくのに使う）。</summary>
    public static bool IsCurrent(Vector3 position)
    {
        return HasCheckpoint && (_position - position).sqrMagnitude < 0.0001f;
    }

    public static void Clear()
    {
        _has = false;
        _sceneName = null;
        _order = 0;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_has || mode != LoadSceneMode.Single)
            return;

        // 別のシーンに移った（拠点、タイトルなど）なら、記録を消す。
        if (scene.name != _sceneName)
        {
            Clear();
            return;
        }

        MovePlayerToCheckpoint();
    }

    private static void MovePlayerToCheckpoint()
    {
        // Playerタグが付いたもののうち、Rigidbody2Dを持つものが本物のプレイヤー
        // （キーコンフィグのプレビュー用キャラなどを選ばないようにする）。
        GameObject player = null;
        GameObject[] candidates = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].GetComponent<Rigidbody2D>() != null)
            {
                player = candidates[i];
                break;
            }
        }
        if (player == null)
            return;

        Vector3 p = new Vector3(_position.x, _position.y, player.transform.position.z);
        player.transform.position = p;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = p;
            rb.velocity = Vector2.zero;
        }

        // カメラも、すぐに同じ場所へ（奥行きのzは、そのまま）。
        CameraFollow follow = Object.FindObjectOfType<CameraFollow>();
        if (follow != null)
        {
            Vector3 c = follow.transform.position;
            follow.transform.position = new Vector3(p.x, p.y, c.z);
        }
    }
}
