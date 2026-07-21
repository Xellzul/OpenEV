using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Pilot.Model;

namespace OpenEV.Override.Ports.Pilot;

// Port of FUN_10054b44 (EV Override-11.c lines 34698-34747): the lighter new-pilot
// world reset — re-arms the available missions, clears spob trading and the
// control/cron/beam/projectile/debris/nebula tables, restarts the game clock at the
// EVO start date (the host date + 250 years) and defaults the pilot/ship names.
public static class InitializeNewPilotWorld
{
    public static void Run()
    {
        for (short i = 0; i < PersTable.Count; i++)
        {
            if (0 < GameData.Pers[i].AppearGate)
            {
                GameData.Pers[i].AvailableFlag = 1;
                GameData.Pers[i].AcceptedFlag = 0;
            }
        }
        for (short i = 0; i < SpobTable.Count; i++)
        {
            GameData.Spobs[i].TradingEnabled = 0;
        }
        for (short i = 0; i < ControlBits.Count; i++)
        {
            ControlBits.Set(i, 0);
        }
        for (short i = 0; i < CronTable.Count; i++)
        {
            GameData.Crons[i].StateCountdown = 0;
        }
        for (short i = 0; i < BeamTable.Count; i++)
        {
            GameData.Beams[i].Life = BeamRecord.Killed;
        }
        for (short i = 0; i < ProjectileTable.Count; i++)
        {
            GameData.Projectiles[i].LifeRemaining = ProjectileRecord.Killed;
        }
        for (short i = 0; i < DebrisTable.Count; i++)
        {
            GameData.Debris[i].LifeRemaining = DebrisRecord.Killed;
        }
        for (short i = 0; i < MapNebulaTable.Count; i++)
        {
            MapNebulaTable.Store[i].Charted = 0;
        }
        ReseedBackgroundNebulae.Run();
        WorldState.LandingTargetSpob = -1;
        WorldState.LandingApproachState = -1;

        // The EVO start date is the real date 250 years on; the game clock is the
        // managed GameDate now.
        GameDate.SetCurrentToHostClock();
        var gameClock = GameDate.Current;
        gameClock.Year = (short)(gameClock.Year + 250);
        GameDate.Current = gameClock;

        // The decompile clears a middle-name buffer here (0x1009030c) — a dead write
        // with no reader (see PilotSaveSources); the names default to "No Name".
        PilotIdentity.Name = "No Name";
        PilotIdentity.ShipName = "No Name";

        // Written 1 at new-pilot world init, read nowhere in the binary — faithful dead write.
        WorldState.FlagF72e = 1;
        WorldState.FirstEntryCutsceneShown = false;
    }
}
