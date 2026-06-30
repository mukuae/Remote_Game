using UnityEngine;
using UnityEngine.SceneManagement;
public class PauseMenu : MonoBehaviour
{
    int MainMenu = 0;
    public static bool GameIsPaused = false;
    public GameObject pauseMenuUI;
  
    

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
         if (GameIsPaused) {
            Resume();
            Debug.Log("ESC pressed");
            Debug.Log("Paused: "+ GameIsPaused);
            

         }
         else {
            Pause();
            Debug.Log("ESC pressed");
            Debug.Log("Paused: "+ GameIsPaused);
            
         }
    }
    }
    public void Resume ()
{
    pauseMenuUI.SetActive(false);
    Time.timeScale = 1f;
    GameIsPaused = false;
    Debug.Log("Resume");
    Debug.Log(System.Environment.StackTrace); 
}
    public void Pause()
{
    pauseMenuUI.SetActive(true);
    Time.timeScale = 0f;
    GameIsPaused = true;
}
    public void LoadMenu()
    {
        SceneManager.LoadScene(MainMenu);
        Time.timeScale = 1f;
    }
    public void QuitGame()
    {
        Debug.Log("Exiting to Desktop...");
        Application.Quit();
    }

}

