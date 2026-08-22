using UnityEditor;

[InitializeOnLoad]
public static class GamePlayModeQuitHandler
{
    static GamePlayModeQuitHandler()
    {
        GameHudController.QuitRequested -= StopEditorPlayMode;
        GameHudController.QuitRequested += StopEditorPlayMode;
    }

    private static void StopEditorPlayMode()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
    }
}
