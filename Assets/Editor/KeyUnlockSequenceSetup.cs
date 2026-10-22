using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キー獲得の演出（KeyConfigUnlockSequence）の部品を、キー獲得ポップアップ（KeyAcquiredPopup）に付ける。
///
/// 開いているシーンにある KeyAcquiredPopup（プレハブを開いているときは、そのプレハブの中のもの）に、
/// KeyConfigUnlockSequence を付けて、キーキャラクターのプレハブ（KeyNPC_Visual）を割り当てる。
/// すでに付いていれば、空の欄だけを埋める（調整した値は、そのまま）。
///
/// メニュー：Tools > KEY ESCAPE > キー解放演出 > シーンに追加する
///
/// もう1つのメニュー「ポップアップにキーキャラクターと一言を足す」は、キー獲得ポップアップ（KeyAcquiredPopup）に、
/// キーキャラクターの映像（RawImage）・一言の文字・紙吹雪の入れ物を足して、キーキャラクターのプレハブを割り当てる。
/// すでに割り当てのある欄は、そのまま。足したものの位置と大きさは、Unityで調整する。
/// </summary>
public static class KeyUnlockSequenceSetup
{
    private const string KeyPrefabPath = "Assets/Prefab/Mob/Enemy/KeyNPC_Visual.prefab";

    [MenuItem("Tools/KEY ESCAPE/キー解放演出/シーンに追加する")]
    public static void Setup()
    {
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        KeyAcquiredPopup[] popups = prefabStage != null
            ? prefabStage.prefabContentsRoot.GetComponentsInChildren<KeyAcquiredPopup>(true)
            : Object.FindObjectsOfType<KeyAcquiredPopup>(true);

        GameObject keyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefabPath);
        if (keyPrefab == null)
            Debug.LogWarning("[KeyUnlockSequenceSetup] " + KeyPrefabPath + " が見つかりません。Key Character Prefab を、手動で入れてください。");

        int count = 0;
        for (int i = 0; i < popups.Length; i++)
        {
            KeyAcquiredPopup popup = popups[i];
            if (popup == null || !popup.gameObject.scene.IsValid())
                continue;

            KeyConfigUnlockSequence seq = popup.GetComponent<KeyConfigUnlockSequence>();
            if (seq == null)
                seq = Undo.AddComponent<KeyConfigUnlockSequence>(popup.gameObject);

            Undo.RecordObject(seq, "キー解放演出の設定");
            if (seq.keyCharacterPrefab == null)
                seq.keyCharacterPrefab = keyPrefab;

            Undo.RecordObject(popup, "キー解放演出の設定");
            popup.unlockSequence = seq;

            EditorUtility.SetDirty(seq);
            EditorUtility.SetDirty(popup);
            EditorSceneManager.MarkSceneDirty(popup.gameObject.scene);
            count++;
        }

        if (count == 0)
        {
            EditorUtility.DisplayDialog("キー解放演出",
                "開いているシーン（またはプレハブ）に、KeyAcquiredPopup が見つかりませんでした。\nキー獲得ポップアップのあるシーンを開いて、もう一度実行してください。", "OK");
            return;
        }

        Debug.Log("[KeyUnlockSequenceSetup] " + count + " 個の KeyAcquiredPopup に、キー解放演出を付けました。シーンを保存（Ctrl+S）してください。");
    }

    [MenuItem("Tools/KEY ESCAPE/キー解放演出/ポップアップにキーキャラクターと一言を足す")]
    public static void AddPopupParts()
    {
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        KeyAcquiredPopup[] popups = prefabStage != null
            ? prefabStage.prefabContentsRoot.GetComponentsInChildren<KeyAcquiredPopup>(true)
            : Object.FindObjectsOfType<KeyAcquiredPopup>(true);

        GameObject keyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefabPath);
        if (keyPrefab == null)
            Debug.LogWarning("[KeyUnlockSequenceSetup] " + KeyPrefabPath + " が見つかりません。Key Character Prefab を、手動で入れてください。");

        int count = 0;
        for (int i = 0; i < popups.Length; i++)
        {
            KeyAcquiredPopup popup = popups[i];
            if (popup == null || !popup.gameObject.scene.IsValid() || popup.panelRoot == null)
                continue;

            Undo.RecordObject(popup, "ポップアップにキーキャラクターと一言を足す");
            Transform panel = popup.panelRoot.transform;

            // キーキャラクターの映像：キーの文字と同じ位置に置く
            if (popup.characterView == null)
            {
                Transform parent = popup.keyCapGroup != null ? popup.keyCapGroup.transform : panel;
                GameObject view = new GameObject("KeyCharacterView", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(view, "KeyCharacterView");
                view.transform.SetParent(parent, false);
                RectTransform vr = (RectTransform)view.transform;
                vr.anchorMin = new Vector2(0.5f, 0.5f);
                vr.anchorMax = new Vector2(0.5f, 0.5f);
                vr.pivot = new Vector2(0.5f, 0.5f);
                vr.sizeDelta = new Vector2(260f, 260f);
                if (popup.keyLetterText != null)
                    vr.position = popup.keyLetterText.rectTransform.position;
                RawImage raw = view.AddComponent<RawImage>();
                raw.raycastTarget = false;
                popup.characterView = raw;
            }

            // 一言：キーの下の線の、少し下に置く
            if (popup.commentText == null)
            {
                GameObject obj = new GameObject("CommentText", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(obj, "CommentText");
                obj.transform.SetParent(panel, false);
                RectTransform tr = (RectTransform)obj.transform;
                tr.anchorMin = new Vector2(0.5f, 0.5f);
                tr.anchorMax = new Vector2(0.5f, 0.5f);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.sizeDelta = new Vector2(760f, 90f);
                if (popup.underlineGroup != null)
                    tr.position = popup.underlineGroup.transform.position;
                else if (popup.keyLetterText != null)
                    tr.position = popup.keyLetterText.rectTransform.position;
                tr.anchoredPosition += new Vector2(0f, -70f);

                TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
                if (popup.keyLetterText != null)
                {
                    tmp.font = popup.keyLetterText.font;
                    tmp.color = popup.keyLetterText.color;
                }
                tmp.text = "ここに一言が出ます";
                tmp.fontSize = 36f;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 20f;
                tmp.fontSizeMax = 36f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = true;
                tmp.raycastTarget = false;
                popup.commentText = tmp;
            }

            // 紙吹雪の入れ物：ポップアップのCanvasいっぱいに、一番手前に置く
            if (popup.confettiLayer == null)
            {
                Canvas canvas = popup.panelRoot.GetComponentInParent<Canvas>();
                Transform host = canvas != null ? canvas.rootCanvas.transform : panel;
                GameObject layer = new GameObject("KeyConfettiLayer", typeof(RectTransform), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(layer, "KeyConfettiLayer");
                layer.transform.SetParent(host, false);
                layer.GetComponent<LayoutElement>().ignoreLayout = true;
                RectTransform lr = (RectTransform)layer.transform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero;
                lr.offsetMax = Vector2.zero;
                layer.transform.SetAsLastSibling();
                popup.confettiLayer = lr;
            }

            if (popup.keyCharacterPrefab == null)
                popup.keyCharacterPrefab = keyPrefab;

            EditorUtility.SetDirty(popup);
            EditorSceneManager.MarkSceneDirty(popup.gameObject.scene);
            count++;
        }

        if (count == 0)
        {
            EditorUtility.DisplayDialog("キー獲得ポップアップ",
                "開いているシーン（またはプレハブ）に、KeyAcquiredPopup が見つかりませんでした。\nキー獲得ポップアップのあるシーンを開いて、もう一度実行してください。", "OK");
            return;
        }

        Debug.Log("[KeyUnlockSequenceSetup] " + count + " 個の KeyAcquiredPopup に、キーキャラクター・一言・紙吹雪の部品を足しました。"
                  + "位置と大きさを調整して、シーンを保存（Ctrl+S）してください。");
    }
}
