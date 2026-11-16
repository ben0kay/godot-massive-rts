// Keeps campaign actions available without drawing controls over the battlefield.
using System;
using System.IO;
using Godot;

namespace MassiveRts;

internal sealed partial class LevelExitInput : Node
{
    private readonly LevelSession session;
    private readonly bool allowTestCompletion;

    public LevelExitInput(LevelSession session, bool allowTestCompletion)
    {
        this.session = session;
        this.allowTestCompletion = allowTestCompletion;
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Echo: true }) return;
        bool testCompletion = allowTestCompletion && input is InputEventKey
            { Pressed: true, Keycode: Key.Escape, ShiftPressed: true, CtrlPressed: false, AltPressed: false, MetaPressed: false };
        if (!testCompletion && !input.IsActionPressed("ui_cancel", exactMatch: true)) return;

        GetViewport().SetInputAsHandled();
        try
        {
            if (testCompletion) session.CompleteCurrentLevel(victory: true);
            MenuNavigation.ReturnToCampaign(this, session.CampaignId);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            GD.PushWarning(error.Message);
        }
    }
}
