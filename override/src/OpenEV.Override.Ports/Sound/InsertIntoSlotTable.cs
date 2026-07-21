namespace OpenEV.Override.Ports.Sound;

// Port of FUN_10076240 (EV Override-11.c lines 49769-49796; semantic name
// InsertIntoSlotTable).
// Stores the 7 params into the first free entry of the 32-entry pending-slot
// table (free test: Words[2] == 0), in the original writer order. Returns the
// slot index, -1 when the table is full.
// The sole real-game caller (the CFM `.start` SoundManager-version
// registration) is deliberately not wired in this port — see the rationale
// in Boot/ProgramEntry.cs step 4.
public static class InsertIntoSlotTable
{
    public static int Run(int word2, int word3, int word4, int word5, int word0, int word1, int word6)
    {
        for (int slotIndex = 0; slotIndex < SoundProcs.PendingSlotTable.Length; slotIndex++)
        {
            int[] words = SoundProcs.PendingSlotTable[slotIndex].Words;
            if (words[2] == 0)
            {
                words[2] = word2;
                words[3] = word3;
                words[4] = word4;
                words[5] = word5;
                words[0] = word0;
                words[1] = word1;
                words[6] = word6;
                return slotIndex;
            }
        }
        return -1;
    }
}
