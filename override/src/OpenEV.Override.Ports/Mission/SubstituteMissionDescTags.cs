using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Text;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Resource;

namespace OpenEV.Override.Ports.Mission;

// FUN_1004f078 (EV Override-11.c lines 32389-32575) — the mission dësc TAG
// SUBSTITUTION pass: rewrites the shared description text (TextScratch.Text)
// in place, replacing <DST>/<DSY>/<RST>/<RSY>/<CT>/<CQ>/<SN>/<DL>/<PN>/<PSN>/<OSN>
// with the destination/return spob + system names, cargo type/qty, govt mission
// name, deadline date, pilot name, player ship name and the hailed pers name.
// Unfilled tags substitute "[Error]" (the default at 0x10084768). param_1 = 0
// reads the govt-mission slot (param_2 = govt idx); else the mission-def table
// (param_2 = mission idx).
//
// DEVIATION (faithful): the original staged the substitution through a temporary
// resizable Handle (Munger replaces each tag in place, growing/shrinking as
// needed) and copied the result back into the fixed 0x400-byte BSS buffer at the
// end, clamped to 0x400 bytes. The port runs the substitution on a C# string
// directly (Handle growth has no C# equivalent to model) and applies the same
// 0x400-byte clamp when writing back to TextScratch.Text.
public static class SubstituteMissionDescTags
{
    // The skip flag byte behind PTR cell 0x10081158 (seeded from 'ëbug' bit 0xe
    // by LoadBarPersonResources): nonzero = leave the dësc text unsubstituted.
    public static byte SkipSubstitution;

    private const string DefaultValue = "[Error]";   // 0x10084768

    public static void Run(byte param_1, short param_2)
    {
        if (SkipSubstitution != 0)
            return;

        string text = TextScratch.Text;

        string destSpob = DefaultValue, destSyst = DefaultValue;
        string retSpob = DefaultValue, retSyst = DefaultValue;
        string cargoType = DefaultValue, cargoQty = DefaultValue;
        string missionName = DefaultValue, deadline = DefaultValue;
        string persName = DefaultValue;

        if (param_1 == 0)
        {
            if (param_2 != -1 && GameData.MissionStates[param_2].IsActive != 0)
            {
                var govt = GameData.Missions[param_2];
                if (-1 < govt.TargetSpob && govt.TargetSpob < 1500)
                {
                    destSpob = GameData.Spobs[govt.TargetSpob].Name;
                    destSyst = MacToolbox.PascalToString(SystTable.Store[GameData.Spobs[govt.TargetSpob].System].Name);
                }
                if (-1 < govt.ReturnSpob && govt.ReturnSpob < 1500)
                {
                    retSpob = GameData.Spobs[govt.ReturnSpob].Name;
                    retSyst = MacToolbox.PascalToString(SystTable.Store[GameData.Spobs[govt.ReturnSpob].System].Name);
                }
                if (-1 < govt.CargoStringIndex && govt.CargoStringIndex < 64)
                {
                    cargoType = CommodityName(govt.CargoStringIndex);
                    cargoQty = govt.CargoMass.ToString();
                }
                var gf = GameData.MissionStates[param_2];
                if (GameDate.Current.Month != gf.DeadlineMonth ||
                    GameDate.Current.Day != gf.DeadlineDay ||
                    GameDate.Current.Year != gf.DeadlineYear)
                {
                    deadline = FormatDateLongFull.Format(gf.DeadlineYear, gf.DeadlineMonth, gf.DeadlineDay);
                }
                missionName = govt.Name;
            }
        }
        else if (param_2 != -1)
        {
            var def = GameData.MissionDefs[param_2];
            if (-1 < def.TargetSpob && def.TargetSpob < 1500)
                destSpob = GameData.Spobs[def.TargetSpob].Name;
            if (-1 < def.TargetSystem && def.TargetSystem < 1000)
                destSyst = MacToolbox.PascalToString(SystTable.Store[def.TargetSystem].Name);
            if (-1 < def.ReturnSpob && def.ReturnSpob < 1500)
                retSpob = GameData.Spobs[def.ReturnSpob].Name;
            if (-1 < def.ReturnSystem && def.ReturnSystem < 1000)
                retSyst = MacToolbox.PascalToString(SystTable.Store[def.ReturnSystem].Name);
            if (-1 < def.CargoType && def.CargoType < 64)
            {
                cargoType = CommodityName(def.CargoType);
                cargoQty = def.CargoQty.ToString();
            }
            if (GameDate.Current.Month != def.DeadlineMonth ||
                GameDate.Current.Day != def.DeadlineDay ||
                GameDate.Current.Year != def.DeadlineYear)
            {
                deadline = FormatDateLongFull.Format(def.DeadlineYear, def.DeadlineMonth, def.DeadlineDay);
            }
        }

        short target = WorldState.CurrentTargetShipId;
        if (-1 < target && target < 36 &&
            -1 < GameData.Ships[target].PersIndex &&
            GameData.Ships[target].PersIndex < 512)
        {
            persName = MacToolbox.PascalToString(GameData.Pers[GameData.Ships[target].PersIndex].Name);
        }

        // Each substituted value is staged with a 63-byte bound (matches the sibling
        // Trunc convention used elsewhere, e.g. TickShipAI/UpdateShipAiObjective) before
        // splicing — pilot Name / ShipName can otherwise exceed it.
        static string T(string s) => s.Length > 63 ? s.Substring(0, 63) : s;
        text = text.Replace("<DST>", T(destSpob))
                   .Replace("<DSY>", T(destSyst))
                   .Replace("<RST>", T(retSpob))
                   .Replace("<RSY>", T(retSyst))
                   .Replace("<CT>", T(cargoType))
                   .Replace("<CQ>", T(cargoQty))
                   .Replace("<SN>", T(missionName))
                   .Replace("<DL>", T(deadline))
                   .Replace("<PN>", T(PilotIdentity.Name))
                   .Replace("<PSN>", T(PilotIdentity.ShipName))
                   .Replace("<OSN>", T(persName));

        // Clamped to the original 0x400-byte buffer size (the BlockMoveData clamp).
        if (text.Length > 1024) text = text.Substring(0, 1024);
        TextScratch.Text = text;
    }

    // STR# 0xfa1 commodity-name cache entry (managed table; pre-boot reads "").
    private static string CommodityName(short idx)
        => ResourceGlobals.NamesStr0fa1[idx];
}
