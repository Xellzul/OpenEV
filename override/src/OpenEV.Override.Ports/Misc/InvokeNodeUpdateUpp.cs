using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Systems;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_1007e2d8 (EV Override-11.c lines 54558-54568).
//
// FUN_1007e2d8(node, upp) is a mixed-mode call glue: it invokes the
// UniversalProcPtr `upp` with `node` as its single argument (via FUN_1008062c
// → `(*in_r12)()`). The render-list dispatcher (FUN_1007d8bc) calls it once per
// node as `FUN_1007e2d8(node, *(node+0x1a))` — node+0x1a holds the per-object
// update UPP that the node-creator (FUN_10061d74) stored from the per-type
// globals _DAT_10081248 (ships) / _DAT_10081240 (spobs) / _DAT_1008123c (anims).
//
// The port has no real Mixed Mode Manager, so the UPP is represented by the original
// PowerPC address of the routine it wraps (the consts below). We dispatch on
// that address here to the C# port. Unknown tokens fall through to the CFM
// glue no-op (matching "UPP not yet wired").
public static class InvokeNodeUpdateUpp
{
    // PowerPC addresses of the per-object render-node update routines, used as
    // UPP sentinel tokens (see EvoGlobals/sprite-table init that stores these
    // into _DAT_1008124x). The dispatcher hands us node+0x1a == one of these.
    public const int ShipUpdateUpp = 0x1001eb2c;   // FUN_1001eb2c → UpdateShipSlotTick
    public const int SpobUpdateUpp = 0x10020548;   // FUN_10020548 → spob/planet-body updater
    public const int ReticleUpdateUpp = 0x10020ad4; // FUN_10020ad4 → TickEscortTractor (target-lock brackets)
    public const int NebulaUpdateUpp = 0x10020184; // FUN_10020184 → TickBackgroundNebulaSprite (UPP global 0x10081168)

    public static void Run(int node, int updateUpp)
    {
        switch (updateUpp)
        {
            case ShipUpdateUpp:
                UpdateShipSlotTick.Run(node);
                return;
            case SpobUpdateUpp:
                TickSpobSprite.Run(node);   // spob/planet-body updater
                return;
            case ReticleUpdateUpp:
                TickEscortTractor.Run(node);   // FUN_10020ad4 → target-lock bracket corners
                return;
            case NebulaUpdateUpp:
                TickBackgroundNebulaSprite.Run(node);   // background scenery wrap updater
                return;
            default:
                // The spawners set node+0x1a to these SpriteNodeUppCells values; dispatch on
                // the cell field (robust whether it holds a code-addr sentinel or a relocated
                // TVector; cell→FUN resolved via tools/resolve_tvec.py).
                if (updateUpp == SpriteNodeUppCells.ProjectileUpdateUpp) { TickProjectile.Run(node); return; }       // FUN_100269a4
                if (updateUpp == SpriteNodeUppCells.ExplosionUpdateUpp) { TickExplosionSprite.Run(node); return; }  // FUN_1001fbf8
                if (updateUpp == SpriteNodeUppCells.StreakUpdateUpp) { TickStreakSprite.Run(node); return; }     // FUN_1001fe88
                if (updateUpp == SpriteNodeUppCells.EscapePodUpdateUpp) { TickCarriedSprite.Run(node); return; }  // FUN_1001f9ac
                if (updateUpp == SpriteNodeUppCells.DockingRingUpdateUpp) { TickDockingRing.Run(node); return; }        // FUN_10020728
                if (updateUpp == SpriteNodeUppCells.HudBlinkOrbUpdateUpp) { TickHudBlinkOrbSprite.Run(node); return; } // FUN_10020474
                if (updateUpp == SpriteNodeUppCells.AnimUpdateUpp) { TickAnimSprite.Run(node); return; }         // FUN_1006a284 (ambient asteroids; recovered from the disassembly)
                if (updateUpp == SpriteNodeUppCells.HudOverlayUpdateUpp) { TickDoubleSpeedIndicator.Run(node); return; } // FUN_1002043c (2x-speed indicator; recovered from the disassembly)
                // Every node-update UPP in the base data now dispatches; a truly-unknown token
                // (an un-seeded relocated TVector none of the cases match) → honest CFM-glue no-op.
                InvokeMacUpp.Run(node);
                return;
        }
    }
}

// The per-object node-UPP token CELLS the node spawner FUN_10061d74
// (Graphics.SpawnWorldSpriteNodes) and SpawnSpecialWeaponShip copy into node+0x1a (update) /
// node+0x1e (draw/collision): PEF-relocated TVector pointers in the data segment,
// still LIVE. V2TitleAdapter.BuildShipSpriteTable re-seeds the two ported
// updaters with InvokeNodeUpdateUpp code-address sentinels; the others keep their
// relocated TVector values, which InvokeNodeUpdateUpp ALSO dispatches on by value
// (e.g. AnimUpdateUpp 0x100825a0 → TickAnimSprite). Accessors keep the ports
// EvoMemory-free.
public static class SpriteNodeUppCells
{
    // MANAGED fields now, initialized with the cells' PEF-relocated TVector
    // values (raw cell + dataBase 0x10080660, dumped) — the same values the
    // relocated data segment held, so un-reseeded families still dispatch to
    // InvokeNodeUpdateUpp's default no-op. BuildShipSpriteTable overwrites the
    // ported ones with code-address sentinels at boot. Old cells noted inline.
    public static int AnimDrawUpp = 0x10082598;  // was 0x10081238 — asteroid/anim draw  -> node+0x1e
    public static int AnimUpdateUpp = 0x100825a0;  // was 0x1008123c — asteroid/anim update -> node+0x1a
    public static int SpobUpdateUpp = 0x100824c0;  // was 0x10081240 — spob/planet update   -> node+0x1a
    public static int ShipDrawUpp = 0x100825a8;  // was 0x10081244 — ship draw/collision  -> node+0x1e
    public static int ShipUpdateUpp = 0x100824f8;  // was 0x10081248 — ship update          -> node+0x1a
    public static int StreakUpdateUpp = 0x100824e0;  // was 0x10081218 — streak update (FUN_1001fe88)
    public static int ExplosionUpdateUpp = 0x100824e8;  // was 0x1008121c — explosion update (FUN_1001fbf8)
    public static int ProjectileDrawUpp = 0x10082590;  // was 0x10081220 — projectile draw/collision
    public static int ProjectileUpdateUpp = 0x100824a0;  // was 0x10081224 — projectile update (FUN_100269a4)
    public static int EscapePodUpdateUpp = 0x100824f0;  // was 0x10081228 — escape-pod/debris update
    public static int NebulaUpdateUpp = 0x100824d8;  // was 0x10081168 — background-nebula update (FUN_10020184)
    public static int DockingRingUpdateUpp = 0x100824b8;  // was 0x10081178 — docking-ring update (FUN_10020728)
    public static int DockingRingDrawUpp = 0x10082578;  // was 0x1008117c — docking-ring draw (FUN_10054624)
    public static int ReticleUpdateUpp = 0x100824b0;  // was 0x10081184 — target-bracket update (FUN_10020ad4)
    public static int ReticleDrawUpp = 0x10082580;  // was 0x10081188 — target-bracket draw (FUN_10054648)
    public static int HudBlinkOrbUpdateUpp = 0x100824c8;  // was 0x10081190 — HUD blink-orb update (FUN_10020474)
    public static int HudOverlayUpdateUpp = 0x100824d0;  // was 0x10081194 — 2x-speed indicator update (FUN_1002043c → TickDoubleSpeedIndicator)

    // Seed the ported families' cells with the code-address sentinels InvokeNodeUpdateUpp
    // dispatches on (ships/spobs/reticle/nebula → their FUN_ addresses; docking-ring/
    // hud-orb literals). In the original these cells hold PEF-relocated TVectors set at
    // CFM load time — correct before ANY spawner runs. The port substitutes code-address
    // sentinels, and this MUST run before the first spawner: the background-nebula nodes
    // are spawned at BOOT (GameBootSequence step 39) and capture this cell into node+0x1a;
    // if it still held the un-dispatchable relocated-TVector value, those nodes route to
    // InvokeNodeUpdateUpp's no-op and the starfield never draws. Idempotent (BuildShipSpriteTable
    // also calls it at Enter Ship). The un-reseeded families (AnimUpdate 0x100825a0,
    // HudOverlay 0x100824d0) keep their relocated-TVector values, which InvokeNodeUpdateUpp
    // ALSO dispatches on by value → TickAnimSprite / TickDoubleSpeedIndicator. All
    // node-update UPP families are now ported.
    public static void SeedDispatchTokens()
    {
        ShipUpdateUpp = InvokeNodeUpdateUpp.ShipUpdateUpp;     // ships → FUN_1001eb2c
        SpobUpdateUpp = InvokeNodeUpdateUpp.SpobUpdateUpp;     // spobs/planets → FUN_10020548
        ReticleUpdateUpp = InvokeNodeUpdateUpp.ReticleUpdateUpp;  // → FUN_10020ad4
        NebulaUpdateUpp = InvokeNodeUpdateUpp.NebulaUpdateUpp;   // → FUN_10020184 (background-nebula wrap updater)
        DockingRingUpdateUpp = 0x10020728;                      // → TickDockingRing
        DockingRingDrawUpp = 0x10054624;
        ReticleDrawUpp = 0x10054648;
        HudBlinkOrbUpdateUpp = 0x10020474;                      // → TickHudBlinkOrbSprite
        HudOverlayUpdateUpp = 0x1002043c;
    }
}
