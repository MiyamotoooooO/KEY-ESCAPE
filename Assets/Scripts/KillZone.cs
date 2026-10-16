using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class KillZone : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.gameObject.tag == "Player")
        {
            // 短く暗転してから、シーンを読み直す（死亡演出の最中なら、そちらに任せる）
            HitFeedback.ReloadWithFade(0.35f);
        }
        else
        {
            Destroy(col.gameObject);
        }
    }
}
