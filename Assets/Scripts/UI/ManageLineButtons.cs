using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ManageLineButtons : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  [SerializeField] private SlotBehaviour slotManager;
  [SerializeField] private TMP_Text num_text;

  void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
  {
    Debug.Log($"Hover entered line button {num_text.text}", this);
    slotManager.GenerateStaticLine(num_text);
  }

  void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
  {
    Debug.Log($"Hover exited line button {num_text.text}", this);
    slotManager.DestroyStaticLine();
  }
}
