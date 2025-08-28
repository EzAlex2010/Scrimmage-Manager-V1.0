using UnityEngine;
using UnityEngine.UI;

public class LeaderboardHeaderSync : MonoBehaviour
{
    public ScrollRect scrollRect;      // reference to your Scroll View
    public RectTransform headerContent; // the inner part of the header (the row with texts)

    void Update()
    {
        // Copy horizontal scroll offset into header
        Vector2 anchoredPos = headerContent.anchoredPosition;
        anchoredPos.x = scrollRect.content.anchoredPosition.x;
        headerContent.anchoredPosition = anchoredPos;
    }
}
