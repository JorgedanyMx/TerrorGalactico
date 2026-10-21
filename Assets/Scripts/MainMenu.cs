using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject creditsPanel;

    private void Awake() => ReturnToMenu();

    public void StartGame()
    {
        if (SceneManager.sceneCountInBuildSettings <= 1)
        {
            Debug.LogError("Agrega la escena de juego con indice 1 a Build Settings.", this);
            return;
        }

        SceneManager.LoadScene(1);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowCredits()
    {
        mainPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    public void ReturnToMenu()
    {
        creditsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }
}
