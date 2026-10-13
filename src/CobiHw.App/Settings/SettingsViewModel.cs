using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.Core;

namespace CobiHw.App.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly DiskOption AutomaticDisk = new(null, "Automático (o mais ocupado)");

    private readonly ThresholdRowViewModel _cpu;
    private readonly ThresholdRowViewModel _gpu;
    private readonly ThresholdRowViewModel _memory;
    private readonly ThresholdRowViewModel _disk;
    private DisplaySettings _appliedDisplay;
    private ColorSettings _appliedColors;

    public SettingsViewModel(DisplaySettings display, ColorSettings colors, IReadOnlyList<DiskActivity> availableDisks)
    {
        _appliedDisplay = display;
        _appliedColors = colors;

        ColorsEnabled = colors.Enabled;
        _cpu = new ThresholdRowViewModel("CPU", colors.Cpu);
        _gpu = new ThresholdRowViewModel("GPU", colors.Gpu);
        _memory = new ThresholdRowViewModel("RAM", colors.Memory);
        _disk = new ThresholdRowViewModel("Disco", colors.Disk);

        Thresholds = [_cpu, _gpu, _memory, _disk];

        foreach (ThresholdRowViewModel row in Thresholds)
        {
            row.PropertyChanged += OnRowPropertyChanged;
        }

        DiskOptions = BuildDiskOptions(availableDisks, display.DiskNumber);
        LoadDisplay(display);
    }

    public event EventHandler<SavedSettings>? Applied;

    public IReadOnlyList<DiskOption> DiskOptions { get; }

    public ObservableCollection<ThresholdRowViewModel> Thresholds { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisplayError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _showCpu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisplayError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _showGpu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisplayError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _showMemory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisplayError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _showDisk;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisplayError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _showNetwork;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private DiskOption _selectedDisk = AutomaticDisk;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _detailedTooltip;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _colorsEnabled;

    public bool HasDisplayError => !(ShowCpu || ShowGpu || ShowMemory || ShowDisk || ShowNetwork);

    private bool CanApply() =>
        !HasDisplayError &&
        Thresholds.All(row => !row.HasError) &&
        (BuildDisplay() != _appliedDisplay || BuildColors() != _appliedColors);

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        _appliedDisplay = BuildDisplay();
        _appliedColors = BuildColors();

        Applied?.Invoke(this, new SavedSettings(_appliedDisplay, _appliedColors));
        ApplyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        LoadDisplay(new DisplaySettings());
        ColorsEnabled = true;

        foreach (ThresholdRowViewModel row in Thresholds)
        {
            row.Reset(UsageThresholds.Default);
        }
    }

    private DisplaySettings BuildDisplay() => new()
    {
        ShowCpu = ShowCpu,
        ShowGpu = ShowGpu,
        ShowMemory = ShowMemory,
        ShowDisk = ShowDisk,
        ShowNetwork = ShowNetwork,
        DiskNumber = SelectedDisk.Number,
        DetailedTooltip = DetailedTooltip,
    };

    private ColorSettings BuildColors() => new()
    {
        Enabled = ColorsEnabled,
        Cpu = _cpu.ToThresholds(),
        Gpu = _gpu.ToThresholds(),
        Memory = _memory.ToThresholds(),
        Disk = _disk.ToThresholds(),
    };

    private static List<DiskOption> BuildDiskOptions(IReadOnlyList<DiskActivity> availableDisks, int? savedNumber)
    {
        List<DiskOption> options = [AutomaticDisk];
        options.AddRange(availableDisks.Select(disk => new DiskOption(disk.Number, $"Disco {DisplayFormat.DiskLabel(disk)}")));

        if (savedNumber is int number && availableDisks.All(disk => disk.Number != number))
        {
            options.Add(new DiskOption(number, $"Disco {number} (não encontrado)"));
        }

        return options;
    }

    private void LoadDisplay(DisplaySettings display)
    {
        ShowCpu = display.ShowCpu;
        ShowGpu = display.ShowGpu;
        ShowMemory = display.ShowMemory;
        ShowDisk = display.ShowDisk;
        ShowNetwork = display.ShowNetwork;
        SelectedDisk = DiskOptions.First(option => option.Number == display.DiskNumber);
        DetailedTooltip = display.DetailedTooltip;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        ApplyCommand.NotifyCanExecuteChanged();
}

public sealed record DiskOption(int? Number, string Label);
