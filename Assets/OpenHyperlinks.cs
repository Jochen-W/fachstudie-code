using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;


[RequireComponent(typeof(TextMeshProUGUI))]
public class OpenHyperlinks : MonoBehaviour, IPointerClickHandler
{
    private TMP_Text textWithLinks;

    private void Awake()
    {
        textWithLinks = GetComponent<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textWithLinks, Input.mousePosition, null);
        if (linkIndex == -1)
        {
            return;
        }
        TMP_LinkInfo linkInfo = textWithLinks.textInfo.linkInfo[linkIndex];
        // open the link id as a url, which is the metadata we added in the text field
        Application.OpenURL(linkInfo.GetLinkID());
    }
}

