using System;
using System.ComponentModel;
using System.Windows;
using Redbright.Core;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Redbright.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
	public partial class MainWindow : Window
{
		private readonly GammaRampService _gammaService;
		private readonly AppSettings _settings;
		private bool _initialized;
		private bool _capturingHotkey;
		private bool _updatingPauseUi;
		private bool _updatingAutoStartUi;
		private enum HotkeySlot { Both, Brightness, Color }
		private HotkeySlot _captureSlot = HotkeySlot.Both;
		private bool _colorOnlyActive;
		private const int HOTKEY_ID_BOTH = 1;
		private const int HOTKEY_ID_BRIGHT = 2;
		private const int HOTKEY_ID_COLOR = 3;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _contextMenu;
    private Forms.ToolStripMenuItem? _toggleMenuItem;
    private Forms.ToolStripMenuItem? _toggleColorMenuItem;
    private Forms.ToolStripMenuItem? _pauseBrightnessMenuItem;
    private Forms.ToolStripMenuItem? _showMenuItem;
    private Forms.ToolStripMenuItem? _exitMenuItem;
    private bool _allowClose;
    private IntPtr _notifyIconHandle;

		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    extern static bool DestroyIcon(IntPtr handle);

		private const uint WM_HOTKEY = 0x0312;
		private const uint MOD_ALT = 0x0001;
		private const uint MOD_CONTROL = 0x0002;
		private const uint MOD_SHIFT = 0x0004;
		private const uint MOD_WIN = 0x0008;
		private const uint MOD_NOREPEAT = 0x4000;

		public MainWindow(AppSettings settings)
    {
			_settings = settings;
        _gammaService = new GammaRampService();
        InitializeComponent();
        InitializeTrayIcon();
			// Initialize UI from settings
			BrightnessSlider.Value = _settings.PauseBrightness ? 100.0 : _settings.BrightnessPercent;
			BrightnessSlider.IsEnabled = !_settings.PauseBrightness;
			PauseBrightnessCheckBox.IsChecked = _settings.PauseBrightness;
			StartMinimizedCheckBox.IsChecked = _settings.StartMinimizedToTray;
			_updatingAutoStartUi = true;
			AutoStartCheckBox.IsChecked = IsAutoStartEnabled();
			_updatingAutoStartUi = false;
			UpdateHotkeyText();
			if (_settings.RedOnlyActive)
			{
				_gammaService.ApplyRedOnlyBrightness(_settings.PauseBrightness ? 100.0 : _settings.BrightnessPercent);
			}
			else
			{
				_gammaService.ApplyBrightnessOnly(_settings.PauseBrightness ? 100.0 : _settings.BrightnessPercent);
			}
			_initialized = true;
        UpdateMenuTexts();
    }

    private void ReconcileColorState()
    {
        if (_settings.RedOnlyActive != _gammaService.IsRedOnlyActive)
        {
            _settings.RedOnlyActive = _gammaService.IsRedOnlyActive;
            SettingsStorage.Save(_settings);
        }
    }
    private void ToggleRedButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleColorOnly();
        UpdateMenuTexts();
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_gammaService == null || !_initialized || _settings.PauseBrightness) return;
        ReconcileColorState();
        if (_gammaService.IsRedOnlyActive)
        {
            _gammaService.ApplyRedOnlyBrightness(e.NewValue);
        }
        else
        {
            _gammaService.ApplyBrightnessOnly(e.NewValue);
        }
			_settings.BrightnessPercent = e.NewValue;
			SettingsStorage.Save(_settings);
    }

    private void InitializeTrayIcon()
    {
        _contextMenu = new Forms.ContextMenuStrip();
        _toggleColorMenuItem = new Forms.ToolStripMenuItem("Toggle Red", null, (_, __) => { ToggleColorOnly(); UpdateMenuTexts(); });
        _pauseBrightnessMenuItem = new Forms.ToolStripMenuItem("Pause/Unpause Brightness", null, (_, __) => { TogglePauseBrightness(); UpdateMenuTexts(); });
        _toggleMenuItem = new Forms.ToolStripMenuItem("Toggle Red + Brightness", null, (_, __) => { ToggleBothWithPause(); UpdateMenuTexts(); });
        _showMenuItem = new Forms.ToolStripMenuItem("Show", null, (_, __) =>
        {
            if (this.Visibility == Visibility.Visible && this.ShowInTaskbar)
            {
                HideToTray();
            }
            else
            {
                ShowFromTray();
            }
        });
        _exitMenuItem = new Forms.ToolStripMenuItem("Exit", null, (_, __) => { ExitRequested(); });
        _contextMenu.Items.AddRange(new Forms.ToolStripItem[] {
            _toggleColorMenuItem,
            _pauseBrightnessMenuItem,
            _toggleMenuItem,
            new Forms.ToolStripSeparator(),
            _showMenuItem,
            new Forms.ToolStripSeparator(),
            _exitMenuItem });

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Redbright",
            ContextMenuStrip = _contextMenu
        };
        _notifyIcon.DoubleClick += (_, __) => ToggleTrayVisibility();
        _notifyIcon.MouseUp += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                ToggleTrayVisibility();
            }
        };

        // Default Windows behavior: minimize stays on taskbar; do not auto-hide to tray on minimize.
    }

    private Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/assets/icon_crop.png", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                double scale = 1.0;
                try
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    scale = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
                }
                catch { /* fallback to 1.0 */ }
                int target = (int)Math.Max(16, Math.Round(16 * scale));
                using var bmp = new Drawing.Bitmap(streamInfo.Stream);
                using var resized = new Drawing.Bitmap(bmp, new Drawing.Size(target, target));
                _notifyIconHandle = resized.GetHicon();
                return Drawing.Icon.FromHandle(_notifyIconHandle);
            }
        }
        catch
        {
            // fall through
        }
        return Drawing.SystemIcons.Application;
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
               ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
               ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
               ?? string.Empty;
    }

    private static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
        var value = key?.GetValue("Redbright") as string;
        return !string.IsNullOrEmpty(value);
    }

    private static void EnableAutoStart()
    {
        var exe = GetExecutablePath();
        if (string.IsNullOrEmpty(exe)) throw new InvalidOperationException("Cannot determine executable path.");
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key == null) throw new InvalidOperationException("Cannot open HKCU Run key.");
        key.SetValue("Redbright", $"\"{exe}\"");
    }

    private static void DisableAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("Redbright", false);
        }
        catch { }
    }

    // Scheduled Task helper methods removed; using HKCU Run only

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this);
        if (source != null)
        {
            source.AddHook(WndProc);
        }
		// Register hotkeys only after a handle exists
		RegisterConfiguredHotkeys();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case HOTKEY_ID_BOTH:
                    ToggleBothWithPause();
                    UpdateMenuTexts();
                    handled = true;
                    break;
                case HOTKEY_ID_BRIGHT:
                    TogglePauseBrightness();
                    handled = true;
                    break;
                case HOTKEY_ID_COLOR:
                    ToggleColorOnly();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    private void ToggleTrayVisibility()
    {
        if (this.Visibility == Visibility.Visible && this.ShowInTaskbar)
        {
            HideToTray();
        }
        else
        {
            ShowFromTray();
        }
    }

    // Removed old ToggleBoth; unified on ToggleBothWithPause

    private void ToggleBothWithPause()
    {
        if (!_settings.RedOnlyActive)
        {
            if (!_settings.PauseBrightness)
            {
                _settings.SavedBrightnessBeforePause = _settings.BrightnessPercent;
                _settings.PauseBrightness = true;
                _updatingPauseUi = true;
                PauseBrightnessCheckBox.IsChecked = true;
                _updatingPauseUi = false;
                BrightnessSlider.IsEnabled = false;
            }
            BrightnessSlider.Value = 100.0;
            _gammaService.ApplyRedOnlyBrightness(100.0);
            _settings.RedOnlyActive = true;
            _colorOnlyActive = false;
        }
        else
        {
            // Avoid flicker by applying brightness-only directly without restoring first
            _settings.RedOnlyActive = false;
            _colorOnlyActive = false;

            if (_settings.PauseBrightness)
            {
                _settings.PauseBrightness = false;
                _updatingPauseUi = true;
                PauseBrightnessCheckBox.IsChecked = false;
                _updatingPauseUi = false;
                BrightnessSlider.IsEnabled = true;
                BrightnessSlider.Value = _settings.SavedBrightnessBeforePause;
                _gammaService.ApplyBrightnessOnly(_settings.SavedBrightnessBeforePause);
            }
            else
            {
                _gammaService.ApplyBrightnessOnly(BrightnessSlider.Value);
            }
        }
        SettingsStorage.Save(_settings);
    }

	private void TogglePauseBrightness()
	{
		if (_settings.PauseBrightness)
		{
			// Unpause: restore previous brightness, enable slider
			_settings.PauseBrightness = false;
			_updatingPauseUi = true;
			PauseBrightnessCheckBox.IsChecked = false;
			_updatingPauseUi = false;
			BrightnessSlider.IsEnabled = true;
			BrightnessSlider.Value = _settings.SavedBrightnessBeforePause;
			if (_gammaService.IsRedOnlyActive)
			{
				_gammaService.ApplyRedOnlyBrightness(_settings.SavedBrightnessBeforePause);
			}
			else
			{
				_gammaService.ApplyBrightnessOnly(_settings.SavedBrightnessBeforePause);
			}
		}
		else
		{
			// Pause: store current brightness, set to 100 and lock slider
			_settings.SavedBrightnessBeforePause = _settings.BrightnessPercent;
			_settings.PauseBrightness = true;
			_updatingPauseUi = true;
			PauseBrightnessCheckBox.IsChecked = true;
			_updatingPauseUi = false;
			BrightnessSlider.IsEnabled = false;
			BrightnessSlider.Value = 100.0;
			if (_gammaService.IsRedOnlyActive)
			{
				_gammaService.ApplyRedOnlyBrightness(100.0);
			}
			else
			{
				_gammaService.ApplyBrightnessOnly(100.0);
			}
		}
		SettingsStorage.Save(_settings);
	}

	private void ToggleColorOnly()
	{
		if (_colorOnlyActive)
		{
			// Avoid flicker by applying brightness-only directly without restoring first
			_colorOnlyActive = false;
			// Re-apply brightness-only with current effective brightness
			var effectiveBrightness = _settings.PauseBrightness ? 100.0 : _settings.BrightnessPercent;
			_gammaService.ApplyBrightnessOnly(effectiveBrightness);
			_settings.RedOnlyActive = false;
		}
		else
		{
			// Apply red-only honoring current brightness or pause
			var effectiveBrightness = _settings.PauseBrightness ? 100.0 : _settings.BrightnessPercent;
			_gammaService.ApplyRedOnlyBrightness(effectiveBrightness);
			_colorOnlyActive = true;
			_settings.RedOnlyActive = true;
		}
	}

    private void PauseBrightnessCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized || _updatingPauseUi) return;
        if (PauseBrightnessCheckBox.IsChecked == true)
        {
            _settings.SavedBrightnessBeforePause = _settings.BrightnessPercent;
            _settings.PauseBrightness = true;
            BrightnessSlider.IsEnabled = false;
            BrightnessSlider.Value = 100.0;
            if (_gammaService.IsRedOnlyActive)
            {
                _gammaService.ApplyRedOnlyBrightness(100.0);
            }
			else
			{
				_gammaService.ApplyBrightnessOnly(100.0);
			}
        }
        else
        {
            _settings.PauseBrightness = false;
            BrightnessSlider.IsEnabled = true;
            BrightnessSlider.Value = _settings.SavedBrightnessBeforePause;
            if (_gammaService.IsRedOnlyActive)
            {
                _gammaService.ApplyRedOnlyBrightness(_settings.SavedBrightnessBeforePause);
            }
			else
			{
				_gammaService.ApplyBrightnessOnly(_settings.SavedBrightnessBeforePause);
			}
        }
        SettingsStorage.Save(_settings);
    }

    private void UpdateMenuTexts()
    {
        ToggleRedButton.Content = _gammaService.IsRedOnlyActive ? "Restore Normal Colors" : "Turn Screen Red";
        if (_toggleColorMenuItem != null) _toggleColorMenuItem.Text = "Toggle Red";
        if (_toggleMenuItem != null) _toggleMenuItem.Text = "Toggle Red + Brightness";
        if (_pauseBrightnessMenuItem != null) _pauseBrightnessMenuItem.Text = "Pause/Unpause Brightness";
        if (_showMenuItem != null) _showMenuItem.Text = this.Visibility == Visibility.Visible ? "Hide" : "Show";
    }

    public void MinimizeToTrayInitially()
    {
        HideToTray();
    }

    private void ShowFromTray()
    {
        this.ShowInTaskbar = true;
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        UpdateMenuTexts();
    }

    private void HideToTray()
    {
        this.ShowInTaskbar = false;
        this.Hide();
        UpdateMenuTexts();
    }

    private void SetHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyTextBox.Text = "Press shortcut...";
        HotkeyTextBox.Focus();
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        UnregisterAllHotkeys();
        _settings.HotkeyModifiers = 0;
        _settings.HotkeyVirtualKey = 0;
        SettingsStorage.Save(_settings);
        UpdateHotkeyText();
        RegisterConfiguredHotkeys();
    }

    private void ClearHotkeyBrightness_Click(object sender, RoutedEventArgs e)
    {
        UnregisterAllHotkeys();
        _settings.HotkeyBrightnessModifiers = 0;
        _settings.HotkeyBrightnessVirtualKey = 0;
        SettingsStorage.Save(_settings);
        UpdateHotkeyText();
        RegisterConfiguredHotkeys();
    }

    private void ClearHotkeyColor_Click(object sender, RoutedEventArgs e)
    {
        UnregisterAllHotkeys();
        _settings.HotkeyColorModifiers = 0;
        _settings.HotkeyColorVirtualKey = 0;
        SettingsStorage.Save(_settings);
        UpdateHotkeyText();
        RegisterConfiguredHotkeys();
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_capturingHotkey)
        {
            HotkeyTextBox.Text = "Press shortcut...";
        }
    }

    private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = false;
        UpdateHotkeyText();
    }



    private void HotkeyBrightnessTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_capturingHotkey && _captureSlot == HotkeySlot.Brightness)
        {
            HotkeyBrightnessTextBox.Text = "Press shortcut...";
        }
    }

    private void HotkeyBrightnessTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_captureSlot == HotkeySlot.Brightness)
        {
            _capturingHotkey = false;
            UpdateHotkeyText();
        }
    }

    private void HotkeyColorTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_capturingHotkey && _captureSlot == HotkeySlot.Color)
        {
            HotkeyColorTextBox.Text = "Press shortcut...";
        }
    }

    private void HotkeyColorTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_captureSlot == HotkeySlot.Color)
        {
            _capturingHotkey = false;
            UpdateHotkeyText();
        }
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturingHotkey) return;

        var key = (e.Key == Key.System) ? e.SystemKey : e.Key;
        if (key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
        {
            e.Handled = true;
            return;
        }

        uint mods = 0;
        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= MOD_ALT;
        if ((Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))) mods |= MOD_WIN;

        int vk = KeyInterop.VirtualKeyFromKey(key);

        UnregisterAllHotkeys();
        if (_captureSlot == HotkeySlot.Both)
        {
            _settings.HotkeyModifiers = (int)mods;
            _settings.HotkeyVirtualKey = vk;
        }
        else if (_captureSlot == HotkeySlot.Brightness)
        {
            _settings.HotkeyBrightnessModifiers = (int)mods;
            _settings.HotkeyBrightnessVirtualKey = vk;
        }
        else if (_captureSlot == HotkeySlot.Color)
        {
            _settings.HotkeyColorModifiers = (int)mods;
            _settings.HotkeyColorVirtualKey = vk;
        }
        SettingsStorage.Save(_settings);
        RegisterConfiguredHotkeys();

        _capturingHotkey = false;
        UpdateHotkeyText();
        e.Handled = true;
    }

    private void HotkeyBrightnessTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _captureSlot = HotkeySlot.Brightness;
        HotkeyTextBox_PreviewKeyDown(sender, e);
    }

    private void HotkeyColorTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        _captureSlot = HotkeySlot.Color;
        HotkeyTextBox_PreviewKeyDown(sender, e);
    }

    private void UpdateHotkeyText()
    {
        // Both
        if (_settings.HotkeyVirtualKey == 0) HotkeyTextBox.Text = "None";
        else HotkeyTextBox.Text = BuildHotkeyDisplay((uint)_settings.HotkeyModifiers, (uint)_settings.HotkeyVirtualKey);
        // Brightness
        if (HotkeyBrightnessTextBox != null)
        {
            if (_settings.HotkeyBrightnessVirtualKey == 0) HotkeyBrightnessTextBox.Text = "None";
            else HotkeyBrightnessTextBox.Text = BuildHotkeyDisplay((uint)_settings.HotkeyBrightnessModifiers, (uint)_settings.HotkeyBrightnessVirtualKey);
        }
        // Color
        if (HotkeyColorTextBox != null)
        {
            if (_settings.HotkeyColorVirtualKey == 0) HotkeyColorTextBox.Text = "None";
            else HotkeyColorTextBox.Text = BuildHotkeyDisplay((uint)_settings.HotkeyColorModifiers, (uint)_settings.HotkeyColorVirtualKey);
        }
    }

    private static string BuildHotkeyDisplay(uint mods, uint vk)
    {
        System.Collections.Generic.List<string> parts = new();
        if ((mods & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mods & MOD_ALT) != 0) parts.Add("Alt");
        if ((mods & MOD_WIN) != 0) parts.Add("Win");
        try
        {
            var key = KeyInterop.KeyFromVirtualKey((int)vk);
            parts.Add(key.ToString());
        }
        catch
        {
            parts.Add($"VK{vk}");
        }
        return string.Join("+", parts);
    }

    private void RegisterConfiguredHotkeys()
    {
        var source = (HwndSource)PresentationSource.FromVisual(this);
        if (source?.Handle == null) return;
        // Unregister first to avoid duplicates
        UnregisterAllHotkeys();
        if (_settings.HotkeyVirtualKey != 0)
            _ = RegisterHotKey(source.Handle, HOTKEY_ID_BOTH, (uint)_settings.HotkeyModifiers | MOD_NOREPEAT, (uint)_settings.HotkeyVirtualKey);
        if (_settings.HotkeyBrightnessVirtualKey != 0)
            _ = RegisterHotKey(source.Handle, HOTKEY_ID_BRIGHT, (uint)_settings.HotkeyBrightnessModifiers | MOD_NOREPEAT, (uint)_settings.HotkeyBrightnessVirtualKey);
        if (_settings.HotkeyColorVirtualKey != 0)
            _ = RegisterHotKey(source.Handle, HOTKEY_ID_COLOR, (uint)_settings.HotkeyColorModifiers | MOD_NOREPEAT, (uint)_settings.HotkeyColorVirtualKey);
    }

    private void UnregisterAllHotkeys()
    {
        var source = (HwndSource)PresentationSource.FromVisual(this);
        if (source?.Handle == null) return;
        _ = UnregisterHotKey(source.Handle, HOTKEY_ID_BOTH);
        _ = UnregisterHotKey(source.Handle, HOTKEY_ID_BRIGHT);
        _ = UnregisterHotKey(source.Handle, HOTKEY_ID_COLOR);
    }

    private void BeginHotkeyCapture(HotkeySlot slot)
    {
        _capturingHotkey = true;
        _captureSlot = slot;
        switch (slot)
        {
            case HotkeySlot.Both:
                HotkeyTextBox.Text = "Press shortcut...";
                HotkeyTextBox.Focus();
                break;
            case HotkeySlot.Brightness:
                HotkeyBrightnessTextBox.Text = "Press shortcut...";
                HotkeyBrightnessTextBox.Focus();
                break;
            case HotkeySlot.Color:
                HotkeyColorTextBox.Text = "Press shortcut...";
                HotkeyColorTextBox.Focus();
                break;
        }
    }

    private void HotkeyTextBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_settings.HotkeyVirtualKey != 0)
        {
            UnregisterAllHotkeys();
            _settings.HotkeyModifiers = 0;
            _settings.HotkeyVirtualKey = 0;
            SettingsStorage.Save(_settings);
            UpdateHotkeyText();
        }
        BeginHotkeyCapture(HotkeySlot.Both);
    }

    private void HotkeyBrightnessTextBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_settings.HotkeyBrightnessVirtualKey != 0)
        {
            UnregisterAllHotkeys();
            _settings.HotkeyBrightnessModifiers = 0;
            _settings.HotkeyBrightnessVirtualKey = 0;
            SettingsStorage.Save(_settings);
            UpdateHotkeyText();
        }
        BeginHotkeyCapture(HotkeySlot.Brightness);
    }

    private void HotkeyColorTextBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_settings.HotkeyColorVirtualKey != 0)
        {
            UnregisterAllHotkeys();
            _settings.HotkeyColorModifiers = 0;
            _settings.HotkeyColorVirtualKey = 0;
            SettingsStorage.Save(_settings);
            UpdateHotkeyText();
        }
        BeginHotkeyCapture(HotkeySlot.Color);
    }

		private void StartMinimizedCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			_settings.StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true;
			SettingsStorage.Save(_settings);
		}

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingAutoStartUi) return;
        var desired = AutoStartCheckBox.IsChecked == true;
        bool success = false;
        try
        {
            if (desired) EnableAutoStart();
            else DisableAutoStart();
            success = true;
        }
        catch
        {
            success = false;
        }
        if (!success)
        {
            _updatingAutoStartUi = true;
            AutoStartCheckBox.IsChecked = !desired;
            _updatingAutoStartUi = false;
            try
            {
                System.Windows.MessageBox.Show(this,
                    "Could not change autostart via HKCU Run.\n\nTip: Ensure your user can write to HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run.",
                    "Redbright", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { /* ignore UI errors */ }
            return;
        }
        _settings.AutoStart = desired;
        SettingsStorage.Save(_settings);
    }

    private void ExitRequested()
    {
        _allowClose = true;
        this.Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            if (_gammaService.IsRedOnlyActive)
            {
                _gammaService.RestoreOriginal();
            }
            UnregisterAllHotkeys();
            SettingsStorage.Save(_settings);
        }
        finally
        {
            _gammaService.Dispose();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            if (_notifyIconHandle != IntPtr.Zero)
            {
                DestroyIcon(_notifyIconHandle);
                _notifyIconHandle = IntPtr.Zero;
            }
        }
        base.OnClosed(e);
    }
}