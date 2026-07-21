namespace OpenEV.Override.Ports.Boot;

// Port of FUN_10000000 (EV Override-11.c 1176-1178) — the fragment's offset-0 function
// (`sub_0`), an empty no-op the C-runtime startup calls at program entry (ProgramEntry
// step 5, `bl sub_0`). ProgramEntry documents the call but skips it since it does nothing.
public static class EmptyStub
{
    public static void Run()
    {
    }
}
