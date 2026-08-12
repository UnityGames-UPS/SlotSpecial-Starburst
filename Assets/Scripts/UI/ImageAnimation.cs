using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ImageAnimation : MonoBehaviour
{
	public enum ImageState
	{
		NONE,
		PLAYING,
		PAUSED
	}

	[HideInInspector] public ImageState currentAnimationState;
	public static ImageAnimation Instance;
	internal string id;
	public List<Sprite> textureArray;
	public Image rendererDelegate;
	public bool useSharedMaterial = true;
	public bool doLoopAnimation = true;
	private int indexOfTexture;
	private float idealFrameRate = 0.0416666679f;
	private float delayBetweenAnimation;
	public float AnimationSpeed = 5f;
	public float delayBetweenLoop;
	public bool startOnAwake =false;
	internal bool IsAnim = false;

	// Sprites displayed since the current playback started. Monotonic — unlike indexOfTexture it
	// never wraps on loop, so it is safe to wait on. Waiters must never compare rendererDelegate.sprite
	// directly: a throttled browser tab holds each sprite for a single frame, so an identity poll can
	// miss its target frame outright and hang forever.
	internal int FramesElapsed { get; private set; }
	// Bumped every time a playback actually starts, so a waiter can detect being restarted under it.
	internal int PlaybackId { get; private set; }

	internal bool HasReachedFrame(int index)
	{
		return FramesElapsed > index;
	}

	// Waits until `anim` has displayed frame `frameIndex` of its CURRENT playback, or until
	// `timeoutSeconds` of wall-clock time has passed. Realtime rather than scaled, because a
	// backgrounded WebGL tab throttles the loop and clamps deltaTime, so scaled time crawls.
	internal static IEnumerator WaitForFrame(ImageAnimation anim, int frameIndex, float timeoutSeconds)
	{
		if (anim == null) yield break;
		int playback = anim.PlaybackId;
		float deadline = Time.realtimeSinceStartup + timeoutSeconds;
		while (!anim.HasReachedFrame(frameIndex))
		{
			if (anim.PlaybackId != playback) yield break;                          // restarted under us
			if (anim.currentAnimationState == ImageState.NONE) yield break;        // stopped elsewhere
			if (Time.realtimeSinceStartup >= deadline) yield break;                // stalled - carry on
			yield return null;
		}
	}

	private void OnValidate() {
		rendererDelegate = GetComponent<Image>();

		if (rendererDelegate == null)
		{
			Debug.LogError("No Image component found on this GameObject. Please add one.");
		}
	}

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		if(startOnAwake){
			StartAnimation();
		}
    }

	private void OnDisable()
	{
		StopAnimation();
	}

	private void AnimationProcess()
	{
		SetTextureOfIndex();
		FramesElapsed++;
		indexOfTexture++;
		if (indexOfTexture == textureArray.Count)
		{
			indexOfTexture = 0;
			if (doLoopAnimation)
			{
				Invoke("AnimationProcess", delayBetweenAnimation + delayBetweenLoop);
			}
		}
		else
		{
			Invoke("AnimationProcess", delayBetweenAnimation);
		}
	}

	public void StartAnimation()
	{
		indexOfTexture = 0;
		if (currentAnimationState == ImageState.NONE)
		{
			RevertToInitialState();
			FramesElapsed = 0;
			PlaybackId++;
			delayBetweenAnimation = idealFrameRate * (float)textureArray.Count / AnimationSpeed;
			currentAnimationState = ImageState.PLAYING;
			Invoke("AnimationProcess", delayBetweenAnimation);
		}
	}

	public void PauseAnimation()
	{
		if (currentAnimationState == ImageState.PLAYING)
		{
			CancelInvoke("AnimationProcess");
			currentAnimationState = ImageState.PAUSED;
		}
	}

	public void ResumeAnimation()
	{
		if (currentAnimationState == ImageState.PAUSED && !IsInvoking("AnimationProcess"))
		{
			Invoke("AnimationProcess", delayBetweenAnimation);
			currentAnimationState = ImageState.PLAYING;
		}
	}

	public void StopAnimation()
	{
		if (currentAnimationState != 0)
		{
			// Several callers clear textureArray around this call, so don't assume index 0 exists.
			if (textureArray != null && textureArray.Count > 0)
			{
				rendererDelegate.sprite = textureArray[0];
			}
			CancelInvoke("AnimationProcess");
			currentAnimationState = ImageState.NONE;
		}
	}

	public void RevertToInitialState()
	{
		indexOfTexture = 0;
		SetTextureOfIndex();
	}

	private void SetTextureOfIndex()
	{
		if (useSharedMaterial)
		{
			rendererDelegate.sprite = textureArray[indexOfTexture];
		}
		else
		{
			rendererDelegate.sprite = textureArray[indexOfTexture];
		}
	}
}
