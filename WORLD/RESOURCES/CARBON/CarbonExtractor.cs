// Destructible, worker-constructed deposit attachment that credits the shared Carbon wallet.
using Godot;

namespace MassiveRts;

internal sealed class CarbonExtractor : CommandStructure
{
    private static readonly StandardMaterial3D SharedMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true
    };
    private static Mesh sharedMesh;
    private readonly CarbonExtractorSettings settings;
    private readonly MeshInstance3D model;
    private readonly Label3D progress;
    private float extractedFraction;
    public CarbonDeposit Deposit { get; }
    public float BuildProgress { get; private set; }
    public bool Operational => BuildProgress >= 1f && !Destroyed && !Deposit.Depleted;
    public int CarbonPerSecond => settings.CarbonPerSecond;

    public CarbonExtractor(CarbonDeposit deposit, CarbonExtractorSettings settings, DamageRules rules)
        : base(deposit.Area, settings.Health, Factions.Blue,
            Definition(settings), rules,
            readyView: new Node3D { Name = "CarbonExtractor", Position = deposit.GlobalPosition },
            groundHeight: deposit.GlobalPosition.Y)
    {
        Deposit = deposit;
        this.settings = settings;
        model = new MeshInstance3D { Mesh = sharedMesh ??= MakeMesh(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        float width = Mathf.Min(deposit.Area.Max.X - deposit.Area.Min.X,
            deposit.Area.Max.Y - deposit.Area.Min.Y);
        float size = Mathf.Min(1f, width / 5f);
        model.Scale = Vector3.One * size * 0.7f;
        View.AddChild(model);
        progress = new Label3D
        {
            Position = Vector3.Up * (3.2f * size), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 26, PixelSize = 0.016f, Text = "0%"
        };
        View.AddChild(progress);
    }

    private static BuildingDefinition Definition(CarbonExtractorSettings settings) => new()
    {
        Id = "carbon_extractor",
        DisplayName = "Carbon Extractor",
        Health = settings.Health,
        ArmourClass = settings.ArmourClass,
        Armour = settings.Armour,
        AttacksSoldiers = false
    };

    // Called only for the assigned worker in construction range.
    public void Work(float delta)
    {
        if (Destroyed || BuildProgress >= 1f) return;
        float seconds = Mathf.Max(0.1f, settings.BuildSeconds);
        BuildProgress = Mathf.Min(1f, BuildProgress + delta / seconds);
        float size = Mathf.Min(1f, Mathf.Min(Area.Max.X - Area.Min.X,
            Area.Max.Y - Area.Min.Y) / 5f);
        model.Scale = Vector3.One * size * Mathf.Lerp(0.7f, 1f, BuildProgress);
        progress.Text = $"{BuildProgress:P0}";
        if (BuildProgress >= 1f) progress.Hide();
    }

    // Deposit amount remains authoritative; no floating Carbon objects or hauling.
    public void Extract(float delta, CarbonBalance wallet)
    {
        if (!Operational) return;
        extractedFraction += Mathf.Max(0, settings.CarbonPerSecond) * delta;
        int requested = Mathf.FloorToInt(extractedFraction);
        if (requested <= 0) return;
        extractedFraction -= requested;
        wallet.Credit(Deposit.Extract(requested));
    }

    private static Mesh MakeMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(SharedMaterial);
        void Part(float x, float y, float z, float w, float h, float d, Color color) =>
            FacetedSoldierMeshes.AppendBeveledRectangle(surface,
                new Vector3(x, y, z), new Vector3(w, h, d), color);
        var dark = new Color("#243848");
        var steel = new Color("#42647a");
        var cyan = new Color("#45cae5");
        // An elevated open rig caps the mineral without hiding its faceted rock geometry.
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
            Part(x * 1.52f, 1.26f, z * 1.52f, 0.28f, 2.52f, 0.28f, dark);
        Part(0, 2.48f, 0, 3.55f, 0.20f, 3.55f, steel);
        Part(0, 2.68f, 0, 2.15f, 0.16f, 2.15f, dark);
        Part(0, 2.80f, 0, 1.10f, 0.10f, 1.10f, cyan);
        return surface.Commit();
    }
}
