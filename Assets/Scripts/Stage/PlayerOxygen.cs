using UnityEngine;

/// <summary>
/// KEY ESCAPE - プレイヤーの酸素。川（RiverCurrent）の水中にいる間だけ、減る。
///
/// 動き：
///   ・川が、頭が水面より下にいる間、毎フレーム ReportUnderwater() で知らせる。
///   ・知らせが届いている間は、secondsPerCell 秒ごとに、1マスずつ酸素が減る。
///     そのあいだ、HPゲージ（HpGaugeUI）は青くなって、酸素のマスを見せる。HPそのものは減らない。
///   ・酸素が0になると、その場でやられる（死因は「溺れた」）。
///   ・水面に頭が出ると、ゲージは赤いHPに戻り、酸素は、1秒あたり refill マスずつ回復する。
///
/// シーンに置く必要はない。川が初めて知らせた時に、自分で作られて、シーンをまたいで残る。
/// ポーズ中・読み込み中・死亡演出中は、酸素を動かさない。
/// </summary>
public class PlayerOxygen : MonoBehaviour
{
    private static PlayerOxygen _instance;

    private CharacterController2D _player;
    private bool _initialized;
    private float _cells;
    private float _maxCells = 5f;
    private float _secondsPerCell = 0.5f;
    private float _refillPerSecond = 4f;
    private float _lastReport = -999f;

    // 川からの知らせが、この秒数より前のものなら、「もう水中にいない」とみなす
    private const float ReportTimeout = 0.1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    /// <summary>川から呼ぶ：このフレーム、プレイヤーの頭が水中にある。</summary>
    public static void ReportUnderwater(CharacterController2D player, float secondsPerCell, int cellCount, float refillCellsPerSecond)
    {
        if (player == null)
            return;
        if (_instance == null)
        {
            GameObject go = new GameObject("PlayerOxygen");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlayerOxygen>();
        }
        _instance.Report(player, secondsPerCell, cellCount, refillCellsPerSecond);
    }

    private void Report(CharacterController2D player, float secondsPerCell, int cellCount, float refillCellsPerSecond)
    {
        int n = Mathf.Max(1, cellCount);
        if (!_initialized || player != _player || n != Mathf.RoundToInt(_maxCells))
        {
            // 初めて、または別のプレイヤー（シーンを読み直した）：満タンから始める
            _maxCells = n;
            _cells = n;
            _initialized = true;
        }
        _player = player;
        _secondsPerCell = Mathf.Max(0.05f, secondsPerCell);
        _refillPerSecond = Mathf.Max(0f, refillCellsPerSecond);
        _lastReport = Time.time;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
        HpGaugeUI.ClearOxygen();
    }

    private void Update()
    {
        if (!_initialized)
            return;

        // プレイヤーがいなくなった（シーンを読み直した）：リセット
        if (_player == null)
        {
            _initialized = false;
            HpGaugeUI.ClearOxygen();
            return;
        }

        if (GamePause.IsPaused || LoadingScreen.IsLoading)
            return;
        if (HitFeedback.IsDying || _player.life <= 0f)
            return;

        float dt = Time.deltaTime;
        bool underwater = Time.time - _lastReport <= ReportTimeout;

        if (underwater)
        {
            _cells -= dt / _secondsPerCell;

            // 1マスずつ消える：残りが 4.5 マスなら、4マスぶん見せる
            int shown = Mathf.Max(0, Mathf.FloorToInt(_cells + 0.0001f));
            HpGaugeUI.SetOxygen(shown / _maxCells);

            if (_cells <= 0f)
                Drown();
        }
        else
        {
            _cells = Mathf.Min(_maxCells, _cells + _refillPerSecond * dt);
            HpGaugeUI.ClearOxygen();
        }
    }

    private void Drown()
    {
        HpGaugeUI.ClearOxygen();
        // 無敵中でも、溺れる。位置はプレイヤー自身にして、弾かれないようにする
        _player.invincible = false;
        _player.ApplyDamage(_player.life + 1f, _player.transform.position, DeathCause.Drown, -1f);
    }
}
