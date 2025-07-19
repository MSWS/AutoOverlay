using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutoOverlay {
  public class AutoOverlay : Mod {
    public AutoOverlay(ModContentPack content) : base(content) {
      var harmony = new Harmony("xyz.msws.autooverlay");
      harmony.PatchAll();
    }
  }

  // [HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawRadiusRing),
  //   typeof(IntVec3), typeof(float))]
  // public class PatchGenDraw_RandomRadius {
  //   private static readonly float startTime = Time.time;
  //
  //   static bool Prefix(IntVec3 center, float radius) {
  //     GenDraw.DrawRadiusRing(center, radius, getRainbowColor());
  //     return false;
  //   }
  //
  //   private static Color getRainbowColor() {
  //     var t = (Time.time - startTime) / 5f; // 5 seconds for a full cycle
  //     t = t - Mathf.Floor(t);               // Normalize to [0, 1]
  //     return Color.HSVToRGB(t, 1f, 1f);     // Full saturation and brightness
  //   }
  // }

  [HarmonyPatch(typeof(Thing), nameof(Thing.DrawExtraSelectionOverlays))]
  public class PatchThing_DrawExtraSelectionOverlays {
    static bool Prefix(Thing __instance) { return false; }
  }

  [HarmonyPatch(typeof(Building_TurretGun),
    nameof(Building_TurretGun.DrawExtraSelectionOverlays))]
  public class PatchBuilding_TurretGun_DrawExtraSelectionOverlays {
    static bool Prefix(Building_TurretGun __instance) { return true; }
  }

  [HarmonyPatch(typeof(Designator_Place),
    nameof(Designator_Place.SelectedUpdate))]
  public class PatchDesignator_Place_SelectedUpdate {
    static void Postfix(Designator_Place __instance) {
      foreach (var building in getRelatedBuildings(
        __instance.PlacingDef as ThingDef))
        renderBuilding(building);

      var blueprintGrid = Find.CurrentMap.blueprintGrid;
      var innerArray    = blueprintGrid?.InnerArray;

      if (innerArray == null) {
        Log.Warning(
          "AutoOverlay: blueprintGrid.InnerArray is null, skipping drawing overlays.");
        return;
      }

      // var flattened = innerArray.Where(x => x != null)
      //  .SelectMany(x => x)
      //  .Where(x => x.EntityToBuild() == __instance.PlacingDef);
      // var blueprints = flattened as Blueprint[] ?? flattened.ToArray();
      var blueprints = getRelatedBlueprints(__instance.PlacingDef as ThingDef);

      foreach (var blueprint in blueprints) renderBlueprint(blueprint);
    }

    private static void renderBuilding(Building building) {
      if (building.def != null && building.def.specialDisplayRadius > 0)
        GenDraw.DrawRadiusRing(building.Position,
          building.def.specialDisplayRadius, building.DrawColor.WithAlpha(128));
      if (building?.def?.PlaceWorkers == null) return;

      render(building.def, building.Position, building.Rotation, building);
    }

    private static void renderBlueprint(Blueprint blueprint) {
      if (blueprint?.def == null) return;

      if (blueprint.EntityToBuild().specialDisplayRadius > 0)
        GenDraw.DrawRadiusRing(blueprint.Position,
          blueprint.EntityToBuild().specialDisplayRadius, blueprint.DrawColor);

      if (blueprint.def.PlaceWorkers == null) return;

      render(blueprint.EntityToBuild(), blueprint.Position, blueprint.Rotation,
        blueprint);
    }

    private static void render(BuildableDef buildableDef, IntVec3 position,
      Rot4 rotation, Thing thingToIgnore) {
      if (buildableDef == null) {
        Log.Warning("AutoOverlay: buildableDef is null, skipping drawing.");
        return;
      }

      foreach (var placeWorker in buildableDef.PlaceWorkers) {
        if (placeWorker == null) {
          Log.Warning(
            $"AutoOverlay: PlaceWorker is null for {buildableDef.defName}, skipping.");
          continue;
        }

        try {
          placeWorker.AllowsPlacing(buildableDef, position, rotation,
            Find.CurrentMap, thingToIgnore);
          placeWorker.DrawGhost(buildableDef as ThingDef, position, rotation,
            thingToIgnore.DrawColor);
        } catch (Exception e) {
          Log.Error(
            $"Error in DrawPlaceMouseAttachments for {buildableDef.defName}: {e.Message}");
        }
      }
    }

    private static readonly List<Predicate<string>> relatedBuildingTypes =
      new List<Predicate<string>> {
        s => s.StartsWith("TrapIED_"),
        s => s.Contains("Lamp") || s.Equals("FloodLight"),
        s => s.Contains("Turret") && !s.Equals("Turret_FoamTurret"),
        s => s.Equals("Turret_FoamTurret") || s.Equals("FirefoamPopper")
      };

    private static List<Building> getRelatedBuildings(ThingDef def) {
      var relatedBuildings = new List<Building>();
      var sourceList =
        relatedBuildingTypes.FirstOrDefault(list => list.Invoke(def.defName));

      foreach (var building in Find.CurrentMap.listerBuildings
       .allBuildingsColonist.Where(building => building.def != null)) {
        if (building.def == def) {
          relatedBuildings.Add(building);
          continue;
        }

        if (sourceList == null) continue;

        if (sourceList.Invoke(building.def.defName))
          relatedBuildings.Add(building);
      }

      return relatedBuildings;
    }

    private static List<Blueprint> getRelatedBlueprints(ThingDef def) {
      var relatedBlueprints = new List<Blueprint>();
      var sourceList =
        relatedBuildingTypes.FirstOrDefault(list => list.Invoke(def.defName));

      foreach (var blueprint in Find.CurrentMap.blueprintGrid.InnerArray
       .Where(s => s != null)
       .SelectMany(s => s)
       .Where(blueprint => blueprint.EntityToBuild() != null)) {
        if (blueprint.EntityToBuild() == def) {
          relatedBlueprints.Add(blueprint);
          continue;
        }

        if (sourceList == null) continue;

        if (sourceList.Invoke(blueprint.EntityToBuild().defName))
          relatedBlueprints.Add(blueprint);
      }

      return relatedBlueprints;
    }
  }
}