// Root namespace so unqualified `ShipAiType` resolves in every OpenEV.Override.Ports.* file.
namespace OpenEV.Override.Ports;

// A ship's active AI behaviour type — which per-frame AI routine ShipAi.Run dispatches
// (ShipRec.AiBehaviorType, ShipRecord field +0x48). Four fields share this exact value
// domain and are raw-copied into each other with no scaling anywhere in the decompile:
//   * ShipClassRecord.InherentAI (ship class +0x14) — the class's fallback/default value,
//     copied onto AiBehaviorType whenever a ship reverts to its class AI (decompile line
//     1206-1207: ship+0x48 = class+0x14).
//   * DudeSpawnTable.AiType (düde spawn entry +0x00) — a düde's authored AI roll; < 1 means
//     "use the ship class's InherentAI instead" (FUN_1006615c, decompile lines 42545-42553:
//     `dude.AiType < 1 ? cls.InherentAI : dude.AiType` copied onto ship+0x48, matching
//     SpawnDudeShip.cs/SpawnSystArrivalNpc.cs/SpawnMissionNpc.cs exactly).
//   * PersRecord.AppearGate (përs record +0x04) — a pers's authored AI value; also doubles as
//     the "is this pers eligible for a random spawn" gate (`AppearGate > 0`, FUN_1006c110
//     decompile line 44435: `0 < *(short *)(pers+4)`), then raw-copied onto ship+0x48
//     (decompile lines 44533-44534: ship+0x48 = pers+4). Kept its established field name
//     (not renamed) since only its TYPE changed here.
// ShipAi.Run's dispatch (ShipAi.cs ~1453): 1 TickAi, 2 TickAttackerAi, 3 TickDefenderAi,
// 4 TickInterceptorAi, 5 TickEscortAi, 6 TickFollowMasterAi.
// Names + descriptions are the Override Bible's, cross-verified against the editor's
// ShipAiTypes/DudeAiTypes choice tables (editor/src/OpenEV.Editor.Schema/Schemas.More.cs,
// "[CONFIRMED 2026-06-22 vs code + Bible]"). -1 (Inactive) is a ShipRecord-only runtime
// sentinel (docked/inactive/ownerless — e.g. RunShipCaptureSwap's abandon path, a fresh
// ship slot before spawn-init) — it is not part of the ship-class/düde/përs authoring
// range 0-6 and never appears in InherentAI/AiType/AppearGate resource data.
public enum ShipAiType : short
{
    Inactive = -1,     // ship: inactive/docked/ownerless — never dispatched (ShipAi.cs switch has no case)
    None = 0,           // no inherent AI / "inherit from ship class" — never dispatched; a pers with this never random-spawns
    WimpyTrader = 1,    // flees when attacked — TickAi
    BraveTrader = 2,    // fights back, then flees — TickAttackerAi
    Warship = 3,        // govt-defender — TickDefenderAi
    Interceptor = 4,    // TickInterceptorAi
    NavalFighter = 5,   // carried fighter / escort-of-carrier — reserved, not an authoring value — TickEscortAi
    Escort = 6,         // hired escort / wingman — reserved, not an authoring value — TickFollowMasterAi
}
