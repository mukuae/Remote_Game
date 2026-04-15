using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eveld.DynamicCamera.Demo
{
    /// <summary>
    /// Simple scene switch class for the dynamic camera demo scenes
    /// </summary>
    public class SceneSwitcher : MonoBehaviour
    {
        private void OnGUI()
        {
            int width = 300;
            int heigth = 60;

            Rect rect = new Rect(Screen.width / 2 - width / 2, 1, width, heigth);
            GUILayout.BeginArea(rect);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Scene Select:");
            if (GUILayout.Button("Speed Scene", GUILayout.Width(100)))
            {
                SceneManager.LoadScene("SpeedScene");
            }

            if (GUILayout.Button("Slow Scene", GUILayout.Width(100)))
            {
                SceneManager.LoadScene("SlowScene");
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

        }
    }
}