// Edits draft unit resources, previews their geometry, and runs isolated benchmark tests.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Godot;

namespace MassiveRts;

public partial class RuntimeEntityEditor : Control
{
    private sealed record Draft(UnitDefinition Unit, string Path, byte[] Baseline)
    {
        public readonly Dictionary<PropertyInfo, object> Saved = new();
        public string[] SavedMounts { get; set; } = MountSnapshot(Unit);
        public bool Changed => Saved.Any(field => !Equals(field.Value, field.Key.GetValue(Unit))) ||
            !SavedMounts.SequenceEqual(MountSnapshot(Unit));
    }

    private static readonly PropertyInfo[] EditableFields = typeof(UnitDefinition).GetProperties()
        .Where(field => field.GetCustomAttribute<ExportAttribute>() != null && field.Name != nameof(UnitDefinition.Id)
            && field.Name != nameof(UnitDefinition.RocketMounts)
            && !field.Name.StartsWith("TurretRocket", StringComparison.Ordinal)
            && field.Name != nameof(UnitDefinition.Projectile)).ToArray();

    private static readonly PropertyInfo[] MountFields = typeof(RocketMount).GetProperties()
        .Where(field => field.GetCustomAttribute<ExportAttribute>() != null).ToArray();

    private static string[] MountSnapshot(UnitDefinition unit) =>
        (unit.RocketMounts ?? new()).Select(mount => mount == null ? "" : string.Join("|",
            MountFields.Select(field => field.GetValue(mount) is Resource resource
                ? resource.ResourcePath : field.GetValue(mount)?.ToString()))).ToArray();

    private readonly List<Draft> drafts = new();
    private readonly List<Button> unitOptions = new();
    private Button unitChoice;
    private PopupPanel unitPopup;
    private VBoxContainer unitList;
    private Mesh selectedMesh;
    private VBoxContainer editScreen, fields;
    private SubViewportContainer testScreen;
    private SubViewport testViewport;
    private BenchmarkArena arena;
    private MeshInstance3D model, detectionRing, attackRing;
    private Label detectionLabel;
    private bool showDetection;
    private float detectionRadius, attackRadius;
    private Camera3D previewCamera;
    private Label status;
    private Draft current;
    private Button saveButton;
    private float yaw = 0.6f, pitch = -0.45f, zoom = 1f;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (!LevelEditorSave.CanWrite)
        {
            MenuLayout.Create(this, "Entity Editor", "Open GameEditor.tscn and press F6 in the Godot editor.");
            return;
        }

        var background = new ColorRect { Color = UIMasterConfig.Current.Background, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string edge in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + edge, 12);
        var body = new VBoxContainer();
        margin.AddChild(body);
        var toolbar = new HBoxContainer();
        body.AddChild(toolbar);
        toolbar.AddChild(UIButtonFactory.Create("Game Editor", ReturnToHub));
        toolbar.AddChild(UIButtonFactory.Create("Edit Unit", ShowEditor));
        toolbar.AddChild(UIButtonFactory.Create("Test Battlefield", OpenTest, UIButtonStyle.Primary));
        saveButton = UIButtonFactory.Create("Save to Project", SaveCurrent);
        saveButton.Disabled = true;
        toolbar.AddChild(saveButton);
        status = MenuLayout.Label("", muted: true);
        body.AddChild(status);

        editScreen = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(editScreen);
        editScreen.AddChild(MenuLayout.Label("Entity Editor"));
        BuildUnitPicker();
        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 16);
        editScreen.AddChild(columns);
        var previewPanel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddChild(previewPanel);
        var (preview, world) = CreateViewport();
        previewPanel.AddChild(preview);
        previewPanel.AddChild(MenuLayout.Label("Drag to rotate · Scroll to zoom", muted: true));
        var detection = new CheckButton { Text = "Radii" };
        previewPanel.AddChild(detection);
        detectionLabel = MenuLayout.Label("", muted: true);
        previewPanel.AddChild(detectionLabel);
        detection.Toggled += enabled => { showDetection = enabled; RefreshDetection(); };
        preview.GuiInput += OrbitPreview;
        previewCamera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal };
        world.AddChild(previewCamera);
        previewCamera.MakeCurrent();
        model = new MeshInstance3D();
        world.AddChild(model);
        detectionRing = new MeshInstance3D { Visible = false, Position = Vector3.Up * 0.025f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(detectionRing);
        attackRing = new MeshInstance3D { Visible = false, Position = Vector3.Up * 0.035f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        world.AddChild(attackRing);
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        columns.AddChild(scroll);
        fields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(fields);

        (testScreen, testViewport) = CreateViewport();
        body.AddChild(testScreen);
        testScreen.Hide();
        try
        {
            var catalog = ResourceLoader.Load<UnitCatalog>(BenchmarkArena.UnitCatalogPath, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
            foreach (var source in catalog.Definitions)
            {
                var draft = new Draft((UnitDefinition)source.Duplicate(), source.ResourcePath,
                    File.ReadAllBytes(ProjectSettings.GlobalizePath(source.ResourcePath)));
                draft.Unit.RocketMounts = new Godot.Collections.Array<RocketMount>(
                    source.RocketMounts.Select(mount => mount == null ? null : (RocketMount)mount.Duplicate()));
                foreach (var field in EditableFields) draft.Saved.Add(field, field.GetValue(draft.Unit));
                AddUnitOption(draft);
                drafts.Add(draft);
            }
            if (drafts.Count > 0) SelectUnit(0);
            else status.Text = "The unit catalogue is empty.";
        }
        catch (Exception error)
        {
            drafts.Clear();
            unitChoice.Disabled = true;
            status.Text = error.Message;
        }
    }

    private void BuildUnitPicker()
    {
        unitChoice = new Button
        {
            Text = "Choose unit ▾", Alignment = HorizontalAlignment.Left, ClipText = true,
            CustomMinimumSize = new Vector2(240f, 0f), SizeFlagsHorizontal = SizeFlags.ShrinkBegin, Disabled = true
        };
        editScreen.AddChild(unitChoice);
        unitPopup = new PopupPanel();
        unitChoice.AddChild(unitPopup);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        unitPopup.AddChild(scroll);
        unitList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(unitList);
        unitChoice.Pressed += () =>
        {
            var position = (Vector2I)(unitChoice.GetScreenPosition() + new Vector2(0f, unitChoice.Size.Y));
            int height = Math.Min(400, Mathf.CeilToInt(unitList.GetCombinedMinimumSize().Y) + 12);
            unitPopup.Popup(new Rect2I(position, new Vector2I((int)unitChoice.Size.X, height)));
            unitOptions[drafts.IndexOf(current)].GrabFocus();
        };
        unitPopup.MouseExited += RestoreSelectedMesh;
        unitPopup.PopupHide += RestoreSelectedMesh;
    }

    private void AddUnitOption(Draft draft)
    {
        int index = drafts.Count;
        var option = new Button { Text = draft.Unit.DisplayName, Alignment = HorizontalAlignment.Left, ClipText = true };
        unitList.AddChild(option);
        unitOptions.Add(option);
        option.MouseEntered += () => PreviewUnit(index);
        option.FocusEntered += () => PreviewUnit(index);
        option.Pressed += () =>
        {
            unitPopup.Hide();
            SelectUnit(index);
        };
    }

    private void PreviewUnit(int index)
    {
        var draft = drafts[index];
        if (draft == current)
        {
            RestoreSelectedMesh();
            return;
        }

        model.Mesh = FacetedSoldierMeshes.CreatePlacementPreview(draft.Unit, ArmyRenderer.FactionColor(Factions.Blue));
        detectionRing.Hide();
        attackRing.Hide();
        detectionLabel.Hide();
        FramePreview();
    }

    private void RestoreSelectedMesh()
    {
        if (selectedMesh == null || model.Mesh == selectedMesh) return;
        model.Mesh = selectedMesh;
        detectionRing.Visible = detectionRadius > 0f;
        attackRing.Visible = attackRadius > 0f;
        detectionLabel.Show();
        FramePreview();
    }

    private static (SubViewportContainer Container, SubViewport World) CreateViewport()
    {
        var container = new SubViewportContainer { Stretch = true, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var world = new SubViewport { OwnWorld3D = true, Size = new Vector2I(960, 640),
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
        container.AddChild(world);
        return (container, world);
    }

    private void SelectUnit(int index)
    {
        if (index < 0 || index >= drafts.Count) return;
        current = drafts[index];
        unitChoice.Text = current.Unit.DisplayName + " ▾";
        unitChoice.Disabled = false;
        foreach (var child in fields.GetChildren())
        {
            fields.RemoveChild(child);
            child.QueueFree();
        }
        fields.AddChild(MenuLayout.Label(current.Unit.Id, muted: true));
        foreach (var field in EditableFields)
        {
            bool dreadOnly = field.Name.StartsWith("Turret", StringComparison.Ordinal) || field.Name == nameof(UnitDefinition.HullLength) ||
                field.Name == nameof(UnitDefinition.UseLegacyMiniguns);
            if (dreadOnly && current.Unit.Id != RuntimeUnitCatalog.DreadnaughtId) continue;
            if (field.Name == nameof(UnitDefinition.Shape) && current.Unit.Id == RuntimeUnitCatalog.DreadnaughtId) continue;
            AddField(current, field);
        }
        AddMountFields(current);
        RefreshPreview();
        UpdateStatus();
    }

    private void AddMountFields(Draft draft)
    {
        fields.AddChild(MenuLayout.Label("Weapon mounts"));
        fields.AddChild(UIButtonFactory.Create("Add independent mount", () =>
        {
            draft.Unit.RocketMounts.Add(new RocketMount { IndependentAim = true, ReplacePrimaryWeapon = true, Model = TurretModel.CompactCannon, Position = Vector3.Up * draft.Unit.Height * .5f });
            SelectUnit(drafts.IndexOf(draft));
        }));
        for (int i = 0; i < draft.Unit.RocketMounts.Count; i++)
        {
            int index = i;
            RocketMount mount = draft.Unit.RocketMounts[i];
            if (mount == null) continue;
            fields.AddChild(UIButtonFactory.Create($"Remove mount {i + 1}", () =>
            {
                draft.Unit.RocketMounts.RemoveAt(index);
                SelectUnit(drafts.IndexOf(draft));
            }));
            foreach (var field in MountFields)
                AddField(draft, field, mount);
        }
    }

    private void AddField(Draft draft, PropertyInfo field, RocketMount mount = null)
    {
        object owner = (object)mount ?? draft.Unit;
        var label = Regex.Replace(field.Name, "(?<!^)([A-Z])", " $1");
        var row = new HBoxContainer();
        fields.AddChild(row);
        var title = MenuLayout.Label(label);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(title);
        void Apply(object value)
        {
            field.SetValue(owner, value);
            if (mount != null || field.Name is nameof(UnitDefinition.Width) or nameof(UnitDefinition.Height) or nameof(UnitDefinition.Shape) or
                nameof(UnitDefinition.SizeMultiplier) or nameof(UnitDefinition.HullLength) or nameof(UnitDefinition.FootprintRadius))
                RefreshPreview();
            else if (showDetection) RefreshDetection();
            UpdateStatus();
        }
        object currentValue = field.GetValue(owner);
        if (currentValue is bool flag)
        {
            var check = new CheckBox { ButtonPressed = flag };
            row.AddChild(check);
            check.Toggled += value => Apply(value);
        }
        else if (currentValue is string text)
        {
            var input = new LineEdit { Text = text, CustomMinimumSize = new Vector2(160, 0) };
            row.AddChild(input);
            input.TextChanged += value => Apply(value);
        }
        else if (field.PropertyType.IsEnum)
        {
            var picker = new OptionButton();
            foreach (var value in Enum.GetValues(field.PropertyType))
            {
                if (value is UnitShape.Dreadnaught) continue;
                int id = Convert.ToInt32(value);
                picker.AddItem(value.ToString(), id);
            }
            picker.Select(picker.GetItemIndex(Convert.ToInt32(currentValue)));
            row.AddChild(picker);
            picker.ItemSelected += index => Apply(Enum.ToObject(field.PropertyType, picker.GetItemId((int)index)));
        }
        else if (field.PropertyType == typeof(ProjectileDefinition))
        {
            var path = new LineEdit { Text = (currentValue as Resource)?.ResourcePath ?? "",
                PlaceholderText = "Inherit unit projectile", CustomMinimumSize = new Vector2(240, 0) };
            row.AddChild(path);
            path.TextSubmitted += value =>
            {
                ProjectileDefinition projectile = string.IsNullOrWhiteSpace(value) ? null :
                    ResourceLoader.Load<ProjectileDefinition>(value.Trim());
                if (projectile == null && !string.IsNullOrWhiteSpace(value))
                    status.Text = "Enter a valid projectile resource path.";
                else Apply(projectile);
            };
        }
        else if (currentValue is Vector3 position)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int component = axis;
                var number = Number(row, position[axis], -100, 100, .001);
                number.ValueChanged += value =>
                {
                    var updatedPosition = (Vector3)field.GetValue(owner);
                    updatedPosition[component] = (float)value;
                    Apply(updatedPosition);
                };
            }
        }
        else if (currentValue is Vector4 vector)
        {
            fields.RemoveChild(row);
            row.QueueFree();
            string[] mounts = { "Front left", "Front right", "Rear left", "Rear right" };
            for (int channel = 0; channel < 4; channel++)
            {
                int component = channel;
                var mountRow = new HBoxContainer();
                fields.AddChild(mountRow);
                var mountLabel = MenuLayout.Label($"{label} · {mounts[channel]}");
                mountLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                mountRow.AddChild(mountLabel);
                var number = Number(mountRow, vector[channel], 0, 100, 0.1);
                number.ValueChanged += value =>
                {
                    var ranges = (Vector4)field.GetValue(draft.Unit);
                    ranges[component] = (float)value;
                    Apply(ranges);
                };
            }
        }
        else if (currentValue is int or float)
        {
            bool integer = currentValue is int;
            double value = Convert.ToDouble(currentValue);
            var number = Number(row, value, Math.Min(mount == null ? -1 : -360, value), Math.Max(1000000, value), integer ? 1 : 0.001);
            number.ValueChanged += edited => Apply(integer ? (object)(int)edited : (float)edited);
        }
    }

    private static SpinBox Number(Control parent, double value, double min, double max, double step)
    {
        var number = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value,
            CustomMinimumSize = new Vector2(120, 0) };
        parent.AddChild(number);
        return number;
    }

    private void RefreshPreview()
    {
        selectedMesh = FacetedSoldierMeshes.CreatePlacementPreview(current.Unit, ArmyRenderer.FactionColor(Factions.Blue));
        model.Mesh = selectedMesh;
        RefreshDetection();
    }

    private void RefreshDetection()
    {
        detectionRadius = attackRadius = 0f;
        detectionRing.Visible = attackRing.Visible = false;
        detectionLabel.Text = "";
        if (showDetection && current != null)
        {
            try
            {
                var compiled = DraftCatalog().Compile(1f);
                if (compiled.TryResolve(current.Unit.Id, out byte id))
                {
                    ref readonly var stats = ref compiled[id];
                    float cellSize = new BenchmarkConfig().CellSize;
                    detectionRadius = !stats.TargetsUnits ? 0f : stats.Ranged ? stats.Reach :
                        Mathf.Max(cellSize * 2f, stats.MeleeDetectionReach + compiled.MaxFootprint);
                    attackRadius = stats.TargetsUnits && stats.Ranged ? stats.Reach : 0f;
                    detectionLabel.Text = attackRadius > 0f
                        ? $"Cyan: detection {detectionRadius:0.##} u · Amber dashes: attack {attackRadius:0.##} u"
                        : $"Cyan: local detection {detectionRadius:0.##} u";
                    DrawRadius(detectionRing, detectionRadius, new Color(0.34f, 0.82f, 1f), false);
                    DrawRadius(attackRing, attackRadius, new Color(1f, 0.67f, 0.24f), true);
                }
            }
            catch (Exception)
            {
                detectionLabel.Text = "Detection preview requires a valid unit draft.";
            }
        }
        FramePreview();
    }

    private static void DrawRadius(MeshInstance3D ring, float radius, Color color, bool dashed)
    {
        if (radius <= 0f) return;
        using var lines = new SurfaceTool();
        lines.Begin(Mesh.PrimitiveType.Lines);
        lines.SetMaterial(new StandardMaterial3D
        {
            AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        });
        for (int i = 0; i < 64; i += dashed ? 2 : 1)
        {
            Point(i);
            Point(i + 1);
        }
        ring.Mesh = lines.Commit();
        ring.Visible = true;

        void Point(int i)
        {
            float angle = Mathf.Tau * i / 64f;
            lines.AddVertex(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }

    private void OrbitPreview(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0)
        {
            yaw -= motion.Relative.X * 0.012f;
            pitch = Mathf.Clamp(pitch - motion.Relative.Y * 0.012f, -1.45f, 1.45f);
        }
        else if (input is InputEventMouseButton { Pressed: true } scroll &&
            scroll.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            zoom = Mathf.Clamp(zoom * (scroll.ButtonIndex == MouseButton.WheelUp ? 0.9f : 1.1f), 0.3f, 4f);
        else return;
        FramePreview();
        ((SubViewportContainer)previewCamera.GetViewport().GetParent()).AcceptEvent();
    }

    private void FramePreview()
    {
        float extent = Mathf.Max(0.1f, model.Mesh.GetAabb().Size.Length());
        if (model.Mesh == selectedMesh)
            extent = Mathf.Max(extent, Mathf.Max(detectionRadius, attackRadius) * 1.6f);
        previewCamera.Size = extent * 1.4f * zoom;
        previewCamera.Position = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), -Mathf.Sin(pitch),
            Mathf.Cos(yaw) * Mathf.Cos(pitch)) * (extent * 3f);
        previewCamera.LookAt(Vector3.Zero);
        previewCamera.Far = Mathf.Max(100f, extent * 6f);
    }

    private UnitCatalog DraftCatalog()
    {
        var result = new UnitCatalog();
        foreach (var draft in drafts) result.Definitions.Add((UnitDefinition)draft.Unit.Duplicate());
        return result;
    }

    private void OpenTest()
    {
        if (current == null) return;
        BenchmarkArena next = null;
        try
        {
            var setup = arena == null ? default : arena.CaptureTestSetup();
            next = MenuNavigation.LoadScene("res://BENCHMARK/BenchmarkArena.tscn").Instantiate<BenchmarkArena>();
            next.Config = (BenchmarkConfig)next.Config.Duplicate();
            next.ConfigureEditorTest(DraftCatalog(), setup.Layout, setup.Settings);
        }
        catch (Exception error)
        {
            next?.Free();
            // The previous battlefield stays available so a now-overlapping setup can be adjusted.
            if (arena != null) ShowTest();
            status.Text = error.Message;
            return;
        }
        if (arena != null)
        {
            testViewport.RemoveChild(arena);
            arena.QueueFree();
        }
        arena = next;
        testViewport.AddChild(arena);
        ShowTest();
        status.Text = "Paint both teams, then select Start painted battle. This test uses your draft stats.";
    }

    private void ShowTest()
    {
        editScreen.Hide();
        testScreen.Show();
        arena.ProcessMode = ProcessModeEnum.Inherit;
    }

    private void ShowEditor()
    {
        if (arena != null) arena.ProcessMode = ProcessModeEnum.Disabled;
        testScreen.Hide();
        editScreen.Show();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (current == null) return;
        saveButton.Disabled = !current.Changed;
        status.Text = current.Changed ? $"Unsaved changes · {current.Path}" : current.Path;
    }

    private void SaveCurrent()
    {
        if (current == null || !current.Changed) return;
        var draft = current;
        try
        {
            if (!LevelEditorSave.CanWrite) throw new InvalidOperationException("Project saving requires the Godot editor.");
            var changed = draft.Saved.Where(field => !Equals(field.Value, field.Key.GetValue(draft.Unit)))
                .Select(field => $"{field.Key.Name}: {field.Value} → {field.Key.GetValue(draft.Unit)}");
            string mounts = draft.SavedMounts.SequenceEqual(MountSnapshot(draft.Unit)) ? "" : "\nWeapon mounts updated.";
            Confirm("Save to Project", draft.Path + "\n\n" + string.Join("\n", changed) + mounts, () => Publish(draft));
        }
        catch (Exception error) { status.Text = error.Message; }
    }

    private void Publish(Draft draft)
    {
        string temporary = null;
        try
        {
            DraftCatalog().Compile(1f);
            if (string.IsNullOrWhiteSpace(draft.Unit.DisplayName))
                throw new InvalidOperationException("Enter a display name before saving.");
            if (!draft.Path.StartsWith("res://", StringComparison.Ordinal) ||
                !draft.Path.EndsWith(".tres", StringComparison.OrdinalIgnoreCase) || draft.Path.Contains("::"))
                throw new InvalidOperationException("This unit is not backed by a standalone project .tres file.");

            string destination = ProjectSettings.GlobalizePath(draft.Path);
            if (!File.ReadAllBytes(destination).SequenceEqual(draft.Baseline))
                throw new IOException("The resource changed on disk. Reopen the Game Editor before saving it.");
            temporary = $"{draft.Path.GetBaseDir()}/.entity-edit-{Guid.NewGuid():N}.tres";
            Error result = ResourceSaver.Save(draft.Unit, temporary);
            if (result != Error.Ok) throw new IOException($"Could not stage the unit resource ({result}).");
            var staged = ResourceLoader.Load<UnitDefinition>(temporary, cacheMode: ResourceLoader.CacheMode.Ignore);
            if (staged == null || staged.Id != draft.Unit.Id ||
                EditableFields.Any(field => !Equals(field.GetValue(staged), field.GetValue(draft.Unit))) ||
                !MountSnapshot(staged).SequenceEqual(MountSnapshot(draft.Unit)))
                throw new IOException("The staged resource failed validation.");
            long uid = ResourceLoader.GetResourceUid(draft.Path);
            if (uid != -1 && ResourceSaver.SetUid(temporary, uid) != Error.Ok)
                throw new IOException("Could not preserve the resource UID.");
            if (!File.ReadAllBytes(destination).SequenceEqual(draft.Baseline))
                throw new IOException("The resource changed while saving. Your draft is still open.");
            string stagedPath = ProjectSettings.GlobalizePath(temporary);
            byte[] saved = File.ReadAllBytes(stagedPath);
            File.Move(stagedPath, destination, overwrite: true);
            var updated = draft with { Baseline = saved, SavedMounts = MountSnapshot(draft.Unit) };
            foreach (var field in EditableFields) updated.Saved[field] = field.GetValue(draft.Unit);
            int index = drafts.IndexOf(draft);
            drafts[index] = updated;
            unitOptions[index].Text = draft.Unit.DisplayName;
            if (current == draft)
            {
                current = updated;
                unitChoice.Text = draft.Unit.DisplayName + " ▾";
            }
            UpdateStatus();
            status.Text = $"Saved {draft.Path}";
        }
        catch (Exception error) { status.Text = error.Message; }
        finally
        {
            if (temporary != null && File.Exists(ProjectSettings.GlobalizePath(temporary)))
            {
                try { File.Delete(ProjectSettings.GlobalizePath(temporary)); }
                catch (IOException error) { GD.PushWarning(error.Message); }
                catch (UnauthorizedAccessException error) { GD.PushWarning(error.Message); }
            }
        }
    }

    private void ReturnToHub()
    {
        void Leave() => MenuNavigation.Open(this, GameEditor.ScenePath);
        if (drafts.Any(draft => draft.Changed))
            Confirm("Discard drafts?", "Unsaved unit changes will be lost.", Leave);
        else Leave();
    }

    private void Confirm(string title, string message, Action action)
    {
        var dialog = new ConfirmationDialog { Title = title, DialogText = message };
        AddChild(dialog);
        dialog.Confirmed += () => { dialog.QueueFree(); action(); };
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCentered();
    }
}
