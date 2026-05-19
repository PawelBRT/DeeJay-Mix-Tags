using System;
using System.ComponentModel;
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
        readonly ComboBox cmbSource    = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly Button btnBrowseMp3   = new() { Text = "Przeglądaj...", Width = 110, Height = 32 };
        readonly Button btnBrowseJson  = new() { Text = "Przeglądaj...", Width = 110, Height = 32 };
        readonly Button btnStart       = new() { Text = "Uruchom", Width = 120, Height = 34 };
        readonly Button btnLoadGrid    = new() { Text = "Wczytaj GRID", Width = 120, Height = 34 };
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
        readonly CheckBox chkWriteTxxx         = new() { Text = "Dopnij 'DJPromo.pl' na końcu TXXX:LABEL", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteDmcComment   = new() { Text = "Dopisz komentarz DMC", AutoSize = true };
        readonly CheckBox chkRepairDmcComment  = new() { Text = "Napraw komentarz DMC", AutoSize = true };
        readonly CheckBox chkCleanupComment    = new() { Text = "Wyczyść Record label/Key/Energy z COMMENT", AutoSize = true };
        readonly CheckBox chkWriteDmcGenreTag  = new() { Text = "Zapisz TXXX:DMC_GENRE", AutoSize = true };
        readonly CheckBox chkDryRun            = new() { Text = "Dry run (bez zapisu plików)", AutoSize = true };

        // CSV/Backup
        readonly CheckBox chkCsvReport        = new() { Text = "Zapisz raport CSV", AutoSize = true, Checked = true };
        readonly CheckBox chkPerFileBackup    = new() { Text = "Backup tagów przed zapisem (1 plik JSON / sesja)", AutoSize = true, Checked = true };
        readonly ComboBox cmbDjoidGenreSource = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
        readonly ComboBox cmbDjoidGenreWriteMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly CheckBox chkWriteDjoidGenreTag = new() { Text = "GENRE", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteDjoidSubgenreTag = new() { Text = "SUBGENRE", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteDjoidEnergyTag = new() { Text = "ENERGY", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteDjoidDanceTag = new() { Text = "DANCEABILITY", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteDjoidEmotionTag = new() { Text = "EMOTION", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteDjoidKeyTag = new() { Text = "KEY", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteDjoidBpmTag = new() { Text = "BPM", AutoSize = true, Checked = true };
        readonly CheckBox chkWriteDjoidComment = new() { Text = "Zapisz podsumowanie DJOID w COMMENT", AutoSize = true };
        readonly CheckBox chkScaleDjoidToTen = new() { Text = "Skaluj do 1-10", AutoSize = true, Checked = true };
        readonly Label lblDjoidGenre = new() { Text = "GENRE z DJOID:", AutoSize = true, ForeColor = Color.FromArgb(45, 45, 48) };
        readonly Label lblDjoidTags = new() { Text = "Zapisz dodatkowe pola jako TXXX:DJOID_*", AutoSize = true, ForeColor = Color.FromArgb(45, 45, 48) };

        // Liczniki (UI)
        readonly Label lblCounts = new() { AutoSize = true, Text = "—" };

        // Settings
        readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mp3TaggerGUI");
        readonly string SettingsPath;
        UiSettings SettingsCache = new();
        CancellationTokenSource? _runCts;
        BindingList<TagEditRow> _gridRows = new();
        GridEditorForm? _gridForm;
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
            AppUiStyle.ApplyForm(this);

            var lblMp3  = new Label { Text = "Folder z MP3:", AutoSize = true, Left = 12, Top = 18, ForeColor = Color.FromArgb(45, 45, 48) };
            txtMp3Dir.Left = 130; txtMp3Dir.Top = 15; txtMp3Dir.Width = 938;
            btnBrowseMp3.Left = 1078; btnBrowseMp3.Top = 12;

            var lblSource = new Label { Text = "Źródło danych:", AutoSize = true, Left = 12, Top = 52, ForeColor = Color.FromArgb(45, 45, 48) };
            cmbSource.Left = 130; cmbSource.Top = 49;
            var lblJson = new Label { Text = "Plik JSON (baza):", AutoSize = true, Left = 368, Top = 52, ForeColor = Color.FromArgb(45, 45, 48) };
            txtJson.Left = 130; txtJson.Top = 49; txtJson.Width = 938;
            txtJson.Left = 480; txtJson.Top = 49; txtJson.Width = 588;
            btnBrowseJson.Left = 1078; btnBrowseJson.Top = 46;

            var grp = new GroupBox { Text = "Opcje", Left = 12, Top = 90, Width = 1160, Height = 330, ForeColor = Color.FromArgb(45, 45, 48) };
            Controls.AddRange([lblMp3, txtMp3Dir, btnBrowseMp3, lblSource, cmbSource, lblJson, txtJson, btnBrowseJson, grp]);

            var commonBox = new GroupBox { Text = "Wspólne czyszczenie GENRE", Left = 16, Top = 24, Width = 350, Height = 285 };
            var sourceBox = new GroupBox { Text = "Opcje aktywnego źródła", Left = 388, Top = 24, Width = 350, Height = 285 };
            var outputBox = new GroupBox { Text = "Zapis i raport", Left = 760, Top = 24, Width = 380, Height = 285 };

            chkDoGenre.Left = 14; chkDoGenre.Top = 28;
            chkPrependNew.Left = 14; chkPrependNew.Top = 58;
            chkDedup.Left = 14; chkDedup.Top = 96;
            chkNormalizeSeps.Left = 14; chkNormalizeSeps.Top = 126;
            chkTitleCase.Left = 14; chkTitleCase.Top = 156;
            chkForcePopUpper.Left = 14; chkForcePopUpper.Top = 186;
            chkRemoveWorldPoland.Left = 14; chkRemoveWorldPoland.Top = 216;

            chkDoLabel.Left = 14; chkDoLabel.Top = 28;
            chkFallback.Left = 14; chkFallback.Top = 58;
            chkAppendDjPromo.Left = 14; chkAppendDjPromo.Top = 88;
            chkWriteTxxx.Left = 14; chkWriteTxxx.Top = 118;
            chkWriteDmcComment.Left = 14; chkWriteDmcComment.Top = 154;
            chkRepairDmcComment.Left = 14; chkRepairDmcComment.Top = 184;
            chkCleanupComment.Left = 14; chkCleanupComment.Top = 214;
            chkWriteDmcGenreTag.Left = 14; chkWriteDmcGenreTag.Top = 244;

            lblDjoidGenre.Left = 14; lblDjoidGenre.Top = 28;
            cmbDjoidGenreSource.Left = 14; cmbDjoidGenreSource.Top = 52; cmbDjoidGenreSource.Width = 300;
            cmbDjoidGenreWriteMode.Left = 14; cmbDjoidGenreWriteMode.Top = 84; cmbDjoidGenreWriteMode.Width = 300;
            lblDjoidTags.Left = 14; lblDjoidTags.Top = 126;
            chkWriteDjoidGenreTag.Left = 14; chkWriteDjoidGenreTag.Top = 150;
            chkWriteDjoidSubgenreTag.Left = 120; chkWriteDjoidSubgenreTag.Top = 150;
            chkWriteDjoidEnergyTag.Left = 14; chkWriteDjoidEnergyTag.Top = 176;
            chkWriteDjoidDanceTag.Left = 120; chkWriteDjoidDanceTag.Top = 176;
            chkWriteDjoidEmotionTag.Left = 14; chkWriteDjoidEmotionTag.Top = 202;
            chkWriteDjoidKeyTag.Left = 120; chkWriteDjoidKeyTag.Top = 202;
            chkWriteDjoidBpmTag.Left = 14; chkWriteDjoidBpmTag.Top = 228;
            chkScaleDjoidToTen.Left = 120; chkScaleDjoidToTen.Top = 228;
            chkWriteDjoidComment.Left = 14; chkWriteDjoidComment.Top = 254;

            chkDryRun.Left = 14; chkDryRun.Top = 28;
            chkCsvReport.Left = 14; chkCsvReport.Top = 58;
            chkPerFileBackup.Left = 14; chkPerFileBackup.Top = 88;

            commonBox.Controls.AddRange([
                chkDoGenre, chkPrependNew, chkDedup, chkNormalizeSeps, chkTitleCase, chkForcePopUpper, chkRemoveWorldPoland
            ]);
            sourceBox.Controls.AddRange([
                chkDoLabel, chkFallback, chkAppendDjPromo, chkWriteTxxx,
                chkWriteDmcComment, chkRepairDmcComment, chkCleanupComment, chkWriteDmcGenreTag,
                lblDjoidGenre, cmbDjoidGenreSource, cmbDjoidGenreWriteMode, chkScaleDjoidToTen,
                lblDjoidTags, chkWriteDjoidGenreTag, chkWriteDjoidSubgenreTag, chkWriteDjoidEnergyTag,
                chkWriteDjoidDanceTag, chkWriteDjoidEmotionTag, chkWriteDjoidKeyTag, chkWriteDjoidBpmTag,
                chkWriteDjoidComment
            ]);
            outputBox.Controls.AddRange([chkDryRun, chkCsvReport, chkPerFileBackup]);
            grp.Controls.AddRange([commonBox, sourceBox, outputBox]);

            btnStart.Left = 12; btnStart.Top = grp.Bottom + 10;
            btnLoadGrid.Left = 140; btnLoadGrid.Top = grp.Bottom + 10;
            btnCancel.Left = 268; btnCancel.Top = grp.Bottom + 10;
            progressBar.Left = 396; progressBar.Top = grp.Bottom + 16; progressBar.Width = 554;

            lblCounts.Left = 12; lblCounts.Top = progressBar.Bottom + 12; lblCounts.Width = 1160;
            txtLog.Left = 12; txtLog.Top = lblCounts.Bottom + 8; txtLog.Width = 1160; txtLog.Height = 270;
            txtLog.Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            txtLog.BackColor = Color.White;

            txtMp3Dir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnBrowseMp3.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtJson.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnBrowseJson.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            grp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnStart.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnLoadGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblCounts.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            AppUiStyle.StyleSecondaryButton(btnBrowseMp3);
            AppUiStyle.StyleSecondaryButton(btnBrowseJson);
            AppUiStyle.StylePrimaryButton(btnStart);
            AppUiStyle.StyleSecondaryButton(btnLoadGrid);
            AppUiStyle.StyleSecondaryButton(btnCancel);

            Controls.AddRange([btnStart, btnLoadGrid, btnCancel, progressBar, lblCounts, txtLog]);

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
            tip.SetToolTip(chkWriteTxxx, "Dopisuje/aktualizuje TXXX:LABEL wartością z pola LABEL (TPUB).");
            tip.SetToolTip(chkWriteDmcComment, "Dopisuje pełny komentarz DMC na końcu COMMENT, bez duplikowania istniejącego pełnego tekstu.");
            tip.SetToolTip(chkRepairDmcComment, "Usuwa niepełny/stary komentarz DMC i zapisuje pełny, poprawny tekst.");
            tip.SetToolTip(chkCleanupComment, "Czyści z COMMENT stare wpisy Record label, Key i Energy oraz porządkuje separatory.");
            tip.SetToolTip(chkWriteDmcGenreTag, "Zapisuje kopię finalnego GENRE z DJPromo do TXXX:DMC_GENRE, jako backup przed operacją DJOID.");
            tip.SetToolTip(chkWriteDjoidComment, "Zapisuje w COMMENT blok z wartościami DJOID: danceability, emotion, energy, key, genre i subgenre. Istniejący blok DJOID zostanie wymieniony, nie zduplikowany.");
            tip.SetToolTip(chkScaleDjoidToTen, "Skaluje wartości DJOID energy i danceability do zakresu 1-10.");
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
            cmbSource.SelectedIndexChanged += (s, e) => RefreshSourceUi();
            cmbDjoidGenreSource.SelectedIndexChanged += (s, e) => RefreshSourceUi();
            btnStart.Click += async (s, e) => await RunAsync();
            btnLoadGrid.Click += async (s, e) => await LoadGridAsync();
            btnCancel.Click += (s, e) => _runCts?.Cancel();
            _uiFlushTimer.Tick += (s, e) => FlushUiBatches();
            FormClosing += (s, e) =>
            {
                if (_gridForm != null && !_gridForm.IsDisposed)
                {
                    _gridForm.FormClosing -= GridFormUserClosing;
                    _gridForm.Close();
                    _gridForm.Dispose();
                    _gridForm = null;
                }
            };

            this.AcceptButton = btnStart;

            cmbSource.Items.AddRange(["DJPromo JSON", "DJOID JSON"]);
            cmbSource.SelectedIndex = 0;
            cmbDjoidGenreSource.Items.AddRange(["Nie zmieniaj GENRE", "Tylko genre", "Tylko subgenre", "Genre + subgenre"]);
            cmbDjoidGenreSource.SelectedIndex = 0;
            cmbDjoidGenreWriteMode.Items.AddRange(["Dopisz na końcu", "Dodaj na początek", "Podmień GENRE"]);
            cmbDjoidGenreWriteMode.SelectedIndex = 0;
            RefreshSourceUi();

            // wczytaj zapamiętane ustawienia
            LoadUiSettings();
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

        private void RefreshSourceUi()
        {
            var isDjoid = cmbSource.SelectedIndex == 1;
            chkDoGenre.Visible = !isDjoid;
            chkPrependNew.Visible = !isDjoid;
            chkDoLabel.Enabled = !isDjoid;
            chkWriteTxxx.Enabled = !isDjoid;
            chkFallback.Enabled = !isDjoid;
            chkAppendDjPromo.Enabled = !isDjoid;
            chkWriteDmcComment.Enabled = !isDjoid;
            chkRepairDmcComment.Enabled = !isDjoid;
            chkCleanupComment.Enabled = !isDjoid;
            chkWriteDmcGenreTag.Enabled = !isDjoid;
            chkRemoveWorldPoland.Enabled = true;
            chkNormalizeSeps.Enabled = true;
            chkTitleCase.Enabled = true;
            chkForcePopUpper.Enabled = true;

            chkDoLabel.Visible = !isDjoid;
            chkWriteTxxx.Visible = !isDjoid;
            chkFallback.Visible = !isDjoid;
            chkAppendDjPromo.Visible = !isDjoid;
            chkWriteDmcComment.Visible = !isDjoid;
            chkRepairDmcComment.Visible = !isDjoid;
            chkCleanupComment.Visible = !isDjoid;
            chkWriteDmcGenreTag.Visible = !isDjoid;

            cmbDjoidGenreSource.Enabled = isDjoid;
            cmbDjoidGenreWriteMode.Enabled = isDjoid && cmbDjoidGenreSource.SelectedIndex != 0;
            chkScaleDjoidToTen.Enabled = isDjoid;
            chkWriteDjoidGenreTag.Enabled = isDjoid;
            chkWriteDjoidSubgenreTag.Enabled = isDjoid;
            chkWriteDjoidEnergyTag.Enabled = isDjoid;
            chkWriteDjoidDanceTag.Enabled = isDjoid;
            chkWriteDjoidEmotionTag.Enabled = isDjoid;
            chkWriteDjoidKeyTag.Enabled = isDjoid;
            chkWriteDjoidBpmTag.Enabled = isDjoid;
            chkWriteDjoidComment.Enabled = isDjoid;

            lblDjoidGenre.Visible = isDjoid;
            lblDjoidTags.Visible = isDjoid;
            cmbDjoidGenreSource.Visible = isDjoid;
            cmbDjoidGenreWriteMode.Visible = isDjoid;
            chkScaleDjoidToTen.Visible = isDjoid;
            chkWriteDjoidGenreTag.Visible = isDjoid;
            chkWriteDjoidSubgenreTag.Visible = isDjoid;
            chkWriteDjoidEnergyTag.Visible = isDjoid;
            chkWriteDjoidDanceTag.Visible = isDjoid;
            chkWriteDjoidEmotionTag.Visible = isDjoid;
            chkWriteDjoidKeyTag.Visible = isDjoid;
            chkWriteDjoidBpmTag.Visible = isDjoid;
            chkWriteDjoidComment.Visible = isDjoid;

            if (isDjoid)
            {
                chkDedup.Top = 28;
                chkNormalizeSeps.Top = 58;
                chkTitleCase.Top = 88;
                chkForcePopUpper.Top = 118;
                chkRemoveWorldPoland.Top = 148;
            }
            else
            {
                chkDedup.Top = 96;
                chkNormalizeSeps.Top = 126;
                chkTitleCase.Top = 156;
                chkForcePopUpper.Top = 186;
                chkRemoveWorldPoland.Top = 216;
            }
        }

        private TaggingOptions BuildTaggingOptions() => new()
        {
            DataSource = cmbSource.SelectedIndex == 1 ? TagDataSource.DjoidJson : TagDataSource.DjPromoJson,
            DoGenre = cmbSource.SelectedIndex == 1 ? cmbDjoidGenreSource.SelectedIndex != 0 : chkDoGenre.Checked,
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
            WritePerFileBackup = chkPerFileBackup.Checked,
            WriteDmcComment = chkWriteDmcComment.Checked,
            RepairDmcComment = chkRepairDmcComment.Checked,
            CleanupCommentMetadata = chkCleanupComment.Checked,
            WriteDmcGenreTag = chkWriteDmcGenreTag.Checked,
            DjoidGenreSource = cmbDjoidGenreSource.SelectedIndex switch
            {
                1 => DjoidGenreSource.GenreOnly,
                2 => DjoidGenreSource.SubgenreOnly,
                3 => DjoidGenreSource.GenreAndSubgenre,
                _ => DjoidGenreSource.None
            },
            DjoidGenreWriteMode = cmbDjoidGenreWriteMode.SelectedIndex switch
            {
                1 => GenreWriteMode.Prepend,
                2 => GenreWriteMode.Replace,
                _ => GenreWriteMode.Append
            },
            WriteDjoidGenreTag = chkWriteDjoidGenreTag.Checked,
            WriteDjoidSubgenreTag = chkWriteDjoidSubgenreTag.Checked,
            WriteDjoidEnergyTag = chkWriteDjoidEnergyTag.Checked,
            WriteDjoidDanceabilityTag = chkWriteDjoidDanceTag.Checked,
            WriteDjoidEmotionTag = chkWriteDjoidEmotionTag.Checked,
            WriteDjoidKeyTag = chkWriteDjoidKeyTag.Checked,
            WriteDjoidBpmTag = chkWriteDjoidBpmTag.Checked,
            WriteDjoidComment = chkWriteDjoidComment.Checked,
            ScaleDjoidEnergyDanceToTen = chkScaleDjoidToTen.Checked
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

        private GridEditorForm EnsureGridForm()
        {
            if (_gridForm == null || _gridForm.IsDisposed)
            {
                _gridForm = new GridEditorForm();
                _gridForm.SaveRequested += async (s, e) => await SaveGridAsync();
                _gridForm.FormClosing += GridFormUserClosing;
                _gridForm.ApplyPersistedView(SettingsCache.GridPreset, SettingsCache.GridChangesOnly, SettingsCache.GridColumnLayout);
            }

            _gridForm.SetRows(_gridRows);
            return _gridForm;
        }

        private void ShowGridWindow()
        {
            var form = EnsureGridForm();
            var isDjoid = _gridRows.Any(r => r.IsDjoid);
            form.SetDjoidMode(isDjoid);
            if (!form.Visible)
                form.Show(this);
            else
                form.Activate();
        }

        private void GridFormUserClosing(object? sender, FormClosingEventArgs e)
        {
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

        private async Task LoadGridAsync()
        {
            if (!TryGetValidatedInputs(out var mp3, out var json)) return;

            var opts = BuildTaggingOptions();
            ToggleForRun(true);
            ResetRunUiState();
            _uiFlushTimer.Start();
            _runCts = new CancellationTokenSource();

            try
            {
                Log("INFO: Wczytuję pliki do GRID...");
                var rows = await Task.Run(() =>
                    TaggingLogic.LoadGridRows(mp3, json, opts,
                        onTotal: total => BeginInvoke(new Action(() => HandleTotal(total))),
                        onStep: EnqueueStep,
                        onLog: EnqueueLog,
                        cancellationToken: _runCts.Token),
                    _runCts.Token);

                FlushUiBatches();
                _gridRows = new BindingList<TagEditRow>(rows);
                ShowGridWindow();
                lblCounts.Text = $"Wczytano do GRID: {_gridRows.Count} plików | Do zapisu: {_gridRows.Count(r => r.Apply)}";
                Log($"INFO: GRID gotowy. Wierszy: {_gridRows.Count}, zaznaczonych do zapisu: {_gridRows.Count(r => r.Apply)}");
            }
            catch (OperationCanceledException)
            {
                FlushUiBatches();
                Log("INFO: Wczytywanie GRID anulowane.");
                lblCounts.Text = "Wczytywanie GRID anulowane.";
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
                SaveUiSettings();
            }
        }

        private async Task SaveGridAsync()
        {
            _gridForm?.EndGridEdit();

            var mp3 = txtMp3Dir.Text.Trim();
            if (!Directory.Exists(mp3))
            {
                MessageBox.Show(this, "Wskaż poprawny folder z MP3.");
                return;
            }

            if (_gridRows.Count == 0)
            {
                MessageBox.Show(this, "Najpierw wczytaj pliki do GRID.");
                return;
            }

            var selected = _gridRows.Count(r => r.Apply);
            if (selected == 0)
            {
                MessageBox.Show(this, "Zaznacz przynajmniej jeden wiersz do zapisu.");
                return;
            }

            var opts = BuildTaggingOptions();
            ToggleForRun(true);
            ResetRunUiState();
            _uiFlushTimer.Start();
            _runCts = new CancellationTokenSource();

            try
            {
                Log($"INFO: Zapisuję GRID. Wierszy zaznaczonych: {selected}");
                var result = await Task.Run(() =>
                    TaggingLogic.ApplyGridRows(mp3, _gridRows.ToList(), opts,
                        onTotal: total => BeginInvoke(new Action(() => HandleTotal(total))),
                        onStep: EnqueueStep,
                        onLog: EnqueueLog,
                        cancellationToken: _runCts.Token),
                    _runCts.Token);

                FlushUiBatches();
                _gridForm?.RefreshRows();
                lblCounts.Text = BuildSummaryText(result);
            }
            catch (OperationCanceledException)
            {
                FlushUiBatches();
                _gridForm?.RefreshRows();
                Log("INFO: Zapis GRID anulowany.");
                lblCounts.Text = "Zapis GRID anulowany.";
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
                SaveUiSettings();
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
            cmbSource.SelectedIndex = settings.DataSource == TagDataSource.DjoidJson ? 1 : 0;

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
            chkWriteDmcComment.Checked = settings.WriteDmcComment;
            chkRepairDmcComment.Checked = settings.RepairDmcComment;
            chkCleanupComment.Checked = settings.CleanupCommentMetadata;
            chkWriteDmcGenreTag.Checked = settings.WriteDmcGenreTag;
            chkDryRun.Checked = settings.DryRun;
            chkCsvReport.Checked = settings.WriteCsvReport;
            chkPerFileBackup.Checked = settings.WritePerFileBackup;
            cmbDjoidGenreSource.SelectedIndex = settings.DjoidGenreSource switch
            {
                DjoidGenreSource.GenreOnly => 1,
                DjoidGenreSource.SubgenreOnly => 2,
                DjoidGenreSource.GenreAndSubgenre => 3,
                _ => 0
            };
            cmbDjoidGenreWriteMode.SelectedIndex = settings.DjoidGenreWriteMode switch
            {
                GenreWriteMode.Prepend => 1,
                GenreWriteMode.Replace => 2,
                _ => 0
            };
            chkWriteDjoidGenreTag.Checked = settings.WriteDjoidGenreTag;
            chkWriteDjoidSubgenreTag.Checked = settings.WriteDjoidSubgenreTag;
            chkWriteDjoidEnergyTag.Checked = settings.WriteDjoidEnergyTag;
            chkWriteDjoidDanceTag.Checked = settings.WriteDjoidDanceabilityTag;
            chkWriteDjoidEmotionTag.Checked = settings.WriteDjoidEmotionTag;
            chkWriteDjoidKeyTag.Checked = settings.WriteDjoidKeyTag;
            chkWriteDjoidBpmTag.Checked = settings.WriteDjoidBpmTag;
            chkWriteDjoidComment.Checked = settings.WriteDjoidComment;
            chkScaleDjoidToTen.Checked = settings.ScaleDjoidEnergyDanceToTen;
            RefreshSourceUi();
        }

        private UiSettings ReadSettingsFromUi() => new()
        {
            Mp3Dir = txtMp3Dir.Text,
            JsonPath = txtJson.Text,
            DataSource = cmbSource.SelectedIndex == 1 ? TagDataSource.DjoidJson : TagDataSource.DjPromoJson,
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
            WriteDmcComment = chkWriteDmcComment.Checked,
            RepairDmcComment = chkRepairDmcComment.Checked,
            CleanupCommentMetadata = chkCleanupComment.Checked,
            WriteDmcGenreTag = chkWriteDmcGenreTag.Checked,
            DryRun = chkDryRun.Checked,
            WriteCsvReport = chkCsvReport.Checked,
            WritePerFileBackup = chkPerFileBackup.Checked,
            DjoidGenreSource = cmbDjoidGenreSource.SelectedIndex switch
            {
                1 => DjoidGenreSource.GenreOnly,
                2 => DjoidGenreSource.SubgenreOnly,
                3 => DjoidGenreSource.GenreAndSubgenre,
                _ => DjoidGenreSource.None
            },
            DjoidGenreWriteMode = cmbDjoidGenreWriteMode.SelectedIndex switch
            {
                1 => GenreWriteMode.Prepend,
                2 => GenreWriteMode.Replace,
                _ => GenreWriteMode.Append
            },
            WriteDjoidGenreTag = chkWriteDjoidGenreTag.Checked,
            WriteDjoidSubgenreTag = chkWriteDjoidSubgenreTag.Checked,
            WriteDjoidEnergyTag = chkWriteDjoidEnergyTag.Checked,
            WriteDjoidDanceabilityTag = chkWriteDjoidDanceTag.Checked,
            WriteDjoidEmotionTag = chkWriteDjoidEmotionTag.Checked,
            WriteDjoidKeyTag = chkWriteDjoidKeyTag.Checked,
            WriteDjoidBpmTag = chkWriteDjoidBpmTag.Checked,
            WriteDjoidComment = chkWriteDjoidComment.Checked,
            ScaleDjoidEnergyDanceToTen = chkScaleDjoidToTen.Checked
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
                if (_gridForm != null && !_gridForm.IsDisposed)
                {
                    var view = _gridForm.CaptureViewState();
                    SettingsCache.GridPreset = view.preset;
                    SettingsCache.GridChangesOnly = view.changesOnly;
                    SettingsCache.GridColumnLayout = view.layout;
                }
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(SettingsCache, Formatting.Indented));
            }
            catch { /* ignoruj */ }
        }

    }
}
