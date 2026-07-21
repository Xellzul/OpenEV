namespace OpenEV.Override.Ports.Ship.Model;

// Typed HANDLE over one ship record. The record data lives in a managed ShipRecord
// object (ShipTable.Store[index]) — there is no raw byte backing; the old EvoMemory
// byte range this used to address was removed once every consumer moved to typed
// fields (see Misc.OriginalGameStateTotalBytes).
//
// The handle stays because raw `int` record pointers still flow in from other
// subsystems (a render node's ObjectPtr, the DialogShipPtr scratch global, an
// `int shipPtr` parameter):
//   * `Ptr` / implicit `operator int` carry that address; `FromPtr` wraps one back
//     into a handle. `Index` maps the Ptr to Store[index].
//   * the NAMED properties read/write the typed fields on that shared object —
//     they are the API (there is no byte-addressable backing anymore).
//
// Use as `Ship.Model.ShipTable.Ships[i].TargetSlot` / `.Player` / `.FromPtr(ptr)`.
public readonly struct ShipRec
{
    public readonly int Ptr;
    public ShipRec(int ptr) { Ptr = ptr; }

    public bool IsNull => Ptr == 0;
    public bool IsPlayer => Index == 0;

    // Slot index relative to record[0]. (Ptr - Base) / Stride.
    public int Index => (Ptr - ShipTable.Base) / ShipTable.Stride;

    // Pass a ShipRec into any port/helper that takes a raw `int shipPtr`; the
    // callee re-wraps it via FromPtr to reach the typed record.
    public static implicit operator int(ShipRec s) => s.Ptr;

    // The backing typed object for this handle's record.
    private ShipRecord Rec
    {
        get
        {
            int i = Index;
            if ((uint)i >= (uint)ShipTable.Count)
                throw new System.NotSupportedException(
                    $"ShipRec.Ptr 0x{Ptr:x8} maps to ship index {i} (out of [0,{ShipTable.Count})) — "
                    + "likely a sub-address or stale pointer. The ship record is now a typed object; "
                    + "use a record-aligned handle and a named field.");
            return ShipTable.Store[i];
        }
    }

    // ---- named fields → typed ShipRecord ----------------------------------------
    public float PosX { get => Rec.PosX; set => Rec.PosX = value; }
    public float PosY { get => Rec.PosY; set => Rec.PosY = value; }
    public float VelX { get => Rec.VelX; set => Rec.VelX = value; }
    public float VelY { get => Rec.VelY; set => Rec.VelY = value; }

    public short Heading { get => Rec.Heading; set => Rec.Heading = value; }
    public short HeadingPrev { get => Rec.HeadingPrev; set => Rec.HeadingPrev = value; }
    public short NavMode { get => Rec.NavMode; set => Rec.NavMode = value; }
    public short NavTargetSpob { get => Rec.NavTargetSpob; set => Rec.NavTargetSpob = value; }
    public short TargetSlot { get => Rec.TargetSlot; set => Rec.TargetSlot = value; }
    public short CurrentSystem { get => Rec.CurrentSystem; set => Rec.CurrentSystem = value; }

    public int Credits { get => Rec.Credits; set => Rec.Credits = value; }
    public float Shield { get => Rec.Shield; set => Rec.Shield = value; }

    // ---- typed accessors → ShipRecord (byte offsets documented on the backing fields) ----
    public float DesiredAccel { get => Rec.DesiredAccel; set => Rec.DesiredAccel = value; }
    public float DesiredSpeed { get => Rec.DesiredSpeed; set => Rec.DesiredSpeed = value; }
    public float Fuel { get => Rec.Fuel; set => Rec.Fuel = value; }
    public float DeathTimer { get => Rec.DeathTimer; set => Rec.DeathTimer = value; }
    public float PilotSkillScale { get => Rec.PilotSkillScale; set => Rec.PilotSkillScale = value; }

    public short AiActionTimer { get => Rec.AiActionTimer; set => Rec.AiActionTimer = value; }
    public short HasTargetLock { get => Rec.HasTargetLock; set => Rec.HasTargetLock = value; }
    public short SelectedWeaponSlot { get => Rec.SelectedWeaponSlot; set => Rec.SelectedWeaponSlot = value; }
    public short ShipClass { get => Rec.ShipClass; set => Rec.ShipClass = value; }
    public short DudeSpawnIndex { get => Rec.DudeSpawnIndex; set => Rec.DudeSpawnIndex = value; }
    public short[] CargoHold => Rec.CargoHold;
    public short SlotIndex { get => Rec.SlotIndex; set => Rec.SlotIndex = value; }
    public ShipAiType AiBehaviorType { get => Rec.AiBehaviorType; set => Rec.AiBehaviorType = value; }
    public short SpawningMissionSlot { get => Rec.SpawningMissionSlot; set => Rec.SpawningMissionSlot = value; }
    public short DefendedSpobIndex { get => Rec.DefendedSpobIndex; set => Rec.DefendedSpobIndex = value; }
    public short StrafeHeading { get => Rec.StrafeHeading; set => Rec.StrafeHeading = value; }
    public short LastVictimSlot { get => Rec.LastVictimSlot; set => Rec.LastVictimSlot = value; }
    public short DockedSpobIndex { get => Rec.DockedSpobIndex; set => Rec.DockedSpobIndex = value; }
    public short TurretMountCycle { get => Rec.TurretMountCycle; set => Rec.TurretMountCycle = value; }
    public short PriorSystem { get => Rec.PriorSystem; set => Rec.PriorSystem = value; }
    public short JumpWindupTimer { get => Rec.JumpWindupTimer; set => Rec.JumpWindupTimer = value; }
    public short ProvokedFlag { get => Rec.ProvokedFlag; set => Rec.ProvokedFlag = value; }
    public short Govt { get => Rec.Govt; set => Rec.Govt = value; }
    public short OwnerSlot { get => Rec.OwnerSlot; set => Rec.OwnerSlot = value; }

    public int AiTickStamp { get => Rec.AiTickStamp; set => Rec.AiTickStamp = value; }

    public short[] WeaponSlotType => Rec.WeaponSlotType;
    public short[] WeaponSlotAmmo => Rec.WeaponSlotAmmo;
    public float[] WeaponSlotReload => Rec.WeaponSlotReload;

    public byte HasWorldSpriteNode { get => Rec.HasWorldSpriteNode; set => Rec.HasWorldSpriteNode = value; }
    public byte IsActive { get => Rec.IsActive; set => Rec.IsActive = value; }
    public byte SalvageClaimed { get => Rec.SalvageClaimed; set => Rec.SalvageClaimed = value; }  // +0x6e — disabled hull already boarded/looted/abandoned
    public byte HasSelectedWeapon { get => Rec.HasSelectedWeapon; set => Rec.HasSelectedWeapon = value; }
    public byte IsTractored { get => Rec.IsTractored; set => Rec.IsTractored = value; }
    public byte IsCarriedFighter { get => Rec.IsCarriedFighter; set => Rec.IsCarriedFighter = value; }
    public byte HailQuoteSpoken { get => Rec.HailQuoteSpoken; set => Rec.HailQuoteSpoken = value; }
    public byte HasAfterburner { get => Rec.HasAfterburner; set => Rec.HasAfterburner = value; }

    public short CreditsEasterEggShown { get => Rec.CreditsEasterEggShown; set => Rec.CreditsEasterEggShown = value; }

    public ShipAiState AiState { get => (ShipAiState)Rec.AiState; set => Rec.AiState = (short)value; }
    public ShipManeuverState AiManeuverState { get => (ShipManeuverState)Rec.AiManeuverState; set => Rec.AiManeuverState = (short)value; }
    public short AiCourage { get => Rec.AiCourage; set => Rec.AiCourage = value; }
    public short IncomingDamageThreat { get => Rec.IncomingDamageThreat; set => Rec.IncomingDamageThreat = value; }
    public short PersIndex { get => Rec.PersIndex; set => Rec.PersIndex = value; }
    public short GrudgeMissionIndex { get => Rec.GrudgeMissionIndex; set => Rec.GrudgeMissionIndex = value; }
    public short AltFireSide { get => Rec.AltFireSide; set => Rec.AltFireSide = value; }
}
