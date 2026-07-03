using UnityEngine;

public class audio_manager : MonoBehaviour
{
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource SFXSource;

    public AudioClip MainMenuTheme;
    public AudioClip Click_On;
    public AudioClip Click_off;
    
    private void Start()
    {
        musicSource.clip = MainMenuTheme;
        musicSource.Play();
    }

}
