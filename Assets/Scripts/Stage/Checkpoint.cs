using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - チェックポイント。プレイヤーが触れると、ここを再開位置として記録する。
///
/// 付け方：Is Triggerをオンにした Collider2D（縦に長い箱がおすすめ）と、このスクリプトを付ける。
/// ふだんは Assets/Prefab/Object/Checkpoint.prefab を置くだけでよい
/// （Tools > KEY ESCAPE > チェックポイント > 旗のプレハブを作る で作れる）。
///
/// 見た目の切り替え（どれも任意。空なら何もしない）：
///   ・Inactive Visual / Active Visual … 通る前／通った後に表示する物（子オブジェクトを割り当てる）
///   ・エモート … 旗の上に、通る前と通った後で別のエモート（Assets/Prefab/Emotes のプレハブ）を出す
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    public string playerTag = "Player";

    [Tooltip("ステージの進み具合の順番。手前から0, 1, 2…と付ける。小さい番号のチェックポイントに戻っても、再開位置は手前に戻らない。")]
    public int order = 0;

    [Tooltip("任意。再開する位置。空なら、このオブジェクトの位置。プレイヤーの足元が地面に着くよう、少し上に置く。")]
    public Transform respawnPoint;

    [Header("見た目（どちらも任意）")]
    [Tooltip("まだ通っていない時に表示する物（消えた旗など）。")]
    public GameObject inactiveVisual;
    [Tooltip("通った後に表示する物（点灯した旗など）。")]
    public GameObject activeVisual;

    [Header("エモート（どちらも任意。空なら何も出さない）")]
    [Tooltip("まだ通っていない時に、旗の上に出すエモート（Assets/Prefab/Emotes のプレハブ）。")]
    public GameObject emoteBeforePrefab;
    [Tooltip("通った後に、旗の上に出すエモート（Assets/Prefab/Emotes のプレハブ）。")]
    public GameObject emoteAfterPrefab;
    [Tooltip("エモートを出す位置（このオブジェクトの位置からのずれ）。")]
    public Vector3 emoteOffset = new Vector3(0.6f, 3f, 0f);
    [Tooltip("エモートの大きさの倍率。")]
    public float emoteScale = 1f;
    [Tooltip("通った後のエモートを出しておく秒数。0ならずっと出す。")]
    public float emoteAfterDuration = 0f;
    [Tooltip("エモートの描く順番の底上げ。大きいほど手前に描く（旗やキャラに隠れないように）。")]
    public int emoteSortingBoost = 100;

    [Header("紙吹雪（初めて通った時に、旗の上に降らせる。どれも任意）")]
    [Tooltip("紙吹雪の画像（色違いを、いくつか入れる）。空なら、赤・黄・青の四角を、コードで作って降らせる。")]
    public Sprite[] confettiSprites;
    [Tooltip("紙吹雪を出す位置（このオブジェクトの位置からのずれ）。旗の上あたり。")]
    public Vector3 confettiOffset = new Vector3(0.6f, 3.2f, 0f);
    [Tooltip("紙吹雪を出す範囲（幅, 高さ）。")]
    public Vector2 confettiArea = new Vector2(1.6f, 0.6f);
    [Tooltip("紙吹雪の数。0なら、出さない。")]
    public int confettiCount = 28;
    [Tooltip("紙吹雪1枚の高さ（ワールドの単位）。大きさは、少しずつばらつく。")]
    public float confettiSize = 0.25f;
    [Tooltip("落ちる速さ（ワールドの単位/秒）。")]
    public float confettiFallSpeed = 2.2f;
    [Tooltip("紙吹雪が消えるまでの秒数。")]
    public float confettiDuration = 2.2f;
    [Tooltip("紙吹雪の描く順番の底上げ。大きいほど手前に描く。")]
    public int confettiSortingBoost = 100;

    private bool _lit;
    private GameObject _before;
    private GameObject _after;
    private float _afterTimer;

    private Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Start()
    {
        BuildEmotes();

        // シーンを読み直した後でも、ここが今の再開位置なら、点灯させておく。
        SetLit(CheckpointManager.IsCurrent(RespawnPosition));
    }

    private void Update()
    {
        // 通った後のエモートを、決めた秒数だけ出して消す。
        if (_afterTimer > 0f)
        {
            _afterTimer -= Time.deltaTime;
            if (_afterTimer <= 0f && _after != null)
                _after.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (CheckpointManager.Set(RespawnPosition, order))
        {
            bool wasLit = _lit;
            SetLit(true);
            if (!wasLit)
                PlayConfetti(); // 初めて通った時だけ（点灯済みの旗を通っても、出さない）
        }
    }

    private void SetLit(bool lit)
    {
        _lit = lit;
        if (activeVisual != null) activeVisual.SetActive(lit);
        if (inactiveVisual != null) inactiveVisual.SetActive(!lit);

        if (_before != null) _before.SetActive(!lit);
        if (_after != null) _after.SetActive(lit);
        _afterTimer = (lit && emoteAfterDuration > 0f) ? emoteAfterDuration : 0f;
    }

    // ---- 紙吹雪（旗の上だけに降らせる） ----------------------------------------

    private class ConfettiPiece
    {
        public Transform tr;
        public SpriteRenderer sr;
        public float vy;
        public float sway;
        public float swayAmp;
        public float swaySpeed;
        public float spin;
    }

    private static Sprite _whiteSprite;

    private static Sprite WhiteSprite()
    {
        if (_whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
        return _whiteSprite;
    }

    private void PlayConfetti()
    {
        if (confettiCount <= 0)
            return;
        SoundEffects.Play(SEId.CheckpointConfetti);
        StartCoroutine(ConfettiRoutine());
    }

    private IEnumerator ConfettiRoutine()
    {
        var root = new GameObject("CheckpointConfetti");
        Vector3 center = transform.TransformPoint(confettiOffset);
        var pieces = new List<ConfettiPiece>();

        for (int i = 0; i < confettiCount; i++)
        {
            Sprite sprite = PickConfettiSprite();
            bool fallback = sprite == null;
            if (fallback)
                sprite = WhiteSprite();

            var go = new GameObject("Piece");
            go.transform.SetParent(root.transform, false);
            go.transform.position = center + new Vector3(
                Random.Range(-confettiArea.x * 0.5f, confettiArea.x * 0.5f),
                Random.Range(0f, confettiArea.y),
                0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = confettiSortingBoost;
            if (fallback)
                sr.color = PickFallbackColor();

            float s = confettiSize / Mathf.Max(0.0001f, sprite.bounds.size.y) * Random.Range(0.8f, 1.25f);
            go.transform.localScale = new Vector3(fallback ? s * 0.6f : s, s, 1f);

            var p = new ConfettiPiece();
            p.tr = go.transform;
            p.sr = sr;
            p.vy = confettiFallSpeed * Random.Range(0.7f, 1.3f);
            p.sway = Random.Range(0f, 6.28f);
            p.swayAmp = Random.Range(0.3f, 0.9f);
            p.swaySpeed = Random.Range(2f, 5f);
            p.spin = Random.Range(-260f, 260f);
            pieces.Add(p);
        }

        const float FadeTime = 0.5f;
        float t = 0f;
        while (t < confettiDuration)
        {
            float dt = Time.deltaTime;
            t += dt;
            float alpha = Mathf.Clamp01((confettiDuration - t) / FadeTime);

            for (int i = 0; i < pieces.Count; i++)
            {
                ConfettiPiece p = pieces[i];
                if (p.tr == null)
                    continue;

                p.sway += p.swaySpeed * dt;
                Vector3 pos = p.tr.position;
                pos.y -= p.vy * dt;
                pos.x += Mathf.Sin(p.sway) * p.swayAmp * dt;
                p.tr.position = pos;
                p.tr.Rotate(0f, 0f, p.spin * dt);

                Color c = p.sr.color;
                c.a = alpha;
                p.sr.color = c;
            }
            yield return null;
        }

        Destroy(root);
    }

    private Sprite PickConfettiSprite()
    {
        if (confettiSprites == null || confettiSprites.Length == 0)
            return null;
        for (int tries = 0; tries < 4; tries++)
        {
            Sprite s = confettiSprites[Random.Range(0, confettiSprites.Length)];
            if (s != null)
                return s;
        }
        return null;
    }

    private static Color PickFallbackColor()
    {
        switch (Random.Range(0, 3))
        {
            case 0: return new Color(0.92f, 0.28f, 0.25f, 1f); // 赤
            case 1: return new Color(0.98f, 0.82f, 0.25f, 1f); // 黄
            default: return new Color(0.28f, 0.45f, 0.90f, 1f); // 青
        }
    }

    /// <summary>エモートを、旗の上に作っておく（出す／消すは SetLit が切り替える）。</summary>
    private void BuildEmotes()
    {
        if (emoteBeforePrefab == null && emoteAfterPrefab == null)
            return;

        var anchor = new GameObject("EmoteAnchor");
        anchor.transform.SetParent(transform, false);
        anchor.transform.localPosition = emoteOffset;
        anchor.transform.localScale = Vector3.one * emoteScale;

        _before = SpawnEmote(emoteBeforePrefab, anchor.transform);
        _after = SpawnEmote(emoteAfterPrefab, anchor.transform);
    }

    private GameObject SpawnEmote(GameObject prefab, Transform parent)
    {
        if (prefab == null)
            return null;

        GameObject go = Instantiate(prefab, parent);
        go.transform.localPosition = Vector3.zero; // プレハブに残っている位置は使わない
        go.transform.localRotation = Quaternion.identity;

        if (emoteSortingBoost != 0)
        {
            SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder += emoteSortingBoost;
        }
        return go;
    }
}
