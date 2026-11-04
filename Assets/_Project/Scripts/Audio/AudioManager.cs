using UnityEngine;
using UnityEngine.Serialization;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource;
    [FormerlySerializedAs("SFXSource")]
    [SerializeField] private AudioSource sfxSource;

    [FormerlySerializedAs("MainMenuTheme")] public AudioClip mainMenuTheme;
    [FormerlySerializedAs("Click_On")] public AudioClip clickOn;
    [FormerlySerializedAs("Click_off")] public AudioClip clickOff;

    private void Start()
    {
        if (musicSource == null || mainMenuTheme == null) return;

        musicSource.clip = mainMenuTheme;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }
}
