// Compiles projectile references into small runtime indices, flight data and shared meshes.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public sealed class ProjectileCatalog
{
    internal const string BasicArrowPath = "res://GAMEPLAY/PROJECTILES/Types/BasicArrow/BasicArrow.tres";
    private readonly Dictionary<(ProjectileDefinition, float, float, float, float), int> indices = new();
    private readonly List<ProjectileFlight> flights = new();
    private readonly List<Mesh> meshes = new();
    private readonly List<float> scales = new();
    private readonly List<bool> factionTints = new();
    private readonly List<bool> groundGlows = new();
    private readonly List<Vector3> ricochetSizes = new();
    private readonly ProjectileDefinition basicArrow = GD.Load<ProjectileDefinition>(BasicArrowPath);

    private readonly List<bool> launchEffects = new();
    private readonly List<bool> flightSmoke = new();

    private Mesh sharedArrowMesh;
    private readonly float movementScale;

    public ProjectileCatalog(float movementScale) => this.movementScale = movementScale;

    public int Count => flights.Count;
    public ProjectileFlight Flight(int index) => flights[index];
    public Mesh Mesh(int index) => meshes[index];
    internal float VisualScale(int index) => scales[index];
    public bool TintsByFaction(int index) => factionTints[index];

    internal bool HasGroundGlow => groundGlows.Contains(true);
    internal bool UsesGroundGlow(int index) => groundGlows[index];
    // Zero means disabled. Cache the source mesh dimensions once, not on impact.
    internal Vector3 RicochetSize(int index) => ricochetSizes[index];

    internal bool HasLaunchEffects => launchEffects.Contains(true);
    internal bool UsesLaunchEffects(int index) => launchEffects[index];

    internal bool HasFlightSmoke => flightSmoke.Contains(true);
    internal bool UsesFlightSmoke(int index) => flightSmoke[index];

    public int Register(ProjectileDefinition definition, float scale = 1f, float splash = -1f, float edge = -1f,
        float arcScale = 1f)
    {
        definition ??= basicArrow;
        if (definition == null) throw new InvalidOperationException("The basic arrow definition could not be loaded.");
        scale = float.IsFinite(scale) && scale > 0f ? scale : 1f;
        splash = float.IsFinite(splash) && splash >= 0f ? Mathf.Clamp(splash, 0f, 64f) : -1f;
        edge = float.IsFinite(edge) && edge >= 0f ? Mathf.Clamp(edge, 0f, 1f) : -1f;
        arcScale = float.IsFinite(arcScale) ? Mathf.Clamp(arcScale, 0.5f, 3f) : 1f;
        var key = (definition, scale, splash, edge, arcScale);
        if (indices.TryGetValue(key, out int index)) return index;

        Mesh mesh = definition.VisualMesh ?? (sharedArrowMesh ??= BasicArrowMesh.Create());
        // Select presentation only; keep the original definition and flight parameters.
        if (definition.ResourcePath == BomberPayloadMesh.DefinitionPath &&
            UnitVisualConfig.Load().UseEnhancedBomberBombVisuals)
            mesh = BomberPayloadMesh.Get();

        index = flights.Count;
        indices.Add(key, index);
        flights.Add(new ProjectileFlight(
            (float.IsFinite(definition.Speed) && definition.Speed > 0 ? definition.Speed : 45f) * movementScale,
            float.IsFinite(definition.ArcHeight) ? Mathf.Max(0, definition.ArcHeight) * arcScale : 0f,
            float.IsFinite(definition.ArcHeightPerDistance) ? Mathf.Clamp(definition.ArcHeightPerDistance, 0, 1) * arcScale : 0f,
            definition.ObstacleFire, definition.BlocksOnObstacles,
            splash >= 0f ? splash : definition.ExplodesOnImpact
                ? (float.IsFinite(definition.ExplosionRadius) ? Mathf.Clamp(definition.ExplosionRadius, 0.1f, 64f) : 7f)
                : 0f,
            edge >= 0f ? edge : float.IsFinite(definition.ExplosionEdgeFraction) ? Mathf.Clamp(definition.ExplosionEdgeFraction, 0f, 1f) : 0.3125f));
        meshes.Add(mesh);
        scales.Add(scale * (float.IsFinite(definition.VisualSizeMultiplier)
            ? Mathf.Clamp(definition.VisualSizeMultiplier, 0.1f, 4f) : 1f));
        factionTints.Add(definition.TintByFaction);
        groundGlows.Add(definition.UseGroundGlow);
        ricochetSizes.Add(definition.ShieldRicochetEnabled && flights[index].ExplosionRadius <= 0f
            ? mesh.GetAabb().Size * scales[index] : Vector3.Zero);
        launchEffects.Add(definition.UseLaunchEffects);
        flightSmoke.Add(definition.UseLaunchEffects && definition.UseFlightSmoke);
        return index;
    }
}
