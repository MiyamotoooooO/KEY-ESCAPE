using UnityEngine;

/// <summary>
/// KEY ESCAPE - キーの動きを、ランダムに切り替えるための設定（Inspectorに出る）。
/// ゴール演出（StageClearDirector）と結果画面（StageResultPanel）が持つ。
/// </summary>
[System.Serializable]
public class KeyMotionSettings
{
    [Tooltip("ONなら、下の動きの中からランダムに選んで、切り替えながら踊る。OFFなら、今までどおり「喜ぶ」だけ。")]
    public bool randomMotion = true;
    [Tooltip("ランダムに選ぶ動きの一覧。出したくない動きは、一覧から外す。")]
    public LimbRig.KeyMotion[] motions = new LimbRig.KeyMotion[]
    {
        LimbRig.KeyMotion.Cheer,
        LimbRig.KeyMotion.Sway,
        LimbRig.KeyMotion.Hop,
        LimbRig.KeyMotion.Wave,
        LimbRig.KeyMotion.Disco,
    };
    [Tooltip("動きを切り替える間隔の、いちばん短い秒数。")]
    public float intervalMin = 2f;
    [Tooltip("動きを切り替える間隔の、いちばん長い秒数。")]
    public float intervalMax = 4f;
}

/// <summary>
/// KEY ESCAPE - キー（LimbRig）に付けて、動きをランダムに切り替える。
///
/// KeyMotionRandomizer.Begin(rig, settings) で始める。
/// 設定がOFF（または動きの一覧が空）のときは、今までどおり SetCheer(true)（喜ぶ）にする。
/// 前と同じ動きは続けて選ばない。最初の切り替えまでの時間はキーごとにずらすので、
/// 複数のキーが同じタイミングで、同じ動きにそろわない。
/// 実時間で数えるので、スローやポーズの影響を受けない。キーが消えれば、この部品も消える。
/// </summary>
public class KeyMotionRandomizer : MonoBehaviour
{
    private LimbRig _rig;
    private KeyMotionSettings _settings;
    private LimbRig.KeyMotion _current = LimbRig.KeyMotion.None;
    private float _timer;

    public static void Begin(LimbRig rig, KeyMotionSettings settings)
    {
        if (rig == null)
            return;

        if (settings == null || !settings.randomMotion || settings.motions == null || settings.motions.Length == 0)
        {
            rig.SetCheer(true);
            return;
        }

        KeyMotionRandomizer r = rig.GetComponent<KeyMotionRandomizer>();
        if (r == null)
            r = rig.gameObject.AddComponent<KeyMotionRandomizer>();
        r._rig = rig;
        r._settings = settings;
        r._current = LimbRig.KeyMotion.None;
        r.Switch();
        // 最初だけ、切り替えまでの時間を短くして、キーごとにずらす。
        r._timer *= Random.Range(0.4f, 1f);
    }

    private void Update()
    {
        if (_rig == null || _settings == null)
            return;

        _timer -= Time.unscaledDeltaTime;
        if (_timer <= 0f)
            Switch();
    }

    private void Switch()
    {
        LimbRig.KeyMotion[] list = _settings.motions;
        LimbRig.KeyMotion next = _current;
        for (int tries = 0; tries < 8; tries++)
        {
            LimbRig.KeyMotion m = list[Random.Range(0, list.Length)];
            if (m == LimbRig.KeyMotion.None)
                continue;
            next = m;
            if (m != _current)
                break;
        }
        if (next == LimbRig.KeyMotion.None)
            next = LimbRig.KeyMotion.Cheer;

        _current = next;
        _rig.SetMotion(next);

        float lo = Mathf.Max(0.3f, _settings.intervalMin);
        float hi = Mathf.Max(lo, _settings.intervalMax);
        _timer = Random.Range(lo, hi);
    }
}
