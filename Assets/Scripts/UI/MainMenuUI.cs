using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单界面
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("按钮")]
    public Button startGameButton;
    public Button exitGameButton;

    void Start()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGame);

        if (exitGameButton != null)
            exitGameButton.onClick.AddListener(OnExitGame);
    }

    void OnDestroy()
    {
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnStartGame);
        if (exitGameButton != null)
            exitGameButton.onClick.RemoveListener(OnExitGame);
    }

    private void OnStartGame()
    {
        GameManager.Instance?.StartNewGame();
        GameStateMachine.Instance?.NextState(); // MainMenu -> HeroSelect
    }

    private void OnExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
