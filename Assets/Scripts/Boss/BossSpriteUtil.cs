using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ボス戦で使う、小さな道具たち。
///   ・コードで作る絵（角の丸い四角・まるい光・白い四角）
///   ・音を鳴らす（設定画面のSEの音量にしたがう）
///   ・カメラを揺らす
///   ・かけら（BossBits）を飛び散らせる
/// どこにも付けなくてよい。
/// </summary>
public static class BossSpriteUtil
{
    private static Sprite s_round;
    private static Sprite s_radial;
    private static Sprite s_white;
    private static CameraFollow s_cam;

    /// <summary>角の丸い四角の絵（大きさ 0.64 × 0.64。Sliced にすると伸ばせる）。</summary>
    public static Sprite Round()
    {
        if (s_round != null)
            return s_round;
        const int n = 64;
        const float radius = 22f;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[n * n];
        float c = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - c) - (c - radius);
                float dy = Mathf.Abs(y + 0.5f - c) - (c - radius);
                float ox = Mathf.Max(dx, 0f);
                float oy = Mathf.Max(dy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        s_round = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
        return s_round;
    }

    /// <summary>まん中が濃く、まわりへ薄れる、まるい光の絵（大きさ 1.28 × 1.28）。</summary>
    public static Sprite Radial()
    {
        if (s_radial != null)
            return s_radial;
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[n * n];
        float c = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - c) / c;
                float dy = (y + 0.5f - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a;
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        s_radial = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        return s_radial;
    }

    /// <summary>白い四角の絵（大きさ 1 × 1）。色は SpriteRenderer の Color で付ける。</summary>
    public static Sprite White()
    {
        if (s_white != null)
            return s_white;
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        Color32[] px = new Color32[16];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        s_white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return s_white;
    }

    /// <summary>効果音を鳴らす。設定画面のSEの音量にしたがう。clip が空なら何もしない。</summary>
    public static void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null)
            return;
        if (AudioManager.Instance != null)
            AudioManager.PlaySE(clip, volume);
        else
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, volume);
    }

    /// <summary>カメラを揺らす。</summary>
    public static void Shake(float seconds, float amount)
    {
        if (s_cam == null)
            s_cam = Object.FindObjectOfType<CameraFollow>();
        if (s_cam != null)
            s_cam.ShakeCamera(seconds, amount);
    }

    /// <summary>カメラを、画面の広さの割合（1が元の広さ。大きいほど広く見える）まで動かす。</summary>
    public static void Zoom(float ratio, float seconds)
    {
        if (s_cam == null)
            s_cam = Object.FindObjectOfType<CameraFollow>();
        if (s_cam != null)
            s_cam.ZoomTo(ratio, seconds);
    }
}

/// <summary>
/// かけらを飛び散らせて、少しして消える。BossBits.Burst(...) で呼ぶ。
/// </summary>
public class BossBits : MonoBehaviour
{
    private class Bit
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float spin;
        public Color color;
    }

    private readonly List<Bit> _bits = new List<Bit>();
    private float _t;
    private float _seconds;
    private float _gravity;

    public static void Burst(Vector3 pos, Color color, int count, float speed, float size, float seconds, int sortingOrder, float gravity)
    {
        GameObject go = new GameObject("BossBits");
        go.transform.position = pos;
        BossBits b = go.AddComponent<BossBits>();
        b._seconds = Mathf.Max(0.1f, seconds);
        b._gravity = gravity;
        for (int i = 0; i < count; i++)
        {
            GameObject g = new GameObject("Bit");
            g.transform.SetParent(go.transform, false);
            g.transform.localPosition = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.1f, 0.3f), 0f);
            float s = size * Random.Range(0.5f, 1.3f);
            g.transform.localScale = new Vector3(s, s * Random.Range(0.6f, 1.2f), 1f);
            g.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = BossSpriteUtil.White();
            Color c = color * Random.Range(0.8f, 1.15f);
            c.a = color.a;
            sr.color = c;
            sr.sortingOrder = sortingOrder;

            Bit bit = new Bit();
            bit.tr = g.transform;
            bit.sr = sr;
            bit.color = c;
            float ang = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float sp = speed * Random.Range(0.4f, 1.1f);
            bit.vel = new Vector2(Mathf.Cos(ang) * sp * Random.Range(-1.4f, 1.4f), Mathf.Sin(ang) * sp);
            bit.spin = Random.Range(-540f, 540f);
            b._bits.Add(bit);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;
        float fade = 1f - Mathf.Clamp01((_t - _seconds * 0.55f) / (_seconds * 0.45f));
        for (int i = 0; i < _bits.Count; i++)
        {
            Bit b = _bits[i];
            if (b.tr == null)
                continue;
            b.vel.y -= _gravity * dt;
            b.tr.position += new Vector3(b.vel.x, b.vel.y, 0f) * dt;
            b.tr.Rotate(0f, 0f, b.spin * dt);
            Color c = b.color;
            c.a *= fade;
            b.sr.color = c;
        }
        if (_t >= _seconds)
            Destroy(gameObject);
    }
}
