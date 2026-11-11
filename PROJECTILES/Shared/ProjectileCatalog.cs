// Compiles projectile references into small runtime indices, flight data and shared meshes.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public sealed class ProjectileCatalog
{
    private const string BasicArrowPath = "res://PROJECTILES/Types/BasicArrow/BasicArrow.tres";
    private readonly Dictionary<ProjectileDefinition, int> indices = new();
    private readonly List<ProjectileFlight> flights = new();
    private readonly List<Mesh> meshes = new();
    private readonly ProjectileDefinition basicArrow = GD.Load<ProjectileDefinition>(BasicArrowPath);

    private Mesh sharedArrowMesh;
    private readonly float movementScale;

    public ProjectileCatalog(float movementScale) => this.movementScale = movementScale;

    public int Count => flights.Count;
    public ProjectileFlight Flight(int index) => flights[index];
    public Mesh Mesh(int index) => meshes[index];

    public int Register(ProjectileDefinition definition)
    {
        definition ??= basicArrow;
        if (definition == null) throw new InvalidOperationException("The basic arrow definition could not be loaded.");
        if (indices.TryGetValue(definition, out int index)) return index;

        Mesh mesh = definition.VisualMesh ?? (sharedArrowMesh ??= BasicArrowMesh.Create());

        index = flights.Count;
        indices.Add(definition, index);
        flights.Add(new ProjectileFlight(
            (float.IsFinite(definition.Speed) && definition.Speed > 0 ? definition.Speed : 45f) * movementScale,
            float.IsFinite(definition.ArcHeight) ? Mathf.Max(0, definition.ArcHeight) : 0f,
            float.IsFinite(definition.ArcHeightPerDistance) ? Mathf.Clamp(definition.ArcHeightPerDistance, 0, 1) : 0f,
            definition.ObstacleFire, definition.BlocksOnObstacles));
        meshes.Add(mesh);
        return index;
    }
}
