using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChaosOrder.Models;
using ChaosOrder.Services;

namespace ChaosOrder.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public const double CanvasSize = 640;
        private static readonly Point Center = new(CanvasSize / 2, CanvasSize / 2 - 10);
        private const double Radius = 260;

        public ObservableCollection<ObservablePoint> Corners { get; } = new();
        public ObservablePoint StartPoint { get; } = new(Center.X, Center.Y);
        public ObservableCollection<DirectionRule> Rules { get; } = new();
        public ObservableCollection<ConfigurationEntry> SavedConfigurations { get; } = new();

        public Array FigureTypeValues { get; } = Enum.GetValues(typeof(FigureType));
        public Array TargetTypeValues { get; } = Enum.GetValues(typeof(DirectionTargetType));

        public List<NamedColor> AvailableColors { get; } = new()
        {
            new NamedColor("Black", Colors.Black),
            new NamedColor("Dark Blue", Colors.DarkBlue),
            new NamedColor("Red", Colors.Red),
            new NamedColor("Green", Colors.ForestGreen),
            new NamedColor("Purple", Colors.Purple),
            new NamedColor("Orange", Colors.DarkOrange),
            new NamedColor("Teal", Colors.Teal),
            new NamedColor("Magenta", Colors.Magenta),
        };

        private readonly Dictionary<ObservablePoint, PropertyChangedEventHandlerRef> _cornerHandlers = new();

        private FigureType _selectedFigureType = FigureType.Triangle;
        public FigureType SelectedFigureType
        {
            get => _selectedFigureType;
            set
            {
                if (SetField(ref _selectedFigureType, value) && value != FigureType.Custom)
                    RegenerateFigure();
            }
        }

        private int _numberOfSimulations = 20000;
        public int NumberOfSimulations
        {
            get => _numberOfSimulations;
            set => SetField(ref _numberOfSimulations, value);
        }

        private NamedColor _selectedColor;
        public NamedColor SelectedColor
        {
            get => _selectedColor;
            set => SetField(ref _selectedColor, value);
        }

        private double _thickness = 2;
        public double Thickness
        {
            get => _thickness;
            set => SetField(ref _thickness, value);
        }

        private PointCollection _polygonPoints = new();
        public PointCollection PolygonPoints
        {
            get => _polygonPoints;
            private set => SetField(ref _polygonPoints, value);
        }

        private string _statusText = "Ready.";
        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetField(ref _isBusy, value);
        }

        private ConfigurationEntry? _selectedConfiguration;
        public ConfigurationEntry? SelectedConfiguration
        {
            get => _selectedConfiguration;
            set => SetField(ref _selectedConfiguration, value);
        }

        public WriteableBitmap SimulationBitmap { get; } =
            new((int)CanvasSize, (int)CanvasSize, 96, 96, PixelFormats.Pbgra32, null);

        public ICommand SimulateCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand AddCornerCommand { get; }
        public ICommand RemoveCornerCommand { get; }
        public ICommand MakeRegularCommand { get; }
        public ICommand AddRuleCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand SaveConfigurationCommand { get; }
        public ICommand LoadConfigurationCommand { get; }
        public ICommand LoadSelectedConfigurationCommand { get; }

        // Raised when the drawing canvas's zoom should snap back to 1x (e.g. after Make Regular).
        // The zoom transform is a view concern, so the view subscribes and resets it itself.
        public event EventHandler? ZoomResetRequested;

        public MainViewModel()
        {
            _selectedColor = AvailableColors[1];

            SimulateCommand = new RelayCommand(async _ => await RunSimulationAsync(), _ => !IsBusy);
            ClearCommand = new RelayCommand(_ => ClearBitmap());
            AddCornerCommand = new RelayCommand(_ => AddCorner());
            RemoveCornerCommand = new RelayCommand(p => RemoveCorner(p as ObservablePoint), _ => Corners.Count > 3);
            MakeRegularCommand = new RelayCommand(_ => MakeRegular(), _ => Corners.Count >= 3);
            AddRuleCommand = new RelayCommand(_ => Rules.Add(new DirectionRule()));
            RemoveRuleCommand = new RelayCommand(p => { if (p is DirectionRule r) Rules.Remove(r); });
            SaveConfigurationCommand = new RelayCommand(_ => SaveConfiguration());
            LoadConfigurationCommand = new RelayCommand(_ => LoadConfigurationFromFile());
            LoadSelectedConfigurationCommand = new RelayCommand(async _ => await LoadSelectedConfigurationAsync(), _ => SelectedConfiguration != null);

            Corners.CollectionChanged += Corners_CollectionChanged;
            RegenerateFigure();

            Rules.Add(new DirectionRule { TargetType = DirectionTargetType.Corners, Step = "1/2", Weight = 1 });

            ClearBitmap();
            LoadConfigurationsFromDisk();
        }

        private void Corners_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var kv in _cornerHandlers)
                    kv.Key.PropertyChanged -= kv.Value.Handler;
                _cornerHandlers.Clear();
            }
            else
            {
                if (e.OldItems != null)
                    foreach (ObservablePoint p in e.OldItems)
                        if (_cornerHandlers.TryGetValue(p, out var h))
                        {
                            p.PropertyChanged -= h.Handler;
                            _cornerHandlers.Remove(p);
                        }

                if (e.NewItems != null)
                    foreach (ObservablePoint p in e.NewItems)
                    {
                        var wrapper = new PropertyChangedEventHandlerRef((_, __) => UpdatePolygon());
                        p.PropertyChanged += wrapper.Handler;
                        _cornerHandlers[p] = wrapper;
                    }
            }

            UpdatePolygon();
        }

        private void UpdatePolygon()
        {
            var pc = new PointCollection();
            foreach (var c in Corners)
                pc.Add(new Point(c.X, c.Y));
            PolygonPoints = pc;
        }

        private void SetFigureTypeSilently(FigureType type)
        {
            _selectedFigureType = type;
            OnPropertyChanged(nameof(SelectedFigureType));
        }

        private void RegenerateFigure()
        {
            Corners.Clear();
            int sides = SelectedFigureType switch
            {
                FigureType.Triangle => 3,
                FigureType.Square => 4,
                FigureType.Pentagon => 5,
                FigureType.Hexagon => 6,
                _ => 0
            };

            if (sides == 0)
                return;

            foreach (var p in PolygonFactory.CreateRegular(sides, Center, Radius))
                Corners.Add(new ObservablePoint(p.X, p.Y));
        }

        private void AddCorner()
        {
            SetFigureTypeSilently(FigureType.Custom);
            Corners.Add(new ObservablePoint(CanvasSize / 2, CanvasSize / 2));
        }

        private void RemoveCorner(ObservablePoint? p)
        {
            if (p == null || Corners.Count <= 3) return;
            SetFigureTypeSilently(FigureType.Custom);
            Corners.Remove(p);
        }

        private static readonly JsonSerializerOptions ConfigJsonOptions = new() { WriteIndented = true };

        // All saved configurations live in one file, configurations.json, next to the .csproj
        // so it is part of the project and gets checked into source control. Falls back to
        // the user's per-user app-data folder if the project source tree can't be found
        // (e.g. running a published build outside the repo).
        private static readonly string ConfigStorePath = ResolveConfigStorePath();

        private static string ResolveConfigStorePath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && dir.GetFiles("*.csproj").Length == 0)
                dir = dir.Parent;

            string root = dir?.FullName
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChaosOrder");
            return Path.Combine(root, "configurations.json");
        }

        private ChaosConfiguration BuildConfigurationSnapshot() => new()
        {
            FigureType = SelectedFigureType.ToString(),
            Corners = Corners.Select(c => new PointDto { X = c.X, Y = c.Y }).ToList(),
            StartPoint = new PointDto { X = StartPoint.X, Y = StartPoint.Y },
            NumberOfSimulations = NumberOfSimulations,
            ColorName = SelectedColor.Name,
            Thickness = Thickness,
            Rules = Rules.Select(r => new RuleDto
            {
                IsEnabled = r.IsEnabled,
                TargetType = r.TargetType.ToString(),
                N = r.N,
                Step = r.Step,
                Weight = r.Weight
            }).ToList()
        };

        // Short label like "5pt_1/2,2/3_2026-08-18": corner count, the distinct steps of
        // the enabled rules, and today's date.
        private string GenerateShortName()
        {
            var steps = Rules.Where(r => r.IsEnabled)
                              .Select(r => string.IsNullOrWhiteSpace(r.Step) ? "?" : r.Step.Trim())
                              .Distinct();
            string stepsPart = steps.Any() ? string.Join(",", steps) : "none";
            return $"{Corners.Count}pt_{stepsPart}_{DateTime.Now:yyyy-MM-dd}";
        }

        private void SaveConfiguration()
        {
            try
            {
                var entry = new ConfigurationEntry
                {
                    Name = GenerateShortName(),
                    SavedAt = DateTime.Now,
                    Config = BuildConfigurationSnapshot()
                };

                SavedConfigurations.Insert(0, entry);
                PersistConfigurations();
                SelectedConfiguration = entry;
                StatusText = $"Saved configuration \"{entry.Name}\".";
            }
            catch (Exception ex)
            {
                StatusText = $"Save failed: {ex.Message}";
            }
        }

        private void PersistConfigurations()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigStorePath)!);
            var json = JsonSerializer.Serialize(SavedConfigurations.ToList(), ConfigJsonOptions);
            File.WriteAllText(ConfigStorePath, json);
        }

        private void LoadConfigurationsFromDisk()
        {
            try
            {
                if (!File.Exists(ConfigStorePath)) return;
                var list = JsonSerializer.Deserialize<List<ConfigurationEntry>>(File.ReadAllText(ConfigStorePath), ConfigJsonOptions);
                if (list == null) return;

                SavedConfigurations.Clear();
                foreach (var entry in list)
                    SavedConfigurations.Add(entry);
            }
            catch
            {
                // Corrupt or unreadable store file: start with an empty list rather than fail startup.
            }
        }

        private async Task LoadSelectedConfigurationAsync()
        {
            if (SelectedConfiguration == null)
            {
                StatusText = "Select a saved configuration first.";
                return;
            }

            if (ApplyConfiguration(SelectedConfiguration.Config))
                StatusText = $"Loaded \"{SelectedConfiguration.Name}\".";

            await RunSimulationAsync();
        }

        private void LoadConfigurationFromFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Chaos configuration (*.chaos.json;*.json)|*.chaos.json;*.json|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var dto = JsonSerializer.Deserialize<ChaosConfiguration>(File.ReadAllText(dlg.FileName));
                if (dto == null)
                {
                    StatusText = "Load failed: file is empty or invalid.";
                    return;
                }

                if (ApplyConfiguration(dto))
                    StatusText = $"Loaded configuration from {Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex)
            {
                StatusText = $"Load failed: {ex.Message}";
            }
        }

        private bool ApplyConfiguration(ChaosConfiguration dto)
        {
            if (dto.Corners.Count < 3)
            {
                StatusText = "Load failed: configuration has fewer than 3 corners.";
                return false;
            }

            SetFigureTypeSilently(Enum.TryParse<FigureType>(dto.FigureType, out var ft) ? ft : FigureType.Custom);

            Corners.Clear();
            foreach (var p in dto.Corners)
                Corners.Add(new ObservablePoint(p.X, p.Y));

            StartPoint.X = dto.StartPoint.X;
            StartPoint.Y = dto.StartPoint.Y;

            NumberOfSimulations = dto.NumberOfSimulations;
            Thickness = dto.Thickness;
            SelectedColor = AvailableColors.FirstOrDefault(c => c.Name == dto.ColorName) ?? AvailableColors[0];

            Rules.Clear();
            foreach (var r in dto.Rules)
            {
                Rules.Add(new DirectionRule
                {
                    IsEnabled = r.IsEnabled,
                    TargetType = Enum.TryParse<DirectionTargetType>(r.TargetType, out var tt) ? tt : DirectionTargetType.Corners,
                    N = r.N,
                    Step = r.Step,
                    Weight = r.Weight
                });
            }

            ClearBitmap();
            return true;
        }

        // Replaces the current corners with a regular n-gon (same corner count), centered
        // exactly on the canvas and scaled up as large as possible while keeping every
        // corner at least FigurePadding away from the canvas edge. A vertex is always placed
        // straight up, which as a consequence of regular-polygon symmetry means: odd n -> the
        // lowest side is horizontal; even n -> the lowest corner sits on the vertical line
        // through the center.
        private const double FigurePadding = 20;

        private void MakeRegular()
        {
            int n = Corners.Count;
            if (n < 3)
            {
                StatusText = "Need at least 3 corners to make a regular figure.";
                return;
            }

            var canvasCenter = new Point(CanvasSize / 2, CanvasSize / 2);
            double halfSpan = CanvasSize / 2 - FigurePadding;
            double radius = MaxRadiusWithinPadding(n, halfSpan);

            var newPts = PolygonFactory.CreateRegular(n, canvasCenter, radius);
            for (int i = 0; i < n; i++)
            {
                Corners[i].X = newPts[i].X;
                Corners[i].Y = newPts[i].Y;
            }

            StartPoint.X = canvasCenter.X;
            StartPoint.Y = canvasCenter.Y;

            SetFigureTypeSilently(n switch
            {
                3 => FigureType.Triangle,
                4 => FigureType.Square,
                5 => FigureType.Pentagon,
                6 => FigureType.Hexagon,
                _ => FigureType.Custom
            });

            ZoomResetRequested?.Invoke(this, EventArgs.Empty);
            StatusText = $"Converted to a regular {n}-gon.";
        }

        // Largest radius for a vertex-up regular n-gon whose axis-aligned bounding box
        // still fits within +/-halfSpan of its (square) center.
        private static double MaxRadiusWithinPadding(int n, double halfSpan)
        {
            double maxComponent = 0;
            for (int i = 0; i < n; i++)
            {
                double angle = -Math.PI / 2 + i * 2 * Math.PI / n;
                maxComponent = Math.Max(maxComponent, Math.Max(Math.Abs(Math.Cos(angle)), Math.Abs(Math.Sin(angle))));
            }
            return maxComponent > 0 ? halfSpan / maxComponent : halfSpan;
        }

        private async Task RunSimulationAsync()
        {
            if (Corners.Count < 3)
            {
                StatusText = "Need at least 3 corners.";
                return;
            }

            IsBusy = true;
            StatusText = "Running simulation...";

            var cornerPts = Corners.Select(c => c.ToPoint()).ToList();
            var startPt = StartPoint.ToPoint();
            var rules = Rules.ToList();
            int n = NumberOfSimulations;

            var points = await Task.Run(() => ChaosGameEngine.Run(cornerPts, startPt, rules, n));

            DrawPoints(points);
            StatusText = points.Count == 0
                ? "No points plotted — enable at least one rule."
                : $"Done — {points.Count} points plotted.";
            IsBusy = false;
        }

        private void DrawPoints(List<Point> points)
        {
            var bmp = SimulationBitmap;
            int w = bmp.PixelWidth, h = bmp.PixelHeight;

            ClearBitmap();
            if (points.Count == 0) return;

            int dotSize = Math.Max(1, (int)Math.Round(Thickness));
            var c = SelectedColor.Value;
            var dotPixels = new byte[dotSize * dotSize * 4];
            for (int i = 0; i < dotSize * dotSize; i++)
            {
                dotPixels[i * 4 + 0] = c.B;
                dotPixels[i * 4 + 1] = c.G;
                dotPixels[i * 4 + 2] = c.R;
                dotPixels[i * 4 + 3] = c.A;
            }
            int dotStride = dotSize * 4;

            bmp.Lock();
            try
            {
                foreach (var p in points)
                {
                    int x = (int)Math.Round(p.X) - dotSize / 2;
                    int y = (int)Math.Round(p.Y) - dotSize / 2;
                    x = Math.Max(0, Math.Min(Math.Max(0, w - dotSize), x));
                    y = Math.Max(0, Math.Min(Math.Max(0, h - dotSize), y));
                    if (dotSize > w || dotSize > h) continue;
                    bmp.WritePixels(new Int32Rect(x, y, dotSize, dotSize), dotPixels, dotStride, 0);
                }
                bmp.AddDirtyRect(new Int32Rect(0, 0, w, h));
            }
            finally
            {
                bmp.Unlock();
            }
        }

        private void ClearBitmap()
        {
            var bmp = SimulationBitmap;
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            var empty = new byte[w * 4];
            bmp.Lock();
            try
            {
                for (int row = 0; row < h; row++)
                    bmp.WritePixels(new Int32Rect(0, row, w, 1), empty, w * 4, 0);
                bmp.AddDirtyRect(new Int32Rect(0, 0, w, h));
            }
            finally
            {
                bmp.Unlock();
            }
            StatusText = "Ready.";
        }

        // Wraps a closure so we can add/remove the exact same delegate instance from an event.
        private class PropertyChangedEventHandlerRef
        {
            public System.ComponentModel.PropertyChangedEventHandler Handler { get; }
            public PropertyChangedEventHandlerRef(System.ComponentModel.PropertyChangedEventHandler handler) => Handler = handler;
        }
    }
}
