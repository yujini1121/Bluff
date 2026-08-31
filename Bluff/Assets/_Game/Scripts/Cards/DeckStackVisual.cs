using System.Collections.Generic;
using UnityEngine;

public class DeckStackVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform stackRoot;

    [Header("Deck")]
    [SerializeField] private int maxCardCount = 40;

    [Tooltip("카드 한 장이 쌓일 때마다 추가되는 Local Position")]
    [SerializeField] private Vector3 stackOffset = new Vector3(0f, 0.001f, 0f);

    private readonly List<GameObject> cards = new();
    private int currentCardCount;

    private void Awake()
    {
        BuildDeck();
        SetCardCount(maxCardCount);
    }

    private void BuildDeck()
    {
        if (cards.Count > 0)
            return;

        for (int i = 0; i < maxCardCount; i++)
        {
            GameObject card = Instantiate(cardPrefab, stackRoot);

            card.transform.localPosition = stackOffset * i;
            card.transform.localRotation = Quaternion.identity;

            cards.Add(card);
        }
    }

    public void SetCardCount(int count)
    {
        count = Mathf.Clamp(count, 0, maxCardCount);
        currentCardCount = count;

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].SetActive(i < count);
        }
    }

    public void ConsumeOneVisualCard()
    {
        if (currentCardCount > 0)
        {
            SetCardCount(currentCardCount - 1);
        }
    }

    public void ResetVisual()
    {
        SetCardCount(maxCardCount);
    }
}
