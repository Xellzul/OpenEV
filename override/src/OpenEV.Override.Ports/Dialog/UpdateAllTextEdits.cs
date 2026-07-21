using OpenEV.Override.Ports.Resource;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_100739cc (EV Override-11.c lines 47874-47892): TEUpdate every
// styled-TextEdit in ResourceGlobals.StyledTeList in its owning port. Driven by
// the shareware-nag dialog's item-5 userItem proc (UpdateAllTextEditsTrampoline).
//
// NO-OP in practice: StyledTeList is always empty (see its field comment in
// ResourceGlobals.cs); real styled text goes through MacToolbox.AddDialogStyledText.
public static class UpdateAllTextEdits
{
    public static void Run()
    {
        int[] savedPort = new int[1];
        MacToolbox.GetPort(savedPort);
        foreach (int teHandle in ResourceGlobals.StyledTeList)
        {
            short[] destRect = MacToolbox.TEGetDestRect(teHandle);
            MacToolbox.SetPort(MacToolbox.TEGetInPort(teHandle));
            MacToolbox.TEUpdate(destRect, teHandle);
        }
        MacToolbox.SetPort(savedPort[0]);
    }
}
