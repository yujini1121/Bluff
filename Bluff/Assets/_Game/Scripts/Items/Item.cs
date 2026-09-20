using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] public ItemSystem itemSystem;
    [SerializeField] public ItemData itemData;

    private void OnMouseDown()
    {
        Use();
    }

    public string GetDescription()
    {
        return itemData?.description ?? string.Empty;
    }

    public void Use()
    {
        itemSystem.RequestPlayerItemUse(gameObject);
    }
}
