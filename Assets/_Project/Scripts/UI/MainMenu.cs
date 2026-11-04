using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Tooltip("Build index of the scene that 'Play' loads.")]
    [SerializeField] private int firstLevelSceneIndex = 1;

    private AudioManager audioManager;

    private void Awake()
    {
        GameObject audioObject = GameObject.FindGameObjectWithTag("Audio");
        if (audioObject != null)
            audioManager = audioObject.GetComponent<AudioManager>();
        else
            Debug.LogWarning("MainMenu: no GameObject tagged 'Audio' found, button sounds are disabled.", this);
    }

    public void PlayGame()
    {
        SceneManager.LoadSceneAsync(firstLevelSceneIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void PlayButtonClickSound()
    {
        if (audioManager != null)
            audioManager.PlaySFX(audioManager.clickOff);
    }
}
