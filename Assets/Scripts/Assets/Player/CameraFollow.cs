using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
	public float FollowSpeed = 2f;
	public Transform Target;

	// Transform of the camera to shake. Grabs the gameObject's transform
	// if null.
	private Transform camTransform;
	private Camera cam;

	// How long the object should shake for.
	public float shakeDuration = 0f;

	// Amplitude of the shake. A larger value shakes the camera harder.
	public float shakeAmount = 0.1f;
	public float decreaseFactor = 1.0f;

	// 今回の揺れの強さ（ShakeCamera の引数で決まる）
	private float currentShakeAmount = 0.1f;

	// 前のフレームで足した揺れ分。これを外すと、本来のカメラ位置に戻せる。
	private Vector3 lastShakeOffset = Vector3.zero;

	// ズーム（死亡演出で、プレイヤーにカメラを寄せる）
	private bool zooming = false;
	private float zoomStartSize;
	private float zoomTargetSize;
	private float zoomDuration = 1f;
	private float zoomElapsed = 0f;
	private float baseSize;

	void Awake()
	{
		Cursor.visible = false;
		if (camTransform == null)
		{
			camTransform = GetComponent(typeof(Transform)) as Transform;
		}
		cam = GetComponent<Camera>();
		if (cam == null)
			cam = Camera.main;
		if (cam != null)
			baseSize = GetSize();
	}

	private void Update()
	{
		// 前のフレームの揺れ分を外して、本来のカメラ位置に戻す
		Vector3 basePos = camTransform.position - lastShakeOffset;

		// ズーム中は、時間がスローになっていてもカメラはしっかり追いかける
		float followDt = zooming ? Mathf.Min(Time.unscaledDeltaTime, 0.05f) : Time.deltaTime;
		float speed = zooming ? FollowSpeed * 2f : FollowSpeed;

		if (Target != null)
		{
			Vector3 newPosition = Target.position;
			newPosition.z = -10;
			basePos = Vector3.Slerp(basePos, newPosition, speed * followDt);
		}

		// 揺れ（ポーズやスロー中でも、実時間で数える）
		Vector3 offset = Vector3.zero;
		if (shakeDuration > 0)
		{
			Vector2 r = Random.insideUnitCircle * currentShakeAmount;
			offset = new Vector3(r.x, r.y, 0f);
			shakeDuration -= Time.unscaledDeltaTime * decreaseFactor;
		}

		camTransform.position = basePos + offset;
		lastShakeOffset = offset;

		// ズーム
		if (zooming && cam != null)
		{
			zoomElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
			float k = Mathf.Clamp01(zoomElapsed / zoomDuration);
			k = 1f - (1f - k) * (1f - k); // はじめ早く、だんだんゆっくり
			SetSize(Mathf.Lerp(zoomStartSize, zoomTargetSize, k));
			if (zoomElapsed >= zoomDuration)
				zooming = false;
		}
	}

	/// <summary>従来どおりの揺れ（プレイヤーが敵に当てた時）。</summary>
	public void ShakeCamera()
	{
		ShakeCamera(0.2f, shakeAmount);
	}

	/// <summary>秒数と強さを指定して揺らす（被弾・死亡の演出用）。</summary>
	public void ShakeCamera(float duration, float amount)
	{
		currentShakeAmount = amount;
		shakeDuration = duration;
	}

	/// <summary>
	/// カメラを寄せる。sizeRatio は 元の画面の広さに対する割合（0.6 なら6割の広さ = 拡大）。
	/// duration 秒かけて寄る。
	/// </summary>
	public void ZoomTo(float sizeRatio, float duration)
	{
		if (cam == null)
			return;
		zoomStartSize = GetSize();
		zoomTargetSize = baseSize * sizeRatio;
		zoomDuration = Mathf.Max(0.01f, duration);
		zoomElapsed = 0f;
		zooming = true;
	}

	/// <summary>ズームを元の大きさに、すぐ戻す。</summary>
	public void ResetZoom()
	{
		zooming = false;
		if (cam != null)
			SetSize(baseSize);
	}

	private float GetSize()
	{
		return cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
	}

	private void SetSize(float value)
	{
		if (cam.orthographic)
			cam.orthographicSize = value;
		else
			cam.fieldOfView = value;
	}
}
