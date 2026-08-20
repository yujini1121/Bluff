using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] public ItemSystem itemSystem;
    [SerializeField] public ItemData itemData;

    private void OnMouseDown()
    {
        Use();
    }

    public void Use()
    {
        if (itemSystem.UseItem(gameObject))
        {
            Destroy(gameObject);
        }
    }
}