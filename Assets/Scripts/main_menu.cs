using UnityEngine;
using UnityEngine.SceneManagement;
public class main_menu : MonoBehaviour
{

    audio_manager audioManager;

    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<audio_manager>();
    }
    public void PlayGame()
    {
        SceneManager.LoadSceneAsync(1);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

        public void Buttonclicksound()
    {
        audioManager.PlaySFX(audioManager.Click_off);
    }
}
