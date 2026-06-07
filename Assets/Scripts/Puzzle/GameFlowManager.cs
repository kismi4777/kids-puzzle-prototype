using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Управляет переходами между уровнями (сценами из Build Settings).
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Гарантирует наличие менеджера (если сцену запустили не с Lvl_1).
    /// </summary>
    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        GameObject host = new GameObject("GameFlowManager");
        host.AddComponent<GameFlowManager>();
    }

    /// <summary>
    /// Загружает следующую сцену по buildIndex. После последней — возврат на первую.
    /// </summary>
    public void LoadNextLevel()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;

        if (nextIndex < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(nextIndex);
        else
            SceneManager.LoadScene(0);
    }
}
