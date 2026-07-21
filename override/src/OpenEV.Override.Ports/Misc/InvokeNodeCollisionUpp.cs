using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Graphics.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1007e30c (EV Override-11.c lines 54572-54584).
//
// FUN_1007e30c(node, other, upp) is the mixed-mode call glue for the sprite-overlap
// COLLISION callback: the collision pass (FUN_1007cc84 + the axis-bucketed variants)
// calls it once per colliding node as FUN_1007e30c(thisNode, otherNode, *(thisNode+0x1e)) —
// node+0x1e holds the per-object collision-handler UPP. The glue forwards (thisNode,
// otherNode) to that UPP via FUN_1008062c → (*r12)().
//
// The host has no real Mixed Mode Manager, so the UPP is the node+0x1e cell VALUE (a
// relocated TVector address held in SpriteNodeUppCells). We dispatch on it here to the
// ported C# handler, mirroring InvokeNodeUpdateUpp. The cell→FUN map (resolved via
// tools/resolve_tvec.py, validated against the draw-cell comments):
// ShipDrawUpp(0x100825a8)→FUN_10062960, ProjectileDrawUpp(0x10082590)→FUN_10062800,
// AnimDrawUpp(0x10082598)→FUN_1006aa7c.
public static class InvokeNodeCollisionUpp
{
    public static void Run(SpriteNode thisNode, SpriteNode otherNode, int collisionUpp)
    {
        if (collisionUpp == SpriteNodeUppCells.ShipDrawUpp)            // FUN_10062960 — ship weapon-hit dispatcher
        {
            RunWeaponHitDispatcher.Run(thisNode, otherNode);
        }
        else if (collisionUpp == SpriteNodeUppCells.ProjectileDrawUpp) // FUN_10062800 — projectile/missile intercept
        {
            HandleMissileIntercept.Run(thisNode, otherNode);
        }
        else if (collisionUpp == SpriteNodeUppCells.AnimDrawUpp)       // FUN_1006aa7c — anim/asteroid collision
        {
            HandleProjectileDeath.Run(thisNode, otherNode);
        }
        else
        {
            // NO-OP: an unported / draw-only +0x1e token (e.g. reticle/docking-ring overlay
            // draw UPPs that also sit in node+0x1e). The original dispatches via CFM glue;
            // here it absorbs into the no-op InvokeMacUpp so nothing fires for unwired handlers.
            InvokeMacUpp.Run(thisNode, otherNode);
        }
    }
}
