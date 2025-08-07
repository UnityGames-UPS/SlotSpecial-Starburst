using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class OrientationChange : MonoBehaviour
{
  [SerializeField] private RectTransform UIWrapper1;
  [SerializeField] private RectTransform UIWrapper2;
  [SerializeField] private CanvasScaler CanvasScaler1;
  [SerializeField] private CanvasScaler CanvasScaler2;
  [SerializeField] private float MatchWidth = 0f;
  [SerializeField] private float MatchHeight = 1f;
  [SerializeField] private float PortraitMatchWandH = 0.5f;
  [SerializeField] private float transitionDuration = 0.2f;
  [SerializeField] private float waitForRotation = 0.2f;

  private Vector2 ReferenceAspect;
  private Tween matchTween1;
  private Tween matchTween2;
  private Tween rotationTween1;
  private Tween rotationTween2;
  private Coroutine rotationRoutine;
  private bool isLandscape;
  private void Awake()
  {
    ReferenceAspect = CanvasScaler1.referenceResolution;
  }

  void SwitchDisplay(string dimensions)
  {
    if (rotationRoutine != null) StopCoroutine(rotationRoutine);
    rotationRoutine = StartCoroutine(RotationCoroutine(dimensions));
  }

  IEnumerator RotationCoroutine(string dimensions)
  {
    yield return new WaitForSecondsRealtime(waitForRotation);
    string[] parts = dimensions.Split(',');
    if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height) && width > 0 && height > 0)
    {
      Debug.Log($"Unity: Received Dimensions - Width: {width}, Height: {height}");

      isLandscape = width > height;

      Quaternion targetRotation = isLandscape ? Quaternion.identity : Quaternion.Euler(0, 0, -90);
      if (rotationTween1 != null && rotationTween1.IsActive()) rotationTween1.Kill();
      rotationTween1 = UIWrapper1.DOLocalRotateQuaternion(targetRotation, transitionDuration).SetEase(Ease.OutCubic);
      if (rotationTween2 != null && rotationTween2.IsActive()) rotationTween2.Kill();
      rotationTween2 = UIWrapper2.DOLocalRotateQuaternion(targetRotation, transitionDuration).SetEase(Ease.OutCubic);

      float currentAspectRatio = isLandscape ? (float)width / height : (float)height / width;
      float referenceAspectRatio = ReferenceAspect.x / ReferenceAspect.y;

      float targetMatch = isLandscape ? (currentAspectRatio > referenceAspectRatio ? MatchHeight : MatchWidth) : PortraitMatchWandH;

      if (matchTween1 != null && matchTween1.IsActive()) matchTween1.Kill();
      matchTween1 = DOTween.To(() => CanvasScaler1.matchWidthOrHeight, x => CanvasScaler1.matchWidthOrHeight = x, targetMatch, transitionDuration).SetEase(Ease.InOutQuad);
      if (matchTween2 != null && matchTween2.IsActive()) matchTween2.Kill();
      matchTween2 = DOTween.To(() => CanvasScaler2.matchWidthOrHeight, x => CanvasScaler2.matchWidthOrHeight = x, targetMatch, transitionDuration).SetEase(Ease.InOutQuad);

      Debug.Log($"matchWidthOrHeight set to: {targetMatch}");
    }
    else
    {
      Debug.LogWarning("Unity: Invalid format received in SwitchDisplay");
    }
  }


#if UNITY_EDITOR
  private void Update()
  {
    if (Input.GetKeyDown(KeyCode.Space))
    {
      SwitchDisplay(Screen.width + "," + Screen.height);  
    }
  }
#endif
}
