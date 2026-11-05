using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene helpers shared by the Level Waves PlayMode tests. GetSceneByPath returns the first loaded copy, so if a
/// failed test ever leaves the test scene loaded, later tests would read that stale copy; these helpers avoid that.
/// </summary>
public static class LevelWavesTestScenes
{
    /// <summary>The most recently loaded copy of the scene at 'path'.</summary>
    public static Scene Newest(string path)
    {
        Scene found = default;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.path == path && s.isLoaded)
            {
                found = s;
            }
        }
        return found;
    }

    /// <summary>Unloads every loaded copy of the scene at 'path'.</summary>
    public static IEnumerator UnloadAll(string path)
    {
        var copies = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.path == path && s.isLoaded)
            {
                copies.Add(s);
            }
        }
        foreach (Scene s in copies)
        {
            yield return SceneManager.UnloadSceneAsync(s);
        }
    }
}
