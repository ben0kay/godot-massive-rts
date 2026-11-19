// Orthographic camera with adjustable battlefield yaw, screen-relative pan and wheel zoom.
using Godot;
namespace MassiveRts;

public partial class IsometricCamera : Camera3D
{
    private Vector3 focus = Vector3.Zero;
    private Vector3 velocity;
    private float targetSize = 180;
    private float yawDegrees = 45f;
    private Vector3 screenRight, screenBack, orbitOffset;

    public float MaximumZoomSize { get; set; } = 2000f;
    public bool MapOverviewEnabled { get; set; }
    public bool TopDown { get; set; }
    public bool SuspendPanWhileEditingText { get; set; }

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal; Size = targetSize; Far = MapOverviewEnabled ? Mathf.Max(6000f, MaximumZoomSize * 2f) : 6000f;
        SetYawDegrees(yawDegrees);
    }

    // =========================================================
    // 0° looks along the world Z axis, while 45° reproduces the original diagonal.
    // Recalculate pan axes only when the user changes camera rotation.
    public void SetYawDegrees(float degrees)
    {
        if (!float.IsFinite(degrees)) return;
        yawDegrees = Mathf.PosMod(degrees, 360f);
        float radians = Mathf.DegToRad(yawDegrees);
        float sine = Mathf.Sin(radians), cosine = Mathf.Cos(radians);
        screenRight = new Vector3(cosine, 0f, -sine);
        screenBack = new Vector3(sine, 0f, cosine);
        // Original offset was (700, 850, 700); preserve radius and viewing pitch.
        orbitOffset = screenBack * 989.9495f + Vector3.Up * 850f;
        Reposition();
    }
    // =========================================================
    public void Frame(float battlefieldSize) { targetSize = Mathf.Clamp(battlefieldSize, 30, Mathf.Min(1800f, MaximumZoomSize)); }
    public void SetOpeningView(Vector3 center, float zoom, float yaw)
    {
        focus = center;
        Frame(zoom);
        Size = targetSize;
        SetYawDegrees(yaw);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (MapOverviewEnabled && input is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.Home)
        {
            focus = velocity = Vector3.Zero;
            targetSize = MaximumZoomSize;
            GetViewport().SetInputAsHandled();
        }
        if (input is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp) targetSize = Mathf.Max(15, targetSize * 0.88f);
            if (mouse.ButtonIndex == MouseButton.WheelDown) targetSize = Mathf.Min(MaximumZoomSize, targetSize / 0.88f);
        }
    }
    // =========================================================
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vector2 input = new((Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0),
            (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0));
        if (SuspendPanWhileEditingText &&
            (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit || GetViewport().GuiGetFocusOwner() is Button))
            input = Vector2.Zero;
        Vector3 desired = (screenRight * input.X + screenBack * input.Y).LimitLength() * Size;
        velocity = velocity.Lerp(desired, 1 - Mathf.Exp(-10 * dt)); focus += velocity * dt;
        Size = Mathf.Lerp(Size, targetSize, 1 - Mathf.Exp(-10 * dt)); Reposition();
    }
    private void Reposition()
    {
        // Move the camera back at overview scales so the near side of the map stays in front of it.
        float distanceScale = MapOverviewEnabled ? Mathf.Max(1f, Size / 1500f) : 1f;
        Position = focus + (TopDown ? Vector3.Up * orbitOffset.Length() : orbitOffset) * distanceScale;
        LookAt(focus, TopDown ? -screenBack : Vector3.Up);
    }
}
