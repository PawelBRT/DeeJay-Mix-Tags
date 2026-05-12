using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Mp3TaggerGUI
{
    internal sealed class GridEditorForm : Form
    {
        private readonly DataGridView _grid = new()
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            ReadOnly = false,
            EditMode = DataGridViewEditMode.EditOnEnter,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White,
            GridColor = Color.FromArgb(220, 225, 232),
            RowHeadersVisible = false
        };

        private readonly Button _saveButton = new() { Text = "Zapisz GRID", Width = 120, Height = 34 };
        private readonly Button _closeButton = new() { Text = "Zamknij", Width = 100, Height = 34 };
        private readonly Button _clearFiltersButton = new() { Text = "Wyczyść filtry", Width = 130, Height = 34 };
        private readonly Button _bulkSetButton = new() { Text = "Ustaw w zazn.", Width = 120, Height = 34 };
        private readonly ComboBox _viewPreset = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox _changesOnly = new() { Text = "Changes only", AutoSize = true };
        private readonly Label _summaryLabel = new() { AutoSize = true, Text = "GRID: 0" };
        private readonly TextBox _filterArtist = new() { Width = 130, BorderStyle = BorderStyle.FixedSingle };
        private readonly TextBox _filterTitle = new() { Width = 130, BorderStyle = BorderStyle.FixedSingle };
        private readonly TextBox _filterGenre = new() { Width = 130, BorderStyle = BorderStyle.FixedSingle };
        private readonly TextBox _filterLabel = new() { Width = 130, BorderStyle = BorderStyle.FixedSingle };
        private readonly TextBox _filterBpm = new() { Width = 90, BorderStyle = BorderStyle.FixedSingle };
        private readonly TextBox _filterKey = new() { Width = 90, BorderStyle = BorderStyle.FixedSingle };
        private BindingList<TagEditRow>? _rows;
        private List<TagEditRow> _sourceRows = new();
        private readonly List<(string prop, bool asc)> _sorts = new();

        public event EventHandler? SaveRequested;

        public GridEditorForm()
        {
            Text = "DeeJay Mix Tags - GRID";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1400, 760);
            MinimumSize = new Size(1000, 560);
            AppUiStyle.ApplyForm(this);

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(10, 8, 10, 8), BackColor = AppUiStyle.AppBackColor };
            _saveButton.Left = 10;
            _saveButton.Top = 8;
            _closeButton.Left = _saveButton.Right + 8;
            _closeButton.Top = 8;
            _bulkSetButton.Left = _closeButton.Right + 8;
            _bulkSetButton.Top = 8;
            _clearFiltersButton.Left = _bulkSetButton.Right + 8;
            _clearFiltersButton.Top = 8;
            _viewPreset.Left = _clearFiltersButton.Right + 8;
            _viewPreset.Top = 10;
            _changesOnly.Left = _viewPreset.Right + 12;
            _changesOnly.Top = 14;
            _summaryLabel.Left = _changesOnly.Right + 16;
            _summaryLabel.Top = 16;
            _summaryLabel.ForeColor = AppUiStyle.TextColor;

            var lblA = new Label { Text = "Artysta", Left = 10, Top = 52, AutoSize = true };
            _filterArtist.Left = 64; _filterArtist.Top = 48;
            var lblT = new Label { Text = "Tytuł", Left = 204, Top = 52, AutoSize = true };
            _filterTitle.Left = 242; _filterTitle.Top = 48;
            var lblG = new Label { Text = "Genre", Left = 382, Top = 52, AutoSize = true };
            _filterGenre.Left = 426; _filterGenre.Top = 48;
            var lblL = new Label { Text = "Label", Left = 566, Top = 52, AutoSize = true };
            _filterLabel.Left = 606; _filterLabel.Top = 48;
            var lblB = new Label { Text = "BPM", Left = 746, Top = 52, AutoSize = true };
            _filterBpm.Left = 782; _filterBpm.Top = 48;
            var lblK = new Label { Text = "KEY", Left = 882, Top = 52, AutoSize = true };
            _filterKey.Left = 918; _filterKey.Top = 48;

            toolbar.Controls.AddRange([
                _saveButton, _closeButton, _bulkSetButton, _clearFiltersButton, _summaryLabel,
                _viewPreset, _changesOnly,
                lblA, _filterArtist, lblT, _filterTitle, lblG, _filterGenre, lblL, _filterLabel, lblB, _filterBpm, lblK, _filterKey
            ]);

            ConfigureGrid();
            AppUiStyle.StylePrimaryButton(_saveButton);
            AppUiStyle.StyleSecondaryButton(_closeButton);
            AppUiStyle.StyleSecondaryButton(_bulkSetButton);
            AppUiStyle.StyleSecondaryButton(_clearFiltersButton);
            Controls.Add(_grid);
            Controls.Add(toolbar);

            _saveButton.Click += (s, e) => SaveRequested?.Invoke(this, EventArgs.Empty);
            _closeButton.Click += (s, e) => Hide();
            _bulkSetButton.Click += (s, e) => BulkSetSelected();
            _clearFiltersButton.Click += (s, e) => ClearFilters();
            _viewPreset.SelectedIndexChanged += (s, e) => ApplyPreset();
            _changesOnly.CheckedChanged += (s, e) => RebuildView();
            _filterArtist.TextChanged += (s, e) => RebuildView();
            _filterTitle.TextChanged += (s, e) => RebuildView();
            _filterGenre.TextChanged += (s, e) => RebuildView();
            _filterLabel.TextChanged += (s, e) => RebuildView();
            _filterBpm.TextChanged += (s, e) => RebuildView();
            _filterKey.TextChanged += (s, e) => RebuildView();
            _grid.CellBeginEdit += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0)
                    return;

                var columnName = _grid.Columns[e.ColumnIndex].DataPropertyName;
                if (columnName != nameof(TagEditRow.Genre)
                    && columnName != nameof(TagEditRow.Label)
                    && columnName != nameof(TagEditRow.Album)
                    && columnName != nameof(TagEditRow.Year)
                    && columnName != nameof(TagEditRow.Track)
                    && columnName != nameof(TagEditRow.Bpm)
                    && columnName != nameof(TagEditRow.Key)
                    && columnName != nameof(TagEditRow.Comment))
                    return;

                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 232);
            };
            _grid.CellEndEdit += (s, e) =>
            {
                if (e.RowIndex >= 0)
                    _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Empty;
            };
            _grid.CellValueChanged += (s, e) =>
            {
                if (_rows == null || e.RowIndex < 0 || e.RowIndex >= _rows.Count)
                    return;

                var columnName = _grid.Columns[e.ColumnIndex].DataPropertyName;
                if (columnName == nameof(TagEditRow.Genre)
                    || columnName == nameof(TagEditRow.Label)
                    || columnName == nameof(TagEditRow.Album)
                    || columnName == nameof(TagEditRow.Year)
                    || columnName == nameof(TagEditRow.Track)
                    || columnName == nameof(TagEditRow.Bpm)
                    || columnName == nameof(TagEditRow.Key)
                    || columnName == nameof(TagEditRow.Comment))
                {
                    _rows[e.RowIndex].Apply = true;
                    _rows[e.RowIndex].Status = "Edytowano";
                    _grid.InvalidateRow(e.RowIndex);
                }

                UpdateSummary(_rows);
            };
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.ColumnIndex < 0 || e.ColumnIndex >= _grid.Columns.Count)
                    return;

                var prop = _grid.Columns[e.ColumnIndex].DataPropertyName;
                if (string.IsNullOrWhiteSpace(prop))
                    return;

                var shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
                AddOrToggleSort(prop, shift);
                RebuildView();
            };
            _grid.CellValidating += GridCellValidating;
            KeyPreview = true;
            KeyDown += GridEditorForm_KeyDown;
            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };

            _viewPreset.Items.AddRange(["Basic", "Extended", "DJOID"]);
            _viewPreset.SelectedIndex = 1;
        }

        public void SetRows(BindingList<TagEditRow> rows)
        {
            _sourceRows = rows.ToList();
            RebuildView();
        }

        public void EndGridEdit() => _grid.EndEdit();

        public void RefreshRows()
        {
            _grid.Refresh();
            UpdateSummary(_sourceRows);
        }

        public void SetDjoidMode(bool isDjoid)
        {
            ToggleDjoidColumns(isDjoid);
            if (_viewPreset.SelectedIndex == 2 || isDjoid)
                _viewPreset.SelectedItem = "DJOID";
            else if (_viewPreset.SelectedIndex < 0)
                _viewPreset.SelectedItem = "Extended";

            ApplyPreset();
        }

        public void ApplyPersistedView(string preset, bool changesOnly, string layout)
        {
            if (_viewPreset.Items.Contains(preset))
                _viewPreset.SelectedItem = preset;
            _changesOnly.Checked = changesOnly;
            if (!string.IsNullOrWhiteSpace(layout))
                ApplyColumnLayout(layout);
            RebuildView();
        }

        public (string preset, bool changesOnly, string layout) CaptureViewState()
        {
            return (_viewPreset.SelectedItem?.ToString() ?? "Extended", _changesOnly.Checked, CaptureColumnLayout());
        }

        private void UpdateSummary(IReadOnlyCollection<TagEditRow> rows)
        {
            var selected = 0;
            foreach (var row in rows)
            {
                if (row.Apply)
                    selected++;
            }

            _summaryLabel.Text = $"GRID: {rows.Count} | Do zapisu: {selected}";
        }

        private void RebuildView()
        {
            IEnumerable<TagEditRow> query = _sourceRows;

            query = query.Where(r =>
                ContainsCi(r.Artist, _filterArtist.Text) &&
                ContainsCi(r.Title, _filterTitle.Text) &&
                ContainsCi(r.Genre, _filterGenre.Text) &&
                ContainsCi(r.Label, _filterLabel.Text) &&
                ContainsCi(r.Bpm, _filterBpm.Text) &&
                ContainsCi(r.Key, _filterKey.Text));

            if (_changesOnly.Checked)
                query = query.Where(HasEffectiveChange);

            var sorted = ApplySort(query).ToList();
            _rows = new BindingList<TagEditRow>(sorted);
            _grid.DataSource = _rows;
            UpdateSummary(_sourceRows);
        }

        private static bool HasEffectiveChange(TagEditRow r)
        {
            return !string.Equals(r.CurrentGenre ?? "", r.Genre ?? "", StringComparison.Ordinal)
                || !string.Equals(r.CurrentLabel ?? "", r.Label ?? "", StringComparison.Ordinal)
                || !string.Equals(r.CurrentAlbum ?? "", r.Album ?? "", StringComparison.Ordinal)
                || !string.Equals(r.CurrentYear ?? "", r.Year ?? "", StringComparison.Ordinal)
                || !string.Equals(r.CurrentTrack ?? "", r.Track ?? "", StringComparison.Ordinal)
                || !string.Equals(r.CurrentBpm ?? "", r.Bpm ?? "", StringComparison.Ordinal)
                || !string.Equals(r.CurrentKey ?? "", r.Key ?? "", StringComparison.Ordinal)
                || !string.Equals(r.CurrentComment ?? "", r.Comment ?? "", StringComparison.Ordinal);
        }

        private IEnumerable<TagEditRow> ApplySort(IEnumerable<TagEditRow> rows)
        {
            if (_sorts.Count == 0)
                return rows;

            IOrderedEnumerable<TagEditRow>? ordered = null;
            foreach (var (prop, asc) in _sorts)
            {
                Func<TagEditRow, string> key = r => GetSortValue(r, prop);
                ordered = ordered == null
                    ? (asc ? rows.OrderBy(key) : rows.OrderByDescending(key))
                    : (asc ? ordered.ThenBy(key) : ordered.ThenByDescending(key));
            }

            return ordered ?? rows;
        }

        private static string GetSortValue(TagEditRow row, string prop)
        {
            return prop switch
            {
                nameof(TagEditRow.Artist) => row.Artist ?? "",
                nameof(TagEditRow.Title) => row.Title ?? "",
                nameof(TagEditRow.Album) => row.Album ?? "",
                nameof(TagEditRow.Year) => row.Year ?? "",
                nameof(TagEditRow.Track) => row.Track ?? "",
                nameof(TagEditRow.Bpm) => row.Bpm ?? "",
                nameof(TagEditRow.Key) => row.Key ?? "",
                nameof(TagEditRow.Genre) => row.Genre ?? "",
                nameof(TagEditRow.Label) => row.Label ?? "",
                nameof(TagEditRow.Status) => row.Status ?? "",
                _ => ""
            };
        }

        private void AddOrToggleSort(string prop, bool keepExisting)
        {
            var idx = _sorts.FindIndex(s => s.prop == prop);
            if (!keepExisting)
            {
                if (idx >= 0)
                {
                    var wasAsc = _sorts[idx].asc;
                    _sorts.Clear();
                    _sorts.Add((prop, !wasAsc));
                }
                else
                {
                    _sorts.Clear();
                    _sorts.Add((prop, true));
                }
                return;
            }

            if (idx >= 0)
            {
                var cur = _sorts[idx];
                _sorts[idx] = (cur.prop, !cur.asc);
            }
            else
            {
                _sorts.Add((prop, true));
            }
        }

        private static bool ContainsCi(string? value, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;
            return (value ?? "").IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ClearFilters()
        {
            _filterArtist.Text = "";
            _filterTitle.Text = "";
            _filterGenre.Text = "";
            _filterLabel.Text = "";
            _filterBpm.Text = "";
            _filterKey.Text = "";
            _sorts.Clear();
            RebuildView();
        }

        private void GridEditorForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                _filterArtist.Focus();
                _filterArtist.SelectAll();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.S)
            {
                SaveRequested?.Invoke(this, EventArgs.Empty);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.Enter)
            {
                BulkSetSelected();
                e.SuppressKeyPress = true;
            }
        }

        private void BulkSetSelected()
        {
            if (_grid.SelectedCells.Count == 0)
                return;

            var prop = _grid.SelectedCells.Cast<DataGridViewCell>()
                .Select(c => _grid.Columns[c.ColumnIndex].DataPropertyName)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (prop.Count != 1 || !IsEditableProperty(prop[0]))
            {
                MessageBox.Show(this, "Zaznacz komórki jednej edytowalnej kolumny.");
                return;
            }

            using var dlg = new BulkValueForm(prop[0]);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            var value = dlg.ValueText ?? "";
            foreach (DataGridViewCell cell in _grid.SelectedCells)
            {
                if (cell.RowIndex < 0 || cell.ColumnIndex < 0)
                    continue;
                if (_grid.Rows[cell.RowIndex].DataBoundItem is not TagEditRow row)
                    continue;

                SetPropertyValue(row, prop[0], value);
                row.Apply = true;
                row.Status = "Edytowano";
            }

            _grid.Refresh();
            UpdateSummary(_sourceRows);
        }

        private static bool IsEditableProperty(string prop)
        {
            return prop == nameof(TagEditRow.Genre)
                || prop == nameof(TagEditRow.Label)
                || prop == nameof(TagEditRow.Album)
                || prop == nameof(TagEditRow.Year)
                || prop == nameof(TagEditRow.Track)
                || prop == nameof(TagEditRow.Bpm)
                || prop == nameof(TagEditRow.Key)
                || prop == nameof(TagEditRow.Comment);
        }

        private static void SetPropertyValue(TagEditRow row, string prop, string value)
        {
            switch (prop)
            {
                case nameof(TagEditRow.Genre): row.Genre = value; break;
                case nameof(TagEditRow.Label): row.Label = value; break;
                case nameof(TagEditRow.Album): row.Album = value; break;
                case nameof(TagEditRow.Year): row.Year = value; break;
                case nameof(TagEditRow.Track): row.Track = value; break;
                case nameof(TagEditRow.Bpm): row.Bpm = value; break;
                case nameof(TagEditRow.Key): row.Key = value; break;
                case nameof(TagEditRow.Comment): row.Comment = value; break;
            }
        }

        private string CaptureColumnLayout()
        {
            var parts = new List<string>();
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (string.IsNullOrWhiteSpace(col.DataPropertyName))
                    continue;
                var visible = col.Visible ? "1" : "0";
                parts.Add($"{col.DataPropertyName}:{col.DisplayIndex}:{col.Width}:{visible}");
            }
            return string.Join("|", parts);
        }

        private void ApplyColumnLayout(string layout)
        {
            var tokens = layout.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var parts = token.Split(':');
                if (parts.Length < 4)
                    continue;

                var prop = parts[0];
                var col = _grid.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.DataPropertyName, prop, StringComparison.Ordinal));
                if (col == null)
                    continue;

                if (int.TryParse(parts[1], out var di) && di >= 0 && di < _grid.Columns.Count)
                    col.DisplayIndex = di;
                if (int.TryParse(parts[2], out var w) && w > 20)
                    col.Width = w;
                col.Visible = parts[3] == "1";
            }
        }

        private void ConfigureGrid()
        {
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 239, 245);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = AppUiStyle.TextColor;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            _grid.ColumnHeadersHeight = 34;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 232, 255);
            _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);

            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(TagEditRow.Apply), HeaderText = "Apply", Width = 56, ReadOnly = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.FileName), HeaderText = "Plik", ReadOnly = true, Width = 210 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Artist), HeaderText = "Artysta", ReadOnly = true, Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Title), HeaderText = "Tytuł", ReadOnly = true, Width = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Version), HeaderText = "Wersja", ReadOnly = true, Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Album), HeaderText = "Album", ReadOnly = false, Width = 200 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Year), HeaderText = "Rok", ReadOnly = false, Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Track), HeaderText = "Track", ReadOnly = false, Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Bpm), HeaderText = "BPM", ReadOnly = false, Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Key), HeaderText = "KEY", ReadOnly = false, Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Comment), HeaderText = "Komentarz", ReadOnly = false, Width = 220 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Genre), HeaderText = "GENRE", Width = 280, ReadOnly = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Label), HeaderText = "LABEL", Width = 220, ReadOnly = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.DjoidGenre), HeaderText = "DJOID genre", ReadOnly = true, Width = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.DjoidSubgenre), HeaderText = "DJOID subgenre", ReadOnly = true, Width = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.DjoidEnergy), HeaderText = "DJOID energy", ReadOnly = true, Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.DjoidDanceability), HeaderText = "DJOID dance", ReadOnly = true, Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.DjoidEmotion), HeaderText = "DJOID emotion", ReadOnly = true, Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.DjoidKey), HeaderText = "DJOID key", ReadOnly = true, Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.DjoidBpm), HeaderText = "DJOID bpm", ReadOnly = true, Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.Status), HeaderText = "Status", ReadOnly = true, Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TagEditRow.FilePath), HeaderText = "Ścieżka", ReadOnly = true, Width = 320 });

            ToggleDjoidColumns(false);
            ApplyPreset();
        }

        private void ToggleDjoidColumns(bool visible)
        {
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                var name = col.DataPropertyName;
                if (name == nameof(TagEditRow.DjoidGenre)
                    || name == nameof(TagEditRow.DjoidSubgenre)
                    || name == nameof(TagEditRow.DjoidEnergy)
                    || name == nameof(TagEditRow.DjoidDanceability)
                    || name == nameof(TagEditRow.DjoidEmotion)
                    || name == nameof(TagEditRow.DjoidKey)
                    || name == nameof(TagEditRow.DjoidBpm))
                {
                    col.Visible = visible;
                }
            }
        }

        private void ApplyPreset()
        {
            var preset = (_viewPreset.SelectedItem?.ToString() ?? "Extended").ToLowerInvariant();
            var basic = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(TagEditRow.Apply), nameof(TagEditRow.FileName), nameof(TagEditRow.Artist), nameof(TagEditRow.Title),
                nameof(TagEditRow.Version), nameof(TagEditRow.Genre), nameof(TagEditRow.Label), nameof(TagEditRow.Status)
            };

            var extended = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(TagEditRow.Apply), nameof(TagEditRow.FileName), nameof(TagEditRow.Artist), nameof(TagEditRow.Title), nameof(TagEditRow.Version),
                nameof(TagEditRow.Album), nameof(TagEditRow.Year), nameof(TagEditRow.Track), nameof(TagEditRow.Bpm),
                nameof(TagEditRow.Key), nameof(TagEditRow.Comment), nameof(TagEditRow.Genre), nameof(TagEditRow.Label),
                nameof(TagEditRow.Status)
            };

            var djoid = new HashSet<string>(extended, StringComparer.Ordinal)
            {
                nameof(TagEditRow.DjoidGenre), nameof(TagEditRow.DjoidSubgenre), nameof(TagEditRow.DjoidEnergy),
                nameof(TagEditRow.DjoidDanceability), nameof(TagEditRow.DjoidEmotion), nameof(TagEditRow.DjoidKey), nameof(TagEditRow.DjoidBpm)
            };

            var visibleSet = preset switch
            {
                "basic" => basic,
                "djoid" => djoid,
                _ => extended
            };

            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (string.IsNullOrWhiteSpace(col.DataPropertyName))
                    continue;
                col.Visible = visibleSet.Contains(col.DataPropertyName);
            }
        }

        private void GridCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var prop = _grid.Columns[e.ColumnIndex].DataPropertyName;
            if (prop != nameof(TagEditRow.Year) && prop != nameof(TagEditRow.Track) && prop != nameof(TagEditRow.Bpm))
                return;

            var raw = (e.FormattedValue?.ToString() ?? "").Trim();
            var ok = string.IsNullOrWhiteSpace(raw) || uint.TryParse(raw, out _);

            if (!ok)
            {
                e.Cancel = true;
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.FromArgb(255, 230, 230);
                _grid.Rows[e.RowIndex].ErrorText = "Wartość musi być liczbą całkowitą lub pusta.";
                return;
            }

            _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.Empty;
            _grid.Rows[e.RowIndex].ErrorText = "";
        }

        private sealed class BulkValueForm : Form
        {
            private readonly TextBox _txt = new() { Left = 12, Top = 34, Width = 360, BorderStyle = BorderStyle.FixedSingle };
            public string ValueText => _txt.Text;

            public BulkValueForm(string propName)
            {
                Text = $"Ustaw wartość: {propName}";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(384, 110);
                AppUiStyle.ApplyForm(this);

                var lbl = new Label { Left = 12, Top = 12, AutoSize = true, Text = "Wartość dla zaznaczonych komórek:" };
                var ok = new Button { Text = "OK", Left = 216, Top = 70, Width = 75, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Anuluj", Left = 297, Top = 70, Width = 75, DialogResult = DialogResult.Cancel };
                AppUiStyle.StylePrimaryButton(ok);
                AppUiStyle.StyleSecondaryButton(cancel);

                Controls.AddRange([lbl, _txt, ok, cancel]);
                AcceptButton = ok;
                CancelButton = cancel;
            }
        }
    }
}
