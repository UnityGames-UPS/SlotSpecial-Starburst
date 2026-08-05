mergeInto(LibraryManager.library, {
    SendPostMessage: function(messagePtr) {
      var message = UTF8ToString(messagePtr);
      console.log('SendReactPostMessage, message sent: ' + message);
      if(window.parent.dispatchReactUnityEvent != null){
        window.parent.dispatchReactUnityEvent(message);
      }
    },

    RegisterVisibilityChangeListener: function(gameObjectNamePtr) {
      var gameObjectName = UTF8ToString(gameObjectNamePtr);

      // Instant JS-side mute. Kills audio before Unity's throttled loop can react.
      function setUnityAudioSuspended(suspended) {
          try {
              var wa = (typeof WEBAudio !== 'undefined') ? WEBAudio
                     : (typeof Module !== 'undefined' && Module.WEBAudio) ? Module.WEBAudio
                     : null;
              if (!wa || !wa.audioContext) return;
              if (suspended) {
                  if (wa.audioContext.state === 'running') wa.audioContext.suspend();
              } else {
                  if (wa.audioContext.state === 'suspended') wa.audioContext.resume();
              }
          } catch (err) { console.warn('[JS] Unity audio suspend/resume failed:', err); }
      }

      function sendFocusToUnity(focused) {
          setUnityAudioSuspended(!focused);
          try {
              var value = focused ? '1' : '0';
              if (typeof SendMessage === 'function') {
                  SendMessage(gameObjectName, 'OnFocusChanged', value);
              } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
                  unityInstance.SendMessage(gameObjectName, 'OnFocusChanged', value);
              }
          } catch (err) {
              console.error('[JS] Error sending focus message to Unity:', err);
          }
      }

      window._unityVisibilityCallback = function() {
          var hidden = document.hidden || document.webkitHidden;
          sendFocusToUnity(!hidden);
      };
      window._unityWindowBlurCallback  = function() { sendFocusToUnity(false); };
      window._unityWindowFocusCallback = function() { sendFocusToUnity(true); };

      // Remove before re-adding to avoid duplicates
      document.removeEventListener('visibilitychange',       window._unityVisibilityCallback);
      document.removeEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
      window.removeEventListener('blur',  window._unityWindowBlurCallback);
      window.removeEventListener('focus', window._unityWindowFocusCallback);

      document.addEventListener('visibilitychange',       window._unityVisibilityCallback);
      document.addEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
      window.addEventListener('blur',  window._unityWindowBlurCallback);
      window.addEventListener('focus', window._unityWindowFocusCallback);
    }
});
