namespace PS2_ISO_Manager;

public sealed class DuplicateDialog : Form
{
    private DataGridView dgv;
    private Button btnDelete;
    private Label lblSummary;

    private readonly List<Ps2IsoInfo> _games;
    private readonly List<DuplicateEntry> _entries = new();
    private readonly List<Ps2IsoInfo> _deletedGames = new();
    public IReadOnlyList<Ps2IsoInfo> DeletedGames => _deletedGames;

    public DuplicateDialog(List<Ps2IsoInfo> games)
    {
        _games = games;

        Text = "Find Duplicates";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 500);
        MinimizeBox = false;

        InitializeControls();
        StyleGrid();
        Detect();
    }

    private void StyleGrid()
    {
        dgv.RowHeadersVisible = false;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.AllowUserToResizeRows = false;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.BackgroundColor = SystemColors.Window;
        dgv.BorderStyle = BorderStyle.FixedSingle;
        dgv.EnableHeadersVisualStyles = false;

        var headerStyle = dgv.ColumnHeadersDefaultCellStyle;
        headerStyle.BackColor = Color.FromArgb(240, 240, 240);
        headerStyle.ForeColor = Color.Black;
        headerStyle.Font = new Font(dgv.Font, FontStyle.Bold);
        headerStyle.SelectionBackColor = headerStyle.BackColor;
        headerStyle.SelectionForeColor = headerStyle.ForeColor;

        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
        dgv.DefaultCellStyle.SelectionForeColor = Color.Black;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
    }

    private void InitializeControls()
    {
        dgv = new DataGridView
        {
            Location = new Point(12, 12),
            Size = new Size(860, 400),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoGenerateColumns = false
        };

        var chkCol = new DataGridViewCheckBoxColumn
        {
            HeaderText = "Delete",
            Width = 50,
            ReadOnly = false
        };
        dgv.Columns.Add(chkCol);

        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File Name", DataPropertyName = "FileName", Width = 280 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Game ID", DataPropertyName = "GameId", Width = 110 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "SizeDisplay", Width = 80 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Directory", DataPropertyName = "Directory", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reason", DataPropertyName = "Reason", Width = 200 });

        Controls.Add(dgv);

        lblSummary = new Label
        {
            Location = new Point(12, 420),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            AutoSize = true
        };
        Controls.Add(lblSummary);

        btnDelete = new Button
        {
            Text = "Delete Checked",
            Location = new Point(775, 420),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Size = new Size(95, 23),
            UseVisualStyleBackColor = true
        };
        btnDelete.Click += DeleteChecked;
        Controls.Add(btnDelete);
    }

    private void Detect()
    {
        _entries.Clear();
        var valid = _games.Where(g => File.Exists(g.IsoPath)).ToList();

        // Duplicate = same Game ID + same size + same title + same version
        var duplicates = valid
            .Where(g => g.GameId != "Unknown")
            .GroupBy(g => (g.GameId, g.SizeBytes, g.Title, g.Version))
            .Where(g => g.Count() > 1);

        foreach (var group in duplicates)
        {
            bool first = true;
            foreach (var g in group)
            {
                _entries.Add(new DuplicateEntry { Game = g, IsFirst = first,
                    Reason = first ? $"Duplicate: {group.Key.Title} | {group.Key.GameId} | {g.SizeDisplay}" : "" });
                first = false;
            }
        }

        dgv.DataSource = null;
        dgv.DataSource = _entries;
        int groups = _entries.Where(e => e.IsFirst).Count();
        lblSummary.Text = $"{_entries.Count} files found across {groups} duplicate groups";
    }

    private void DeleteChecked(object sender, EventArgs e)
    {
        var toDelete = new List<DuplicateEntry>();
        foreach (DataGridViewRow row in dgv.Rows)
        {
            if (row.DataBoundItem is DuplicateEntry entry && entry.Delete)
                toDelete.Add(entry);
        }

        if (toDelete.Count == 0)
        {
            MessageBox.Show("No items checked for deletion.", "Info");
            return;
        }

        var confirm = MessageBox.Show(
            $"Delete {toDelete.Count} file(s)?\n\nThis cannot be undone.",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        int deleted = 0;
        foreach (var entry in toDelete)
        {
            try
            {
                if (File.Exists(entry.Game.IsoPath))
                {
                    File.Delete(entry.Game.IsoPath);
                    _deletedGames.Add(entry.Game);
                    _games.Remove(entry.Game);
                    _entries.Remove(entry);
                    deleted++;
                }
            }
            catch { }
        }

        dgv.DataSource = null;
        dgv.DataSource = _entries;
        lblSummary.Text = $"Deleted {deleted} file(s). {_entries.Count} entries remaining.";
    }
}

public class DuplicateEntry
{
    public bool Delete { get; set; }
    public string FileName => Game.FileName;
    public string GameId => Game.GameId;
    public string SizeDisplay => Game.SizeDisplay;
    public string Directory => Game.Directory;
    public string Reason { get; set; } = "";
    public bool IsFirst { get; set; }
    public Ps2IsoInfo Game { get; set; } = null!;
}
