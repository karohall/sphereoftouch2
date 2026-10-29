using UnityEngine;
using UnityEditor;

public class ExitPlayMode
{
    [MenuItem("Fullscreen/Stop Play %q")]
    static void StopPlay()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
    }
}