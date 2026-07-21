using OpenEV.Override.Ports.Resource;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Dialog;

// Port of FUN_10073a58 (EV Override-11.c lines 47896-47921): dispose every
// styled-TextEdit in ResourceGlobals.StyledTeList — invalidate its destRect in
// its owning port and TEDispose it — then clear the list.
//
// NO-OP in practice: StyledTeList is always empty (see its field comment in
// ResourceGlobals.cs); real styled text goes through MacToolbox.AddDialogStyledText.
public static class DisposeAllTextEditList
{
    public static void Run()
    {
        int[] savedPort = new int[1];
        MacToolbox.GetPort(savedPort);

        foreach (int teHandle in ResourceGlobals.StyledTeList)
        {
            short[] destRect = MacToolbox.TEGetDestRect(teHandle);
            MacToolbox.SetPort(MacToolbox.TEGetInPort(teHandle));
            MacToolbox.InvalRect(destRect);
            MacToolbox.TEDispose(teHandle);
        }

        ResourceGlobals.StyledTeList.Clear();
        MacToolbox.SetPort(savedPort[0]);
    }
}
