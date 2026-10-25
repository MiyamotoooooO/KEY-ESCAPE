using UnityEngine;

/// <summary>
/// KEY ESCAPE - レバーから「オン／オフ」を受け取る物の、もとになる部品。
/// 橋・扉・川の流れなど、レバーで動かしたい物は、これを受け継いで作る（例：LeverBridge）。
/// レバー（Lever）の「つなげる物（Targets）」に入れると、レバーが切り替わるたびに、SetLeverState が呼ばれる。
/// </summary>
public abstract class LeverReceiver : MonoBehaviour
{
    /// <summary>
    /// レバーの状態が決まった時に呼ばれる。
    /// on：レバーがオンか。
    /// instant：true なら、動きの演出なしで、すぐその状態にする（シーンが始まった時に、覚えていた状態を戻す時）。
    /// </summary>
    public abstract void SetLeverState(bool on, bool instant);

    /// <summary>
    /// レバーを押した時に、カメラが映す場所（ワールドの座標）。橋なら、倒れた橋の真ん中。
    /// 受け継ぐ側で、変えられる。
    /// </summary>
    public virtual Vector3 FocusPosition { get { return transform.position; } }

    /// <summary>
    /// レバーが on になった時に、動きが終わるまでの秒数。カメラは、この間、その場所を映したままにする。
    /// 動きが無ければ 0。
    /// </summary>
    public virtual float GetEffectSeconds(bool on) { return 0f; }

    /// <summary>
    /// レバーの「On Changed」から呼ぶための入り口。
    /// On Changed の関数の選択で、「（この部品の名前）> OnLeverChanged」を選ぶと、レバーのオン／オフが、そのまま渡る。
    /// （ふつうは、レバーの「Targets」に入れるだけでよい。）
    /// </summary>
    public void OnLeverChanged(bool on)
    {
        SetLeverState(on, false);
    }
}
