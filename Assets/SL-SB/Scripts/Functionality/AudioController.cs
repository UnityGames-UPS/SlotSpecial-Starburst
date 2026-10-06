using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    [SerializeField] private AudioSource bg_adudio;
    [SerializeField] internal AudioSource audioPlayer_wl;
    [SerializeField] internal AudioSource audioPlayer_button;
    [SerializeField] internal AudioSource audioSpin_button;
    [SerializeField] private AudioClip[] clips;

    private readonly List<AudioSource> allSources = new();
    private readonly Dictionary<AudioSource, bool> preFocusMuteState = new();
    private bool isForceMuted = false;

    private void Awake()
    {
        allSources.Add(bg_adudio);
        allSources.Add(audioPlayer_wl);
        allSources.Add(audioPlayer_button);
        allSources.Add(audioSpin_button);
    }

    private void Start()
    {
        if (bg_adudio) bg_adudio.Play();
        audioPlayer_button.clip = clips[clips.Length-1];
        audioSpin_button.clip = clips[clips.Length-2];
    }

    // Focus-driven mute. Called from BOTH the JS OnFocusChanged path (UIManager) and the
    // native OnApplicationFocus path (SlotBehaviour). Guarded so a duplicate call for the
    // same direction cannot clobber the captured "restore to" state.
    internal void SetMuteAll(bool forceMute)
    {
        if (forceMute == isForceMuted) return;
        isForceMuted = forceMute;

        foreach (AudioSource source in allSources)
        {
            if (source == null) continue;
            if (forceMute)
            {
                preFocusMuteState[source] = source.mute;
                source.mute = true;
            }
            else
            {
                source.mute = preFocusMuteState.TryGetValue(source, out bool prevMuted) ? prevMuted : source.mute;
            }
        }
    }

    // internal void SwitchBGSound(bool isbonus)
    // {
    //     if(isbonus)
    //     {
    //         if (bg_audioBonus) bg_audioBonus.enabled = true;
    //         if (bg_adudio) bg_adudio.enabled = false;
    //     }
    //     else
    //     {
    //         if (bg_audioBonus) bg_audioBonus.enabled = false;
    //         if (bg_adudio) bg_adudio.enabled = true;
    //     }
    // }

    internal void PlayWLAudio(string type)
    {
        audioPlayer_wl.loop = false;
        int index = 0;
        switch (type)
        {
            case "bigwin":
                index = 0;
                break;
            case "win":
                index = 1;
                break;
            case "spinStop":
                index = 2;
                break;
            case "megaWin":
                index = 3;
                break;
            case "Star":
                index = 4;
                break;
        }
        StopWLAaudio();
        audioPlayer_wl.clip = clips[index];
        audioPlayer_wl.Play();
    }

    internal void PlayButtonAudio()
    {
        audioPlayer_button.Play();
    }

    internal void PlaySpinButtonAudio()
    {
        audioSpin_button.Play();
    }

    internal void StopWLAaudio()
    {
        audioPlayer_wl.Stop();
        audioPlayer_wl.loop = false;
    }

    // User-toggle-driven (sound/music button). An explicit interaction proves the game really
    // has focus, so it releases any stale forced-mute first — otherwise a stray unpaired blur
    // signal would leave the button visibly doing nothing.
    internal void ToggleMute(bool toggle, string type)
    {
        SetMuteAll(false);

        switch (type)
        {
            case "music":
                bg_adudio.mute = toggle;
                // bg_audioBonus.mute = toggle;
                break;
            case "sound":
                audioPlayer_button.mute = toggle;
                audioPlayer_wl.mute = toggle;
                audioSpin_button.mute = toggle;
                // audioPlayer_Bonus.mute = toggle;
                break;
        }
    }

}
