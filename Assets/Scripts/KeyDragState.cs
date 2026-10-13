using System;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - 「キー一覧のキーをドラッグでつかんでいる最中かどうか」を全体に知らせる。
/// KeyGridButtonUIがドラッグの開始・終了でSet()を呼び、ActionBindingRowがChangedを
/// 受け取って、ドロップ先の強調表示（黒い半透明の背景）を出し入れする。
/// </summary>
public static class KeyDragState
{
    public static bool IsDragging { get; private set; }

    /// <summary>ドラッグ状態が変わった時に呼ばれる（true=つかみ始め、false=離した）。</summary>
    public static event Action<bool> Changed;

    public static void Set(bool dragging)
    {
        if (IsDragging == dragging)
            return;
        IsDragging = dragging;
        Changed?.Invoke(dragging);
    }

    // Enter Play Modeの設定でドメインリロードを切っている場合に、前回のPlayの状態が
    // 残らないようにする。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        IsDragging = false;
        Changed = null;
    }
}