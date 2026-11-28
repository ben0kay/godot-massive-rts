// Exposes unlimited support calls in the Level Editor's Clash sandbox.
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    internal bool EnableSupportTest { get; set; }
    private BattleSupport battleSupport;
    private BattleSupport.Ability? selectedSupport;
    private VBoxContainer supportControls;
    private Label supportStatus;
    private int supportDamageType;

    private void AddSupportControls(CanvasLayer layer)
    {
        var center = new CenterContainer
        {
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetTop = -74, OffsetBottom = -12, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layer.AddChild(center);
        supportControls = new VBoxContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        center.AddChild(supportControls);
        supportStatus = TacticalHudStyle.Label("BLUE SUPPORT · UNLIMITED", 12, TacticalHudStyle.Text, HorizontalAlignment.Center);
        supportControls.AddChild(supportStatus);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        supportControls.AddChild(buttons);
        buttons.AddChild(TacticalHudStyle.Button("Airstrike", "Call a precision airstrike", () => ArmSupport(BattleSupport.Ability.Airstrike)));
        buttons.AddChild(TacticalHudStyle.Button("Repair pod", "Deploy three temporary repair drones", () => ArmSupport(BattleSupport.Ability.Repair)));
    }

    private void ArmSupport(BattleSupport.Ability ability)
    {
        selectedSupport = selectedSupport == ability ? null : ability;
        supportStatus.Text = selectedSupport.HasValue
            ? (ability == BattleSupport.Ability.Airstrike ? "AIRSTRIKE" : "REPAIR POD") + " · CLICK MAP   /   ESC TO CANCEL"
            : "BLUE SUPPORT · UNLIMITED";
    }

    private bool HandleSupportInput(InputEvent input)
    {
        if (selectedSupport is not BattleSupport.Ability ability || supportControls?.Visible != true) return false;
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            ArmSupport(ability);
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return false;
        GetViewport().SetInputAsHandled();
        if (units == null || grid == null || !painter.TryGround(click.Position, out var point) ||
            Mathf.Abs(point.X) > BattleBoundary || Mathf.Abs(point.Z) > BattleBoundary)
        {
            supportStatus.Text = "CHOOSE A POINT INSIDE THE BATTLEFIELD";
            return true;
        }
        if (battleSupport == null)
        {
            battleSupport = new BattleSupport();
            AddChild(battleSupport);
            supportDamageType = catalog.DamageRules.DamageType("Impact");
        }
        if (!battleSupport.Call(ability, units.OnTerrain(point), BattleBoundary))
        {
            supportStatus.Text = "WAIT FOR AN ACTIVE SUPPORT CALL TO FINISH";
            return true;
        }
        selectedSupport = null;
        supportStatus.Text = "BLUE SUPPORT · UNLIMITED";
        return true;
    }

    private void ResetSupport()
    {
        selectedSupport = null;
        if (supportControls != null)
        {
            supportControls.Hide();
            supportStatus.Text = "BLUE SUPPORT · UNLIMITED";
        }
        if (battleSupport == null) return;
        RemoveChild(battleSupport);
        battleSupport.QueueFree();
        battleSupport = null;
    }
}
