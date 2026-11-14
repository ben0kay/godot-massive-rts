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
    public readonly System.Func<byte, bool> Release;

    public CommandStructure(ObstacleRectangle area, float health, bool citadel, System.Func<CommandStructure, byte, bool> release)
    {
        Area = area;
        Vector2 center = (area.Min + area.Max) * 0.5f;
        Center = new Vector3(center.X, 0, center.Y);
        View.Position = Center;
        Health = MaxHealth = health;
        Built = citadel ? 1f : 0;
        if (!citadel)
        {
            Production = new FabricatorProduction();
            Release = type => release(this, type);
        }
        Mesh mesh = citadel ? (citadelMesh ??= MakeMesh(true)) : (fabricatorMesh ??= MakeMesh(false));
        solid = new MeshInstance3D { Mesh = mesh, Visible = citadel };
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
    }

    public void Damage(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0) return;
        Health = Mathf.Max(0, Health - amount);
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
