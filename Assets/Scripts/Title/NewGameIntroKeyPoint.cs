using UnityEngine;

/// <summary>
/// KEY ESCAPE - 「はじめから」のあと、拠点（SelectScene）で、怪我をしたKキーが座っている場所の目印。
/// 家の前に置く。ゲームの中では、何も見えない（Sceneビューにだけ、黄色の枠が出る）。
///
/// 置き方：SelectScene を開いて、メニュー Tools > KEY ESCAPE > はじめからの会話 > Kキーの位置の目印を置く
/// 置いたあとは、Sceneビューで、家の前に動かす（足元が、ほぼ地面の高さになるように）。
/// 目印が無いときは、主人公の右の、少し先にKキーが出る。
/// </summary>
public class NewGameIntroKeyPoint : MonoBehaviour
{
    [Tooltip("ONなら、この目印の真下にある地面に、足元をそろえる。OFFなら、この目印の高さに、足元を置く。")]
    public bool snapToGround = true;

    private void OnDrawGizmos()
    {
        Vector3 p = transform.position;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.95f);
        Gizmos.DrawWireCube(p + Vector3.up * 0.5f, new Vector3(0.9f, 1f, 0.1f));
        Gizmos.DrawLine(p + Vector3.left * 0.6f, p + Vector3.right * 0.6f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(p + Vector3.up * 1.35f, "Kキー（はじめから）");
#endif
    }
}
