namespace OpenEV.Override.Ports.Systems.Model;

// One in-system ambient asteroid particle (formerly 0x1a bytes at
// _DAT_1008a528 + i*0x1a; the original symbol was misnamed "dust"). The 10 particles
// live in the typed managed AsteroidTable.Store, spawned/updated at runtime by
// Systems.Asteroids (Init/Tick) — there is no resource loader and no raw heap backing,
// so the managed objects ARE the storage.
public sealed class AsteroidParticle
{
    public float PosX;       // +0x00  world X
    public float PosY;       // +0x04  world Y
    public float VelX;       // +0x08  X drift / velocity
    public float VelY;       // +0x0c  Y drift / velocity
    public short SpriteVariant;  // +0x10  sprite-variant flag (rng%6==0 ? 1 : 0): 0 = spïn 800, 1 = spïn 801
    public short AnimFrame;  // +0x12  rotation-sprite frame index — seeded rng%20, advanced by Direction
                             //        and wrapped [0,20) (spïn 800) / [0,30) (spïn 801) by Systems.TickAnimSprite.
                             //        (decompile symbol: "Life" — misleading; the real lifespan is Timer, +0x16.)
    public short Direction;  // +0x14  frame-step direction (1 / -1)
    public short Timer;      // +0x16  lifespan (rng-seeded 350 / 1000)
    public byte Active;     // +0x18  1 = live particle
    public byte Spawned;    // +0x19  1 = sprite node created (Graphics.SpawnWorldSpriteNodes)
}
