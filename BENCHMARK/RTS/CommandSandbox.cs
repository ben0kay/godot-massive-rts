// Standalone flat-ground scene for testing Fortress Defense squad commands.
using Godot;

namespace MassiveRts;

public partial class CommandSandbox : Node3D
{
    private CommandMovement movement;
    private ArmyRenderer army;

    public override void _Ready()
    {
        var camera = new IsometricCamera();
        AddChild(camera);
        camera.MakeCurrent();
        camera.Frame(30f);
        camera.Size = 30f;
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(5000f, 5000f) },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.25f, 0.26f, 0.29f)
            }
        });

        var settings = GD.Load<SimulationSettings>(SimulationSettings.DefaultPath) ?? new SimulationSettings();
        var catalog = GD.Load<UnitCatalog>("res://UNITS/CATALOG/DefaultUnitCatalog.tres").Compile(settings.EffectiveMovementSpeedMultiplier);
        var units = new UnitState(CommandMovement.SquadSize, catalog) { Count = CommandMovement.SquadSize };
        movement = new CommandMovement(units);
        for (int i = 0; i < units.Count; i++)
        {
            units.Type[i] = RuntimeUnitCatalog.Circle;
            units.Faction[i] = Factions.Blue;
            units.Health[i] = catalog[units.Type[i]].Health;
            units.Target[i] = -1;
            units.Position[i] = movement.Slot(Vector3.Back * -1.5f * movement.Spacing,
                Vector3.Right, CommandMovement.DefaultColumns, i);
            units.Dirty[i] = true;
            units.RegisterSpawn(units.Faction[i], units.Type[i]);
        }

        army = new ArmyRenderer();
        AddChild(army);
        army.Initialize(units, FacetedSoldierMeshes.Create(catalog), null, false);

        var hud = new CanvasLayer();
        AddChild(hud);
        var overlay = new CommandOverlay { View = camera, Units = units, Movement = movement };
        hud.AddChild(overlay);
        var input = new SquadCommandInput();
        input.Initialize(overlay);
        AddChild(input);
        hud.AddChild(new Label
        {
            Position = new Vector2(20f, 16f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "Fortress Defense Command Sandbox\nClick or box-select the squad · Right-click to move · Right-drag to set formation\nWASD or arrows to pan · Wheel to zoom"
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        if (movement != null && movement.Step((float)delta)) army.Sync();
    }
}
