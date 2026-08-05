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

  private void Start()
  {
    ApplyMatch(Screen.width, Screen.height);
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
      ApplyMatch(width, height);
    }
    else
    {
      Debug.LogWarning("Unity: Invalid format received in SwitchDisplay");
    }
  }

  private void ApplyMatch(int width, int height)
  {
    isLandscape = width > height;

    Quaternion targetRotation = isLandscape ? Quaternion.identity : Quaternion.Euler(0, 0, -90);
    if (rotationTween1 != null && rotationTween1.IsActive()) rotationTween1.Kill();
    rotationTween1 = UIWrapper1.DOLocalRotateQuaternion(targetRotation, transitionDuration).SetEase(Ease.OutCubic);
    if (rotationTween2 != null && rotationTween2.IsActive()) rotationTween2.Kill();
    rotationTween2 = UIWrapper2.DOLocalRotateQuaternion(targetRotation, transitionDuration).SetEase(Ease.OutCubic);

    float refW = ReferenceAspect.x;
    float refH = ReferenceAspect.y;

    float widthScale = (float)width / refW;
    float heightScale = (float)height / refH;

    float targetScale;
    if (isLandscape)
    {
      targetScale = Mathf.Min(widthScale, heightScale);
    }
    else
    {
      float portraitWidthScale = (float)height / refW;
      float portraitHeightScale = (float)width / refH;
      targetScale = Mathf.Min(portraitWidthScale, portraitHeightScale);
    }

    float targetMatch;
    if (Mathf.Abs(heightScale - widthScale) < 0.0001f)
    {
      targetMatch = 0.5f;
    }
    else
    {
      float logRatio = Mathf.Log(heightScale / widthScale);
      targetMatch = Mathf.Log(targetScale / widthScale) / logRatio;
      targetMatch = Mathf.Clamp01(targetMatch);
    }

    if (matchTween1 != null && matchTween1.IsActive()) matchTween1.Kill();
    matchTween1 = DOTween.To(() => CanvasScaler1.matchWidthOrHeight, x => CanvasScaler1.matchWidthOrHeight = x, targetMatch, transitionDuration).SetEase(Ease.InOutQuad);
    if (matchTween2 != null && matchTween2.IsActive()) matchTween2.Kill();
    matchTween2 = DOTween.To(() => CanvasScaler2.matchWidthOrHeight, x => CanvasScaler2.matchWidthOrHeight = x, targetMatch, transitionDuration).SetEase(Ease.InOutQuad);
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
