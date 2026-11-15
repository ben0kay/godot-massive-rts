// Shared meshes and runtime state for the Fabricator001 and Citadel in command mode.
using Godot;

namespace MassiveRts;

internal sealed class CommandStructure
{
    public const int FabricatorCells = 3;
    public const int FabricatorCost = 500;
    public const float BuildSeconds = 5f;
    private static readonly Color Cyan = new("#40e5ff");
    private static readonly StandardMaterial3D SolidMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true
    };
    private static Mesh fabricatorMesh, citadelMesh;
    private static QuadMesh productionBarMesh;
    private static Shader productionBarShader;
    private readonly DamageRules rules;
    private readonly float damageTaken;
    public readonly byte Faction;
    public readonly int ArmourClass;
    public readonly ObstacleRectangle Area;
    public readonly Vector3 Center;
    public readonly float MaxHealth;
    public float Health { get; private set; }
    public bool Destroyed => Health <= 0;
    public bool Ready => Built >= 1f;
    public float Built { get; private set; }
    public readonly FabricatorProduction Production;
    public readonly Node3D View = new();
    public Vector3? Rally;
    public bool UnitCapBlocked;
    private readonly MeshInstance3D solid, hologram;
    private readonly StandardMaterial3D ghostMaterial;
    private readonly Label3D progress;
    private readonly MeshInstance3D productionBar;
    private readonly ShaderMaterial productionBarMaterial;
    public readonly System.Func<byte, bool> Release;

    public CommandStructure(ObstacleRectangle area, float health, bool citadel, byte faction,
        BuildingDefinition definition, DamageRules rules, System.Func<CommandStructure, byte, bool> release = null)
    {
        Area = area;
        Faction = faction;
        this.rules = rules;
        ArmourClass = rules.ArmourClass(definition.ArmourClass);
        damageTaken = 1f - (float.IsFinite(definition.Armour) ? Mathf.Clamp(definition.Armour, 0f, 1f) : 0f);
        Vector2 center = (area.Min + area.Max) * 0.5f;
        Center = new Vector3(center.X, 0, center.Y);
        View.Position = Center;
        Health = MaxHealth = health;
        Built = citadel ? 1f : 0;
        if (!citadel)
        {
            Production = new FabricatorProduction();
            if (release != null) Release = type => release(this, type);
        }
        Mesh mesh = citadel ? (citadelMesh ??= MakeMesh(true)) : (fabricatorMesh ??= MakeMesh(false));
        solid = new MeshInstance3D { Mesh = mesh, Visible = citadel };
        if (faction == Factions.Red)
        {
            var material = (StandardMaterial3D)SolidMaterial.Duplicate();
            material.AlbedoColor = new Color(1f, 0.52f, 0.45f);
            solid.MaterialOverride = material;
        }
        ghostMaterial = GhostMaterial();
        hologram = new MeshInstance3D { Mesh = mesh, MaterialOverride = ghostMaterial, Visible = !citadel };
        progress = new Label3D
        {
            Position = Vector3.Up * 3f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 32, PixelSize = 0.025f, Text = "0%", Visible = !citadel
        };
        View.AddChild(solid);
        View.AddChild(hologram);
        View.AddChild(progress);
        if (!citadel)
        {
            productionBarMesh ??= new QuadMesh { Size = new Vector2(3.0f, 0.30f) };
            productionBarShader ??= GD.Load<Shader>("res://WORLD/BUILDINGS/FABRICATION/ProductionBar.gdshader");
            productionBarMaterial = new ShaderMaterial { Shader = productionBarShader };
            productionBar = new MeshInstance3D
            {
                Mesh = productionBarMesh, MaterialOverride = productionBarMaterial, Position = Vector3.Up * 2.65f,
                Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 2f
            };
            View.AddChild(productionBar);
        }

    }

    public void Damage(float amount, int damageType)
    {
        if (!float.IsFinite(amount) || amount <= 0) return;
        Health = Mathf.Max(0, Health - rules.Resolve(amount, damageType, ArmourClass, damageTaken));
        if (Destroyed) View.Hide();
    }

    public void Construct(float delta)
    {
        if (Ready || Destroyed) return;
        Built = Mathf.Min(1, Built + delta / BuildSeconds);
        solid.Visible = true;
        solid.Scale = new Vector3(1, Built, 1);
        ghostMaterial.AlbedoColor = new Color(Cyan.R, Cyan.G, Cyan.B, 0.26f + 0.08f * Mathf.Sin(Built * 60f));
        hologram.Visible = progress.Visible = !Ready;
        if (!Ready)
        {
            string percent = $"{Built:P0}";
            if (progress.Text != percent) progress.Text = percent;
        }
    }

    public void RefreshProductionBar()
    {
        if (!Ready || Destroyed || Production == null) return;
        bool blocked = Production.Status == "Exit Blocked";
        productionBar.Visible = Production.Status is "Producing" or "Releasing" || blocked;
        if (!productionBar.Visible) return;
        productionBarMaterial.SetShaderParameter("progress", blocked || Production.Status == "Releasing" ? 1f : Production.Progress);
        productionBarMaterial.SetShaderParameter("blocked", blocked);
    }

    public static MeshInstance3D MakePreview() => new()
    {
        Mesh = fabricatorMesh ??= MakeMesh(false), MaterialOverride = GhostMaterial(), Visible = false
    };

    private static StandardMaterial3D GhostMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(0.18f, 0.8f, 1f, 0.33f)
    };

    private static Mesh MakeMesh(bool citadel)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(SolidMaterial);
        var dark = new Color("#273644");
        var steel = new Color("#465b6d");
        void Part(float x, float y, float z, float w, float h, float d, Color color) =>
            FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(x, y, z), new Vector3(w, h, d), color);

        if (citadel)
        {
            Part(0, 0.36f, 0, 9.7f, 0.72f, 9.7f, dark);
            Part(0, 1.15f, 0, 7.8f, 1.1f, 7.8f, steel);
            Part(0, 2.1f, 0, 6.4f, 0.8f, 6.4f, new Color("#2679f5"));
            Part(0, 3.1f, 0, 4.6f, 1.4f, 4.6f, dark);
            Part(0, 4.0f, 0, 2.8f, 0.6f, 2.8f, Cyan);
        }
        else
        {
            Part(0, 0.24f, 0, 5.9f, 0.48f, 5.9f, dark);
            Part(0, 0.94f, -0.8f, 5.4f, 1.1f, 3.9f, steel);
            Part(0, 1.65f, -0.6f, 3.1f, 0.55f, 2.8f, Cyan);
            Part(-2, 0.95f, 1.65f, 1.15f, 1.6f, 2.3f, dark);
            Part(2, 0.95f, 1.65f, 1.15f, 1.6f, 2.3f, dark);
            Part(0, 1.75f, 2.1f, 4.7f, 0.40f, 1.1f, steel);
            Part(0, 1.55f, 2.68f, 2.9f, 0.18f, 0.16f, Cyan);
            Part(0, 0.46f, 2.55f, 2.6f, 0.10f, 0.7f, new Color("#317ea2"));
        }
        return surface.Commit();
    }
}
