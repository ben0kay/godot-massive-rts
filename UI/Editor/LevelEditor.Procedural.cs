// Provides the new-level choice and previews procedural terrain before opening the map.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    // =========================================================
    private void ChooseNewLevel()
    {
        if (selectedCampaignId == null) return;
        var adapter = LevelEditorMode.Supported.Single(entry => entry.CampaignId == selectedCampaignId);
        var dialog = NewLevelDialog("New Level", out var content);
        Button(content, "Blank", () =>
        {
            dialog.Hide();
            dialog.QueueFree();
            Open(null);
        });
        Button(content, "Procedural", () =>
        {
            dialog.Hide();
            dialog.QueueFree();
            ShowProceduralSetup(adapter);
        });
        dialog.PopupCentered();
    }

    private AcceptDialog NewLevelDialog(string title, out VBoxContainer content)
    {
        var dialog = new AcceptDialog { Title = title, Exclusive = true, Transient = true };
        content = new VBoxContainer();
        dialog.AddChild(content);
        interfaceRoot.AddChild(dialog);
        dialog.GetOkButton().Hide();
        dialog.Canceled += dialog.QueueFree;
        return dialog;
    }

    // =========================================================
    private void ShowProceduralSetup(LevelEditorMode adapter)
    {
        var dialog = NewLevelDialog("Procedural Level", out var content);
        var body = new HBoxContainer();
        content.AddChild(body);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(286, 468),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(form);

        Heading(form, "Layout · Temperate", 16);
        var preset = new OptionButton();
        preset.AddItem("Open Plains", (int)ProceduralBattlefield.Layout.OpenPlains);
        preset.AddItem("River Divide", (int)ProceduralBattlefield.Layout.RiverDivide);
        preset.AddItem("Mountain Pass", (int)ProceduralBattlefield.Layout.MountainPass);
        form.AddChild(preset);
        var seedRow = new HBoxContainer();
        form.AddChild(seedRow);
        var seed = Number(seedRow, "Seed", 1, int.MinValue, int.MaxValue);
        seed.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Button(seedRow, "Randomize", () => seed.Value = Random.Shared.Next());
        var size = Number(form, "Map width and depth", 256, ProceduralBattlefield.MinSize, ProceduralBattlefield.MaxSize, 4);
        var relief = Number(form, "Elevation intensity", 9, 0, 24);
        var forest = Number(form, "Forest coverage (%)", 30, 0, 100);
        var rock = Number(form, "Rock density (%)", 40, 0, 100);
        var flat = Number(form, "Flat deployment ends (%) per side", 20, 12, 26);
        var riverOptions = new VBoxContainer();
        form.AddChild(riverOptions);
        var riverWidth = Number(riverOptions, "River width", 16, 8, 40, 4);
        var meander = Number(riverOptions, "River meander (%)", 45, 0, 75, 5);
        var crossings = Number(riverOptions, "Bridge count", 2, 1, 2);
        var bridgeWidth = Number(riverOptions, "Minimum bridge width", 24, 12, 48, 4);

        var previewPanel = new VBoxContainer { CustomMinimumSize = new Vector2(310, 0) };
        body.AddChild(previewPanel);
        var image = new TextureRect { CustomMinimumSize = new Vector2(306, 306),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        previewPanel.AddChild(image);
        var message = Heading(previewPanel, "", 15);
        message.CustomMinimumSize = new Vector2(300, 0);
        message.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        float clearance = 0f;
        foreach (var unit in units)
            clearance = Mathf.Max(clearance, UnitOutline.FromDefinition(unit, UnitOutline.EffectiveSize(unit)).Footprint);
        clearance += WorldPaint.CellSize;

        ProceduralBattlefield.Candidate candidate = null;
        var buttons = new HBoxContainer();
        content.AddChild(buttons);
        Button generateButton = null;
        var previewButton = Button(buttons, "Preview", RefreshPreview);
        generateButton = Button(buttons, "Generate", Generate);
        var cancelButton = Button(buttons, "Cancel", dialog.QueueFree);
        foreach (var button in new[] { previewButton, generateButton, cancelButton })
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        riverOptions.Hide();
        preset.ItemSelected += _ =>
        {
            riverOptions.Visible = preset.GetSelectedId() == (int)ProceduralBattlefield.Layout.RiverDivide;
            Invalidate();
        };
        foreach (var field in new[] { seed, size, relief, forest, rock, flat, riverWidth, meander, crossings, bridgeWidth })
            field.ValueChanged += _ => Invalidate();
        RefreshPreview();
        dialog.PopupCentered();

        void Invalidate()
        {
            candidate = null;
            generateButton.Disabled = true;
            message.Text = "Settings changed. Select Preview to update the map.";
        }

        void RefreshPreview()
        {
            candidate = null;
            try
            {
                var settings = new ProceduralBattlefield.Settings((int)seed.Value, (int)size.Value,
                    (ProceduralBattlefield.Layout)preset.GetSelectedId(), (float)relief.Value, (float)forest.Value / 100f,
                    (float)rock.Value / 100f, (int)riverWidth.Value, (int)crossings.Value,
                    (int)bridgeWidth.Value, (float)meander.Value / 100f, (float)flat.Value / 100f,
                    clearance, !adapter.IsAutoBattler);
                var result = ProceduralBattlefield.Generate(settings);
                ValidateGeneratedPaint(result);
                image.Texture = ImageTexture.CreateFromImage(result.Preview);
                candidate = result;
                message.Text = $"{result.Size} × {result.Size} · Seed {settings.Seed}\n" +
                    $"{result.Paint.ForestCells.Count} forest cells · {result.Paint.RockCells.Count} rock cells · {result.Paint.Bridges.Count} bridges";
            }
            catch (Exception error)
            {
                image.Texture = null;
                message.Text = error.Message;
            }
            generateButton.Disabled = candidate == null;
        }

        void Generate()
        {
            if (candidate == null) return;
            var result = candidate;
            dialog.Hide();
            dialog.QueueFree();
            void OpenGenerated() => OpenMap(adapter, null, result);
            if (dirty) Confirm("Discard unsaved changes?", "Opening the generated map replaces this editing session.", OpenGenerated);
            else OpenGenerated();
        }
    }

    // =========================================================
    private static void ValidateGeneratedPaint(ProceduralBattlefield.Candidate candidate)
    {
        var root = new CommandSandbox { LevelSettings = new LevelDefinition { MapSize = candidate.Size } };
        try
        {
            var world = new WorldPaint { Data = candidate.Paint };
            root.AddChild(world);
            if (candidate.Paint.ForestCells.Count > WorldPaint.MaxPaintedCells ||
                !world.TryMerge(new HashSet<Vector2I>(candidate.Paint.RockCells),
                    new HashSet<Vector2I>(candidate.Paint.WaterCells), out _, out _) ||
                candidate.Paint.Bridges.Any(bridge => !world.BridgeFits(bridge)))
                throw new InvalidOperationException("This terrain exceeds the world-paint budget. Reduce the river, forest or rock settings.");
        }
        finally { root.Free(); }
    }

    private void ApplyGeneratedTerrain(ProceduralBattlefield.Candidate candidate)
    {
        var terrain = map.GetNode<CommandTerrain>("Terrain");
        var before = (terrain.Elevation, paint.Data);
        var after = (candidate.Elevation, candidate.Paint);
        void Apply((Godot.Collections.Dictionary<Vector2I, float> Heights, WorldPaintData Paint) data)
        {
            terrain.Elevation = data.Heights;
            paint.Data = data.Paint;
            RefreshMap(terrainChanged: true);
        }
        Apply(after);
        Remember(() => Apply(before), () => Apply(after));
    }
}
