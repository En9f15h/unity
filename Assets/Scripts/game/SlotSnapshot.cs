using System;

[Serializable]
public struct SlotSnapshot
{
    public int logicalSlotIndex;
    public int actionId;
    public bool isEmpty;
    public bool isLockedContinuation;
    public int sourceLogicalSlotIndex;
    public int ownerActorNumber;
    public int revision;
    public int planningRound;

    public object[] ToPayload(int sightOwnerActorNumber)
    {
        return new object[]
        {
            sightOwnerActorNumber,
            ownerActorNumber,
            logicalSlotIndex,
            actionId,
            isEmpty,
            isLockedContinuation,
            sourceLogicalSlotIndex,
            planningRound,
            revision
        };
    }

    public static bool TryFromPayload(object payload, out int sightOwnerActorNumber, out SlotSnapshot snapshot)
    {
        sightOwnerActorNumber = -1;
        snapshot = default;

        if (!(payload is object[] data) || data.Length < 9)
            return false;

        try
        {
            sightOwnerActorNumber = Convert.ToInt32(data[0]);

            snapshot = new SlotSnapshot
            {
                ownerActorNumber = Convert.ToInt32(data[1]),
                logicalSlotIndex = Convert.ToInt32(data[2]),
                actionId = Convert.ToInt32(data[3]),
                isEmpty = Convert.ToBoolean(data[4]),
                isLockedContinuation = Convert.ToBoolean(data[5]),
                sourceLogicalSlotIndex = Convert.ToInt32(data[6]),
                planningRound = Convert.ToInt32(data[7]),
                revision = Convert.ToInt32(data[8])
            };

            return true;
        }
        catch (Exception)
        {
            snapshot = default;
            sightOwnerActorNumber = -1;
            return false;
        }
    }
}
