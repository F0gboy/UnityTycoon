using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "Game";

    public void StartGame()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
