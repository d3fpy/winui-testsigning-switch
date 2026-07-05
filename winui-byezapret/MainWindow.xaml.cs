using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;

namespace winui_byezapret
{
    public sealed partial class MainWindow : Window
    {
        private DispatcherTimer? _particleTimer;
        private readonly List<ParticleData> _particles = new();
        private readonly Random _rng = new();
        private double _canvasWidth = 800;

        private double _gradientOffset = 0;
        private DispatcherTimer? _gradientTimer;

        private record ParticleData(Ellipse Shape, double SpeedX, double SpeedY);

        [DllImport("kernel32.dll")]
        private static extern int GetOEMCP();

        static MainWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public MainWindow()
        {
            this.InitializeComponent();

            if (MicaController.IsSupported())
                this.SystemBackdrop = new MicaBackdrop();

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            this.Activated += (s, e) =>
            {
                CardEnterAnimation.Begin();
                InfoEnterAnimation.Begin();
            };


            ParticleCanvas.Loaded += (s, e) =>
            {
                _canvasWidth = ParticleCanvas.ActualWidth;
                ParticleCanvas.SizeChanged += (_, _) => _canvasWidth = ParticleCanvas.ActualWidth;
                StartParticles();
            };

            GithubText.Loaded += (s, e) => StartGradientAnimation();

            _ = CheckTestSigningStatusAsync();
        }


        private async Task CheckTestSigningStatusAsync()
        {
            var (isEnabled, debugInfo) = await GetTestSigningStatusElevatedAsync();

            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyTestModeUI(isEnabled);

                FooterText.Text += $"  [DEBUG: {debugInfo}]";

                TestModeToggle.Toggled -= TestModeToggle_Toggled;
                TestModeToggle.IsOn = isEnabled;
                TestModeToggle.Toggled += TestModeToggle_Toggled;
            });
        }

        // <summary>
        /// </summary>
        private async Task<(bool isEnabled, string debugInfo)> GetTestSigningStatusElevatedAsync()
        {
            string tempFile = IOPath.Combine(
                @"C:\Windows\Temp",
                $"testsigning_status_{Guid.NewGuid():N}.txt");

            string debugInfo;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c bcdedit /enum {{current}} > \"{tempFile}\" 2>&1",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                Process? process;
                try
                {
                    process = Process.Start(psi);
                }
                catch (Win32Exception ex)
                {
                    return (false, $"UAC отклонён или ошибка запуска: {ex.Message} (код {ex.NativeErrorCode})");
                }

                if (process == null)
                    return (false, "Process.Start вернул null");

                await process.WaitForExitAsync();
                int exitCode = process.ExitCode;

                await Task.Delay(200);

                if (!IOFile.Exists(tempFile))
                    return (false, $"файл не создан, exitCode={exitCode}, путь={tempFile}");

                Encoding encoding;
                try
                {
                    encoding = Encoding.GetEncoding(GetOEMCP());
                }
                catch
                {
                    // На случай если провайдер кодировок всё равно не сработал —
                    // используем безопасный фолбэк, который точно есть всегда
                    encoding = Encoding.UTF8;
                }

                string output = await IOFile.ReadAllTextAsync(tempFile, encoding);

                if (string.IsNullOrWhiteSpace(output))
                    return (false, $"файл пустой, exitCode={exitCode}");

                foreach (var rawLine in output.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0)
                        continue;

                    if (line.StartsWith("testsigning", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring("testsigning".Length).Trim();
                        bool result = value.Contains("Yes", StringComparison.OrdinalIgnoreCase)
                            || value.Contains("Да", StringComparison.OrdinalIgnoreCase);
                        return (result, $"найдено testsigning='{value}', результат={result}, длина вывода={output.Length}");
                    }
                }

                debugInfo = $"строка testsigning не найдена, длина вывода={output.Length}, первые 80 симв.: {output.Substring(0, Math.Min(80, output.Length)).Replace("\r", "").Replace("\n", "|")}";
            }
            catch (Exception ex)
            {
                debugInfo = $"исключение: {ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
              
            }

            return (false, debugInfo);
        }

        private void ApplyTestModeUI(bool enabled)
        {
            if (enabled)
            {
                StatusText.Text = "ВКЛЮЧЁН";
                StatusText.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                StatusBadge.Background = (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"];
                FooterText.Text = "Статус определён системой: testsigning включён";
            }
            else
            {
                StatusText.Text = "ВЫКЛЮЧЕН";
                StatusText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                StatusBadge.Background = (Brush)Application.Current.Resources["SystemFillColorNeutralBackgroundBrush"];
                FooterText.Text = "Статус определён системой: testsigning выключен";
            }
        }


        private void StartGradientAnimation()
        {
            _gradientTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _gradientTimer.Tick += (s, e) =>
            {
                _gradientOffset += 0.008;
                if (_gradientOffset > 1) _gradientOffset = 0;

                double o = _gradientOffset;
                GradStop1.Color = InterpolateColor(o);
                GradStop2.Color = InterpolateColor((o + 0.33) % 1);
                GradStop3.Color = InterpolateColor((o + 0.66) % 1);
            };
            _gradientTimer.Start();
        }

        private static Color InterpolateColor(double t)
        {
            Color[] colors =
            [
                Color.FromArgb(255, 0, 120, 212),
                Color.FromArgb(255, 138, 43, 226),
                Color.FromArgb(255, 0, 180, 255),
                Color.FromArgb(255, 0, 120, 212),
            ];

            double scaled = t * (colors.Length - 1);
            int i = (int)scaled;
            double f = scaled - i;

            var a = colors[i];
            var b = colors[i + 1];
            return Color.FromArgb(
                255,
                (byte)(a.R + (b.R - a.R) * f),
                (byte)(a.G + (b.G - a.G) * f),
                (byte)(a.B + (b.B - a.B) * f)
            );
        }


        private void StartParticles()
        {
            for (int i = 0; i < 18; i++)
                SpawnParticle(randomY: true);

            _particleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _particleTimer.Tick += UpdateParticles;
            _particleTimer.Start();
        }

        private void SpawnParticle(bool randomY = false)
        {
            double size = _rng.NextDouble() * 3 + 1.5;
            double opacity = _rng.NextDouble() * 0.5 + 0.2;

            Color[] colors =
            [
                Color.FromArgb(255, 0, 120, 212),
                Color.FromArgb(255, 138, 43, 226),
                Color.FromArgb(255, 180, 180, 255),
                Color.FromArgb(255, 255, 255, 255),
            ];

            var color = colors[_rng.Next(colors.Length)];

            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Opacity = opacity,
                Fill = new SolidColorBrush(color)
            };

            double x = _rng.NextDouble() * _canvasWidth;
            double y = randomY ? _rng.NextDouble() * 64 : 64;

            Canvas.SetLeft(ellipse, x);
            Canvas.SetTop(ellipse, y);
            ParticleCanvas.Children.Add(ellipse);

            double speedX = (_rng.NextDouble() - 0.5) * 0.6;
            double speedY = _rng.NextDouble() * 0.8 + 0.3;

            _particles.Add(new ParticleData(ellipse, speedX, speedY));
        }

        private void UpdateParticles(object? sender, object e)
        {
            var toRemove = new List<ParticleData>();

            foreach (var p in _particles)
            {
                double x = Canvas.GetLeft(p.Shape) + p.SpeedX;
                double y = Canvas.GetTop(p.Shape) - p.SpeedY;
                double newOpacity = p.Shape.Opacity - 0.004;

                Canvas.SetLeft(p.Shape, x);
                Canvas.SetTop(p.Shape, y);
                p.Shape.Opacity = newOpacity;

                if (newOpacity <= 0 || y < -5)
                {
                    ParticleCanvas.Children.Remove(p.Shape);
                    toRemove.Add(p);
                }
            }

            foreach (var p in toRemove)
                _particles.Remove(p);

            while (_particles.Count < 18)
                SpawnParticle();
        }

        private async void TestModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                bool turningOn = toggle.IsOn;

                var dialog = new ContentDialog
                {
                    Title = turningOn ? "Тестовый режим включён" : "Тестовый режим выключен",
                    Content = "Для применения изменений необходимо перезагрузить компьютер. Перезагрузить сейчас?",
                    PrimaryButtonText = "Перезагрузить",
                    CloseButtonText = "Позже",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    ApplyTestModeUI(turningOn);
                    FooterText.Text = turningOn
                        ? "Выполнено: bcdedit /set testsigning on"
                        : "Выполнено: bcdedit /set testsigning off";

                    string command = turningOn ? "on" : "off";

                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c bcdedit /set testsigning {command} && shutdown /r /t 5 /c \"Применение настроек тестового режима\"",
                            Verb = "runas",
                            UseShellExecute = true
                        });

                        Application.Current.Exit();
                    }
                    catch (Win32Exception)
                    {
                        ApplyTestModeUI(!turningOn);

                        toggle.Toggled -= TestModeToggle_Toggled;
                        toggle.IsOn = !turningOn;
                        toggle.Toggled += TestModeToggle_Toggled;
                    }
                }
                else
                {
                    toggle.Toggled -= TestModeToggle_Toggled;
                    toggle.IsOn = !turningOn;
                    toggle.Toggled += TestModeToggle_Toggled;
                }
            }
        }
    }
}