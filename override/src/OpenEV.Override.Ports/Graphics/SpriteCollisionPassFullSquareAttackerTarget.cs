using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1007cd8c (EV Override-11.c lines 53698-53737). Per-frame sprite-collision
// pass: full forward (+0x2e) scan with NO early cull, pairing each armed ATTACKER node
// (UpdateUpp set, State > 0) against every TARGET node (State < 0) in the list. On a rect
// overlap both nodes are marked collided (State = 10 / -10) and each node's own collision
// UPP, if set, fires independently via InvokeNodeCollisionUpp. MacRectsOverlap = FUN_1007c324.
public static class SpriteCollisionPassFullSquareAttackerTarget
{
    public static void Run()
    {
        GlobalState.SpriteListLock = 0;
        for (int attackerSprite = GlobalState.SpriteListHead; attackerSprite != 0;
             attackerSprite = SpriteNodes.At(attackerSprite).Next)
        {
            var attacker = SpriteNodes.At(attackerSprite);
            if (attacker.UpdateUpp != 0 && 0 < attacker.State)
            {
                // Restarts from the list head every outer iteration (not attacker.Next) —
                // the attacker/target State-sign split already rules out self-pairing and
                // double dispatch, so there's no Next-from-self shortcut to take here.
                for (int targetSprite = GlobalState.SpriteListHead; targetSprite != 0;
                     targetSprite = SpriteNodes.At(targetSprite).Next)
                {
                    var target = SpriteNodes.At(targetSprite);
                    if (target.UpdateUpp == 0)
                        continue;

                    if (target.State < 0 && EvMath.MacRectsOverlap(attacker, target))
                    {
                        attacker.State = 10;
                        target.State = -10;
                        if (attacker.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(attacker, target, attacker.CollisionUpp);
                        if (target.CollisionUpp != 0)
                            InvokeNodeCollisionUpp.Run(target, attacker, target.CollisionUpp);
                    }
                }
            }
            GlobalState.SpriteListLock |= (byte)(attacker.State < -1 ? 1 : 0);
        }
    }
}
