using UnityEngine;
using UnityEngine.SceneManagement;
public class main_menu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadSceneAsync(1);
    }
}
