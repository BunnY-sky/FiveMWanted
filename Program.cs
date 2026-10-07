using System.Drawing;
using System.Drawing.Imaging;
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
    public Rectangle Hud { get; set; }
    public string Device { get; set; } = "Auto";
    public string Color12 { get; set; } = "#00008B";
    public string Color34 { get; set; } = "#8B0000";
    public string Color5A { get; set; } = "#8B0000";
    public string Color5B { get; set; } = "#00008B";
    public int AlternatingMs { get; set; } = 450;
}

public sealed class MainForm : Form
{
    readonly Label status = new();
    readonly Label wanted = new();
    readonly Button calibrate = new();
    readonly Button reset = new();
    readonly Button test12 = new();
    readonly Button test34 = new();
    readonly Button test5 = new();
    readonly Button applyColors = new();
    readonly CheckBox auto = new();
    readonly NumericUpDown sensitivity = new();
    readonly NumericUpDown interval = new();
    readonly ComboBox deviceBox = new();
    readonly TextBox hex12 = new();
    readonly TextBox hex34 = new();
    readonly TextBox hex5a = new();
    readonly TextBox hex5b = new();
    readonly Panel sw12 = new();
    readonly Panel sw34 = new();
    readonly Panel sw5a = new();
    readonly Panel sw5b = new();
    readonly System.Windows.Forms.Timer timer = new();
    readonly LightingManager lighting = new();
    readonly AppConfig cfg = new();
    Rectangle hud = Rectangle.Empty;
    int lastWanted = -1;
    DateTime lastScreenCheck = DateTime.MinValue;
    readonly string cfgPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    public MainForm()
    {
        Text = "FiveM Wanted Lighting";
        Width = 700; Height = 610;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 12, 24);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var title = new Label { Text = "FIVEM WANTED LIGHTING", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Left = 22, Top = 18, ForeColor = Color.Magenta };
        var sub = new Label { Text = "Razer • Logitech G • ROCCAT/OpenRGB", AutoSize = true, Left = 24, Top = 54, ForeColor = Color.Gainsboro };
        status.Text = "● Beleuchtung: wird verbunden..."; status.AutoSize = true; status.Left = 24; status.Top = 88;
        wanted.Text = "Wanted-Level: --"; wanted.Font = new Font("Segoe UI", 15, FontStyle.Bold); wanted.AutoSize = true; wanted.Left = 24; wanted.Top = 118;

        var devLabel = new Label { Text = "Tastatur", AutoSize = true, Left = 24, Top = 166 };
        deviceBox.Left = 105; deviceBox.Top = 162; deviceBox.Width = 230; deviceBox.DropDownStyle = ComboBoxStyle.DropDownList;
        deviceBox.Items.AddRange(["Auto", "Razer Chroma", "Logitech G", "ROCCAT / OpenRGB"]);
        deviceBox.SelectedIndexChanged += async (_, _) => { cfg.Device = deviceBox.SelectedItem?.ToString() ?? "Auto"; await ConnectLighting(); SaveConfig(); };

        auto.Text = "Automatisch erkennen"; auto.Checked = true; auto.Left = 360; auto.Top = 165; auto.AutoSize = true;
        var sensLabel = new Label { Text = "Empfindlichkeit", AutoSize = true, Left = 24, Top = 202 };
        sensitivity.Left = 115; sensitivity.Top = 198; sensitivity.Minimum = 1; sensitivity.Maximum = 100; sensitivity.Value = 20; sensitivity.Width = 70;

        calibrate.Text = "Wanted-Bereich mit Maus auswählen"; calibrate.Left = 24; calibrate.Top = 235; calibrate.Width = 265; calibrate.Height = 38;
        reset.Text = "Beleuchtung zurücksetzen"; reset.Left = 300; reset.Top = 235; reset.Width = 180; reset.Height = 38;

        var colorTitle = new Label { Text = "Wanted-Farben", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Left = 24, Top = 292 };
        AddColorRow("1–2 Sterne", 330, hex12, sw12, test12, "#00008B");
        AddColorRow("3–4 Sterne", 375, hex34, sw34, test34, "#8B0000");
        AddColorRow("5 Sterne Farbe A", 420, hex5a, sw5a, test5, "#8B0000");
        AddColorRow("5 Sterne Farbe B", 465, hex5b, sw5b, null, "#00008B");

        var intLabel = new Label { Text = "5★ Wechselintervall", AutoSize = true, Left = 24, Top = 512 };
        interval.Left = 155; interval.Top = 508; interval.Minimum = 100; interval.Maximum = 5000; interval.Increment = 50; interval.Value = 450; interval.Width = 80;
        var msLabel = new Label { Text = "ms", AutoSize = true, Left = 240, Top = 512 };
        applyColors.Text = "Farben speichern / anwenden"; applyColors.Left = 350; applyColors.Top = 504; applyColors.Width = 210; applyColors.Height = 36;

        var info = new Label { Text = "0★ = Standard/aus   •   1–2★ = Farbe 1   •   3–4★ = Farbe 2   •   5★ = Farbe A ↔ B\nHex-Farben sind direkt einstellbar, z. B. #8B0000 oder #00008B.", AutoSize = true, Left = 24, Top = 552, ForeColor = Color.LightGray };

        Controls.AddRange([title, sub, status, wanted, devLabel, deviceBox, auto, sensLabel, sensitivity, calibrate, reset, colorTitle, intLabel, interval, msLabel, applyColors, info]);

        calibrate.Click += (_, _) => StartCalibration();
        reset.Click += async (_, _) => await lighting.StopAsync();
        applyColors.Click += async (_, _) => { if (ReadColors()) { SaveConfig(); await ApplyCurrentWantedAsync(force: true); } };
        test12.Click += async (_, _) => { if (ReadColors()) await lighting.SetStaticAsync(ParseHex(cfg.Color12)); };
        test34.Click += async (_, _) => { if (ReadColors()) await lighting.SetStaticAsync(ParseHex(cfg.Color34)); };
        test5.Click += async (_, _) => { if (ReadColors()) await lighting.SetAlternatingAsync(ParseHex(cfg.Color5A), ParseHex(cfg.Color5B), cfg.AlternatingMs); };

        LoadConfig();
        timer.Interval = 120;
        timer.Tick += async (_, _) => await TickAsync();
        timer.Start();
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
                    cfg.Hud = loaded.Hud; cfg.Device = loaded.Device; cfg.Color12 = loaded.Color12; cfg.Color34 = loaded.Color34;
                    cfg.Color5A = loaded.Color5A; cfg.Color5B = loaded.Color5B; cfg.AlternatingMs = loaded.AlternatingMs;
                }
            }
        }
        catch { }
        hud = cfg.Hud;
        hex12.Text = cfg.Color12; hex34.Text = cfg.Color34; hex5a.Text = cfg.Color5A; hex5b.Text = cfg.Color5B;
        interval.Value = Math.Clamp(cfg.AlternatingMs, 100, 5000);
        var idx = deviceBox.Items.IndexOf(cfg.Device); deviceBox.SelectedIndex = idx >= 0 ? idx : 0;
        _ = ConnectLighting();
    }

    void SaveConfig()
    {
        cfg.Hud = hud;
        cfg.Device = deviceBox.SelectedItem?.ToString() ?? "Auto";
        cfg.AlternatingMs = (int)interval.Value;
        File.WriteAllText(cfgPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
    }

    bool ReadColors()
    {
        if (!TryParseHex(hex12.Text, out _)) { MessageBox.Show("Ungültige Farbe bei 1–2 Sterne.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        if (!TryParseHex(hex34.Text, out _)) { MessageBox.Show("Ungültige Farbe bei 3–4 Sterne.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        if (!TryParseHex(hex5a.Text, out _)) { MessageBox.Show("Ungültige Farbe 5★ A.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        if (!TryParseHex(hex5b.Text, out _)) { MessageBox.Show("Ungültige Farbe 5★ B.", "Farbe", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        cfg.Color12 = NormalizeHex(hex12.Text); cfg.Color34 = NormalizeHex(hex34.Text); cfg.Color5A = NormalizeHex(hex5a.Text); cfg.Color5B = NormalizeHex(hex5b.Text);
        cfg.AlternatingMs = (int)interval.Value;
        return true;
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
        if (!auto.Checked || hud.Width < 5 || hud.Height < 5) return;
        if ((DateTime.UtcNow - lastScreenCheck).TotalMilliseconds < 120) return;
        lastScreenCheck = DateTime.UtcNow;
        if (!Process.GetProcessesByName("FiveM").Any() && !Process.GetProcessesByName("GTA5").Any()) return;
        try
        {
            using var bmp = new Bitmap(hud.Width, hud.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(hud.Location, Point.Empty, hud.Size);
            int stars = WantedDetector.DetectStars(bmp, (int)sensitivity.Value);
            if (stars != lastWanted)
            {
                lastWanted = stars;
                wanted.Text = $"Wanted-Level: {new string('★', stars)}";
                await ApplyCurrentWantedAsync();
            }
        }
        catch { }
    }

    async Task ApplyCurrentWantedAsync(bool force = false)
    {
        if (!ReadColors()) return;
        if (lastWanted <= 0) await lighting.StopAsync();
        else if (lastWanted <= 2) await lighting.SetStaticAsync(ParseHex(cfg.Color12));
        else if (lastWanted <= 4) await lighting.SetStaticAsync(ParseHex(cfg.Color34));
        else await lighting.SetAlternatingAsync(ParseHex(cfg.Color5A), ParseHex(cfg.Color5B), cfg.AlternatingMs);
    }

    void StartCalibration()
    {
        using var overlay = new SelectionForm();
        if (overlay.ShowDialog(this) == DialogResult.OK)
        {
            hud = overlay.Selected;
            SaveConfig();
            wanted.Text = $"Bereich: {hud.X},{hud.Y} {hud.Width}×{hud.Height}";
        }
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

public sealed class SelectionForm : Form
{
    public Rectangle Selected { get; private set; }
    Point start; bool dragging;
    public SelectionForm()
    {
        FormBorderStyle = FormBorderStyle.None; TopMost = true; ShowInTaskbar = false; BackColor = Color.Black; Opacity = .28; Cursor = Cursors.Cross; Bounds = SystemInformation.VirtualScreen;
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { start = e.Location; dragging = true; } };
        MouseMove += (_, _) => { if (dragging) Invalidate(); };
        MouseUp += (_, e) => { if (dragging) { dragging = false; var r = Normalize(start, e.Location); Selected = new Rectangle(r.X + Bounds.X, r.Y + Bounds.Y, r.Width, r.Height); DialogResult = DialogResult.OK; Close(); } };
        Paint += (_, e) => { if (dragging) { var p = PointToClient(Cursor.Position); using var pen = new Pen(Color.Magenta, 3); e.Graphics.DrawRectangle(pen, Normalize(start, p)); } };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
    }
    static Rectangle Normalize(Point a, Point b) => new(Math.Min(a.X,b.X), Math.Min(a.Y,b.Y), Math.Abs(a.X-b.X), Math.Abs(a.Y-b.Y));
}

public static class WantedDetector
{
    public static int DetectStars(Bitmap bmp, int sensitivity)
    {
        if (bmp.Width < 25 || bmp.Height < 5) return 0;
        int whiteThreshold = Math.Clamp(210 - sensitivity, 150, 210);
        int activeStars = 0;
        for (int star = 0; star < 5; star++)
        {
            int x0 = star * bmp.Width / 5, x1 = (star + 1) * bmp.Width / 5, whitePixels = 0, consideredPixels = 0;
            int y0 = Math.Max(0, bmp.Height / 10), y1 = Math.Min(bmp.Height, bmp.Height - bmp.Height / 10);
            int innerX0 = x0 + Math.Max(0, (x1-x0)/10), innerX1 = x1 - Math.Max(0, (x1-x0)/10);
            for (int y=y0; y<y1; y++) for (int x=innerX0; x<innerX1; x++) { var c=bmp.GetPixel(x,y); int min=Math.Min(c.R,Math.Min(c.G,c.B)); int max=Math.Max(c.R,Math.Max(c.G,c.B)); int brightness=(c.R+c.G+c.B)/3; if(min>=whiteThreshold && max-min<=45 && brightness>=whiteThreshold) whitePixels++; consideredPixels++; }
            if (consideredPixels > 0 && whitePixels >= 3 && (double)whitePixels/consideredPixels >= .006) activeStars++;
        }
        return Math.Clamp(activeStars,0,5);
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
    public string ActiveName => provider?.Name ?? "nicht verbunden";

    public async Task InitializeAsync(string requested)
    {
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

    public Task SetStaticAsync(Color color) => provider?.SetStaticAsync(color) ?? Task.CompletedTask;
    public Task SetAlternatingAsync(Color a, Color b, int ms) => provider?.SetAlternatingAsync(a,b,ms) ?? Task.CompletedTask;
    public Task StopAsync() => provider?.StopAsync() ?? Task.CompletedTask;
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
    public async Task SetAlternatingAsync(Color a, Color b, int milliseconds)
    {
        StopLoop(); var cts=new CancellationTokenSource(); alternatingCts=cts;
        _=Loop(cts,a,b,Math.Max(100,milliseconds)); await Task.CompletedTask;
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
    public async Task SetAlternatingAsync(Color a, Color b, int ms){StopLoop(); var cts=new CancellationTokenSource(); alternatingCts=cts; try{bool first=true; while(!cts.IsCancellationRequested){SetLighting(a,b,first);first=!first;await Task.Delay(Math.Max(100,ms),cts.Token);}}catch(OperationCanceledException){} }
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
    string Exe => Path.Combine(AppContext.BaseDirectory,"OpenRGB.exe");
    public Task InitializeAsync(){ IsAvailable=File.Exists(Exe) || FindOpenRgb()!=null; if(!IsAvailable) throw new InvalidOperationException("OpenRGB.exe nicht gefunden"); return Task.CompletedTask; }
    string? FindOpenRgb(){ try{var p=Process.GetProcessesByName("OpenRGB").FirstOrDefault(); return p?.MainModule?.FileName;}catch{return null;} }
    string PathToExe()=>File.Exists(Exe)?Exe:(FindOpenRgb()??"OpenRGB.exe");
    public Task SetStaticAsync(Color c){StopLoop(); Run(c);return Task.CompletedTask;}
    public async Task SetAlternatingAsync(Color a,Color b,int ms){StopLoop();var cts=new CancellationTokenSource();alternatingCts=cts;try{bool first=true;while(!cts.IsCancellationRequested){Run(first?a:b);first=!first;await Task.Delay(Math.Max(100,ms),cts.Token);}}catch(OperationCanceledException){}}
    void Run(Color c){try{proc?.Dispose(); proc=Process.Start(new ProcessStartInfo{FileName=PathToExe(),Arguments=$"--mode static --color {c.R:X2}{c.G:X2}{c.B:X2}",UseShellExecute=false,CreateNoWindow=true});}catch{}}
    public Task StopAsync(){StopLoop();return Task.CompletedTask;}
    void StopLoop(){var old=Interlocked.Exchange(ref alternatingCts,null);if(old!=null)try{old.Cancel();}catch{} try{proc?.Dispose();}catch{} proc=null;}
}
