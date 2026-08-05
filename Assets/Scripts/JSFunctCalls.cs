using System.Runtime.InteropServices;
using UnityEngine;

public class JSFunctCalls : MonoBehaviour
{
  [DllImport("__Internal")] private static extern void SendPostMessage(string message);
  [DllImport("__Internal")] private static extern void RegisterVisibilityChangeListener(string gameObjectName);

  internal void SendCustomMessage(string message)
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    SendPostMessage(message);
#endif
  }

  internal void RegisterVisibilityListener(string gameObjectName)
  {
#if UNITY_WEBGL && !UNITY_EDITOR
    Debug.Log($"[JS] Registering visibility change listener on '{gameObjectName}'");
    RegisterVisibilityChangeListener(gameObjectName);
#else
    Debug.Log("[JS] Visibility listener not registered (editor mode)");
#endif
  }
}
