using UnityEngine;

public class LogActive : MonoBehaviour
{
    public GameObject logtext;
    public void ToggleLogging(bool value)
    {
        Debug.Log("Logging set to: " + value);
        if (value) logtext.SetActive(true);
        else logtext.SetActive(false);
    }
}
