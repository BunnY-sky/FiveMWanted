using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Threading;
using System.Windows.Forms;

namespace FiveMRazerWanted;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class AppConfig
{
    public string Device { get; set; } = "Auto";
    public string Color12 { get; set; } = "#00008B";
    public string Color12B { get; set; } = "#FFFFFF";
    public bool Blink12 { get; set; } = false;
    public int Blink12Ms { get; set; } = 450;
    public string Color34 { get; set; } = "#8B0000";
    public string Color34B { get; set; } = "#FFFFFF";
    public bool Blink34 { get; set; } = false;
    public int Blink34Ms { get; set; } = 450;
    public string Color5A { get; set; } = "#8B0000";
    public string Color5B { get; set; } = "#00008B";
    public int AlternatingMs { get; set; } = 450;
    public string ProcessName { get; set; } = "";
}

public sealed class MainForm : Form
{
    readonly Label status = new();
    readonly Label wanted = new();
    readonly Button selectProcess = new();
    readonly Label processLabel = new();
    readonly Button reset = new();
    readonly Button test12 = new();
    readonly Button test34 = new();
    readonly Button test5 = new();
    readonly Button applyColors = new();
    readonly Button testOpenRgb = new();
    readonly CheckBox auto = new();
    readonly CheckBox blink12 = new();
    readonly CheckBox blink34 = new();
    readonly NumericUpDown blink12Interval = new();
    readonly NumericUpDown blink34Interval = new();
    readonly NumericUpDown interval = new();
    readonly ComboBox deviceBox = new();
    readonly TextBox hex12 = new();
    readonly TextBox hex12b = new();
    readonly TextBox hex34 = new();
    readonly TextBox hex34b = new();
    readonly TextBox hex5a = new();
    readonly TextBox hex5b = new();
    readonly Panel sw12 = new();
    readonly Panel sw12b = new();
    readonly Panel sw34 = new();
    readonly Panel sw34b = new();
    readonly Panel sw5a = new();
    readonly Panel sw5b = new();
    readonly System.Windows.Forms.Timer timer = new();
    readonly LightingManager lighting = new();
    readonly AppConfig cfg = new();
    readonly ExternalWantedReader wantedReader = new();
    int lastWanted = -1;
    readonly string cfgPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    public MainForm()
    {
        Text = "FiveM Wanted Lighting";
        Width = 900; Height = 720;
        MinimumSize = new Size(900, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 12, 24);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        AutoScroll = true;

        var title = new Label { Text = "FIVEM WANTED LIGHTING", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Left = 22, Top = 18, ForeColor = Color.Magenta };
        var sub = new Label { Text = "Razer • Logitech G • ROCCAT/OpenRGB", AutoSize = true, Left = 24, Top = 54, ForeColor = Color.Gainsboro };
        status.Text = "● Beleuchtung: wird verbunden..."; status.AutoSize = true; status.Left = 24; status.Top = 88;
        testOpenRgb.Text = "OpenRGB testen"; testOpenRgb.Left = 620; testOpenRgb.Top = 78; testOpenRgb.Width = 150; testOpenRgb.Height = 32;
        wanted.Text = "Wanted-Level: --"; wanted.Font = new Font("Segoe UI", 15, FontStyle.Bold); wanted.AutoSize = true; wanted.Left = 24; wanted.Top = 118;

        var devLabel = new Label { Text = "Tastatur", AutoSize = true, Left = 24, Top = 166 };
        deviceBox.Left = 105; deviceBox.Top = 162; deviceBox.Width = 230; deviceBox.DropDownStyle = ComboBoxStyle.DropDownList;
        deviceBox.Items.AddRange(["Auto", "Razer Chroma", "Logitech G", "ROCCAT / OpenRGB"]);
        deviceBox.SelectedIndexChanged += async (_, _) => { cfg.Device = deviceBox.SelectedItem?.ToString() ?? "Auto"; await ConnectLighting(); SaveConfig(); };

        auto.Text = "Automatisch erkennen"; auto.Checked = true; auto.Left = 360; auto.Top = 165; auto.AutoSize = true;

        selectProcess.Text = "FiveM-Prozess auswählen";
        selectProcess.Left = 24; selectProcess.Top = 198; selectProcess.Width = 220; selectProcess.Height = 38;
        selectProcess.Click += (_, _) => SelectGameProcess();
        processLabel.Text = "Prozess: automatisch"; processLabel.AutoSize = true; processLabel.Left = 260; processLabel.Top = 210; processLabel.ForeColor = Color.LightGray;

        reset.Text = "Beleuchtung zurücksetzen"; reset.Left = 24; reset.Top = 198; reset.Width = 220; reset.Height = 38;

        var memInfo = new Label { Text = "Wanted-Level wird extern aus dem laufenden FiveM-GTA-Prozess gelesen – ohne Resource/NUI/Screenshot.", AutoSize = true, Left = 260, Top = 210, ForeColor = Color.LightGray };

        var colorTitle = new Label { Text = "Wanted-Farben", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Left = 24, Top = 292 };

        AddBlinkColorRow("1–2 Sterne", 330, hex12, sw12, hex12b, sw12b, blink12, blink12Interval, test12, "#00008B", "#FFFFFF");
        AddBlinkColorRow("3–4 Sterne", 415, hex34, sw34, hex34b, sw34b, blink34, blink34Interval, test34, "#8B0000", "#FFFFFF");
        AddColorRow("5 Sterne Farbe A", 500, hex5a, sw5a, test5, "#8B0000");
        AddColorRow("5 Sterne Farbe B", 545, hex5b, sw5b, null, "#00008B");

        var intLabel = new Label { Text = "5★ Wechselintervall", AutoSize = true, Left = 24, Top = 592 };
        interval.Left = 155; interval.Top = 588; interval.Minimum = 40; interval.Maximum = 5000; interval.Increment = 10; interval.Value = 450; interval.Width = 80;
        var msLabel = new Label { Text = "ms", AutoSize = true, Left = 240, Top = 592 };
        applyColors.Text = "Farben speichern / anwenden"; applyColors.Left = 350; applyColors.Top = 584; applyColors.Width = 210; applyColors.Height = 36;

        var info = new Label { Text = "0★ = Standard/aus   •   1–2★ = Farbe 1   •   3–4★ = Farbe 2   •   5★ = Farbe A ↔ B\nBei 1–2★ und 3–4★ kann Blinken jeweils separat aktiviert und eine zweite Farbe gewählt werden.\nDie externe Speicherabfrage funktioniert nur, solange der lokale FiveM-GTA-Prozess läuft.", AutoSize = true, Left = 24, Top = 635, ForeColor = Color.LightGray };
        info.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

        Controls.AddRange([title, sub, status, wanted, devLabel, deviceBox, auto, selectProcess, processLabel, reset, memInfo, colorTitle, intLabel, interval, msLabel, applyColors, testOpenRgb, info]);

        testOpenRgb.Click += async (_, _) =>
        {
            testOpenRgb.Enabled = false;
            try
            {
                var result = await lighting.TestOpenRgbAsync();
                MessageBox.Show(this, result, "OpenRGB Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                status.Text = "● Beleuchtung: " + result.Split("\r\n", StringSplitOptions.None)[0];
                status.ForeColor = Color.Lime;
            }
            catch (Exception ex)
            {
                status.Text = "● Beleuchtung: OpenRGB Fehler";
                status.ForeColor = Color.OrangeRed;
                MessageBox.Show(this, ex.Message, "OpenRGB Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { testOpenRgb.Enabled = true; }
        };

        reset.Click += async (_, _) => await lighting.StopAsync();
        applyColors.Click += async (_, _) => { if (ReadColors()) { SaveConfig(); await ApplyCurrentWantedAsync(force: true); } };
        test12.Click += async (_, _) => { if (ReadColors()) { if (cfg.Blink12) await lighting.SetAlternatingAsync(ParseHex(cfg.Color12), ParseHex(cfg.Color12B), cfg.Blink12Ms); else await lighting.SetStaticAsync(ParseHex(cfg.Color12)); } };
        test34.Click += async (_, _) => { if (ReadColors()) { if (cfg.Blink34) await lighting.SetAlternatingAsync(ParseHex(cfg.Color34), ParseHex(cfg.Color34B), cfg.Blink34Ms); else await lighting.SetStaticAsync(ParseHex(cfg.Color34)); } };
        test5.Click += async (_, _) => { if (ReadColors()) await lighting.SetAlternatingAsync(ParseHex(cfg.Color5A), ParseHex(cfg.Color5B), cfg.AlternatingMs); };

        LoadConfig();
        timer.Interval = 120;
        timer.Tick += async (_, _) => await TickAsync();
        timer.Start();
        FormClosed += (_, _) => wantedReader.Dispose();
    }

    void AddColorRow(string labelText, int top, TextBox hex, Panel swatch, Button? test, string defaultHex)
    {
        var label = new Label { Text = labelText, AutoSize = true, Left = 24, Top = top + 4 };
        hex.Left = 135; hex.Top = top; hex.Width = 95; hex.Text = defaultHex; hex.MaxLength = 7;
        swatch.Left = 240; swatch.Top = top; swatch.Width = 32; swatch.Height = 24; swatch.BorderStyle = BorderStyle.FixedSingle;
        var choose = new Button { Text = "Farbe wählen", Left = 280, Top = top - 1, Width = 105, Height = 27 };
        choose.Click += (_, _) => ChooseColor(hex, swatch);
        Controls.AddRange([label, hex, swatch, choose]);
        if (test is not null)
        {
            test.Text = "Test"; test.Left = 395; test.Top = top - 1; test.Width = 70; test.Height = 27;
            Controls.Add(test);
        }
        hex.TextChanged += (_, _) => UpdateSwatch(hex, swatch);
        UpdateSwatch(hex, swatch);
    }

    void AddBlinkColorRow(string labelText, int top, TextBox mainHex, Panel mainSwatch, TextBox blinkHex, Panel blinkSwatch, CheckBox check, NumericUpDown blinkIntervalControl, Button test, string defaultMain, string defaultBlink)
    {
        var label = new Label { Text = labelText, AutoSize = true, Left = 24, Top = top + 4 };

        mainHex.Left = 135; mainHex.Top = top; mainHex.Width = 95; mainHex.Text = defaultMain; mainHex.MaxLength = 7;
        mainSwatch.Left = 240; mainSwatch.Top = top; mainSwatch.Width = 32; mainSwatch.Height = 24; mainSwatch.BorderStyle = BorderStyle.FixedSingle;
        var chooseMain = new Button { Text = "Farbe wählen", Left = 280, Top = top - 1, Width = 105, Height = 27 };
        chooseMain.Click += (_, _) => ChooseColor(mainHex, mainSwatch);

        check.Text = "Blinken"; check.AutoSize = true; check.Left = 395; check.Top = top + 3;

        blinkHex.Left = 480; blinkHex.Top = top; blinkHex.Width = 95; blinkHex.Text = defaultBlink; blinkHex.MaxLength = 7;
        blinkSwatch.Left = 585; blinkSwatch.Top = top; blinkSwatch.Width = 32; blinkSwatch.Height = 24; blinkSwatch.BorderStyle = BorderStyle.FixedSingle;
        var chooseBlink = new Button { Text = "Blinkfarbe", Left = 625, Top = top - 1, Width = 90, Height = 27 };
        chooseBlink.Click += (_, _) => ChooseColor(blinkHex, blinkSwatch);

        var intLabel = new Label { Text = "Intervall", AutoSize = true, Left = 725, Top = top + 4 };
        blinkIntervalControl.Left = 775; blinkIntervalControl.Top = top; blinkIntervalControl.Width = 75;
        blinkIntervalControl.Minimum = 40; blinkIntervalControl.Maximum = 5000; blinkIntervalControl.Increment = 10; blinkIntervalControl.Value = 450;

        test.Text = "Test"; test.Left = 395; test.Top = top + 32; test.Width = 70; test.Height = 27;
        var mainHint = new Label { Text = "Hauptfarbe", AutoSize = true, Left = 135, Top = top + 27, ForeColor = Color.Gray };
        var blinkHint = new Label { Text = "Blinkfarbe", AutoSize = true, Left = 480, Top = top + 27, ForeColor = Color.Gray };

        Controls.AddRange([label, mainHex, mainSwatch, chooseMain, check, blinkHex, blinkSwatch, chooseBlink, intLabel, blinkIntervalControl, test, mainHint, blinkHint]);
        mainHex.TextChanged += (_, _) => UpdateSwatch(mainHex, mainSwatch);
        blinkHex.TextChanged += (_, _) => UpdateSwatch(blinkHex, blinkSwatch);
        UpdateSwatch(mainHex, mainSwatch);
        UpdateSwatch(blinkHex, blinkSwatch);
    }

    void ChooseColor(TextBox hex, Panel swatch)
    {
        using var dlg = new ColorDialog { FullOpen = true };
        if (TryParseHex(hex.Text, out var c)) dlg.Color = c;
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            hex.Text = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            UpdateSwatch(hex, swatch);
        }
    }

    static void UpdateSwatch(TextBox box, Panel panel)
    {
        if (TryParseHex(box.Text, out var c)) panel.BackColor = c;
        else panel.BackColor = Color.Transparent;
    }

    void LoadConfig()
    {
        try
        {
            if (File.Exists(cfgPath))
            {
                var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(cfgPath));
                if (loaded is not null)
                {
                    cfg.Device = loaded.Device;
                    cfg.ProcessName = loaded.ProcessName;
                    cfg.Color12 = loaded.Color12; cfg.Color12B = loaded.Color12B; cfg.Blink12 = loaded.Blink12; cfg.Blink12Ms = loaded.Blink12Ms;
                    cfg.Color34 = loaded.Color34; cfg.Color34B = loaded.Color34B; cfg.Blink34 = loaded.Blink34; cfg.Blink34Ms = loaded.Blink34Ms;
                    cfg.Color5A = loaded.Color5A; cfg.Color5B = loaded.Color5B; cfg.AlternatingMs = loaded.AlternatingMs;
                }
            }
        }
        catch { }
        hex12.Text = cfg.Color12; hex12b.Text = cfg.Color12B; blink12.Checked = cfg.Blink12; blink12Interval.Value = Math.Clamp(cfg.Blink12Ms, 40, 5000);
        hex34.Text = cfg.Color34; hex34b.Text = cfg.Color34B; blink34.Checked = cfg.Blink34; blink34Interval.Value = Math.Clamp(cfg.Blink34Ms, 40, 5000);
        hex5a.Text = cfg.Color5A; hex5b.Text = cfg.Color5B;
        interval.Value = Math.Clamp(cfg.AlternatingMs, 40, 5000);
        var idx = deviceBox.Items.IndexOf(cfg.Device); deviceBox.SelectedIndex = idx >= 0 ? idx : 0;
        processLabel.Text = string.IsNullOrWhiteSpace(cfg.ProcessName) ? "Prozess: automatisch" : $"Prozess: {cfg.ProcessName}";
        wantedReader.SelectedProcessName = cfg.ProcessName;
        _ = ConnectLighting();
    }

    void SaveConfig()
    {
        cfg.Device = deviceBox.SelectedItem?.ToString() ?? "Auto";
        cfg.ProcessName = wantedReader.SelectedProcessName ?? "";
        cfg.Blink12 = blink12.Checked; cfg.Blink12Ms = (int)blink12Interval.Value;
        cfg.Blink34 = blink34.Checked; cfg.Blink34Ms = (int)blink34Interval.Value;
        cfg.AlternatingMs = (int)interval.Value;
        File.WriteAllText(cfgPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
    }

    bool ReadColors()
    {
        if (!TryParseHex(hex12.Text, out _)) { MessageBox.Show("Ungültige Farbe bei 1–2 Sterne.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        if (!TryParseHex(hex12b.Text, out _)) { MessageBox.Show("Ungültige Blinkfarbe bei 1–2 Sterne.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        if (!TryParseHex(hex34.Text, out _)) { MessageBox.Show("Ungültige Farbe bei 3–4 Sterne.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        if (!TryParseHex(hex34b.Text, out _)) { MessageBox.Show("Ungültige Blinkfarbe bei 3–4 Sterne.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        if (!TryParseHex(hex5a.Text, out _)) { MessageBox.Show("Ungültige Farbe 5★ A.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        if (!TryParseHex(hex5b.Text, out _)) { MessageBox.Show("Ungültige Farbe 5★ B.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        cfg.Color12 = NormalizeHex(hex12.Text); cfg.Color12B = NormalizeHex(hex12b.Text);
        cfg.Color34 = NormalizeHex(hex34.Text); cfg.Color34B = NormalizeHex(hex34b.Text);
        cfg.Color5A = NormalizeHex(hex5a.Text); cfg.Color5B = NormalizeHex(hex5b.Text);
        cfg.Blink12 = blink12.Checked; cfg.Blink12Ms = (int)blink12Interval.Value;
        cfg.Blink34 = blink34.Checked; cfg.Blink34Ms = (int)blink34Interval.Value;
        cfg.AlternatingMs = (int)interval.Value;
        return true;
    }

    void SelectGameProcess()
    {
        using var dialog = new ProcessSelectorForm(wantedReader.SelectedProcessName);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedProcess is null) return;

        wantedReader.SelectedProcessName = dialog.SelectedProcess.ProcessName;
        cfg.ProcessName = wantedReader.SelectedProcessName;
        processLabel.Text = $"Prozess: {dialog.SelectedProcess.ProcessName} (PID {dialog.SelectedProcess.Id})";
        wantedReader.Disconnect();
        lastWanted = -1;
        SaveConfig();
    }

    async Task ConnectLighting()
    {
        try
        {
            await lighting.InitializeAsync(cfg.Device);
            status.Text = $"● Beleuchtung: {lighting.ActiveName}";
            status.ForeColor = Color.Lime;
        }
        catch (Exception ex)
        {
            status.Text = $"● Beleuchtung: {ex.Message}";
            status.ForeColor = Color.OrangeRed;
            Debug.WriteLine(ex);
        }
    }

    async Task TickAsync()
    {
        if (!auto.Checked) return;

        try
        {
            int stars = wantedReader.ReadWantedLevel();
            if (stars < 0)
            {
                wanted.Text = "Wanted-Level: --";
                status.Text = wantedReader.StatusText;
                status.ForeColor = Color.Orange;
                return;
            }

            status.Text = $"● {lighting.ActiveName}   •   {wantedReader.StatusText}";
            status.ForeColor = Color.Lime;

            if (stars != lastWanted)
            {
                lastWanted = stars;
                wanted.Text = $"Wanted-Level: {new string('★', stars)}";
                await ApplyCurrentWantedAsync();
            }
        }
        catch (Exception ex)
        {
            status.Text = $"● Externe Abfrage: {ex.Message}";
            status.ForeColor = Color.OrangeRed;
        }
    }

    async Task ApplyCurrentWantedAsync(bool force = false)
    {
        if (!ReadColors()) return;
        if (lastWanted <= 0) await lighting.StopAsync();
        else if (lastWanted <= 2)
        {
            if (cfg.Blink12) await lighting.SetAlternatingAsync(ParseHex(cfg.Color12), ParseHex(cfg.Color12B), cfg.Blink12Ms);
            else await lighting.SetStaticAsync(ParseHex(cfg.Color12));
        }
        else if (lastWanted <= 4)
        {
            if (cfg.Blink34) await lighting.SetAlternatingAsync(ParseHex(cfg.Color34), ParseHex(cfg.Color34B), cfg.Blink34Ms);
            else await lighting.SetStaticAsync(ParseHex(cfg.Color34));
        }
        else await lighting.SetAlternatingAsync(ParseHex(cfg.Color5A), ParseHex(cfg.Color5B), cfg.AlternatingMs);
    }

    static string NormalizeHex(string s) => s.Trim().ToUpperInvariant();
    static bool TryParseHex(string text, out Color color)
    {
        color = Color.Empty;
        string s = text.Trim(); if (s.StartsWith("#")) s = s[1..];
        if (s.Length != 6) return false;
        try { color = Color.FromArgb(Convert.ToInt32(s[0..2],16), Convert.ToInt32(s[2..4],16), Convert.ToInt32(s[4..6],16)); return true; }
        catch { return false; }
    }
    static Color ParseHex(string s) => TryParseHex(s, out var c) ? c : Color.Black;
}

public sealed class ExternalWantedReader : IDisposable
{
    // FiveM's GTA process changes between builds. We therefore locate the World
    // pointer by signature and validate the PlayerInfo/Wanted chain at runtime.
    const uint PROCESS_QUERY_INFORMATION = 0x0400;
    const uint PROCESS_VM_READ = 0x0010;
    const int OFFSET_WORLD_PLAYER = 0x08;
    // World + 0x08 = local CPed. CPed + 0x10B8/0x10C8 = CPlayerInfo.
    static readonly int[] PlayerInfoOffsets = [0x10B8, 0x10C8, 0x10A8];
    // CPlayerInfo::wanted level; builds commonly use 0x868 or 0x888.
    static readonly int[] WantedOffsets = [0x888, 0x868];

    IntPtr process = IntPtr.Zero;
    Process? attached;
    IntPtr moduleBase;
    int moduleSize;
    int playerInfoOffset;
    int wantedOffset;
    IntPtr world;

    public string SelectedProcessName { get; set; } = "";
    public string StatusText { get; private set; } = "FiveM nicht gefunden";

    public void Disconnect() => Close();

    public int ReadWantedLevel()
    {
        try
        {
            if (attached is null || attached.HasExited || process == IntPtr.Zero)
            {
                if (!Attach()) return -1;
            }

            if (world == IntPtr.Zero || playerInfoOffset == 0 || wantedOffset == 0)
            {
                if (!Attach()) return -1;
            }

            // World + 0x08 directly contains the local CPed pointer.
            if (!TryReadPointer(world + OFFSET_WORLD_PLAYER, out var localPlayer) || localPlayer == IntPtr.Zero)
            {
                StatusText = "FiveM-Prozess gefunden, lokaler Spieler noch nicht verfügbar";
                return -1;
            }

            if (!TryReadPointer(localPlayer + playerInfoOffset, out var playerInfo) || playerInfo == IntPtr.Zero)
            {
                StatusText = $"FiveM verbunden, PlayerInfo nicht lesbar (Offset 0x{playerInfoOffset:X})";
                return ReattachAndRead();
            }

            if (!TryReadInt32(playerInfo + wantedOffset, out var wanted) || wanted < 0 || wanted > 6)
            {
                StatusText = $"FiveM verbunden, Wanted-Wert nicht gültig (0x{wantedOffset:X})";
                return ReattachAndRead();
            }

            StatusText = $"FiveM extern verbunden";
            return Math.Clamp(wanted, 0, 5);
        }
        catch
        {
            Close();
            StatusText = "FiveM-Speicher nicht lesbar";
            return -1;
        }
    }

    int ReattachAndRead()
    {
        Close();
        return Attach() ? ReadWantedLevel() : -1;
    }

    bool Attach()
    {
        Close();

        var all = Process.GetProcesses();
        var candidates = all
            .Where(p =>
            {
                try
                {
                    var n = p.ProcessName;
                    if (!string.IsNullOrWhiteSpace(SelectedProcessName))
                        return n.Equals(SelectedProcessName, StringComparison.OrdinalIgnoreCase);
                    return n.Equals("FiveM_GTAProcess", StringComparison.OrdinalIgnoreCase)
                        || n.Equals("FiveM_GameProcess", StringComparison.OrdinalIgnoreCase)
                        || n.Equals("GTA5", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            })
            .OrderByDescending(p => p.ProcessName.Contains("FiveM", StringComparison.OrdinalIgnoreCase));

        foreach (var p in candidates)
        {
            try
            {
                var h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, p.Id);
                if (h == IntPtr.Zero) continue;

                var mod = p.MainModule;
                if (mod is null)
                {
                    CloseHandle(h);
                    continue;
                }

                attached = p;
                process = h;
                moduleBase = mod.BaseAddress;
                moduleSize = mod.ModuleMemorySize;
                world = FindWorldPointer();

                if (world == IntPtr.Zero)
                {
                    StatusText = $"{p.ProcessName} gefunden, World-Signatur nicht gefunden";
                    Close(false);
                    continue;
                }

                if (!FindPlayerInfoLayout())
                {
                    StatusText = $"{p.ProcessName} gefunden, PlayerInfo/Wanted-Struktur nicht gefunden";
                    Close(false);
                    continue;
                }

                StatusText = $"{p.ProcessName} verbunden";
                return true;
            }
            catch
            {
                try { CloseHandle(process); } catch { }
                process = IntPtr.Zero;
                attached = null;
            }
        }

        StatusText = string.IsNullOrWhiteSpace(SelectedProcessName)
            ? "Kein passender FiveM/GTA-Prozess gefunden"
            : $"Prozess '{SelectedProcessName}' nicht gefunden";
        return false;
    }

    IntPtr FindWorldPointer()
    {
        // Current FiveM builds commonly expose the CWorld global through this
        // RIP-relative instruction sequence. Wildcards cover the build-specific displacement.
        var signatures = new[]
        {
            // Current/recent GTA/FiveM builds: CWorld global.
            "48 8B 05 ?? ?? ?? ?? 45 ?? ?? ?? ?? 48 8B 48 08 48 85 C9 74 07",
            // Same World reference with a different compiler/code path.
            "48 8B 05 ?? ?? ?? ?? 33 D2 48 8B 40 08 8A CA 48 85 C0 74 16 48 8B",
            // Older builds.
            "48 8B 05 ?? ?? ?? ?? 48 8B 58 08 48 85 DB 74 32"
        };

        foreach (var signatureText in signatures)
        {
            var signature = ParseSignature(signatureText);
            var match = PatternScan(signature);
            if (match == IntPtr.Zero) continue;
            var global = ResolveRipRelative(match, 3, 7);
            if (TryReadPointer(global, out var value) && value != IntPtr.Zero && IsLikelyAddress(value))
                return value;
        }
        return IntPtr.Zero;
    }

    bool FindPlayerInfoLayout()
    {
        if (!TryReadPointer(world + OFFSET_WORLD_PLAYER, out var localPlayer) || localPlayer == IntPtr.Zero)
            return false;

        foreach (var piOffset in PlayerInfoOffsets)
        {
            if (!TryReadPointer(localPlayer + piOffset, out var playerInfo) || playerInfo == IntPtr.Zero)
                continue;

            foreach (var wantedOff in WantedOffsets)
            {
                if (!TryReadInt32(playerInfo + wantedOff, out var value)) continue;
                if (value >= 0 && value <= 6)
                {
                    playerInfoOffset = piOffset;
                    wantedOffset = wantedOff;
                    return true;
                }
            }
        }
        return false;
    }

    IntPtr PatternScan(byte?[] pattern)
    {
        if (moduleBase == IntPtr.Zero || moduleSize <= 0) return IntPtr.Zero;

        const int chunkSize = 1024 * 1024;
        var buffer = new byte[chunkSize];
        long moduleEnd = moduleBase.ToInt64() + moduleSize;

        for (long address = moduleBase.ToInt64(); address < moduleEnd; address += chunkSize - pattern.Length)
        {
            int read = (int)Math.Min(chunkSize, moduleEnd - address);
            if (!ReadProcessMemory(process, new IntPtr(address), buffer, read, out var bytesRead) || bytesRead < pattern.Length)
                continue;

            int limit = (int)bytesRead - pattern.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool ok = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    var b = pattern[j];
                    if (b.HasValue && buffer[i + j] != b.Value) { ok = false; break; }
                }
                if (ok) return new IntPtr(address + i);
            }
        }
        return IntPtr.Zero;
    }

    static byte?[] ParseSignature(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x == "??" || x == "?" ? (byte?)null : Convert.ToByte(x, 16)).ToArray();

    IntPtr ResolveRipRelative(IntPtr instruction, int displacementOffset, int instructionLength)
    {
        if (!TryReadInt32(instruction + displacementOffset, out var displacement)) return IntPtr.Zero;
        return instruction + instructionLength + displacement;
    }

    bool TryReadPointer(IntPtr address, out IntPtr value)
    {
        value = IntPtr.Zero;
        if (!ReadProcessMemory(process, address, out long raw, IntPtr.Size, out var read) || read != IntPtr.Size)
            return false;
        value = new IntPtr(raw);
        return IsLikelyAddress(value);
    }

    bool TryReadInt32(IntPtr address, out int value)
    {
        value = 0;
        return ReadProcessMemory(process, address, out value, sizeof(int), out var read) && read == sizeof(int);
    }

    bool IsLikelyAddress(IntPtr address)
    {
        long a = address.ToInt64();
        return a > 0x10000 && a < 0x00007FFFFFFFFFFF;
    }

    void Close(bool clearStatus = true)
    {
        if (process != IntPtr.Zero)
        {
            try { CloseHandle(process); } catch { }
        }
        process = IntPtr.Zero;
        attached = null;
        moduleBase = IntPtr.Zero;
        moduleSize = 0;
        world = IntPtr.Zero;
        playerInfoOffset = 0;
        wantedOffset = 0;
        if (clearStatus) StatusText = "FiveM nicht gefunden";
    }

    public void Dispose() => Close();

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, int size, out IntPtr bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr process, IntPtr address, out long buffer, int size, out IntPtr bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr process, IntPtr address, out int buffer, int size, out IntPtr bytesRead);
}


public sealed class ProcessSelectorForm : Form
{
    public Process? SelectedProcess { get; private set; }
    readonly ListView list = new();
    readonly Label hint = new();

    public ProcessSelectorForm(string selectedName)
    {
        Text = "FiveM-Prozess auswählen";
        Width = 760; Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(18, 12, 24); ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(650, 400);

        hint.Text = "Wähle den GTA/FiveM-Prozess aus, aus dem das Wanted-Level gelesen werden soll. " +
                    "Normalerweise ist das FiveM_GTAProcess. Der Prozess muss nach dem Start von FiveM bereits laufen.";
        hint.Left = 15; hint.Top = 15; hint.Width = 700; hint.Height = 42;
        hint.ForeColor = Color.LightGray;

        list.Left = 15; list.Top = 65; list.Width = 715; list.Height = 330;
        list.View = View.Details; list.FullRowSelect = true; list.GridLines = true;
        list.Columns.Add("Prozess", 230); list.Columns.Add("PID", 90); list.Columns.Add("Fenster", 370);
        list.DoubleClick += (_, _) => Confirm();

        var refresh = new Button { Text = "Aktualisieren", Left = 15, Top = 410, Width = 120, Height = 32 };
        var ok = new Button { Text = "Auswählen", Left = 550, Top = 410, Width = 85, Height = 32, DialogResult = DialogResult.None };
        var cancel = new Button { Text = "Abbrechen", Left = 645, Top = 410, Width = 85, Height = 32, DialogResult = DialogResult.Cancel };
        refresh.Click += (_, _) => LoadProcesses(selectedName);
        ok.Click += (_, _) => Confirm();
        Controls.AddRange([hint, list, refresh, ok, cancel]);
        AcceptButton = ok; CancelButton = cancel;
        LoadProcesses(selectedName);
    }

    void LoadProcesses(string selectedName)
    {
        list.Items.Clear();
        var preferred = new[] { "FiveM_GTAProcess", "FiveM_GameProcess", "GTA5" };
        var processes = Process.GetProcesses()
            .Select(p =>
            {
                try { return new { Process = p, Name = p.ProcessName, Title = p.MainWindowTitle ?? "" }; }
                catch { p.Dispose(); return null; }
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => preferred.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var x in processes)
        {
            var item = new ListViewItem(x.Name);
            item.SubItems.Add(x.Process.Id.ToString());
            item.SubItems.Add(x.Title);
            item.Tag = x.Process;
            list.Items.Add(item);
            if (!string.IsNullOrWhiteSpace(selectedName) && x.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase))
                item.Selected = true;
        }
    }

    void Confirm()
    {
        if (list.SelectedItems.Count == 0)
        {
            MessageBox.Show(this, "Bitte zuerst einen Prozess auswählen.", "Prozess", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        SelectedProcess = list.SelectedItems[0].Tag as Process;
        DialogResult = DialogResult.OK;
        Close();
    }
}

public interface ILightingProvider
{
    string Name { get; }
    bool IsAvailable { get; }
    Task InitializeAsync();
    Task SetStaticAsync(Color color);
    Task SetAlternatingAsync(Color a, Color b, int milliseconds);
    Task StopAsync();
}

public sealed class LightingManager
{
    ILightingProvider? provider;
    CancellationTokenSource? alternatingCts;

    public string ActiveName => provider?.Name ?? "nicht verbunden";

    public async Task InitializeAsync(string requested)
    {
        StopAlternating();
        var providers = new ILightingProvider[] { new RazerChroma(), new LogitechGLighting(), new RoccatOpenRgb() };
        IEnumerable<ILightingProvider> candidates = requested switch
        {
            "Razer Chroma" => providers.Where(p => p is RazerChroma),
            "Logitech G" => providers.Where(p => p is LogitechGLighting),
            "ROCCAT / OpenRGB" => providers.Where(p => p is RoccatOpenRgb),
            _ => providers
        };
        foreach (var p in candidates)
        {
            try { await p.InitializeAsync(); if (p.IsAvailable) { provider = p; return; } } catch { }
        }
        provider = null;
        throw new InvalidOperationException(requested == "Auto" ? "Keine unterstützte Beleuchtung gefunden" : $"{requested} nicht verfügbar");
    }

    public Task SetStaticAsync(Color color)
    {
        StopAlternating();
        return provider?.SetStaticAsync(color) ?? Task.CompletedTask;
    }

    public Task SetAlternatingAsync(Color a, Color b, int ms)
    {
        StopAlternating();
        if (provider is null) return Task.CompletedTask;

        var cts = new CancellationTokenSource();
        alternatingCts = cts;
        var delay = Math.Max(40, ms);
        _ = RunAlternatingAsync(provider, a, b, delay, cts);
        return Task.CompletedTask;
    }

    async Task RunAlternatingAsync(ILightingProvider p, Color a, Color b, int ms, CancellationTokenSource cts)
    {
        bool first = true;
        try
        {
            while (!cts.IsCancellationRequested)
            {
                // Set one complete static effect, wait, then switch to the other color.
                await p.SetStaticAsync(first ? a : b);
                first = !first;
                await Task.Delay(ms, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Debug.WriteLine(ex); }
        finally
        {
            if (ReferenceEquals(alternatingCts, cts)) alternatingCts = null;
            cts.Dispose();
        }
    }

    public async Task<string> TestOpenRgbAsync()
    {
        var test = new RoccatOpenRgb();
        try
        {
            var result = await test.DiagnoseAsync();
            return result;
        }
        finally
        {
            await test.StopAsync();
        }
    }

    public async Task StopAsync()
    {
        StopAlternating();
        if (provider is not null) await provider.StopAsync();
    }

    void StopAlternating()
    {
        var old = Interlocked.Exchange(ref alternatingCts, null);
        if (old is not null)
        {
            try { old.Cancel(); } catch { }
        }
    }
}

public sealed class RazerChroma : ILightingProvider
{
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };
    string? baseUri;
    CancellationTokenSource? alternatingCts;
    readonly SemaphoreSlim effectLock = new(1,1);
    public string Name => "Razer Chroma";
    public bool IsAvailable => baseUri is not null;

    public async Task InitializeAsync()
    {
        var body = new { title="FiveM Wanted Lighting", description="Wanted lighting", author=new { name="Marek Stelter", contact="local" }, device_supported=new[]{"keyboard"}, category="game" };
        using var resp = await http.PostAsJsonAsync("http://localhost:54235/razer/chromasdk", body);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        baseUri = doc.RootElement.GetProperty("uri").GetString();
    }
    public async Task SetStaticAsync(Color color) { StopLoop(); await PutSafeAsync("keyboard", new { effect="CHROMA_STATIC", param=new { color=Rgb(color) } }); }
    public Task SetAlternatingAsync(Color a, Color b, int milliseconds)
    {
        // Alternation is handled centrally by LightingManager.
        return SetStaticAsync(a);
    }
    async Task Loop(CancellationTokenSource cts, Color a, Color b, int ms)
    {
        bool first=true; try { while(!cts.IsCancellationRequested) { await PutSafeAsync("keyboard", new { effect="CHROMA_STATIC", param=new { color=Rgb(first?a:b) } }, cts.Token); first=!first; await Task.Delay(ms,cts.Token); } } catch(OperationCanceledException){} catch{} finally { if(ReferenceEquals(alternatingCts,cts)) alternatingCts=null; cts.Dispose(); }
    }
    public async Task StopAsync(){ StopLoop(); await PutSafeAsync("keyboard",new{effect="CHROMA_NONE"}); }
    void StopLoop(){var old=Interlocked.Exchange(ref alternatingCts,null); if(old!=null) try{old.Cancel();}catch{} }
    async Task PutSafeAsync(string device, object body, CancellationToken token=default)
    {
        try { await effectLock.WaitAsync(token); try { if(baseUri is null) await InitializeAsync(); if(baseUri is null)return; using var r=await http.PutAsJsonAsync($"{baseUri}/{device}",body,token); r.EnsureSuccessStatusCode(); } finally { effectLock.Release(); } }
        catch(OperationCanceledException){baseUri=null;} catch(HttpRequestException){baseUri=null;} catch{}
    }
    static int Rgb(Color c)=>(c.R<<16)|(c.G<<8)|c.B;
}

public sealed class LogitechGLighting : ILightingProvider
{
    CancellationTokenSource? alternatingCts;
    public string Name => "Logitech G";
    public bool IsAvailable { get; private set; }

    [DllImport("LogitechLedEnginesWrapper.dll", CallingConvention=CallingConvention.Cdecl)] static extern bool LogiLedInit();
    [DllImport("LogitechLedEnginesWrapper.dll", CallingConvention=CallingConvention.Cdecl)] static extern bool LogiLedShutdown();
    [DllImport("LogitechLedEnginesWrapper.dll", CallingConvention=CallingConvention.Cdecl)] static extern bool LogiLedSetLighting(int redPercentage, int greenPercentage, int bluePercentage);

    public Task InitializeAsync()
    {
        try { IsAvailable = LogiLedInit(); }
        catch { IsAvailable=false; }
        if(!IsAvailable) throw new InvalidOperationException("Logitech LED SDK/G HUB nicht verfügbar");
        return Task.CompletedTask;
    }
    public Task SetStaticAsync(Color c){ if(!IsAvailable) return Task.CompletedTask; LogiLedSetLighting(c.R*100/255,c.G*100/255,c.B*100/255); StopLoop(); return Task.CompletedTask; }
    public Task SetAlternatingAsync(Color a, Color b, int ms)
    {
        // Alternation is handled centrally by LightingManager.
        return SetStaticAsync(a);
    }
    void SetLighting(Color a,Color b,bool first){var c=first?a:b; LogiLedSetLighting(c.R*100/255,c.G*100/255,c.B*100/255);}
    public Task StopAsync(){StopLoop(); try{LogiLedShutdown();}catch{} return Task.CompletedTask;}
    void StopLoop(){var old=Interlocked.Exchange(ref alternatingCts,null);if(old!=null)try{old.Cancel();}catch{}}
}

public sealed class RoccatOpenRgb : ILightingProvider
{
    Process? proc;
    CancellationTokenSource? alternatingCts;

    public string Name => "ROCCAT / OpenRGB";
    public bool IsAvailable { get; private set; }

    // This is deliberately the same OpenRGB method as the working version:
    // let OpenRGB do all device detection and control, and invoke its CLI.
    string Exe => Path.Combine(AppContext.BaseDirectory, "OpenRGB.exe");

    public Task InitializeAsync()
    {
        IsAvailable = File.Exists(Exe) || FindOpenRgb() != null;
        if (!IsAvailable)
            throw new InvalidOperationException("OpenRGB.exe nicht gefunden");
        return Task.CompletedTask;
    }

    string? FindOpenRgb()
    {
        try
        {
            var p = Process.GetProcessesByName("OpenRGB").FirstOrDefault();
            return p?.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    string PathToExe() => File.Exists(Exe) ? Exe : (FindOpenRgb() ?? "OpenRGB.exe");

    public Task SetStaticAsync(Color c)
    {
        StopLoop();
        return RunAsync(c);
    }

    public async Task SetAlternatingAsync(Color a, Color b, int ms)
    {
        StopLoop();
        var cts = new CancellationTokenSource();
        alternatingCts = cts;
        try
        {
            bool first = true;
            while (!cts.IsCancellationRequested)
            {
                await RunAsync(first ? a : b);
                first = !first;
                await Task.Delay(Math.Max(40, ms), cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(alternatingCts, cts)) alternatingCts = null;
            cts.Dispose();
        }
    }

    async Task RunAsync(Color c)
    {
        try
        {
            var path = PathToExe();
            if (!File.Exists(path) && FindOpenRgb() == null) return;

            var p = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Arguments = $"--mode direct --color {c.R:X2}{c.G:X2}{c.B:X2}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (p == null) return;
            proc = p;

            // Important for the Cynosa Lite: wait for the CLI operation to finish
            // before the next color is sent. Do not stack OpenRGB.exe processes.
            var waitTask = p.WaitForExitAsync();
            var timeoutTask = Task.Delay(3000);
            await Task.WhenAny(waitTask, timeoutTask);

            if (!p.HasExited)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
            }

            try { await p.WaitForExitAsync(); } catch { }
            if (ReferenceEquals(proc, p)) proc = null;
            p.Dispose();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public Task StopAsync()
    {
        StopLoop();
        return Task.CompletedTask;
    }

    void StopLoop()
    {
        var old = Interlocked.Exchange(ref alternatingCts, null);
        if (old != null)
        {
            try { old.Cancel(); } catch { }
        }

        var p = Interlocked.Exchange(ref proc, null);
        if (p != null)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            try { p.Dispose(); } catch { }
        }
    }

    public Task<string> DiagnoseAsync()
    {
        try
        {
            var path = PathToExe();
            if (!File.Exists(path) && FindOpenRgb() == null)
                throw new InvalidOperationException("OpenRGB.exe nicht gefunden");

            var test = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--mode static --color FF00FF",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (test == null)
                throw new InvalidOperationException("OpenRGB konnte nicht gestartet werden.");

            return Task.FromResult($"OpenRGB erkannt und CLI gestartet.\r\nPfad: {path}\r\nTestfarbe: FF00FF");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"OpenRGB-Test fehlgeschlagen: {ex.Message}", ex);
        }
    }
}
