using System;
using UnityEngine;

/// <summary>
/// KEY ESCAPE - ゲーム全体の設定（BGM音量／SE音量／スクリーンモード／操作ガイドの表示）。
/// staticなクラスなので、シーンが変わっても値は維持される。
/// 値はPlayerPrefsに保存され、ゲームを終了して次に起動した時も引き継がれる。
///
/// 使い方（読む側）：GameSettings.BgmVolume / SeVolume を参照する、または
/// GameSettings.Changed イベントを購読して、変更されたら反映する。
/// 使い方（書く側）：SetBgmVolume() などを呼ぶ（設定画面のSettingsUIが呼ぶ）。
/// </summary>
public static class GameSettings
{
    public const float DefaultBgmVolume = 0.5f;
    public const float DefaultSeVolume = 0.7f;
    public const bool DefaultFullscreen = true;
    public const bool DefaultControlGuideVisible = true;

    private const string KeyBgm = "Settings.BgmVolume";
    private const string KeySe = "Settings.SeVolume";
    private const string KeyFullscreen = "Settings.Fullscreen";
    private const string KeyGuide = "Settings.ControlGuideVisible";

    /// <summary>BGM音量（0〜1）。</summary>
    public static float BgmVolume { get; private set; } = DefaultBgmVolume;
    /// <summary>効果音の音量（0〜1）。</summary>
    public static float SeVolume { get; private set; } = DefaultSeVolume;
    /// <summary>trueならフルスクリーン、falseならウィンドウ。</summary>
    public static bool Fullscreen { get; private set; } = DefaultFullscreen;
    /// <summary>操作ガイドを表示するか。</summary>
    public static bool ControlGuideVisible { get; private set; } = DefaultControlGuideVisible;

    /// <summary>どれかの設定が変わった時に呼ばれる。</summary>
    public static event Action Changed;

    // 最初のシーンが読み込まれる前に、保存済みの設定を読み込んで反映する。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Changed = null; // ドメインリロードを切っている場合に、前回のPlayの購読が残らないようにする。

        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyBgm, DefaultBgmVolume));
        SeVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySe, DefaultSeVolume));
        Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, DefaultFullscreen ? 1 : 0) == 1;
        ControlGuideVisible = PlayerPrefs.GetInt(KeyGuide, DefaultControlGuideVisible ? 1 : 0) == 1;

        ApplyScreenMode();

        Application.quitting -= Save;
        Application.quitting += Save;
    }

    public static void SetBgmVolume(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(BgmVolume, value))
            return;
        BgmVolume = value;
        PlayerPrefs.SetFloat(KeyBgm, value);
        Changed?.Invoke();
    }

    public static void SetSeVolume(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(SeVolume, value))
            return;
        SeVolume = value;
        PlayerPrefs.SetFloat(KeySe, value);
        Changed?.Invoke();
    }

    public static void SetFullscreen(bool fullscreen)
    {
        if (Fullscreen == fullscreen)
            return;
        Fullscreen = fullscreen;
        PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
        ApplyScreenMode();
        Changed?.Invoke();
    }

    public static void SetControlGuideVisible(bool visible)
    {
        if (ControlGuideVisible == visible)
            return;
        ControlGuideVisible = visible;
        PlayerPrefs.SetInt(KeyGuide, visible ? 1 : 0);
        Changed?.Invoke();
    }

    /// <summary>「初期設定に戻す」から呼ぶ。全項目を初期値に戻して保存する。</summary>
    public static void ResetToDefaults()
    {
        SetBgmVolume(DefaultBgmVolume);
        SetSeVolume(DefaultSeVolume);
        SetFullscreen(DefaultFullscreen);
        SetControlGuideVisible(DefaultControlGuideVisible);
        Save();
    }

    /// <summary>PlayerPrefsをディスクに書き込む。設定画面を閉じた時と、ゲーム終了時に呼ばれる。</summary>
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private static void ApplyScreenMode()
    {
        // エディタのGameビューでは効かない。ビルドしたゲームで確認すること。
        Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}