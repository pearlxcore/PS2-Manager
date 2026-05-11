using PS2_ISO_Manager.Helper;

namespace PS2_ISO_Manager;

public sealed class MoveDialog : Form
{
    private TextBox tbTemplate;
    private Label lblPreview;
    private RadioButton rbSelected;
    private RadioButton rbAll;
    private Button btnOk;
    private Button btnCancel;

    private readonly Ps2IsoInfo _selectedGame;
    private readonly IReadOnlyList<Ps2IsoInfo> _allGames;

    public string Template => tbTemplate.Text.Trim();
    public bool ApplyToAll => rbAll.Checked;

    public MoveDialog(Ps2IsoInfo selectedGame, IReadOnlyList<Ps2IsoInfo> allGames)
    {
        _selectedGame = selectedGame;
        _allGames = allGames;

        Text = "Move ISO to Separate Folder";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 240);

        InitializeControls();
        UpdatePreview();
    }

    private void InitializeControls()
    {
        var lblTemplate = new Label
        {
            Text = "Folder name template:",
            Location = new Point(12, 12),
            AutoSize = true
        };
        Controls.Add(lblTemplate);

        tbTemplate = new TextBox
        {
            Location = new Point(12, 32),
            Size = new Size(480, 23),
            Text = LoadLastTemplate()
        };
        tbTemplate.TextChanged += (_, _) => UpdatePreview();
        Controls.Add(tbTemplate);

        var lblParams = new Label
        {
            Text = "Click to insert:",
            Location = new Point(12, 62),
            AutoSize = true
        };
        Controls.Add(lblParams);

        int x = 12;
        foreach (var param in Ps2FileRenamer.ParameterNames)
        {
            var btn = new Button
            {
                Text = param,
                Location = new Point(x, 82),
                AutoSize = true,
                UseVisualStyleBackColor = true
            };
            btn.Click += (_, _) =>
            {
                tbTemplate.Text = tbTemplate.Text.Insert(tbTemplate.SelectionStart, btn.Text);
                tbTemplate.Focus();
            };
            Controls.Add(btn);
            x += btn.Width + 6;
        }

        var lblPreviewTitle = new Label
        {
            Text = "Preview:",
            Location = new Point(12, 115),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        };
        Controls.Add(lblPreviewTitle);

        lblPreview = new Label
        {
            Location = new Point(12, 133),
            AutoSize = true,
            ForeColor = Color.DarkBlue
        };
        Controls.Add(lblPreview);

        var gbScope = new GroupBox
        {
            Text = "Apply to",
            Location = new Point(12, 160),
            Size = new Size(200, 45)
        };
        rbSelected = new RadioButton
        {
            Text = "Selected game",
            Location = new Point(10, 18),
            Checked = true,
            AutoSize = true
        };
        rbAll = new RadioButton
        {
            Text = $"All games ({_allGames.Count})",
            Location = new Point(110, 18),
            AutoSize = true
        };
        rbSelected.CheckedChanged += (_, _) => UpdatePreview();
        gbScope.Controls.Add(rbSelected);
        gbScope.Controls.Add(rbAll);
        Controls.Add(gbScope);

        btnOk = new Button
        {
            Text = "Move",
            Location = new Point(336, 175),
            Size = new Size(75, 23),
            DialogResult = DialogResult.OK,
            UseVisualStyleBackColor = true
        };
        btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(417, 175),
            Size = new Size(75, 23),
            DialogResult = DialogResult.Cancel,
            UseVisualStyleBackColor = true
        };
        Controls.Add(btnOk);
        Controls.Add(btnCancel);

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        btnOk.Click += (_, _) => SaveTemplate(tbTemplate.Text.Trim());
    }

    private static string TemplatePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "move_template.txt");

    private static string LoadLastTemplate()
    {
        try
        {
            if (File.Exists(TemplatePath))
                return File.ReadAllText(TemplatePath).Trim();
        }
        catch { }
        return "{title}";
    }

    private static void SaveTemplate(string template)
    {
        try { File.WriteAllText(TemplatePath, template); }
        catch { }
    }

    private void InitializeComponent()
    {

    }

    private void UpdatePreview()
    {
        if (string.IsNullOrWhiteSpace(tbTemplate.Text))
        {
            lblPreview.Text = "(empty template)";
            return;
        }

        var game = rbSelected.Checked ? _selectedGame : _allGames.FirstOrDefault();
        if (game == null)
        {
            lblPreview.Text = "(no games)";
            return;
        }

        string folderName = Ps2FileRenamer.BuildFolderName(game, tbTemplate.Text);
        string parentDir = Path.GetDirectoryName(game.IsoPath) ?? "?";
        string fileName = Path.GetFileName(game.IsoPath);
        lblPreview.Text = $@"{parentDir}\{folderName}\{fileName}";
    }
}
