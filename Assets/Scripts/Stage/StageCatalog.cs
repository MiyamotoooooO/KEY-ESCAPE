using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ステージクリアで解放できる報酬の種類。</summary>
public enum StageReward
{
    Dash,          // ダッシュ（アクションの解放。使うには、キーを割り当てる）
    RangedAttack,  // 遠距離攻撃（アクションの解放。使うには、キーを割り当てる）
    ModifierKey,   // 特殊キー（Ctrl）の解放。Ctrl+攻撃の強攻撃に使う
    DoubleJump,    // 二段ジャンプ（Jumpのキーでそのまま使える）
}

/// <summary>
/// KEY ESCAPE - ステージの一覧（順番、名前、シーン名）をまとめて持つ設定アセット。
/// Resourcesフォルダに「StageCatalog」という名前で1つだけ置く。
/// ステージは、リストの上から順番に解放されていく（1つクリアすると、次が解放される）。
/// </summary>
[CreateAssetMenu(fileName = "StageCatalog", menuName = "KEY ESCAPE/Stage Catalog")]
public class StageCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("ステージを区別するID。セーブデータにも使うので、後から変えないこと。例：stage1")]
        public string id = "stage1";
        [Tooltip("画面に表示する名前。例：ステージ1")]
        public string displayName = "ステージ1";
        [Tooltip("ステージ選択画面のカードに表示する番号。例：1-1")]
        public string stageNumber = "1-1";
        [Tooltip("ステージ選択画面のカードに表示する、ステージの小さな絵。空なら、絵は出さない。")]
        public Sprite thumbnail;
        [TextArea]
        [Tooltip("ステージ選択画面に表示する説明。")]
        public string description;
        [Tooltip("このステージのシーン名。Build Settingsにも追加しておくこと。")]
        public string sceneName;
        [Tooltip("このステージをクリアした時に解放される報酬。空なら、何も解放しない。")]
        public List<StageReward> rewards = new List<StageReward>();
    }

    [Tooltip("拠点（Defaultステージ）のシーン名。ステージをクリアした後に、ここへ戻る。")]
    public string hubSceneName = "Default";

    [Tooltip("ステージの一覧。上から順番に解放される。")]
    public List<Entry> stages = new List<Entry>();

    private static StageCatalog _instance;

    /// <summary>Resourcesフォルダの「StageCatalog」を読み込んで返す。無ければnull。</summary>
    public static StageCatalog Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<StageCatalog>("StageCatalog");
            return _instance;
        }
    }

    public int IndexOf(string id)
    {
        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i].id == id)
                return i;
        }
        return -1;
    }

    public Entry Find(string id)
    {
        int i = IndexOf(id);
        return i >= 0 ? stages[i] : null;
    }
}