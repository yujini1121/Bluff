using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class RoundProgressView : MonoBehaviour
{
    [Serializable]
    private struct Marker
    {
        public Image border;
        public Image fill;
    }

    [SerializeField] private Marker[] markers = Array.Empty<Marker>();
    [SerializeField] private Color futureFillColor;
    [SerializeField] private Color currentFillColor;
    [SerializeField] private Color pastFillColor;
    [SerializeField] private Color futureBorderColor;
    [FormerlySerializedAs("futureItemColor")]
    [SerializeField] private Color futureItemBorderColor;
    [FormerlySerializedAs("itemBorderColor")]
    [SerializeField] private Color currentItemColor;
    [SerializeField] private Color pastItemColor;

    public void Refresh(int currentRoundInCycle, Predicate<int> isItemRewardRound)
    {
        for (int index = 0; index < markers.Length; index++)
        {
            int round = index + 1;
            bool isFuture = round > currentRoundInCycle;
            bool isCurrent = round == currentRoundInCycle;
            bool hasItemReward = isItemRewardRound?.Invoke(round) == true;
            Marker marker = markers[index];
            Color fillColor = isFuture ? futureFillColor : hasItemReward
                ? (isCurrent ? currentItemColor : pastItemColor)
                : (isCurrent ? currentFillColor : pastFillColor);

            marker.fill.color = fillColor;
            marker.border.color = isFuture
                ? (hasItemReward ? futureItemBorderColor : futureBorderColor)
                : fillColor;
        }
    }
}
