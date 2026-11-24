// Schedules optional AutoBattler reserves and releases them through the shared arena.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    private sealed class ReserveWave
    {
        public readonly SoldierPlacement[] Soldiers;
        public readonly float Seconds;
        public readonly RedReserveFormation.Lane[] Lanes;
        public float TimeToFinish { get; }
        public UnitState Marching;
        public ArmyRenderer MarchRenderer;
        public int Released;
        public bool Pending => Released < Soldiers.Length;

        public ReserveWave(SoldierPlacement[] soldiers, float seconds, RedReserveFormation.Lane[] lanes = null)
        {
            Soldiers = soldiers;
            Seconds = seconds;
            Lanes = lanes;
            TimeToFinish = seconds + (lanes?.Max(lane => lane.Distance) ?? 0f) / RedReserveFormation.MarchSpeed;
        }
    }

    private readonly Dictionary<byte, int> blueReserve = new();
    private long blueReserveCount;
    private float blueReserveSeconds = 30f;
    private float reserveClock;
    private ReserveWave blueWave, redWave;
    private bool ReservesPending => blueWave?.Pending == true || redWave?.Pending == true;
    private int ReserveCapacity => (blueWave?.Soldiers.Length ?? 0) + (redWave?.Soldiers.Length ?? 0);
    private int paintedRedReserveCount;
    private int AuthoredRedReserve => paintedRedReserveCount > 0 ? paintedRedReserveCount :
        Math.Max(0, autoBattlerPlaytestMap?.AutoBattlerRules?.RedReserveCount ?? 0);

    private int Reserved(byte type) => blueReserve.GetValueOrDefault(type);
    private int MaxReserve(byte type) => campaignRoster.Owned(catalog[type].Id) - painter.Layout.CountForType(type);

    private void SetReserve(byte type, int count)
    {
        blueReserveCount += (long)count - Reserved(type);
        if (count == 0) blueReserve.Remove(type);
        else blueReserve[type] = count;
        UpdateCampaignDeployment();
    }

    private bool PrepareReserveWaves(out string error)
    {
        // The editor test and campaign both read Red's authored wave. Blue uses the campaign roster.
        blueWave = redWave = null;
        reserveClock = 0;
        error = null;
        var rules = autoBattlerPlaytestMap.AutoBattlerRules;
        if (paintedRedReserveCount > 0)
            redWave = BuildPaintedRedWave(out error);
        else if (AuthoredRedReserve > 0)
        {
            if (!catalog.TryResolve(rules.RedReserveUnitId, out byte type))
            {
                error = "Choose a valid Red reinforcement unit in Map Settings.";
                return false;
            }
            redWave = BuildReserveWave(Factions.Red, new[] { (type, AuthoredRedReserve) },
                rules.RedReserveSeconds, rules.RedReserveEdgeOffset);
            if (redWave == null) error = "The Red reinforcement entry has insufficient clear ground. Move the entry offset or clear the map edge.";
        }
        if (error == null && blueReserve.Count > 0)
        {
            blueWave = BuildReserveWave(Factions.Blue, blueReserve.OrderBy(pair => pair.Key)
                .Select(pair => (pair.Key, pair.Value)), blueReserveSeconds, 0f);
            if (blueWave == null) error = "The Blue reinforcement entry has insufficient clear ground along the Blue map edge.";
        }
        return error == null;
    }

    private ReserveWave BuildPaintedRedWave(out string error)
    {
        error = null;
        var entryField = BuildAutoBattlerField(autoBattlerCover.Concat(autoBattlerPaint?.MovementWater ?? autoBattlerWater)
            .Concat(autoBattlerDefenses.Select(defense => defense.Footprint(buildings))), autoBattlerPaint?.BridgeAreas);
        var authored = new List<(Vector3 Local, byte Type)>(paintedRedReserveCount);
        foreach (var group in RedReserveFormation.Groups(autoBattlerPlaytestMap))
        {
            if (!catalog.TryResolve(group.Unit?.Id, out byte type))
            {
                error = "A staged Red formation has an unrecognized unit type.";
                return null;
            }
            foreach (var point in group.Positions(catalog[type].Footprint))
                authored.Add((group.GetParent<Node3D>().ToLocal(point), type));
        }

        // Nearest the south edge first. Lateral order resolves ties deterministically.
        var ordered = authored.OrderByDescending(item => item.Local.Z).ThenBy(item => item.Local.X).ToArray();
        var soldiers = new SoldierPlacement[ordered.Length];
        var lanes = new RedReserveFormation.Lane[ordered.Length];
        float leadIn = ordered.Max(item => catalog[item.Type].Footprint);
        for (int i = 0; i < ordered.Length; i++)
        {
            var item = ordered[i];
            float radius = catalog[item.Type].Footprint;
            var lane = RedReserveFormation.Project(item.Local, radius, autoBattlerPlaytestMap, entryField, leadIn);
            if (!lane.Clear || Mathf.Abs(item.Local.X) + radius > RedReserveFormation.HalfWidth ||
                Mathf.Abs(item.Local.Z) + radius > RedReserveFormation.HalfWidth)
            {
                error = "A painted Red entry lane is blocked or outside the map. Check Red entry preview in the Level Editor.";
                return null;
            }
            lanes[i] = lane;
            soldiers[i] = new SoldierPlacement(0, 0, Factions.Red, item.Type)
                { AuthoredPosition = lane.Goal, AuthoredFacing = lane.Facing };
        }

        float seconds = autoBattlerPlaytestMap.AutoBattlerRules.RedReserveSeconds;
        return new ReserveWave(soldiers, float.IsFinite(seconds) ? Mathf.Clamp(seconds, 0, 299) : 30f, lanes);
    }

    private void BeginReserveMarch(ReserveWave wave)
    {
        // The entry renderer is separate from combat state until each soldier clears the edge.
        wave.Marching = new UnitState(wave.Soldiers.Length, catalog);
        for (int i = 0; i < wave.Soldiers.Length; i++)
        {
            var soldier = wave.Soldiers[i];
            var lane = wave.Lanes[i];
            int id = wave.Marching.TrySpawn(soldier.Type, soldier.Faction, ReserveMarchPosition(lane, 0f));
            wave.Marching.Facing[id] = lane.Facing;
        }
        wave.MarchRenderer = new ArmyRenderer();
        AddChild(wave.MarchRenderer);
        var entryMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://LEVELS/AutoBattler/ReserveEntryClip.gdshader")
        };
        entryMaterial.SetShaderParameter("map_half_width", autoBattlerPlaytestMap.MapSize * 0.5f);
        // Entry shadows would extend past the map edge. Combat rendering restores them on handoff.
        wave.MarchRenderer.Initialize(wave.Marching, soldierMeshes, soldierShadow, false, unitVisuals, entryMaterial);
    }

    private Vector3 ReserveMarchPosition(RedReserveFormation.Lane lane, float distance)
    {
        Vector3 point = lane.Start.MoveToward(lane.Goal, distance);
        if (terrain == null) return point;
        float half = autoBattlerPlaytestMap.MapSize * 0.5f;
        point.Y = terrain.At(Mathf.Clamp(point.X, -half, half), Mathf.Clamp(point.Z, -half, half));
        return point;
    }

    private void ClearReserveMarch(ReserveWave wave)
    {
        if (wave?.MarchRenderer == null) return;
        RemoveChild(wave.MarchRenderer);
        wave.MarchRenderer.QueueFree();
        wave.MarchRenderer = null;
        wave.Marching = null;
    }

    private void SyncReserveMarch()
    {
        if (redWave?.Marching == null) return;
        float distance = (reserveClock - redWave.Seconds) * RedReserveFormation.MarchSpeed;
        for (int i = redWave.Released; i < redWave.Soldiers.Length; i++)
        {
            redWave.Marching.Position[i] = ReserveMarchPosition(redWave.Lanes[i], distance);
            redWave.Marching.Dirty[i] = true;
        }
        redWave.MarchRenderer.Sync();
    }

    private ReserveWave BuildReserveWave(byte faction, IEnumerable<(byte Type, int Count)> composition, float seconds, float offset)
    {
        var groups = composition.ToArray();
        int count = groups.Sum(group => group.Count);
        var soldiers = new SoldierPlacement[count];
        var rules = autoBattlerPlaytestMap.AutoBattlerRules;
        bool blue = faction == Factions.Blue;
        bool alongZ = rules.LineAxis == AutoBattlerRules.DeploymentAxis.X;
        float sign = blue != rules.FlipBlueSide ? -1f : 1f;
        Vector2 inward = alongZ ? new Vector2(-sign, 0) : new Vector2(0, -sign);
        float half = autoBattlerPlaytestMap.MapSize * 0.5f;
        float radius = groups.Max(group => catalog[group.Type].Footprint);
        float stride = 2f * radius + 0.15f;
        float limit = half - radius - 0.05f;
        if (limit <= 0f) return null;
        float center = (float.IsFinite(offset) ? Mathf.Clamp(offset, -100f, 100f) : 0f) * 0.01f * limit;
        var entryField = BuildAutoBattlerField(autoBattlerCover.Concat(autoBattlerPaint?.MovementWater ?? autoBattlerWater)
            .Concat(autoBattlerDefenses.Select(defense => defense.Footprint(buildings))), autoBattlerPaint?.BridgeAreas);
        int acrossSlots = Mathf.FloorToInt(2f * limit / stride) + 1;
        int next = 0, groupIndex = 0, remaining = groups[0].Count;
        int preferredDepth = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count) * 0.5f));
        var entries = new List<Vector2>();
        for (int column = 0; column < acrossSlots * 2; column++)
        {
            float across = center + (column % 2 == 0 ? -1 : 1) * ((column + 1) / 2) * stride;
            if (Mathf.Abs(across) <= limit)
                entries.Add(alongZ ? new Vector2(sign * limit, across) : new Vector2(across, sign * limit));
        }
        foreach (Vector2 entry in entries)
        {
            if (next == count) break;
            // Fill each clear lane from the edge inward. Stop that lane at the first obstruction.
            for (int row = 0; row < preferredDepth && next < count; row++)
            {
                Vector2 point = entry + inward * (row * stride);
                var world = new Vector3(point.X, 0f, point.Y);
                if (Mathf.Abs(point.X) > limit || Mathf.Abs(point.Y) > limit || !rules.AllowsSide(world, radius, blue) ||
                    entryField?.IntersectsCircle(point, radius) == true ||
                    (row > 0 && entryField?.BlocksCorridor(point - inward * stride, point, radius) == true)) break;
                soldiers[next++] = new SoldierPlacement(0, 0, faction, groups[groupIndex].Type)
                    { AuthoredPosition = world, AuthoredFacing = inward };
                if (--remaining == 0 && ++groupIndex < groups.Length) remaining = groups[groupIndex].Count;
            }
        }
        return next == count ? new ReserveWave(soldiers, float.IsFinite(seconds) ? Mathf.Clamp(seconds, 0, 299) : 30f) : null;
    }

    private void ReleaseReserves(float dt)
    {
        reserveClock += dt;
        int first = units.Count;
        int budget = Math.Min(Config.SpawnPerFrame, 256);
        Release(blueWave);
        Release(redWave);
        if (first == units.Count) return;

        if (guards == null) autoAttackCount = units.Count;
        else
        {
            if (autoAttackIds == null)
            {
                autoAttackIds = new int[units.Position.Length];
                for (int id = 0; id < autoAttackCount; id++) autoAttackIds[id] = id;
            }
            for (int id = first; id < units.Count; id++) autoAttackIds[autoAttackCount++] = id;
        }
        if (Enumerable.Range(first, units.Count - first).Any(id => catalog[units.Type[id]].Trample.Enabled))
            trample = TrampleSystem.Create(units, autoAttackCount, autoAttackIds, trample);
        if (Enumerable.Range(first, units.Count - first).Any(id => units.Type[id] == RuntimeUnitCatalog.Dreadnaught ||
            catalog.IndependentRockets(units.Type[id]).Length > 0))
        {
            if (dreadnaughts != null)
            {
                RemoveChild(dreadnaughts);
                dreadnaughts.QueueFree();
            }
            dreadnaughts = DreadnaughtCombat.Create(units);
            if (dreadnaughts != null) AddChild(dreadnaughts);
        }

        void Release(ReserveWave wave)
        {
            if (wave?.Pending != true || reserveClock < wave.Seconds) return;
            float distance = (reserveClock - wave.Seconds) * RedReserveFormation.MarchSpeed;
            if (wave.Lanes != null && wave.Marching == null) BeginReserveMarch(wave);
            while (wave.Pending && budget > 0)
            {
                if (wave.Lanes != null && distance < wave.Lanes[wave.Released].Distance) break;
                if (wave.Marching != null) wave.Marching.Kill(wave.Released);
                var soldier = wave.Soldiers[wave.Released++];
                int id = units.TrySpawn(soldier.Type, soldier.Faction, soldier.Position());
                units.Facing[id] = soldier.AuthoredFacing;
                budget--;
            }
            if (!wave.Pending) ClearReserveMarch(wave);
        }
    }

    private string ReserveCountdown()
    {
        if (!ReservesPending) return "";
        string Format(string faction, ReserveWave wave) => wave?.Pending == true
            ? wave.Marching != null ? $"{faction} reinforcements · entering" : $"{faction} reinforcements · {Mathf.CeilToInt(Mathf.Max(0f, wave.Seconds - reserveClock))}s" : "";
        return string.Join("  |  ", new[] { Format("Blue", blueWave), Format("Red", redWave) }.Where(text => text.Length > 0));
    }
}
