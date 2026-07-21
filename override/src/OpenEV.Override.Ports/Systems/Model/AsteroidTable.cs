namespace OpenEV.Override.Ports.Systems.Model;

// The in-system ambient-asteroid particle table (the drifting asteroids; the original
// '_DAT_1008a528' symbol was misnamed "dust"). 0x1008a528 held a POINTER to particle[0],
// 10 × 0x1a bytes (alloc 0x104 at toc+0x1ec8). Particles are runtime-spawned by
// Systems.Asteroids, not loaded from resources — Store IS the data. No raw heap backing
// and no address-based Base/Stride: render nodes key off the Store INDEX instead (see
// Graphics.SpawnWorldSpriteNodes / Systems.TickAnimSprite).
public static class AsteroidTable
{
    public const int Count = 10;

    public static readonly AsteroidParticle[] Store = CreateStore();
    private static AsteroidParticle[] CreateStore()
    {
        var s = new AsteroidParticle[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new AsteroidParticle();
        return s;
    }
}
