using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

public class ManageLineButtons : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler, IPointerDownHandler
{
  [SerializeField] private SlotBehaviour slotManager;
  [SerializeField] private TMP_Text num_text;
  [SerializeField] private Sprite HighlightedSprite;
  [SerializeField] private Sprite NormalSprite;
  private Image buttonImage;

  void Awake()
  {
    buttonImage = this.gameObject.GetComponent<Image>();
  }

  public void OnPointerEnter(PointerEventData eventData)
  {
    slotManager.GenerateStaticLine(num_text);
    buttonImage.sprite = HighlightedSprite;
  }

  public void OnPointerExit(PointerEventData eventData)
  {
    slotManager.DestroyStaticLine();
    buttonImage.sprite = NormalSprite;
  }

  public void OnPointerDown(PointerEventData eventData)
  {
    if (Application.platform == RuntimePlatform.WebGLPlayer && Application.isMobilePlatform)
    {
      // this.gameObject.GetComponent<Button>().Select();
      //Debug.Log("run on pointer down");
      slotManager.GenerateStaticLine(num_text);
    }
  }

  public void OnPointerUp(PointerEventData eventData)
  {
    if (Application.platform == RuntimePlatform.WebGLPlayer && Application.isMobilePlatform)
    {
      //Debug.Log("run on pointer up");
      slotManager.DestroyStaticLine();
      DOVirtual.DelayedCall(0.1f, () =>
      {
        this.gameObject.GetComponent<Button>().spriteState = default;
        EventSystem.current.SetSelectedGameObject(null);
      });
    }
  }

  
}
