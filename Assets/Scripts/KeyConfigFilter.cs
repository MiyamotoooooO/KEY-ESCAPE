using UnityEngine;

/// <summary>
/// KEY ESCAPE - Key Config画面左側のキー一覧を絞り込むためのタブ。
/// すべて／6性格（まじめ・ぶきよう・非情・らんぼう・おくびょう・きまぐれ）
/// ／特殊／未入手（性格仕様書.mdへの移行に合わせて、旧・属性ベースの
/// 「移動／アクション」タブを性格ベースに完全に置き換えた）。
///
/// [InspectorName]は見た目（Inspectorのドロップダウン表示）だけのためで、
/// コード上の識別子や実際にシリアライズされる値（並び順）には影響しない。
/// </summary>
public enum KeyConfigFilter
{
    [InspectorName("すべて")]
    All,
    [InspectorName("まじめ")]
    Serious,
    [InspectorName("ぶきよう")]
    Clumsy,
    [InspectorName("非情")]
    Ruthless,
    [InspectorName("らんぼう")]
    Rough,
    [InspectorName("おくびょう")]
    Cowardly,
    [InspectorName("きまぐれ")]
    Fickle,
    [InspectorName("特殊")]
    Special,
    [InspectorName("未入手")]
    Unowned,
}
