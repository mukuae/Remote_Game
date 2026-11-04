using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class VolumeSettings : MonoBehaviour
{
    private const string MusicKey = "musicVolume";
    private const string SfxKey = "SFXVolume";
    private const string MasterKey = "masterVolume";
    private const float MinVolume = 0.0001f; // Log10(0) would be -infinity

    [SerializeField] private AudioMixer myMixer;
    [SerializeField] private Slider musicSlider;
    [FormerlySerializedAs("SFXSlider")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider masterSlider;

    private void Start()
    {
        // Each value falls back to the slider's current value if it was never saved.
        musicSlider.value = PlayerPrefs.GetFloat(MusicKey, musicSlider.value);
        sfxSlider.value = PlayerPrefs.GetFloat(SfxKey, sfxSlider.value);
        masterSlider.value = PlayerPrefs.GetFloat(MasterKey, masterSlider.value);

        SetMusicVolume();
        SetSFXVolume();
        SetMasterVolume();
    }

    public void SetMusicVolume()
    {
        ApplyVolume("music", MusicKey, musicSlider.value);
    }

    public void SetSFXVolume()
    {
        ApplyVolume("SFX", SfxKey, sfxSlider.value);
    }

    public void SetMasterVolume()
    {
        ApplyVolume("master", MasterKey, masterSlider.value);
    }

    private void ApplyVolume(string mixerParameter, string prefsKey, float volume)
    {
        myMixer.SetFloat(mixerParameter, Mathf.Log10(Mathf.Max(volume, MinVolume)) * 20f);
        PlayerPrefs.SetFloat(prefsKey, volume);
    }
}
