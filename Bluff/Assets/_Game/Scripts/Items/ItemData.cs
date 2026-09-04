using UnityEngine;

public enum ItemType
{
    test,
    refreshCard,
    prizmChip,
    chipPocket,
    defy
}

public enum CanUseType
{
    SetupPhase,
    BettingPhase,
    ResultPhase
}

// 에셋 생성 메뉴 경로 및 기본 파일 이름 설정
[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Item Info")]
    public string itemName;
    public ItemType itemType;
    public CanUseType canUseType;

    [TextArea] public string description;
}