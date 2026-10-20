using System;
using UnityEngine;

/// <summary>音の種類。新しい動作の音を増やす時は、ここに名前を足し、下のSoundEffectsにも欄を足す。</summary>
public enum SEId
{
    KeyPickup,       // キーを回収した
    KeyBind,         // キーを操作に割り当てた
    DialogueOpen,    // 会話が始まった
    DialogueTalk,    // 会話の文字が出る（1文字ごとの音）
    DialogueAdvance, // 会話の次の文へ進んだ
    DialogueClose,   // 会話が終わった
    PlayerDamage,    // プレイヤーが攻撃を受けた
    PlayerDeath,     // プレイヤーが死亡した
    DeathFlash,      // 死亡した瞬間のフラッシュ
    DialogueChoiceShow, // 会話の選択肢が1つ表示された（上から順に1つずつ鳴る）
    StageSelectOpen,    // ステージ選択を開いた
    StageSelectMove,    // ステージ選択で、選んでいるカードが動いた
    StageSelectDecide,  // ステージ選択で、解放済みのステージを決定した
    StageSelectLocked,  // ステージ選択で、ロック中のステージを押した
    StageSelectPage,    // ステージ選択で、ページをめくった
    SettingsOpen,       // 設定画面を開いた
    KeyConfigOpen,      // キーコンフィグ画面を開いた
    StageClearGoal,     // ゴールした瞬間（フラッシュ）
    StageClearConfetti, // 紙吹雪が出る
    StageClearJingle,   // 「STAGE CLEAR!」の看板が出る
    StageClearGoal2     // 2回目のフラッシュ（さらにスローになる瞬間）
}

/// <summary>
/// KEY ESCAPE - 動作の効果音をまとめて持つ設定アセット。
/// Resourcesフォルダに「SoundEffects」という名前で1つだけ置く。
/// 音は AudioManager 経由で鳴るので、設定画面のSEの音量に従う。
///
/// 使い方（スクリプトから）：SoundEffects.Play(SEId.KeyPickup);
/// 使い方（コード不要）：PlaySEOnEnable をパネルなどに付ける。
/// </summary>
[CreateAssetMenu(fileName = "SoundEffects", menuName = "KEY ESCAPE/Sound Effects")]
public class SoundEffects : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("キー")]
    public Entry keyPickup = new Entry();
    public Entry keyBind = new Entry();

    [Header("会話")]
    public Entry dialogueOpen = new Entry();
    public Entry dialogueTalk = new Entry();
    public Entry dialogueAdvance = new Entry();
    public Entry dialogueClose = new Entry();
    public Entry dialogueChoiceShow = new Entry();

    [Header("プレイヤー")]
    public Entry playerDamage = new Entry();
    public Entry playerDeath = new Entry();
    public Entry deathFlash = new Entry();

    [Header("ステージ選択")]
    public Entry stageSelectOpen = new Entry();
    public Entry stageSelectMove = new Entry();
    public Entry stageSelectDecide = new Entry();
    public Entry stageSelectLocked = new Entry();
    public Entry stageSelectPage = new Entry();

    [Header("画面を開く")]
    public Entry settingsOpen = new Entry();
    public Entry keyConfigOpen = new Entry();

    [Header("ステージクリア")]
    public Entry stageClearGoal = new Entry();
    public Entry stageClearGoal2 = new Entry();
    public Entry stageClearConfetti = new Entry();
    public Entry stageClearJingle = new Entry();

    private static SoundEffects _instance;
    private static readonly float[] _lastPlayTime = new float[Enum.GetValues(typeof(SEId)).Length];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        for (int i = 0; i < _lastPlayTime.Length; i++)
            _lastPlayTime[i] = -10f;
    }

    private static SoundEffects Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<SoundEffects>("SoundEffects");
            return _instance;
        }
    }

    /// <summary>
    /// 効果音を鳴らす。minIntervalに秒数を入れると、その間隔より短く続けて鳴らすことはしない
    /// （会話の文字ごとの音のように、速く繰り返す音用）。
    /// </summary>
    public static void Play(SEId id, float minInterval = 0f)
    {
        SoundEffects se = Instance;
        if (se == null)
            return;

        if (minInterval > 0f)
        {
            // ゲームを止めている間も使えるよう、unscaledを使う。
            if (Time.unscaledTime - _lastPlayTime[(int)id] < minInterval)
                return;
            _lastPlayTime[(int)id] = Time.unscaledTime;
        }

        Entry e = se.Get(id);
        if (e != null && e.clip != null)
            AudioManager.PlaySE(e.clip, e.volume);
    }

    private Entry Get(SEId id)
    {
        switch (id)
        {
            case SEId.KeyPickup: return keyPickup;
            case SEId.KeyBind: return keyBind;
            case SEId.DialogueOpen: return dialogueOpen;
            case SEId.DialogueTalk: return dialogueTalk;
            case SEId.DialogueAdvance: return dialogueAdvance;
            case SEId.DialogueClose: return dialogueClose;
            case SEId.PlayerDamage: return playerDamage;
            case SEId.PlayerDeath: return playerDeath;
            case SEId.DeathFlash: return deathFlash;
            case SEId.DialogueChoiceShow: return dialogueChoiceShow;
            case SEId.StageSelectOpen: return stageSelectOpen;
            case SEId.StageSelectMove: return stageSelectMove;
            case SEId.StageSelectDecide: return stageSelectDecide;
            case SEId.StageSelectLocked: return stageSelectLocked;
            case SEId.StageSelectPage: return stageSelectPage;
            case SEId.SettingsOpen: return settingsOpen;
            case SEId.KeyConfigOpen: return keyConfigOpen;
            case SEId.StageClearGoal: return stageClearGoal;
            case SEId.StageClearGoal2: return stageClearGoal2;
            case SEId.StageClearConfetti: return stageClearConfetti;
            case SEId.StageClearJingle: return stageClearJingle;
            default: return null;
        }
    }
}