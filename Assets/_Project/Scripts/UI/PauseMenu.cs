using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    // Read by gameplay scripts so they ignore input while the game is paused.
    public static bool GameIsPaused { get; private set; }

    [SerializeField] private int mainMenuSceneIndex = 0;
    public GameObject pauseMenuUI;

    private void Awake()
    {
        // A level always starts unpaused, even if the previous one was left through the pause menu.
        SetPaused(false);
    }

    private void OnDestroy()
    {
        GameIsPaused = false;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        if (GameIsPaused)
            Resume();
        else
            Pause();
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void Pause()
    {
        SetPaused(true);
    }

    public void LoadMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene(mainMenuSceneIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void SetPaused(bool paused)
    {
        GameIsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(paused);
    }
}
