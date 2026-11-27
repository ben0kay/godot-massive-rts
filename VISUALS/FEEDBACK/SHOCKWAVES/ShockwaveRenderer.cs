// Batches expanding impact rings and warm explosive flashes.
using Godot;

namespace MassiveRts;

public partial class ShockwaveRenderer : MultiMeshInstance3D
{
    private const int Capacity = 256;
    private const int ChipsPerHit = 6;
    private const int Segments = 64;
    private const float Duration = 0.35f;
    private readonly record struct Ring(Vector3 Origin, float Radius, float Age, Vector3? BurstOrigin, byte? Faction, bool StructureChips, bool Rally);
    private readonly Ring[] rings = new Ring[Capacity];
    private MultiMesh bursts, chips, rallyMarkers;
    private MeleeEffects meleeEffects;
    private ScorchMarks scorchMarks;
    private readonly CavalryDust cavalryDust = new();
    private TreadMarks treads;
    private MuzzleSmoke buildingSmoke;
    private float smokeCooldown;
    private int smokeCursor;
    private int count;
    private bool dirty;

    public override void _Ready()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        for (int i = 0; i < Segments; i++)
        {
            Vector3 a = Direction(i), b = Direction(i + 1);
            surface.SetColor(Colors.White);
            surface.AddVertex(a);
            surface.AddVertex(b);
            surface.AddVertex(a * 0.985f);
            surface.AddVertex(a * 0.985f);
            surface.AddVertex(b);
            surface.AddVertex(b * 0.985f);
        }
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = surface.Commit(),
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var flashMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://VISUALS/FEEDBACK/SHOCKWAVES/ExplosionBurst.gdshader")
        };
        bursts = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            UseColors = true,
            Mesh = new QuadMesh { Material = flashMaterial },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = bursts, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 64f
        });
        chips = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = Vector3.One, Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.49f, 0.45f, 0.39f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }},
            InstanceCount = Capacity * ChipsPerHit,
            VisibleInstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D { Multimesh = chips, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private static Vector3 Direction(int index)
    {
        float angle = index * Mathf.Tau / Segments;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    public void Emit(Vector3 origin, float radius, Vector3? burstOrigin = null, byte? faction = null, bool structureChips = false, bool rally = false)
    {
        if (count == Capacity || (radius <= 0f && !structureChips)) return;
        rings[count++] = new Ring(origin + Vector3.Up * 0.035f, radius, 0f, burstOrigin, faction, structureChips, rally);
        dirty = true;
    }

    internal void EmitRally(Vector3 origin, float radius, byte faction) =>
        Emit(origin + Vector3.Up * 0.10f, radius, faction: faction, rally: true);

    internal void EmitScorch(Vector3 ground, float blastRadius, TerrainHeightField terrain)
    {
        if (blastRadius <= 0f) return;
        scorchMarks ??= new ScorchMarks(this);
        scorchMarks.Emit(ground, blastRadius, terrain);
    }

    internal void EmitMelee(Vector3 contact, Vector2 direction, float footprint, bool cleave)
    {
        meleeEffects ??= new MeleeEffects(this);
        meleeEffects.Emit(contact, direction, Mathf.Clamp(footprint * 1.6f, 0.58f, 1.2f), 0);
        if (cleave)
            meleeEffects.Emit(contact, direction, Mathf.Clamp(footprint * 3.9f, 1.3f, 3.1f), 1);
    }

    internal void SampleCavalry(UnitState units, float delta)
    {
        cavalryDust.Sample(units, delta, this);
        treads ??= new TreadMarks(this);
        treads.Sample(units, delta);
    }

    // =========================================================
    // Scan only the small structure roster at a low cadence, not the army grid.
    internal void SampleDamagedBuildings(System.Collections.Generic.IReadOnlyList<StructureHealth> buildings,
        TerrainHeightField terrain, float delta)
    {
        if (buildings == null || buildings.Count == 0) return;
        smokeCooldown -= delta;
        if (smokeCooldown > 0f) return;
        smokeCooldown = 0.7f;
        int emitted = 0;
        for (int n = 0; n < buildings.Count; n++)
        {
            int slot = (smokeCursor + n) % buildings.Count;
            var building = buildings[slot];
            if (building == null || building.Destroyed || building.Health > building.MaxHealth * 0.25f) continue;
            Vector3 point = BuildingGround(building, terrain);
            float size = BuildingRadius(building);
            buildingSmoke ??= new MuzzleSmoke(this, MuzzleSmoke.DefaultWind);
            Vector3 vent = point + Vector3.Up * Mathf.Clamp(size * 0.45f, 0.65f, 2.5f);
            buildingSmoke.Emit(vent, Mathf.Clamp(size * 0.35f, 0.65f, 1.8f), dark: true);
            // Occasional short electrical glints reuse the melee contact pool.
            if ((slot + Mathf.FloorToInt(point.X + point.Z)) % 3 == 0)
            {
                meleeEffects ??= new MeleeEffects(this);
                meleeEffects.Emit(vent + Vector3.Right * size * 0.20f, Vector2.Zero, Mathf.Clamp(size * 0.35f, 0.7f, 1.8f), 0);
            }
            if (++emitted >= 12)
            {
                smokeCursor = (slot + 1) % buildings.Count;
                return;
            }
        }
    }

    private static Vector3 BuildingGround(StructureHealth building, TerrainHeightField terrain)
    {
        Vector2 center = (building.Area.Min + building.Area.Max) * 0.5f;
        return new Vector3(center.X, terrain?.At(center.X, center.Y) ?? 0f, center.Y);
    }

    private static float BuildingRadius(StructureHealth building) =>
        Mathf.Clamp((building.Area.Max - building.Area.Min).Length() * 0.55f, 2f, 9f);

    // One event from the existing removal lifecycle; no damage or wreck obstacle.
    internal void EmitBuildingDestruction(StructureHealth building, TerrainHeightField terrain)
    {
        Vector3 ground = BuildingGround(building, terrain);
        float radius = BuildingRadius(building);
        Vector3 burst = ground + Vector3.Up * Mathf.Clamp(radius * 0.32f, 0.5f, 2f);
        Emit(ground, radius, burst, structureChips: true);
        EmitScorch(ground, radius, terrain);
        buildingSmoke ??= new MuzzleSmoke(this, MuzzleSmoke.DefaultWind);
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.Tau / 3f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * 0.25f;
            buildingSmoke.Emit(burst + offset, Mathf.Clamp(radius * 0.5f, 1f, 3f), dark: true);
        }
    }

    internal void EmitCavalryDust(Vector3 position, float size)
    {
        meleeEffects ??= new MeleeEffects(this);
        meleeEffects.Emit(position, Vector2.Zero, size, 2);
    }

    public void Advance(float dt)
    {
        meleeEffects?.Advance(dt);
        scorchMarks?.Advance(dt);
        treads?.Advance(dt);
        buildingSmoke?.Advance(dt);
        if (count == 0) return;
        dirty = true;
        for (int i = count - 1; i >= 0; i--)
        {
            float age = rings[i].Age + dt;
            if (age >= Duration) rings[i] = rings[--count];
            else rings[i] = rings[i] with { Age = age };
        }
    }

    public void Sync(UnitState units = null)
    {
        SyncRallyMarkers(units);
        meleeEffects?.Sync();
        if (!dirty) return;
        int ringCount = 0, flashCount = 0, chipCount = 0;
        for (int i = 0; i < count; i++)
        {
            Ring ring = rings[i];
            float progress = ring.Age / Duration;
            Color faction = ring.Faction is byte id ? Factions.Color(id) : Colors.White;
            if (ring.Radius > 0f)
            {
                float radius = ring.Radius * Mathf.Lerp(0.08f, 1f, progress);
                Multimesh.SetInstanceTransform(ringCount, new Transform3D(Basis.Identity.Scaled(new Vector3(radius, 1f, radius)), ring.Origin));
                Color ringColor = ring.Rally ? faction.Lightened(0.25f) : Colors.White.Lerp(faction, 0.22f);
                ringColor.A = 1f - progress * progress;
                Multimesh.SetInstanceColor(ringCount++, ringColor);
            }
            if (ring.BurstOrigin is not Vector3 impact) continue;
            if (ring.StructureChips)
                for (int chip = 0; chip < ChipsPerHit; chip++)
                {
                    float angle = impact.X * 0.42f + impact.Z * 0.17f + chip * 2.4f;
                    Vector3 kick = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 point = impact + kick * (0.24f + progress * (0.9f + chip * 0.12f));
                    point.Y += 0.1f + 2.5f * progress - 2.8f * progress * progress;
                    float size = (0.08f + chip * 0.014f) * (1f - progress * progress);
                    var spin = new Basis(new Vector3(1f, 1f, 0.5f).Normalized(), angle + progress * 7f);
                    chips.SetInstanceTransform(chipCount++, new Transform3D(spin.Scaled(new Vector3(size, size * 0.65f, size)), point));
                }
            if (ring.Radius > 0f)
            {
                float extent = ring.Radius * 0.88f;
                Vector3 center = impact + Vector3.Up * (extent * 0.16f);
                bursts.SetInstanceTransform(flashCount, new Transform3D(Basis.Identity.Scaled(Vector3.One * extent), center));
                float seed = impact.X * 0.173f + impact.Z * 0.271f;
                seed -= Mathf.Floor(seed);
                faction.A = ring.Faction.HasValue ? 1f : 0f;
                bursts.SetInstanceColor(flashCount, faction);
                bursts.SetInstanceCustomData(flashCount++, new Color(progress, seed, 0f, 0f));
            }
        }
        Multimesh.VisibleInstanceCount = ringCount;
        chips.VisibleInstanceCount = chipCount;
        bursts.VisibleInstanceCount = flashCount;
        dirty = false;
    }

    private void SyncRallyMarkers(UnitState units)
    {
        var recipients = units?.Rally?.Recipients;
        if (recipients == null || recipients.Count == 0)
        {
            if (rallyMarkers != null) rallyMarkers.VisibleInstanceCount = 0;
            return;
        }

        const int markerCapacity = 384;
        if (rallyMarkers == null)
        {
            rallyMarkers = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = new BoxMesh { Size = new Vector3(0.26f, 0.035f, 0.09f),
                    Material = new StandardMaterial3D
                    {
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        VertexColorUseAsAlbedo = true
                    }
                },
                InstanceCount = markerCapacity, VisibleInstanceCount = 0
            };
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = rallyMarkers, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }

        int slot = 0;
        foreach (int id in recipients)
        {
            if (units.Health[id] <= 0f) continue;
            Vector3 position = units.Position[id];
            position.Y += 0.065f;
            position.Z += units.Catalog[units.Type[id]].Footprint * 0.8f;
            rallyMarkers.SetInstanceTransform(slot, new Transform3D(Basis.Identity, position));
            rallyMarkers.SetInstanceColor(slot++, Factions.Color(units.Faction[id]).Lightened(0.55f));
            if (slot == markerCapacity) break;
        }
        rallyMarkers.VisibleInstanceCount = slot;
    }

    public void Clear()
    {
        cavalryDust.Clear();
        meleeEffects?.Clear();
        scorchMarks?.Clear();
        treads?.Clear();
        buildingSmoke?.Clear();
        smokeCooldown = 0f;
        smokeCursor = 0;
        count = 0;
        dirty = true;
        Sync();
    }
}
