using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using Newtonsoft.Json;

namespace Mp3TaggerGUI
{
    public class MainForm : Form
    {
        // Ścieżki
        readonly TextBox txtMp3Dir     = new() { Width = 938, BorderStyle = BorderStyle.FixedSingle };
        readonly TextBox txtJson       = new() { Width = 938, BorderStyle = BorderStyle.FixedSingle };
        readonly Button btnBrowseMp3   = new() { Text = "Przeglądaj...", Width = 110, Height = 32 };
        readonly Button btnBrowseJson  = new() { Text = "Przeglądaj...", Width = 110, Height = 32 };
        readonly Button btnStart       = new() { Text = "Uruchom", Width = 120, Height = 34 };
        readonly Button btnCancel      = new() { Text = "Anuluj", Width = 120, Height = 34, Enabled = false };
        readonly ProgressBar progressBar = new() { Width = 918, Height = 20 };
        readonly TextBox txtLog        = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Width = 1160, Height = 220, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle };

        // Checkboxy — czytelne nazwy
        readonly CheckBox chkDoGenre         = new() { Text = "Aktualizuj gatunki (GENRE)", AutoSize = true, Checked = true };
        readonly CheckBox chkDoLabel         = new() { Text = "Aktualizuj wytwórnię (LABEL)", AutoSize = true, Checked = true };
        readonly CheckBox chkFallback        = new() { Text = "Fallback: dopasuj po nazwie pliku", AutoSize = true, Checked = true };
        readonly CheckBox chkPrependNew      = new() { Text = "Nowe wartości na początek (prepend)", AutoSize = true, Checked = true };

        readonly CheckBox chkDedup           = new() { Text = "Dedupikuj wartości", AutoSize = true, Checked = true };
        readonly CheckBox chkNormalizeSeps   = new() { Text = "Normalizuj separatory (| , ; / :)", AutoSize = true, Checked = true };
        readonly CheckBox chkTitleCase       = new() { Text = "Title Case (każde słowo od wielkiej)", AutoSize = true, Checked = true };
        readonly CheckBox chkForcePopUpper   = new() { Text = "POP wielkimi literami", AutoSize = true, Checked = true };

        readonly CheckBox chkRemoveWorldPoland = new() { Text = "Usuń 'Świat/Polska' z GENRE", AutoSize = true, Checked = true };
        readonly CheckBox chkAppendDjPromo     = new() { Text = "Dopnij 'DJPromo.pl' na końcu GENRE", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteTxxx         = new() { Text = "Zapisz też do TXXX:LABEL", AutoSize = true, Checked = true };
        readonly CheckBox chkDryRun            = new() { Text = "Dry run (bez zapisu plików)", AutoSize = true };

        // CSV/Backup
        readonly CheckBox chkCsvReport        = new() { Text = "Zapisz raport CSV", AutoSize = true, Checked = true };
        readonly CheckBox chkPerFileBackup    = new() { Text = "Backup tagów przed zapisem (1 plik JSON / sesja)", AutoSize = true, Checked = true };

        // Liczniki (UI)
        readonly Label lblCounts = new() { AutoSize = true, Text = "—" };

        // Settings
        readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mp3TaggerGUI");
        readonly string SettingsPath;
        UiSettings SettingsCache = new();
        CancellationTokenSource? _runCts;
        readonly ConcurrentQueue<string> _pendingLogs = new();
        readonly System.Windows.Forms.Timer _uiFlushTimer = new() { Interval = 200 };
        int _pendingStepIncrements;

        public MainForm()
        {
            SettingsPath = Path.Combine(SettingsDir, "user-settings.json");

            Text = "DeeJay Mix Tags";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1200, 820);
            MinimumSize = new Size(1100, 760);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(245, 247, 250);

            var lblMp3  = new Label { Text = "Folder z MP3:", AutoSize = true, Left = 12, Top = 18, ForeColor = Color.FromArgb(45, 45, 48) };
            txtMp3Dir.Left = 130; txtMp3Dir.Top = 15; txtMp3Dir.Width = 938;
            btnBrowseMp3.Left = 1078; btnBrowseMp3.Top = 12;

            var lblJson = new Label { Text = "Plik JSON (baza):", AutoSize = true, Left = 12, Top = 52, ForeColor = Color.FromArgb(45, 45, 48) };
            txtJson.Left = 130; txtJson.Top = 49; txtJson.Width = 938;
            btnBrowseJson.Left = 1078; btnBrowseJson.Top = 46;

            var grp = new GroupBox { Text = "Opcje", Left = 12, Top = 90, Width = 1160, Height = 240, ForeColor = Color.FromArgb(45, 45, 48) };
            Controls.AddRange([lblMp3, txtMp3Dir, btnBrowseMp3, lblJson, txtJson, btnBrowseJson, grp]);

            int x1 = 16, x2 = 380, x3 = 760;
            int y = 24, dy = 26;

            chkDoGenre.Left = x1; chkDoGenre.Top = y;
            chkDoLabel.Left = x1; chkDoLabel.Top = y += dy;
            chkFallback.Left = x1; chkFallback.Top = y += dy;
            chkPrependNew.Left = x1; chkPrependNew.Top = y += dy;

            y = 24;
            chkDedup.Left = x2; chkDedup.Top = y;
            chkNormalizeSeps.Left = x2; chkNormalizeSeps.Top = y += dy;
            chkTitleCase.Left = x2; chkTitleCase.Top = y += dy;
            chkForcePopUpper.Left = x2; chkForcePopUpper.Top = y += dy;

            y = 24;
            chkRemoveWorldPoland.Left = x3; chkRemoveWorldPoland.Top = y;
            chkAppendDjPromo.Left = x3; chkAppendDjPromo.Top = y += dy;
            chkWriteTxxx.Left = x3; chkWriteTxxx.Top = y += dy;
            chkDryRun.Left = x3; chkDryRun.Top = y += dy;
            chkCsvReport.Left = x3; chkCsvReport.Top = y += dy;
            chkPerFileBackup.Left = x3; chkPerFileBackup.Top = y += dy;

            grp.Controls.AddRange([
                chkDoGenre, chkDoLabel, chkFallback, chkPrependNew,
                chkDedup, chkNormalizeSeps, chkTitleCase, chkForcePopUpper,
                chkRemoveWorldPoland, chkAppendDjPromo, chkWriteTxxx, chkDryRun,
                chkCsvReport, chkPerFileBackup
            ]);

            btnStart.Left = 12; btnStart.Top = grp.Bottom + 10;
            btnCancel.Left = 140; btnCancel.Top = grp.Bottom + 10;
            progressBar.Left = 270; progressBar.Top = grp.Bottom + 16; progressBar.Width = 680;

            lblCounts.Left = 12; lblCounts.Top = progressBar.Bottom + 12; lblCounts.Width = 1160;
            txtLog.Left = 12; txtLog.Top = lblCounts.Bottom + 8; txtLog.Width = 1160; txtLog.Height = 220;
            txtLog.Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            txtLog.BackColor = Color.White;

            txtMp3Dir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnBrowseMp3.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtJson.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnBrowseJson.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            grp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnStart.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblCounts.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            StyleSecondaryButton(btnBrowseMp3);
            StyleSecondaryButton(btnBrowseJson);
            StylePrimaryButton(btnStart);

            Controls.AddRange([btnStart, btnCancel, progressBar, lblCounts, txtLog]);

            var tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };
            tip.SetToolTip(chkDoGenre, "Uzupełnia/normalizuje pole GENRE na podstawie JSON. Może usuwać 'Świat/Polska' i dopinać DJPromo.pl.");
            tip.SetToolTip(chkDoLabel, "Uzupełnia/normalizuje pole LABEL (TPUB) na podstawie JSON. Może też zapisać kopię do TXXX:LABEL.");
            tip.SetToolTip(chkFallback, "Jeśli nie znajdzie dopasowania po tagach (artysta/tytuł/wersja), próbuje dopasować po nazwie pliku.");
            tip.SetToolTip(chkPrependNew, "Nowe wartości (z JSON) będą dodane przed istniejącymi. Odznacz, aby dopisać na końcu.");
            tip.SetToolTip(chkDedup, "Usuwa duplikaty wartości (np. 'Armada Music | Armada Music' → 'Armada Music').");
            tip.SetToolTip(chkNormalizeSeps, "Zamienia przecinki/średniki/ukośniki na separator ' | ' i czyści nadmiarowe spacje/kropki.");
            tip.SetToolTip(chkTitleCase, "Formatuje słowa z wielkiej litery (np. 'club house' → 'Club House').");
            tip.SetToolTip(chkForcePopUpper, "Wymusza zapis 'POP' wielkimi literami w GENRE.");
            tip.SetToolTip(chkRemoveWorldPoland, "Z GENRE usuń wpisy 'Świat'/'Polska' i prefiksy 'Świat:'/'Polska:'.");
            tip.SetToolTip(chkAppendDjPromo, "Jeśli w GENRE brak 'DJPromo.pl', dopnij go na końcu (stały sufiks).");
            tip.SetToolTip(chkWriteTxxx, "Zapisz też wartość LABEL do ramki TXXX:LABEL (kopiuj TPUB → TXXX).");
            tip.SetToolTip(chkDryRun, "Tryb testowy: nie zapisuje zmian w plikach, tylko loguje co by się zmieniło.");
            tip.SetToolTip(chkCsvReport, "Zapisuje raport CSV (_tagger_report.csv) w folderze MP3 z listą zmian.");
            tip.SetToolTip(chkPerFileBackup, "Przed zapisem tworzy jeden wspólny backup sesji: _tagger_backup_YYYYMMDD_HHMMSS.json.");

            btnBrowseMp3.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Wybierz folder z MP3" };
                if (dlg.ShowDialog(this) == DialogResult.OK) txtMp3Dir.Text = dlg.SelectedPath;
            };
            btnBrowseJson.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json|All files (*.*)|*.*" };
                if (dlg.ShowDialog(this) == DialogResult.OK) txtJson.Text = dlg.FileName;
            };
            btnStart.Click += async (s, e) => await RunAsync();
            btnCancel.Click += (s, e) => _runCts?.Cancel();
            _uiFlushTimer.Tick += (s, e) => FlushUiBatches();

            this.AcceptButton = btnStart;

            // wczytaj zapamiętane ustawienia
            LoadUiSettings();
        }

        private static void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 215);
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(45, 45, 48);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseCompatibleTextRendering = true;
            button.Cursor = Cursors.Hand;
        }

        private static void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(0, 120, 212);
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
        }

        private bool TryGetValidatedInputs(out string mp3, out string json)
        {
            mp3 = txtMp3Dir.Text.Trim();
            json = txtJson.Text.Trim();

            if (!Directory.Exists(mp3))
            {
                MessageBox.Show(this, "Wskaż poprawny folder z MP3.");
                return false;
            }

            if (!File.Exists(json))
            {
                MessageBox.Show(this, "Wskaż poprawny plik JSON.");
                return false;
            }

            return true;
        }

        private TaggingOptions BuildTaggingOptions() => new()
        {
            DoGenre = chkDoGenre.Checked,
            DoLabel = chkDoLabel.Checked,
            FilenameFallback = chkFallback.Checked,
            AlwaysAppendToGenre = chkAppendDjPromo.Checked,
            RemoveWorldPoland = chkRemoveWorldPoland.Checked,
            NormalizeSeparators = chkNormalizeSeps.Checked,
            TitleCase = chkTitleCase.Checked,
            ForcePopUpper = chkForcePopUpper.Checked,
            Dedup = chkDedup.Checked,
            WriteTxxxLabel = chkWriteTxxx.Checked,
            PrependNew = chkPrependNew.Checked,
            DryRun = chkDryRun.Checked,
            WriteCsvReport = chkCsvReport.Checked,
            WritePerFileBackup = chkPerFileBackup.Checked
        };

        private void ResetRunUiState()
        {
            lblCounts.Text = "—";
            txtLog.Clear();
            progressBar.Value = 0;
        }

        private void HandleTotal(int total)
        {
            progressBar.Minimum = 0;
            progressBar.Maximum = Math.Max(1, total);
            progressBar.Value = 0;
            Log($"INFO: Plików MP3 do przetworzenia: {total}");
        }

        private void HandleStep()
        {
            if (progressBar.Value < progressBar.Maximum) progressBar.Value++;
        }

        private void EnqueueStep() => Interlocked.Increment(ref _pendingStepIncrements);

        private void EnqueueLog(string message) => _pendingLogs.Enqueue(message);

        private void FlushUiBatches()
        {
            var steps = Interlocked.Exchange(ref _pendingStepIncrements, 0);
            if (steps > 0 && progressBar.Maximum > 0)
                progressBar.Value = Math.Min(progressBar.Maximum, progressBar.Value + steps);

            if (_pendingLogs.IsEmpty)
                return;

            var sb = new StringBuilder();
            while (_pendingLogs.TryDequeue(out var line))
                sb.AppendLine(line);

            if (sb.Length > 0)
                LogBatch(sb.ToString());
        }

        private static string BuildSummaryText(TaggingLogic.Result result) =>
            $"Zaktualizowano: {result.Updated} | Bez zmian: {result.Unchanged} | Brak w bazie: {result.Missing} | Błędy: {result.Errors} | Razem: {result.Total}" +
            (string.IsNullOrEmpty(result.CsvPath) ? "" : $" | Raport: {result.CsvPath}");

        private static (int totalFiles, long totalBytes) PreScanMp3(string root)
        {
            int count = 0;
            long bytes = 0;

            foreach (var path in Directory.EnumerateFiles(root, "*.mp3", SearchOption.AllDirectories))
            {
                count++;
                try
                {
                    bytes += new FileInfo(path).Length;
                }
                catch
                {
                    // pomiń pojedynczy błąd dostępu
                }
            }

            return (count, bytes);
        }

        private async Task RunAsync()
        {
            if (!TryGetValidatedInputs(out var mp3, out var json)) return;

            UseWaitCursor = true;
            var (scanCount, scanBytes) = await Task.Run(() => PreScanMp3(mp3));
            UseWaitCursor = false;

            var scanSizeGb = scanBytes / (1024d * 1024d * 1024d);
            if (scanCount >= 5000)
            {
                var ask = MessageBox.Show(
                    this,
                    $"Wykryto {scanCount:N0} plików MP3 (~{scanSizeGb:F2} GB). Operacja może potrwać długo. Kontynuować?",
                    "Duży wolumen plików",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (ask != DialogResult.Yes)
                    return;
            }

            var opts = BuildTaggingOptions();

            ToggleForRun(true);
            ResetRunUiState();
            _uiFlushTimer.Start();

            Log($"INFO: Pre-scan -> pliki: {scanCount:N0}, rozmiar: ~{scanSizeGb:F2} GB");

            _runCts = new CancellationTokenSource();

            try
            {
                var result = await Task.Run(() =>
                    TaggingLogic.Process(mp3, json, opts,
                        onTotal: total => BeginInvoke(new Action(() => HandleTotal(total))),
                        onStep: EnqueueStep,
                        onLog: EnqueueLog,
                        cancellationToken: _runCts.Token),
                    _runCts.Token
                );

                FlushUiBatches();
                lblCounts.Text = BuildSummaryText(result);
            }
            catch (OperationCanceledException)
            {
                FlushUiBatches();
                Log("INFO: Operacja anulowana przez użytkownika.");
                lblCounts.Text = "Operacja anulowana.";
            }
            catch (Exception ex)
            {
                FlushUiBatches();
                Log($"ERROR: {ex.Message}");
            }
            finally
            {
                _uiFlushTimer.Stop();
                FlushUiBatches();
                _runCts?.Dispose();
                _runCts = null;
                ToggleForRun(false);
                SaveUiSettings(); // zapisz ustawienia po zakończeniu
            }
        }

        private void ToggleForRun(bool isRunning)
        {
            foreach (Control c in Controls) c.Enabled = !isRunning;
            btnCancel.Enabled = isRunning;
            btnStart.Enabled = !isRunning;
            txtLog.Enabled = true;
            progressBar.Enabled = true;
            lblCounts.Enabled = true;
        }

        private void Log(string s)
        {
            txtLog.AppendText(s + Environment.NewLine);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void LogBatch(string text)
        {
            txtLog.AppendText(text);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void ApplySettingsToUi(UiSettings settings)
        {
            txtMp3Dir.Text = settings.Mp3Dir ?? "";
            txtJson.Text = settings.JsonPath ?? "";

            chkDoGenre.Checked = settings.DoGenre;
            chkDoLabel.Checked = settings.DoLabel;
            chkFallback.Checked = settings.FilenameFallback;
            chkPrependNew.Checked = settings.PrependNew;
            chkDedup.Checked = settings.Dedup;
            chkNormalizeSeps.Checked = settings.NormalizeSeparators;
            chkTitleCase.Checked = settings.TitleCase;
            chkForcePopUpper.Checked = settings.ForcePopUpper;
            chkRemoveWorldPoland.Checked = settings.RemoveWorldPoland;
            chkAppendDjPromo.Checked = settings.AlwaysAppendToGenre;
            chkWriteTxxx.Checked = settings.WriteTxxxLabel;
            chkDryRun.Checked = settings.DryRun;
            chkCsvReport.Checked = settings.WriteCsvReport;
            chkPerFileBackup.Checked = settings.WritePerFileBackup;
        }

        private UiSettings ReadSettingsFromUi() => new()
        {
            Mp3Dir = txtMp3Dir.Text,
            JsonPath = txtJson.Text,
            DoGenre = chkDoGenre.Checked,
            DoLabel = chkDoLabel.Checked,
            FilenameFallback = chkFallback.Checked,
            PrependNew = chkPrependNew.Checked,
            Dedup = chkDedup.Checked,
            NormalizeSeparators = chkNormalizeSeps.Checked,
            TitleCase = chkTitleCase.Checked,
            ForcePopUpper = chkForcePopUpper.Checked,
            RemoveWorldPoland = chkRemoveWorldPoland.Checked,
            AlwaysAppendToGenre = chkAppendDjPromo.Checked,
            WriteTxxxLabel = chkWriteTxxx.Checked,
            DryRun = chkDryRun.Checked,
            WriteCsvReport = chkCsvReport.Checked,
            WritePerFileBackup = chkPerFileBackup.Checked
        };

        // --- Pamięć ustawień (AppData\Roaming\Mp3TaggerGUI\user-settings.json) ---
        private void LoadUiSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;

                var json = File.ReadAllText(SettingsPath);
                SettingsCache = JsonConvert.DeserializeObject<UiSettings>(json) ?? new UiSettings();
                ApplySettingsToUi(SettingsCache);
            }
            catch { /* ignoruj */ }
        }

        private void SaveUiSettings()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                SettingsCache = ReadSettingsFromUi();
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(SettingsCache, Formatting.Indented));
            }
            catch { /* ignoruj */ }
        }

    }
}
