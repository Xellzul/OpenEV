namespace OpenEV.Override.Ports.Combat;

using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Combat.Model;

// FUN_100687b0 — EV Override-11.c lines 43370-43451. Spawns an escape pod / debris object from
// ship sourceShipSlot: finds a free debris slot, allocates a sprite node, seeds the debris record
// with the ship's position / velocity / system + random spin, and launches it with a random
// heading and speed.
public static class SpawnEscapePodFromShip
{
    public static void Run(ShipRec ship)
    {
        // Find the first free debris slot.
        short podSlot = -1;
        for (short i = 0; i < DebrisTable.Count; i++)
        {
            if (GameData.Debris[i].LifeRemaining <= DebrisRecord.Killed)
            {
                podSlot = i;
                break;
            }
        }
        if (podSlot == -1)
            return;

        int podObjectPtr = AllocateSpriteRecord.Run(3, 0, 0, 0);
        if (podObjectPtr == 0)
            return;

        var node = SpriteNodes.At(podObjectPtr);
        node.UpdateUpp = SpriteNodeUppCells.EscapePodUpdateUpp;
        node.SortKey = 2;
        node.State = 0;
        node.UpdaterFlag = 0;
        node.UpdaterPayload = 0;
        var pod = GameData.Debris[podSlot];
        // Managed model stores the slot index here (the pod-update UPP reads it back into
        // DebrisTable); the original stored the debris record's address.
        node.ObjectPtr = podSlot;

        // Seed the debris record from the source ship.
        pod.PosX = ship.PosX;
        pod.PosY = ship.PosY;
        pod.VelX = ship.VelX;
        pod.VelY = ship.VelY;
        short angleRoll = (short)SeedEvoRng.Run(90);
        pod.LifeRemaining = (short)(angleRoll + 180);
        pod.SystemId = ship.CurrentSystem;
        pod.AnimFrame = (short)SeedEvoRng.Run(36);

        // Random spin direction.
        short spinDir = (short)SeedEvoRng.Run(4);
        if (spinDir == 0) pod.SpinDir = -2;
        if (spinDir == 1) pod.SpinDir = -1;
        if (spinDir == 2) pod.SpinDir = 1;
        if (spinDir == 3) pod.SpinDir = 2;

        // Launch the pod a half sprite-width away from the ship, opposite its heading.
        int spriteWidth = MacRectWidth.Run(WeaponGraphicsTable.Store[ship.ShipClass * 36]);
        int angle = ship.Heading + 180;
        short scaledWidth = (short)(int)(ShipStatConstants.Half * spriteWidth);
        EvMath.OffsetByHeading(scaledWidth, angle % 360, ref pod.PosX, ref pod.PosY);

        node.ExtentTop = 0;
        node.ExtentLeft = 0;
        node.ExtentRight = (short)MacRectWidth.Run(SpriteFrameDimTable.Ptr);
        node.ExtentBottom = (short)MacRectHeight.Run(SpriteFrameDimTable.Ptr);

        // Launch velocity: a random base speed over a fixed divisor, random heading.
        short speedRoll = (short)SeedEvoRng.Run(40);
        double speed = (speedRoll + 30) / ShipStatConstants.EscapePodSpeedDivisor;
        short offsetRoll = (short)SeedEvoRng.Run(30);
        short spinRoll = (short)SeedEvoRng.Run(15);
        angle = ship.Heading + offsetRoll + 180 - spinRoll;
        EvMath.OffsetByHeading((float)speed, angle % 360, ref pod.VelX, ref pod.VelY);
    }
}
