using CobiHw.App.Settings;

namespace CobiHw.Tests.App;

/// <summary>Appearance editor windows: validation, live preview, Apply and Restore defaults.</summary>
[Collection(AppState.Name)]
public sealed class AppearanceEditorTests : LocalizedTest
{
    [Fact]
    public void Thresholds_AreValidated_AndApplied()
    {
        var editor = Editor(AppearanceGroup.UsageColors);
        ThresholdRowViewModel cpu = editor.Thresholds[0];
        Assert.False(cpu.HasError);
        Assert.False(editor.ApplyCommand.CanExecute(null));

        cpu.WarningText = "80";
        Assert.True(cpu.HasError);
        Assert.Equal("Atenção precisa começar antes de Crítico.", cpu.Error);
        Assert.False(editor.ApplyCommand.CanExecute(null));

        cpu.WarningText = "abc";
        Assert.True(cpu.HasError);

        cpu.WarningText = "0";
        Assert.True(cpu.HasError);

        cpu.WarningText = "60";
        cpu.CriticalText = "101";
        Assert.True(cpu.HasError);

        cpu.CriticalText = "90";
        Assert.False(cpu.HasError);
        Assert.True(editor.ApplyCommand.CanExecute(null));

        AppearancePreview? applied = null;
        editor.Applied += (_, preview) => applied = preview;
        editor.ColorsEnabled = false;
        editor.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.False(applied.Colors.Enabled);
        Assert.Equal(new UsageThresholds(60, 90), applied.Colors.Cpu);
        Assert.Equal(UsageThresholds.Default, applied.Colors.Gpu);
    }

    [Fact]
    public void Thresholds_RestoreDefaults()
    {
        var editor = Editor(AppearanceGroup.UsageColors);
        editor.Thresholds[0].WarningText = "60";
        editor.ColorsEnabled = false;

        editor.RestoreDefaultsCommand.Execute(null);

        Assert.True(editor.ColorsEnabled);
        Assert.Equal("50", editor.Thresholds[0].WarningText);
        Assert.Equal("75", editor.Thresholds[0].CriticalText);
        Assert.False(editor.Thresholds[0].HasError);
    }

    [Fact]
    public void Apply_IsEnabledOnlyWhileSomethingDiffers()
    {
        var editor = Editor(AppearanceGroup.UsageColors);

        editor.Thresholds[0].WarningText = "060";
        Assert.True(editor.ApplyCommand.CanExecute(null));

        editor.Thresholds[0].WarningText = "050";
        Assert.False(editor.ApplyCommand.CanExecute(null));

        editor.Thresholds[0].WarningText = "abc";
        Assert.False(editor.ApplyCommand.CanExecute(null));

        editor.Thresholds[0].WarningText = "50";
        editor.ColorsEnabled = false;
        Assert.True(editor.ApplyCommand.CanExecute(null));

        editor.ColorsEnabled = true;
        Assert.False(editor.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void TextColors_OpensWithDefaultsAndNothingPending()
    {
        var editor = Editor(AppearanceGroup.TextColors);

        Assert.Equal("Cores e texto", editor.Title);
        Assert.True(editor.IsTextColors);
        Assert.False(editor.IsShape);
        Assert.Equal(0, editor.Transparency);
        Assert.Equal(TextWeight.Normal, editor.TextWeight);
        Assert.Equal("#1E1F22", editor.BackgroundColor.Color);
        Assert.Equal(new[] { "Normal", "Atenção", "Crítico" }, editor.LevelColors.Select(color => color.Label));
        Assert.False(editor.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void TextColors_ApplySendsAppearanceAndColors()
    {
        var editor = Editor(AppearanceGroup.TextColors);
        editor.Transparency = 30;
        Assert.True(editor.ApplyCommand.CanExecute(null));

        editor.Transparency = 0;
        editor.TextColor.Color = "#FF0000";
        Assert.True(editor.ApplyCommand.CanExecute(null));

        editor.LevelColors[2].Color = "#0000FF";
        editor.TextWeight = TextWeight.SemiBold;
        editor.Transparency = 25;
        AppearancePreview? applied = null;
        editor.Applied += (_, preview) => applied = preview;
        editor.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.Equal(new AppearanceSettings { Transparency = 25, TextColor = "#FF0000", TextWeight = TextWeight.SemiBold }, applied.Appearance);
        Assert.Equal("#0000FF", applied.Colors.CriticalColor);
        Assert.Equal("#6FD68A", applied.Colors.NormalColor);
    }

    [Fact]
    public void TextColors_RestoreDefaults_TouchesOnlyBackgroundAndText()
    {
        var editor = Editor(AppearanceGroup.TextColors);
        editor.Transparency = 25;
        editor.TextWeight = TextWeight.SemiBold;
        editor.TextColor.Color = "#FF0000";
        editor.LevelColors[2].Color = "#0000FF";

        editor.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(0, editor.Transparency);
        Assert.Equal(TextWeight.Normal, editor.TextWeight);
        Assert.Equal("#E6E6E6", editor.TextColor.Color);
        Assert.Equal("#0000FF", editor.LevelColors[2].Color);
    }

    [Fact]
    public void InvalidSavedColor_OpensAsTheDefault() =>
        Assert.Equal("#E6E6E6", Editor(AppearanceGroup.TextColors, new AppearanceSettings { TextColor = "xyz" }).TextColor.Color);

    [Fact]
    public void Scale_PendingResetAndClamped()
    {
        var editor = Editor(AppearanceGroup.Shape);
        editor.Scale = 130;
        Assert.True(editor.ApplyCommand.CanExecute(null));

        editor.ResetScaleCommand.Execute(null);
        Assert.Equal(100, editor.Scale);
        Assert.False(editor.ApplyCommand.CanExecute(null));

        editor.Scale = 170;
        editor.RestoreDefaultsCommand.Execute(null);
        Assert.Equal(100, editor.Scale);

        Assert.Equal(200, Editor(AppearanceGroup.Shape, new AppearanceSettings { Scale = 999 }).Scale);
    }

    [Fact]
    public void Border_IsLoadedClampedAndApplied()
    {
        var editor = Editor(AppearanceGroup.Shape, new AppearanceSettings { BorderMode = BorderMode.Hover, BorderThickness = 0, BorderTransparency = -5 });
        Assert.Equal(BorderMode.Hover, editor.BorderMode);
        Assert.True(editor.HasBorder);
        Assert.Equal(1, editor.BorderThickness);
        Assert.Equal(0, editor.BorderTransparency);
        Assert.False(editor.ApplyCommand.CanExecute(null));

        Assert.Equal(3, editor.BorderModeOptions.Count);
        Assert.Equal("Ao passar o mouse", editor.BorderModeOptions[2].Label);
        Assert.Equal("Cor da borda", editor.BorderColor.Label);
        Assert.Equal("Tamanho e forma", editor.Title);

        editor.BorderThickness = 4;
        editor.BorderColor.Color = "#FF0000";
        editor.BorderTransparency = 20;
        AppearancePreview? applied = null;
        editor.Applied += (_, preview) => applied = preview;
        editor.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.Equal(BorderMode.Hover, applied.Appearance.BorderMode);
        Assert.Equal(4, applied.Appearance.BorderThickness);
        Assert.Equal("#FF0000", applied.Appearance.BorderColor);
        Assert.Equal(20, applied.Appearance.BorderTransparency);

        editor.BorderMode = BorderMode.Off;
        Assert.False(editor.HasBorder);
    }

    [Fact]
    public void ShapeSummary_DescribesTheBorderAndClampsTheBars()
    {
        Assert.Equal(
            "120% · cantos 8 px · barras 3 px · borda 2 px (ao passar o mouse)",
            AppearanceSummary.Shape(new AppearanceSettings { Scale = 120, CornerRadius = 8, BarRadius = 3, BorderMode = BorderMode.Hover, BorderThickness = 2 }));
        Assert.Contains("barras 3 px", AppearanceSummary.Shape(new AppearanceSettings { BarRadius = 9 }));
        Assert.Contains("barras 0 px", AppearanceSummary.Shape(new AppearanceSettings { BarRadius = -2 }));
    }

    [Fact]
    public void Corners_OpenClamped_AndHintFollowsTheLanguage()
    {
        var editor = Editor(AppearanceGroup.Shape, new AppearanceSettings { CornerRadius = 99 });
        Assert.Equal(30, editor.CornerRadius);
        Assert.Equal(30, editor.MaximumCornerRadius);
        Assert.Contains("(30 px)", editor.CornerRadiusHint);
        Assert.StartsWith("0 deixa", editor.CornerRadiusHint);

        editor.CornerRadius = 8;
        AppearancePreview? applied = null;
        editor.Applied += (_, preview) => applied = preview;
        Assert.True(editor.IsPending);
        Assert.True(editor.ApplyCommand.CanExecute(null));

        editor.ApplyCommand.Execute(null);
        Assert.Equal(8, applied?.Appearance.CornerRadius);

        editor.RestoreDefaultsCommand.Execute(null);
        Assert.Equal(0, editor.CornerRadius);

        SetLanguage("en-US");
        Assert.StartsWith("0 keeps", editor.CornerRadiusHint);
    }

    [Fact]
    public void BarRadius_OpensClamped_PreviewsAndRestores()
    {
        var editor = Editor(AppearanceGroup.Shape, new AppearanceSettings { BarRadius = 7 });
        Assert.Equal(3, editor.BarRadius);
        Assert.Equal(3, editor.MaximumBarRadius);
        Assert.False(editor.ApplyCommand.CanExecute(null));

        var previews = new List<AppearancePreview>();
        editor.Previewed += (_, preview) => previews.Add(preview);
        editor.BarRadius = 1;
        AppearancePreview barPreview = Assert.Single(previews);
        Assert.Equal(1, barPreview.Appearance.BarRadius);
        Assert.True(editor.ApplyCommand.CanExecute(null));

        editor.RestoreDefaultsCommand.Execute(null);
        Assert.Equal(0, editor.BarRadius);
    }

    [Fact]
    public void Preview_FollowsEveryValidChange()
    {
        var editor = Editor(AppearanceGroup.UsageColors);
        var previews = new List<AppearancePreview>();
        editor.Previewed += (_, preview) => previews.Add(preview);
        AppearancePreview? applied = null;
        editor.Applied += (_, preview) => applied = preview;
        Assert.Empty(previews);

        editor.Transparency = 40;
        Assert.Single(previews);
        Assert.Equal(40, previews[0].Appearance.Transparency);
        Assert.Null(applied);
        Assert.True(editor.IsPending);

        editor.Transparency = 40;
        Assert.Single(previews);

        editor.Thresholds[0].WarningText = "abc";
        Assert.Single(previews);
        Assert.False(editor.ApplyCommand.CanExecute(null));

        editor.Thresholds[0].WarningText = "60";
        Assert.Equal(2, previews.Count);
        Assert.Equal(60, previews[1].Colors.Cpu.Warning);
        Assert.Equal(40, previews[1].Appearance.Transparency);

        // With an invalid threshold, other changes still preview with the last valid colors.
        editor.Thresholds[0].WarningText = "abc";
        editor.CornerRadius = 5;
        Assert.Equal(3, previews.Count);
        Assert.Equal(5, previews[2].Appearance.CornerRadius);
        Assert.Equal(60, previews[2].Colors.Cpu.Warning);

        editor.Thresholds[0].WarningText = "50";
        editor.LevelColors[1].Color = "#00FF00";
        Assert.Equal("#00FF00", previews[^1].Colors.WarningColor);
        Assert.Equal(50, previews[^1].Colors.Cpu.Warning);

        int before = previews.Count;
        editor.RestoreDefaultsCommand.Execute(null);
        Assert.Equal(before + 1, previews.Count);
        Assert.Equal("#F0C44C", previews[^1].Colors.WarningColor);
        Assert.Equal(40, previews[^1].Appearance.Transparency);

        editor.ApplyCommand.Execute(null);
        Assert.NotNull(applied);
        Assert.Equal(40, applied.Appearance.Transparency);
        Assert.Equal(5, applied.Appearance.CornerRadius);
        Assert.Equal("#F0C44C", applied.Colors.WarningColor);
    }

    [Fact]
    public void ThresholdError_FollowsTheLanguage()
    {
        SetLanguage("ru-RU");
        var editor = Editor(AppearanceGroup.UsageColors);
        editor.Thresholds[0].WarningText = "90";

        Assert.Equal("«Внимание» должно начинаться раньше, чем «Критично».", editor.Thresholds[0].Error);
    }

    private static AppearanceEditorViewModel Editor(AppearanceGroup group, AppearanceSettings? appearance = null) =>
        new(group, appearance ?? new AppearanceSettings(), new ColorSettings());
}
