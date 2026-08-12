using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
public class UIManager : MonoBehaviour
{
  [Header("Popus UI")]
  [SerializeField] private GameObject MainPopup_Object;

  [Header("Win Popup")]
  [SerializeField] private Image Win_Image;
  [SerializeField] private Button SkipWinAnimation;

  [Header("Reconnection Popup")]
  [SerializeField] private GameObject ReconnectionPopup_Object;

  [Header("Disconnection Popup")]
  [SerializeField] private Button CloseDisconnect_Button;
  [SerializeField] private GameObject DisconnectPopup_Object;

  [Header("AnotherDevice Popup")]
  [SerializeField] private GameObject ADPopup_Object;

  [Header("LowBalance Popup")]
  [SerializeField] private Button LBExit_Button;
  [SerializeField] private GameObject LBPopup_Object;

  [Header("Audio Objects")]
  [SerializeField] private GameObject Settings_Object;
  [SerializeField] private Button SettingsQuit_Button;
  [SerializeField] private Button Sound_Button;
  [SerializeField] private Button Music_Button;
  [SerializeField] private RectTransform SoundToggle_RT;
  [SerializeField] private RectTransform MusicToggle_RT;

  [Header("Paytable Objects")]
  [SerializeField] private GameObject PaytableMenuObject;
  [SerializeField] private Button Paytable_Button;
  [SerializeField] private Button PaytableClose_Button;
  [SerializeField] private Button PaytableClose_Button2;
  [SerializeField] private Button PaytableLeft_Button;
  [SerializeField] private Button PaytableRight_Button;
  [SerializeField] private List<GameObject> GameRulesPages = new();
  private int PageIndex;

  [Header("Game Quit Objects")]
  [SerializeField] private Button Quit_Button;
  [SerializeField] private Button QuitYes_Button;
  [SerializeField] private Button QuitNo_Button;
  [SerializeField] private GameObject QuitMenuObject;

  [Header("Menu Objects")]
  [SerializeField] private Button Settings_Button;

  [Header("Paytable Slot Text")]
  [SerializeField] private List<TMP_Text> SymbolsText = new();

  [Header("Misc Objects")]
  [SerializeField] private Image comboAnimationImage;
  [SerializeField] private ImageAnimation bigWinStartAnimation;
  [SerializeField] private Image targetImage; // Assign the Image in the Inspector

  [Header("Managers")]
  [SerializeField] private AudioController audioController;
  [SerializeField] private SlotBehaviour slotManager;
  [SerializeField] private SocketIOManager socketManager;
  [SerializeField] private JSFunctCalls jsFunctCalls;
  [Header("Win Animation Recovery")]
  // A backgrounded browser tab throttles Unity's loop, which can strand a win visual mid-sequence.
  // These drive the self-heal on refocus; exposed so QA can tune them without a script rebuild.
  [SerializeField] private float AnimationWaitTimeout = 3f;
  [SerializeField] private float WinVisualRecoveryGrace = 2.5f;
  [SerializeField] private float WinVisualRecoveryPoll = 1f;

  private bool isMusic = true;
  private bool isSound = true;
  private bool isExit = false;
  internal bool BigWinAnimating;
  private Tween ColorCycleTween;
  internal bool isComboSpritesAnimating;
  private Coroutine BigWinStartRoutine;
  private Coroutine WinAnimationRoutine;
  private Coroutine ComboRoutine;
  private Coroutine WinVisualWatchdog;

  private void Awake()
  {
    if (jsFunctCalls != null)
      jsFunctCalls.RegisterVisibilityListener(gameObject.name);
  }

  // Invoked by the JS visibility listener via SendMessage — MUST stay public.
  public void OnFocusChanged(string value)
  {
    bool focused = value == "1";
    Debug.Log("UNITY FOCUS CHANGED: " + value + " (focused: " + focused + ")");
    if (audioController) audioController.SetMuteAll(!focused);
    if (socketManager) socketManager.HandleFocusChange(focused);
    if (focused)
    {
      if (WinVisualWatchdog != null) StopCoroutine(WinVisualWatchdog);
      WinVisualWatchdog = StartCoroutine(RecoverStuckWinVisuals());
    }
  }

  // A stalled frame rate can strand a win visual mid-sequence. Let anything still in flight finish
  // on its own during the grace period, then confirm with a second sample before tearing down, so
  // a visual that is merely mid-fade is never cut short.
  private IEnumerator RecoverStuckWinVisuals()
  {
    yield return new WaitForSecondsRealtime(WinVisualRecoveryGrace);
    if (IsWinVisualStuck())
    {
      // Second look a beat later. BigWinAnimating already rules out a win still in progress, so
      // this mainly covers a teardown fade that is still running - which will have finished by now,
      // whereas a stranded visual reads identically.
      yield return new WaitForSecondsRealtime(WinVisualRecoveryPoll);
      if (IsWinVisualStuck())
      {
        Debug.LogWarning("[UI] Stuck win visual detected on focus regain - force resetting.");
        ForceResetWinVisuals();
        if (slotManager) slotManager.ForceResetReelVisuals();
      }
    }
    WinVisualWatchdog = null;
  }

  // "Flag down but pixels still up" is by construction invalid: BigWinAnimating is cleared only
  // inside ForceResetWinVisuals, which also clears every visual below.
  private bool IsWinVisualStuck()
  {
    if (BigWinAnimating) return false;
    if (ColorCycleTween != null && ColorCycleTween.IsActive()) return true;
    if (targetImage && targetImage.color.a > 0.01f) return true;
    if (bigWinStartAnimation && bigWinStartAnimation.rendererDelegate
        && bigWinStartAnimation.rendererDelegate.color.a > 0.01f) return true;
    if (Win_Image && Win_Image.rectTransform.localScale.x > 0.01f) return true;
    if (comboAnimationImage && comboAnimationImage.color.a > 0.01f) return true;
    return false;
  }

  private void Start()
  {
    if (SkipWinAnimation) SkipWinAnimation.onClick.RemoveAllListeners();
    // Full reset rather than StopWinAnimation: this is the player's manual escape from a stranded
    // overlay, so it must also clear BigWinAnimating and stop the owning coroutines.
    if (SkipWinAnimation) SkipWinAnimation.onClick.AddListener(ForceResetWinVisuals);

    if (LBExit_Button) LBExit_Button.onClick.RemoveAllListeners();
    if (LBExit_Button) LBExit_Button.onClick.AddListener(delegate { ClosePopup(LBPopup_Object); });

    if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.RemoveAllListeners();
    if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.AddListener(CallOnExitFunction);

    if (Sound_Button) Sound_Button.onClick.RemoveAllListeners();
    if (Sound_Button) Sound_Button.onClick.AddListener(delegate
    {
      Debug.Log("Here");
      if (isSound)
      {
        SoundOnOFF(false);
      }
      else
      {
        SoundOnOFF(true);
      }
    });

    if (Music_Button) Music_Button.onClick.RemoveAllListeners();
    if (Music_Button) Music_Button.onClick.AddListener(delegate
    {

      if (isMusic)
      {
        MusicONOFF(false);
      }
      else
      {
        MusicONOFF(true);
      }
    });

    if (Quit_Button) Quit_Button.onClick.RemoveAllListeners();
    if (Quit_Button) Quit_Button.onClick.AddListener(OpenQuitPanel);

    if (QuitNo_Button) QuitNo_Button.onClick.RemoveAllListeners();
    if (QuitNo_Button) QuitNo_Button.onClick.AddListener(delegate { ClosePopup(QuitMenuObject); });

    if (QuitYes_Button) QuitYes_Button.onClick.RemoveAllListeners();
    if (QuitYes_Button) QuitYes_Button.onClick.AddListener(CallOnExitFunction);

    if (Paytable_Button) Paytable_Button.onClick.RemoveAllListeners();
    if (Paytable_Button) Paytable_Button.onClick.AddListener(OpenPaytablePanel);

    if (PaytableClose_Button) PaytableClose_Button.onClick.RemoveAllListeners();
    if (PaytableClose_Button) PaytableClose_Button.onClick.AddListener(delegate { ClosePopup(PaytableMenuObject); });

    if (PaytableClose_Button2) PaytableClose_Button2.onClick.RemoveAllListeners();
    if (PaytableClose_Button2) PaytableClose_Button2.onClick.AddListener(delegate { ClosePopup(PaytableMenuObject); });

    if (Settings_Button) Settings_Button.onClick.RemoveAllListeners();
    if (Settings_Button) Settings_Button.onClick.AddListener(OpenSettingsPanel);

    if (SettingsQuit_Button) SettingsQuit_Button.onClick.RemoveAllListeners();
    if (SettingsQuit_Button) SettingsQuit_Button.onClick.AddListener(delegate { ClosePopup(Settings_Object); });

    if (PaytableLeft_Button) PaytableLeft_Button.onClick.RemoveAllListeners();
    if (PaytableLeft_Button) PaytableLeft_Button.onClick.AddListener(() => ChangePage(false));

    if (PaytableRight_Button) PaytableRight_Button.onClick.RemoveAllListeners();
    if (PaytableRight_Button) PaytableRight_Button.onClick.AddListener(() => ChangePage(true));
  }

  private void ChangePage(bool IncDec)
  {
    if (audioController) audioController.PlayButtonAudio();

    if (IncDec)
    {
      if (PageIndex < GameRulesPages.Count - 1)
      {
        PageIndex++;
      }
      if (PageIndex == GameRulesPages.Count - 1)
      {
        if (PaytableRight_Button) PaytableRight_Button.interactable = false;
      }
      if (PageIndex > 0)
      {
        if (PaytableLeft_Button) PaytableLeft_Button.interactable = true;
      }
    }
    else
    {
      if (PageIndex > 0)
      {
        PageIndex--;
      }
      if (PageIndex == 0)
      {
        if (PaytableLeft_Button) PaytableLeft_Button.interactable = false;
      }
      if (PageIndex < GameRulesPages.Count - 1)
      {
        if (PaytableRight_Button) PaytableRight_Button.interactable = true;
      }
    }
    foreach (GameObject g in GameRulesPages)
    {
      g.SetActive(false);
    }
    if (GameRulesPages[PageIndex]) GameRulesPages[PageIndex].SetActive(true);
  }

  private void SoundOnOFF(bool state)
  {
    if (state)
    {
      isSound = true;
      audioController.ToggleMute(!state, "sound");
      DOTween.To(() => SoundToggle_RT.anchoredPosition, (val) => SoundToggle_RT.anchoredPosition = val, new Vector2(SoundToggle_RT.anchoredPosition.x + 108, SoundToggle_RT.anchoredPosition.y), 0.1f);
    }
    else
    {
      isSound = false;
      audioController.ToggleMute(!state, "sound");
      DOTween.To(() => SoundToggle_RT.anchoredPosition, (val) => SoundToggle_RT.anchoredPosition = val, new Vector2(SoundToggle_RT.anchoredPosition.x - 108, SoundToggle_RT.anchoredPosition.y), 0.1f);
    }
  }

  private void MusicONOFF(bool state)
  {
    if (state)
    {
      isMusic = true;
      audioController.ToggleMute(!state, "music");
      DOTween.To(() => MusicToggle_RT.anchoredPosition, (val) => MusicToggle_RT.anchoredPosition = val, new Vector2(MusicToggle_RT.anchoredPosition.x + 108, MusicToggle_RT.anchoredPosition.y), 0.1f);
    }
    else
    {
      isMusic = false;
      audioController.ToggleMute(!state, "music");
      DOTween.To(() => MusicToggle_RT.anchoredPosition, (val) => MusicToggle_RT.anchoredPosition = val, new Vector2(MusicToggle_RT.anchoredPosition.x - 108, MusicToggle_RT.anchoredPosition.y), 0.1f);
    }
  }

  private void OpenSettingsPanel()
  {
    if (audioController) audioController.PlayButtonAudio();
    if (MainPopup_Object) MainPopup_Object.SetActive(true);
    if (Settings_Object) Settings_Object.SetActive(true);
  }

  private void OpenQuitPanel()
  {
    if (audioController) audioController.PlayButtonAudio();
    if (MainPopup_Object) MainPopup_Object.SetActive(true);
    if (QuitMenuObject) QuitMenuObject.SetActive(true);
  }

  private void OpenPaytablePanel()
  {
    if (audioController) audioController.PlayButtonAudio();

    if (MainPopup_Object) MainPopup_Object.SetActive(true);

    PageIndex = 0;

    foreach (GameObject g in GameRulesPages)
    {
      g.SetActive(false);
    }

    GameRulesPages[0].SetActive(true);
    if (PaytableLeft_Button) PaytableLeft_Button.interactable = false;
    if (PaytableRight_Button) PaytableRight_Button.interactable = true;

    if (PaytableMenuObject) PaytableMenuObject.SetActive(true);
  }

  internal void LowBalPopup()
  {
    OpenPopup(LBPopup_Object);
  }

  internal void DisconnectionPopup()
  {
    if (!isExit)
    {
      OpenPopup(DisconnectPopup_Object);
    }
  }

  internal void ReconnectionPopup()
  {
    OpenPopup(ReconnectionPopup_Object);
  }

  internal void CheckAndClosePopups()
  {
    if (ReconnectionPopup_Object.activeInHierarchy)
    {
      ClosePopup(ReconnectionPopup_Object);
    }
    if (DisconnectPopup_Object.activeInHierarchy)
    {
      ClosePopup(DisconnectPopup_Object);
    }
  }

  // Single-flight entry points. These own the coroutines on UIManager so the component that owns
  // the visuals can also stop them; previously they ran on SlotBehaviour with the handle discarded.
  internal void PlayBigWinStart()
  {
    if (BigWinStartRoutine != null) StopCoroutine(BigWinStartRoutine);
    BigWinStartRoutine = StartCoroutine(BigWinStartAnim());
  }

  internal void PlayWinAnimation(Sprite winSprite, Sprite[] animationSprites)
  {
    if (WinAnimationRoutine != null) StopCoroutine(WinAnimationRoutine);
    WinAnimationRoutine = StartCoroutine(StartWinAnimation(winSprite, animationSprites));
  }

  internal void PlayComboSprite(Sprite sprite)
  {
    if (ComboRoutine != null) StopCoroutine(ComboRoutine);
    ComboRoutine = StartCoroutine(AnimateSprite(sprite));
  }

  internal IEnumerator StartWinAnimation(Sprite winSprite, Sprite[] animationSprites)
  {
    ImageAnimation winImageAnimation = Win_Image.GetComponent<ImageAnimation>();
    Win_Image.sprite = winSprite;
    winImageAnimation.textureArray.Clear();
    winImageAnimation.textureArray.AddRange(animationSprites);
    winImageAnimation.doLoopAnimation = true;
    // The scene has this animation playing from Awake, so StartAnimation() alone is a no-op and the
    // frame delay would never be recomputed for the sprite set we just swapped in.
    winImageAnimation.StopAnimation();
    winImageAnimation.StartAnimation();
    Win_Image.rectTransform.DOScale(1, 0.2f);
    yield return new WaitForSeconds(4f);
    WinAnimationRoutine = null;
    StopWinAnimation();
  }

  // Graceful end-of-win teardown - fades out over ~0.5s. Reaches the same terminal state as
  // ForceResetWinVisuals (flags cleared, routines stopped), it just doesn't snap to get there.
  // Use this whenever the win ended normally; ForceResetWinVisuals is for recovery and skip.
  internal void StopWinAnimation()
  {
    if (BigWinStartRoutine != null) { StopCoroutine(BigWinStartRoutine); BigWinStartRoutine = null; }
    if (WinAnimationRoutine != null) { StopCoroutine(WinAnimationRoutine); WinAnimationRoutine = null; }

    // Killed immediately rather than from the fade's OnComplete: if the burst animation calls
    // CycleColors() after that callback ran, the new infinite tween would have no killer left.
    ColorCycleTween?.Kill();
    ColorCycleTween = null;

    if (targetImage)
    {
      targetImage.DOKill();
      targetImage.DOFade(0, 0.5f);
    }

    if (bigWinStartAnimation && bigWinStartAnimation.rendererDelegate)
    {
      ImageAnimation burst = bigWinStartAnimation;
      burst.rendererDelegate.DOKill();
      burst.rendererDelegate.DOFade(0, 0.5f).OnComplete(() => { if (burst) burst.StopAnimation(); });
    }

    if (Win_Image)
    {
      ImageAnimation winImageAnimation = Win_Image.GetComponent<ImageAnimation>();
      Win_Image.rectTransform.DOKill();
      Win_Image.rectTransform.DOScale(0, 0.5f).OnComplete(() => { if (winImageAnimation) winImageAnimation.StopAnimation(); });
    }

    BigWinAnimating = false;
  }

  internal IEnumerator BigWinStartAnim()
  {
    if (bigWinStartAnimation == null || bigWinStartAnimation.rendererDelegate == null) yield break;
    if (bigWinStartAnimation.textureArray.Count < 10) yield break;
    int last = bigWinStartAnimation.textureArray.Count - 1;

    // This animation is a one-shot: once it ends it never calls StopAnimation() on itself, so a
    // previous run that was interrupted leaves it latched PLAYING and every StartAnimation() after
    // that is a no-op. Stopping first is what makes a repeat big win work at all.
    bigWinStartAnimation.StopAnimation();
    bigWinStartAnimation.rendererDelegate.DOKill();
    bigWinStartAnimation.rendererDelegate.DOFade(1, 0.2f);
    bigWinStartAnimation.StartAnimation();

    yield return ImageAnimation.WaitForFrame(bigWinStartAnimation, last - 9, AnimationWaitTimeout);
    CycleColors();
    yield return ImageAnimation.WaitForFrame(bigWinStartAnimation, last - 4, AnimationWaitTimeout);
    bigWinStartAnimation.rendererDelegate.DOFade(0, 0.2f);
    yield return ImageAnimation.WaitForFrame(bigWinStartAnimation, last, AnimationWaitTimeout);
    bigWinStartAnimation.StopAnimation();
    BigWinStartRoutine = null;
  }

  // Idempotent teardown for every big/mega/huge win visual, including the combo sprite. Safe to
  // call from any state and any number of times. The visuals ease out rather than snap; the state
  // that other coroutines gate on (BigWinAnimating, isComboSpritesAnimating, the routine handles)
  // is cleared synchronously before this returns.
  internal void ForceResetWinVisuals()
  {
    StopWinAnimation();
    ResetComboVisual();
  }

  // The combo sprite is made visible at the top of AnimateSprite and cleared only at its tail, so
  // anything that kills that coroutine mid-flight used to strand it at full alpha with no other
  // reset site. This is that reset site - it eases out the same way AnimateSprite's own exit does,
  // while clearing the flag synchronously because CheckPayoutLineBackend spins on it.
  internal void ResetComboVisual()
  {
    if (ComboRoutine != null) { StopCoroutine(ComboRoutine); ComboRoutine = null; }
    if (comboAnimationImage)
    {
      comboAnimationImage.DOKill();
      comboAnimationImage.rectTransform.DOKill();
      if (comboAnimationImage.color.a > 0.01f)
      {
        comboAnimationImage.DOFade(0, 0.3f);
        comboAnimationImage.rectTransform.DOScale(Vector3.one * 1.5f, 0.3f);
      }
      else
      {
        comboAnimationImage.rectTransform.localScale = Vector3.zero;
      }
    }
    isComboSpritesAnimating = false;
  }

  private void CycleColors()
  {
    targetImage.DOFade(1, 0.5f);
    // Tween through the hue range
    ColorCycleTween = DOTween.To(() => 0f, x =>
    {
      // Convert the hue to a color and apply it
      Color newColor = Color.HSVToRGB(x, 1f, 1f);
      newColor.a = 0.4f;
      targetImage.color = newColor;
    }, 1f, 5f)
    .SetEase(Ease.Linear) // Smooth transition
    .SetLoops(-1, LoopType.Restart); // Infinite loop
  }

  internal void ADfunction()
  {
    OpenPopup(ADPopup_Object);
  }

  internal void InitialiseUIData(Paylines symbolsText)
  {
    PopulateSymbolsPayout(symbolsText);
  }

  private void PopulateSymbolsPayout(Paylines paylines)
  {
    for (int i = 0; i < SymbolsText.Count; i++)
    {
      string text = null;
      if (paylines.symbols[i].multiplier[0] != 0)
      {
        text += paylines.symbols[i].multiplier[0].ToString() + "x";
      }
      if (paylines.symbols[i].multiplier[1] != 0)
      {
        text += "\n" + paylines.symbols[i].multiplier[1].ToString() + "x";
      }
      if (paylines.symbols[i].multiplier[2] != 0)
      {
        text += "\n" + paylines.symbols[i].multiplier[2].ToString() + "x";
      }
      if (SymbolsText[i]) SymbolsText[i].text = text;
    }
  }
  internal IEnumerator AnimateSprite(Sprite sprite)
  {
    isComboSpritesAnimating = true;
    comboAnimationImage.sprite = sprite;

    float randomZRotation = UnityEngine.Random.Range(-15f, 15f);
    comboAnimationImage.rectTransform.localEulerAngles = new Vector3(0, 0, randomZRotation);

    // Scale up quickly.
    comboAnimationImage.rectTransform.localScale = Vector3.zero;
    comboAnimationImage.color = new(1, 1, 1, 1);
    comboAnimationImage.rectTransform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);

    // Wait for half a second.
    yield return new WaitForSeconds(0.5f);

    // Fade out the image.
    comboAnimationImage.DOFade(0, 0.3f).WaitForCompletion();
    yield return comboAnimationImage.rectTransform.DOScale(Vector3.one * 1.5f, 0.3f).WaitForCompletion();
    ComboRoutine = null;
    ResetComboVisual();
  }

  private void CallOnExitFunction()
  {
    if (!isExit)
    {
      isExit = true;
      audioController.PlayButtonAudio();
      slotManager.CallCloseSocket();
    }
  }

  private void OpenPopup(GameObject Popup)
  {
    if (audioController) audioController.PlayButtonAudio();

    if (Popup) Popup.SetActive(true);
    if (MainPopup_Object) MainPopup_Object.SetActive(true);
  }

  private void ClosePopup(GameObject Popup)
  {
    if (audioController) audioController.PlayButtonAudio();
    if (Popup) Popup.SetActive(false);
    if (!DisconnectPopup_Object.activeSelf)
    {
      if (MainPopup_Object) MainPopup_Object.SetActive(false);
    }
  }

  private void UrlButtons(string url)
  {
    Application.OpenURL(url);
  }
}
