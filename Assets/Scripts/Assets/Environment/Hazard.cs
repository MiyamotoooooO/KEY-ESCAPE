using UnityEngine;

/// <summary>
/// KEY ESCAPE - 当たると痛い物（トゲなど）。プレイヤーが触れると、ダメージを受ける。
///
/// 使い方：絵（SpriteRenderer）と Collider2D を付けたオブジェクトに、このスクリプトを付ける。
///   ・Collider2D の Is Trigger がオン：すり抜けながら、ダメージを受ける。
///   ・Is Trigger がオフ：ぶつかる（乗れる）上に、ダメージを受ける。
/// 触れ続けている間は、プレイヤーの無敵が切れるたびに、またダメージを受ける。
/// Editor のメニュー「Tools > KEY ESCAPE > ステージ部品 > トゲのプレハブを作る」で、トゲのプレハブも作れる。
///
/// ダメージの量：プレイヤーの最大HPは10、HPゲージは5マス。HP 2 で、ハート（マス）1個分。
/// HPが0になった時の死因は、結果画面の一言コメントを選ぶのに使う（初期値は「その他」）。
/// </summary>
public class Hazard : MonoBehaviour
{
    [Tooltip("1回当たった時のダメージ。2 で、ハート1個分。")]
    public float damage = 2f;

    [Tooltip("当たったあとの無敵の秒数。0以下なら、いつもの被弾と同じ（1秒）。0.5 にすると、触れ続けた時に、0.5秒ごとにダメージを受ける。")]
    public float invincibleTimeAfterHit = -1f;

    [Tooltip("オンなら、真上に弾かれる（トゲの上向き）。オフなら、このオブジェクトの中心から離れる向きに弾かれる。")]
    public bool knockbackUp = true;

    [Tooltip("HPが0になった時の死因（結果画面の一言コメントの種類）。")]
    public DeathCause deathCause = DeathCause.Other;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHit(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryHit(collision.collider);
    }

    private void TryHit(Collider2D other)
    {
        if (other == null)
            return;

        CharacterController2D player = other.GetComponentInParent<CharacterController2D>();
        if (player == null)
            return;

        // 弾かれる向き：ダメージの元の位置から、離れる向きに弾かれる。
        Vector3 source = knockbackUp ? player.transform.position + Vector3.down : transform.position;
        player.ApplyDamage(damage, source, deathCause, invincibleTimeAfterHit);
    }
}
