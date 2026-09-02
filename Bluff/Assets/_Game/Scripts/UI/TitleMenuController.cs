using UnityEngine;

public class TitleMenuController : MonoBehaviour
{
    [SerializeField]
    private TitleMenuButtonFX[] buttons;

    [SerializeField]
    private float entranceInterval = 0.07f;

    private TitleMenuButtonFX hoveredButton;

    private void Start()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Initialize(this);
            buttons[i].PlayEntrance(i * entranceInterval);
        }
    }

    public void SetHovered(TitleMenuButtonFX hovered)
    {
        hoveredButton = hovered;

        foreach (TitleMenuButtonFX button in buttons)
        {
            button.SetDimmed(button != hovered);
        }
    }

    public void ClearHovered(TitleMenuButtonFX hovered)
    {
        if (hoveredButton != hovered)
        {
            return;
        }

        hoveredButton = null;

        foreach (TitleMenuButtonFX button in buttons)
        {
            button.SetDimmed(false);
        }
    }
}