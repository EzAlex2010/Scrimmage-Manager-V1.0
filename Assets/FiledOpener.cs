using UnityEngine;
using System.Diagnostics;

public class FileOpener : MonoBehaviour
{
    public void OpenPersistentDataFolder()
    {
        string folderPath = Application.persistentDataPath;

        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            Process.Start("explorer.exe", folderPath.Replace("/", "\\"));
        #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            Process.Start("open", folderPath);
        #elif UNITY_STANDALONE_LINUX
            Process.Start("xdg-open", folderPath);
        #else
            UnityEngine.Debug.LogWarning("Opening folders is not supported on this platform: " + Application.platform);
        #endif
    }
}
