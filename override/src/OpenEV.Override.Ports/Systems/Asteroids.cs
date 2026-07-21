using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;

namespace OpenEV.Override.Ports.Systems;

// The in-system ambient-asteroid system (the drifting asteroids the original code
// misnamed "dust"). Init seeds the field when entering a system; Tick spawns one
// particle per call until the system reaches its asteroid density. Particle data lives
// in the typed managed Systems.Model.AsteroidTable.Store.
//   Init  = FUN_100699d8 (EV Override-11.c 43769-43821)
//   Tick  = FUN_10069c30 (EV Override-11.c 43822-43945)
public static class Asteroids
{
    public static void Init()
    {
        short asteroidCount = CurrentSystemAsteroidCount();
        if (asteroidCount < 1)
        {
            WorldState.NoAsteroidsFlag = 1;
            return;
        }

        for (short i = 0; i < asteroidCount; i++)
            Tick(0);

        // Seed all 10 slots with a random spread around the camera centre.
        short camXBias = (short)(WorldFlags.CameraCentreX + 128);
        short camYBias = (short)(WorldFlags.CameraCentreY + 128);
        double posScale = ShipStatConstants.Half;
        double spreadScale = MathConstants.OnePercent;
        var player = GameData.Player;

        foreach (var p in GameData.Asteroids)
        {
            short jitter = (short)Misc.SeedEvoRng.Run(camXBias);
            p.PosX = (float)-(posScale * camXBias - (player.PosX + jitter));
            jitter = (short)Misc.SeedEvoRng.Run(camYBias);
            p.PosY = (float)-(posScale * camYBias - (player.PosY + jitter));
            jitter = (short)Misc.SeedEvoRng.Run(400);
            p.VelX = (float)(spreadScale * (jitter - 200));
            jitter = (short)Misc.SeedEvoRng.Run(400);
            p.VelY = (float)(spreadScale * (jitter - 200));
        }
    }

    // Spawn at most one new asteroid into the first free slot, unless the field is
    // already at its density. spawnMode 0 = random spread (entry); 1 = drift in from
    // an edge (ongoing replenishment).
    public static void Tick(byte spawnMode)
    {
        short asteroidCount = CurrentSystemAsteroidCount();
        if (asteroidCount < 1)
            return;

        short liveCount = 0;
        foreach (var p in GameData.Asteroids)
        {
            if (p.Active != 0 || p.Spawned != 0)
                liveCount++;
        }
        if (liveCount >= asteroidCount)
            return;

        foreach (var p in GameData.Asteroids)
        {
            if (p.Active != 0 || p.Spawned != 0)
                continue;

            p.Active = 1;
            p.Spawned = 0;

            short camXBias = (short)(WorldFlags.CameraCentreX + 128);
            short camYBias = (short)(WorldFlags.CameraCentreY + 128);
            var player = GameData.Player;

            if (spawnMode == 0)
            {
                double posScale = ShipStatConstants.Half;
                double spreadScale = MathConstants.OnePercent;
                short jitter = (short)Misc.SeedEvoRng.Run(camXBias);
                p.PosX = (float)-(posScale * camXBias - (player.PosX + jitter));
                jitter = (short)Misc.SeedEvoRng.Run(camYBias);
                p.PosY = (float)-(posScale * camYBias - (player.PosY + jitter));
                jitter = (short)Misc.SeedEvoRng.Run(400);
                p.VelX = (float)(spreadScale * (jitter - 200));
                jitter = (short)Misc.SeedEvoRng.Run(400);
                p.VelY = (float)(spreadScale * (jitter - 200));
            }
            else
            {
                double xScale = MathConstants.DustXScale;
                double offsetScale = MathConstants.DustOffsetScale;
                double spreadScale = MathConstants.OnePercent;

                short edge = camYBias < camXBias ? camXBias : camYBias;
                float drift = (float)(xScale * edge);

                if (Misc.SeedEvoRng.Run(2) == 0)
                {
                    p.PosX = player.PosX + drift;
                    p.VelX = (float)(offsetScale * (short)Misc.SeedEvoRng.Run(200));
                }
                else
                {
                    p.PosX = player.PosX - drift;
                    p.VelX = (float)(spreadScale * (short)Misc.SeedEvoRng.Run(200));
                }
                if (Misc.SeedEvoRng.Run(2) == 0)
                {
                    p.PosY = player.PosY + drift;
                    p.VelY = (float)(offsetScale * (short)Misc.SeedEvoRng.Run(200));
                }
                else
                {
                    p.PosY = player.PosY - drift;
                    p.VelY = (float)(spreadScale * (short)Misc.SeedEvoRng.Run(200));
                }
            }

            p.AnimFrame = (short)Misc.SeedEvoRng.Run(20);
            p.SpriteVariant = (short)(Misc.SeedEvoRng.Run(6) == 0 ? 1 : 0);
            p.Direction = (short)(Misc.SeedEvoRng.Run(2) == 0 ? 1 : -1);
            p.Timer = (short)(p.SpriteVariant == 0 ? 350 : 1000);
            return;
        }
    }

    private static short CurrentSystemAsteroidCount() =>
        SystTable.Store[GameData.Player.CurrentSystem].AsteroidCount;
}
