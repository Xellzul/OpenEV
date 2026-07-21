namespace OpenEV.Override.Ports.Dialog;

// Decompile: EV Override-11.c lines 47641-47646.
// ASM: loc_733F4 in reference/disasm/_code_interstitial.asm (a bare trampoline;
// IDA never gave it its own sub_ entry — DATA-xref-only via the off_825C0 UPP
// cell, same pattern as FUN_1007328c/DefaultDialogFilter next to it — both of
// which live here in Dialog/ too, as this file's other item-proc siblings on
// the same shareware nag dialog, DLOG 900).
public static class UpdateAllTextEditsTrampoline
{
    public static void Run()
    {
        UpdateAllTextEdits.Run();
        return;
    }
}
