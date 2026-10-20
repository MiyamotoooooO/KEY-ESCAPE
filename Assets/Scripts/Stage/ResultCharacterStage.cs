using UnityEngine;

/// <summary>
/// KEY ESCAPE - 結果画面で、動くキャラクターを見せるための「小さな撮影場所」。
///
/// キーコンフィグのプレビューと同じ考え方：キャラクターを画面の外の遠い場所に置き、専用のカメラで撮って、
/// その映像（RenderTexture）を、画面のRawImageに貼る。キャラクターの動き（Animator／LimbRig）は、そのまま流れる。
///
/// 1つの撮影場所に、枠を横に並べて、まとめて撮れる（キーが何個あっても、カメラは1つ）。
/// 枠 i の映像は、RawImage の uvRect に SlotUv(i) を入れると、その枠の分だけが映る。
/// </summary>
public class ResultCharacterStage
{
    private const int MaxTextureWidth = 4096;
    private const float FootMargin = 0.08f; // 足元の余白（見える高さに対する割合）

    private readonly GameObject _root;
    private readonly RenderTexture _texture;
    private readonly int _slots;
    private readonly float _pitch;
    private readonly float _viewHeight;

    /// <summary>撮った映像。</summary>
    public RenderTexture Texture { get { return _texture; } }

    /// <param name="name">撮影場所の名前（Hierarchyで見える）。</param>
    /// <param name="origin">撮影場所の位置（ゲームの遊ぶ場所から、遠く離す）。</param>
    /// <param name="slots">並べる枠の数。</param>
    /// <param name="viewHeight">1つの枠に映る高さ（ワールドの単位）。大きいほど、キャラクターが小さく映る。</param>
    /// <param name="slotAspect">1つの枠の縦横比（横/縦）。</param>
    /// <param name="pixelHeight">映像の高さ（ピクセル）。</param>
    public ResultCharacterStage(string name, Vector3 origin, int slots, float viewHeight, float slotAspect, int pixelHeight)
    {
        _slots = Mathf.Max(1, slots);
        _viewHeight = Mathf.Max(0.5f, viewHeight);
        slotAspect = Mathf.Clamp(slotAspect, 0.2f, 5f);
        _pitch = _viewHeight * slotAspect;

        // 映像の大きさ：横が上限を超えないように、高さを下げる。
        int h = Mathf.Max(32, pixelHeight);
        h = Mathf.Min(h, Mathf.FloorToInt(MaxTextureWidth / (_slots * slotAspect)));
        h = Mathf.Max(32, h);
        int w = Mathf.Clamp(Mathf.RoundToInt(h * slotAspect * _slots), 32, MaxTextureWidth);

        _texture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        _texture.name = name + "RT";
        _texture.filterMode = FilterMode.Bilinear;
        _texture.wrapMode = TextureWrapMode.Clamp;
        _texture.Create();

        _root = new GameObject(name);
        _root.transform.position = origin;

        GameObject camObj = new GameObject("Camera");
        camObj.transform.SetParent(_root.transform, false);
        float centerX = (_slots - 1) * _pitch * 0.5f;
        float centerY = _viewHeight * (0.5f - FootMargin);
        camObj.transform.localPosition = new Vector3(centerX, centerY, -10f);

        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = _viewHeight * 0.5f;
        cam.aspect = w / (float)h;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.depth = -100f;
        cam.targetTexture = _texture;
    }

    /// <summary>枠 i の映像だけを映すための、RawImage用の範囲。</summary>
    public Rect SlotUv(int slot)
    {
        return new Rect(slot / (float)_slots, 0f, 1f / _slots, 1f);
    }

    /// <summary>キャラクターを、枠 i に置く。originAtCenterがfalseなら、足元（原点）が枠の下のほうに来る。trueなら、枠の真ん中に置く（原点が絵の中心にあるキャラクター用）。</summary>
    public void Place(GameObject go, int slot, bool originAtCenter)
    {
        go.transform.SetParent(_root.transform, false);
        float y = originAtCenter ? _viewHeight * (0.5f - FootMargin) : 0f;
        go.transform.localPosition = new Vector3(slot * _pitch, y, 0f);
    }

    /// <summary>撮影場所と映像を、片づける。</summary>
    public void Dispose()
    {
        if (_root != null)
            Object.Destroy(_root);
        if (_texture != null)
        {
            _texture.Release();
            Object.Destroy(_texture);
        }
    }
}
