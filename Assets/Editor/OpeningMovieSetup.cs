using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// オープニングムービーを、タイトルシーンに追加する（手順をまとめて行う）。
///
/// メニュー Tools > KEY ESCAPE > オープニングムービー > タイトルシーンに追加する
///   ・タイトルシーン（TitleMenuがあるシーン）を開いた状態で実行する。
///   ・「OpeningMovie」というオブジェクトを作って（すでにあれば、それを使う）、次を自動で入れる:
///       - キーキャラクター : Assets/Prefab/Mob/Enemy/KeyNPC_Visual.prefab
///       - フォント         : シーンの中の文字から借りる
///       - 絵               : キー素材・DoorImage（ドア枠）・DoorUtigawaImage（ドア）・キーボード背景・モニター・moveBG（部屋の地面）・ゲームの Ground / Grass
///       - タイトルの部品   : ロゴ（シーンの中の dasda_0）・メニューのボタン
///       - モニター正面     : Assets/sprite/UI/モニター正面.png（「はじめから」のムービーの、モニターのアップ。空の欄にだけ入る）
///       - Kキー            : Assets/KeyDefinition/Key_K.asset（「はじめから」のあと、拠点で仲間になる）
///   ・TitleMenu の「Opening Movie」に、割り当てる。
///   ・何回実行しても、同じ結果になる。
/// </summary>
public static class OpeningMovieSetup
{
    private const string NpcPrefabPath = "Assets/Prefab/Mob/Enemy/KeyNPC_Visual.prefab";

    private static GameObject FindObject(string objName, GameObject current)
    {
        if (current != null)
            return current;
        return GameObject.Find(objName);
    }

    private const string MovieSoundDir = "Assets/8Bit Music - 062022/ムービ用/";

    /// <summary>音の欄が空のときだけ、音を入れる（自分で入れた音は、そのまま残る）。</summary>
    private static void AssignClip(ref AudioClip target, string fileName, ref string report)
    {
        if (target != null)
            return;
        AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(MovieSoundDir + fileName);
        if (c != null)
            target = c;
        else
            report += "\n・音が見つかりません（" + MovieSoundDir + fileName + "）。";
    }

    private static void AssignSprite(ref Sprite target, string path, ref string report)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null)
            target = s;
        else
            report += "\n・絵が見つかりません（" + path + "）。Sprite（Texture Type が Sprite）になっているか、確認してください。";
    }

    [MenuItem("Tools/KEY ESCAPE/オープニングムービー/タイトルシーンに追加する")]
    public static void Setup()
    {
        TitleMenu menu = Object.FindObjectOfType<TitleMenu>(true);
        if (menu == null)
        {
            EditorUtility.DisplayDialog("オープニングムービー", "TitleMenu が見つかりません。\nタイトルシーン（TitleScene）を開いてから、もう一度実行してください。", "OK");
            return;
        }

        OpeningMovie movie = Object.FindObjectOfType<OpeningMovie>(true);
        if (movie == null)
        {
            GameObject go = new GameObject("OpeningMovie");
            Undo.RegisterCreatedObjectUndo(go, "オープニングムービーを追加");
            movie = go.AddComponent<OpeningMovie>();
        }
        else
        {
            Undo.RecordObject(movie, "オープニングムービーを設定");
        }

        string report = "";

        // キーキャラクター
        if (movie.keyCharacterPrefab == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            if (prefab != null)
                movie.keyCharacterPrefab = prefab;
            else
                report += "\n・キーキャラクターのプレハブが見つかりません（" + NpcPrefabPath + "）。メニュー「敵キー > プレハブを作成（会話・待機のみ）」で作るか、手で入れてください。";
        }

        // フォント（日本語が出るもの）
        if (movie.font == null)
        {
            TMP_Text[] texts = Object.FindObjectsOfType<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null)
                {
                    movie.font = texts[i].font;
                    break;
                }
            }
            if (movie.font == null)
                report += "\n・フォントが見つかりませんでした。OpeningMovie の Font に、日本語が出るフォントを入れてください。";
        }

        // ムービーで使う絵（自分で用意した絵・ゲームで使っている絵）。実行するたびに、この絵に入れなおす
        AssignSprite(ref movie.keycapSprite, "Assets/sprite/UI/キー素材.png", ref report);
        movie.fingerSprite = null;   // 指は、はじめの仮の絵（肌色の指）を使う
        AssignSprite(ref movie.doorFrameSprite, "Assets/sprite/UI/DoorImage.png", ref report);
        AssignSprite(ref movie.doorSprite, "Assets/sprite/UI/DoorUtigawaImage.png", ref report);
        AssignSprite(ref movie.keyboardSprite, "Assets/sprite/UI/キーボード背景.png", ref report);
        AssignSprite(ref movie.monitorSprite, "Assets/sprite/UI/モニター.png", ref report);
        AssignSprite(ref movie.roomGroundSprite, "Assets/sprite/UI/moveBG.png", ref report);
        // 「はじめから」のムービーの、モニターのアップ（空の欄にだけ入る。自分で入れた絵は、そのまま残る）
        if (movie.introMonitorSprite == null)
            AssignSprite(ref movie.introMonitorSprite, "Assets/sprite/UI/モニター正面.png", ref report);
        AssignSprite(ref movie.groundSprite, "Assets/MetroidvaniaController/Sprites/Ground.png", ref report);
        AssignSprite(ref movie.grassSprite, "Assets/MetroidvaniaController/Sprites/Grass.png", ref report);

        // プレイヤー（キーたちを追いかける。空の欄にだけ入る）
        if (movie.playerPrefab == null)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Mob/Player/DrawCharacter.prefab");
            if (playerPrefab != null)
                movie.playerPrefab = playerPrefab;
            else
                report += "\n・プレイヤー（DrawCharacter）のプレハブが見つかりません。OpeningMovie の Player Prefab に入れてください。";
        }

        // 「はじめから」のあと、拠点で仲間になる、逃げ遅れたキー（Kキー。空の欄にだけ入る）
        if (movie.introKey == null)
        {
            KeyDefinition keyK = AssetDatabase.LoadAssetAtPath<KeyDefinition>("Assets/KeyDefinition/Key_K.asset");
            if (keyK != null)
                movie.introKey = keyK;
            else
                report += "\n・Kキー（Assets/KeyDefinition/Key_K.asset）が見つかりません。OpeningMovie の Intro Key に、Key_K を入れてください。";
        }

        // 会話のあとのヒント（Tabでキーコンフィグ）の説明パネル（空の欄にだけ入る）
        if (movie.introTalk != null && movie.introTalk.tutorial != null && movie.introTalk.tutorial.hintUiPrefab == null)
        {
            GimmickIntroUI hintUi = AssetDatabase.LoadAssetAtPath<GimmickIntroUI>("Assets/Prefab/GimmickIntroUI.prefab");
            if (hintUi != null)
                movie.introTalk.tutorial.hintUiPrefab = hintUi;
            else
                report += "\n・ヒントの説明パネル（Assets/Prefab/GimmickIntroUI.prefab）が見つかりません。OpeningMovie の Intro Talk > Tutorial > Hint Ui Prefab に入れてください。";
        }

        // 音（空の欄にだけ入る。Collapse Se は、まだ決まっていないので、空のまま）
        AssignClip(ref movie.bgm, "BackBGM.mp3", ref report);
        AssignClip(ref movie.slamSe, "impact-slam-64.wav", ref report);
        AssignClip(ref movie.hopSe, "retro-game-jump-32.wav", ref report);
        AssignClip(ref movie.resolveSe, "feedback-success-68.wav", ref report);
        AssignClip(ref movie.doorSe, "door-open-01.wav", ref report);
        AssignClip(ref movie.landSe, "jumpland.wav", ref report);
        AssignClip(ref movie.logoSe, "logo-reveal-03.wav", ref report);
        if (movie.clackClips == null || movie.clackClips.Length == 0)
        {
            AudioClip typeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MovieSoundDir + "タイプ音.wav");
            if (typeClip != null)
                movie.clackClips = new AudioClip[] { typeClip };
            else
                report += "\n・音が見つかりません（" + MovieSoundDir + "タイプ音.wav）。";
        }

        // キーの絵が見つからなかったときは、キーキャラクターと同じ KeyBody を使う
        if (movie.keycapSprite == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/EnemyKey" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant().StartsWith("keybody"))
                {
                    movie.keycapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    break;
                }
            }
        }

        // タイトル画面の部品（ロゴ・メニュー）。ロゴは、シーンの中の dasda_0
        movie.titleLogo = FindObject("dasda_0", movie.titleLogo);
        if (movie.titleLogo == null)
            report += "\n・ロゴ（dasda_0）が見つかりませんでした。OpeningMovie の Title Logo に、ロゴのオブジェクトを入れてください。";

        System.Collections.Generic.List<Button> buttons = new System.Collections.Generic.List<Button>();
        if (menu.newGameButton != null) buttons.Add(menu.newGameButton);
        if (menu.continueButton != null) buttons.Add(menu.continueButton);
        if (menu.settingsButton != null) buttons.Add(menu.settingsButton);
        if (menu.quitButton != null) buttons.Add(menu.quitButton);
        movie.titleButtons = buttons.ToArray();

        Undo.RecordObject(menu, "オープニングムービーを割り当て");
        menu.openingMovie = movie;

        EditorUtility.SetDirty(movie);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeObject = movie.gameObject;

        EditorUtility.DisplayDialog("オープニングムービー",
            "タイトルシーンに追加しました。\nTitleMenu の Opening Movie に、割り当て済みです。\n\nCtrl+S でシーンを保存してから、再生して「はじめから」を押してください。"
            + (report.Length > 0 ? "\n\n【確認】" + report : "")
            + "\n\nムービーは、ゲームを起動して最初にタイトル画面が出たときだけ流れます。\n音（BGM・SE）は、OpeningMovie の欄に、音の素材を入れると鳴ります（空なら無音）。", "OK");
    }
}
