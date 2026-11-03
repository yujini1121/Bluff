using System;
using UnityEngine;

[Serializable]
public sealed class IntroSlotChips
{
    [SerializeField, Min(0)] private int startingChips = 10;
    [SerializeField, Min(1)] private int spinCost = 1;
    [SerializeField, Min(0)] private int winReward = 3;
    [SerializeField, Min(0)] private int jackpotReward = 10;
    [SerializeField, Min(1)] private int refillAmount = 10;
    private bool initialized;
    private int balance, ticket, paidCost, pendingWin, pendingJackpot;
    public int Balance { get { Initialize(); return balance; } }
    public int Cost => Mathf.Max(1, spinCost);
    public int RefillGrant => Mathf.Max(Cost, refillAmount);
    public bool HasPendingSpin => ticket != 0;
    public bool CanSpin => !HasPendingSpin && Balance >= Cost;
    public bool CanRefill => !HasPendingSpin && Balance < Cost;

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        balance = Mathf.Max(0, startingChips);
    }

    public bool DebitAcceptedSpin(int spinTicket)
    {
        if (spinTicket == 0 || !CanSpin) return false;
        ticket = spinTicket;
        paidCost = Cost;
        pendingWin = Mathf.Max(0, winReward);
        pendingJackpot = Mathf.Max(0, jackpotReward);
        balance -= paidCost;
        return true;
    }

    public bool Settle(int spinTicket, SlotSpinResult result, out int reward, out int net)
    {
        reward = net = 0;
        if (!HasPendingSpin || ticket != spinTicket || result == null) return false;
        reward = result.Outcome == SlotOutcome.Jackpot ? pendingJackpot :
            result.Outcome == SlotOutcome.Win ? pendingWin : 0;
        net = reward - paidCost;
        balance = (int)Math.Min(int.MaxValue, (long)balance + reward);
        ticket = 0;
        return true;
    }

    public bool Refund(int spinTicket)
    {
        if (!HasPendingSpin || ticket != spinTicket) return false;
        balance += paidCost;
        ticket = 0;
        return true;
    }

    public bool Refill()
    {
        if (!CanRefill) return false;
        balance = (int)Math.Min(int.MaxValue, (long)balance + RefillGrant);
        return true;
    }
}
