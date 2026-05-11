using PS2_ISO_Manager.Helper;
using PS2_ISO_Manager.Helper.Manifest;
using PS2_ISO_Manager.Helper.Ps2Iso;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PS2_ISO_Manager
{
    public partial class Form1 : Form
    {
        private BindingList<Ps2IsoInfo> _games = new();
        private List<Ps2IsoInfo> _gamesOriginal = new(); // store full list

        private Ps2GameDatabase _db;
        private bool _sizeSortAscending = true;

        public Form1()
        {
            _games = new BindingList<Ps2IsoInfo>();

            InitializeComponent();
            SetupAppIcon();
            SetupDatabaseMenu();
            InitializeGameList();
            LoadDatabase(DatabaseManager.DefaultPath);

        }

        private void SetupAppIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
                this.Icon = new Icon(iconPath);
        }

        private void SetupDatabaseMenu()
        {
            var sep = new ToolStripSeparator();
            toolsToolStripMenuItem.DropDownItems.Add(sep);

            var downloadDb = new ToolStripMenuItem("Download Latest Database");
            downloadDb.Click += downloadLatestDatabase_Click;
            toolsToolStripMenuItem.DropDownItems.Add(downloadDb);

            var flatten = new ToolStripMenuItem("Flatten Directory (Move All to Root)");
            flatten.Click += flattenDirectory_Click;
            toolsToolStripMenuItem.DropDownItems.Add(flatten);

            var fixExt = new ToolStripMenuItem("Fix File Extensions (.bin ↔ .iso)");
            fixExt.Click += fixExtensions_Click;
            toolsToolStripMenuItem.DropDownItems.Add(fixExt);
        }

        private void SetStatus(string message)
        {
            DebugLog.Write(message);
            if (toolStrip1.InvokeRequired)
                toolStrip1.Invoke(() => lblStatus.Text = message);
            else
                lblStatus.Text = message;
        }

        private void InitializeGameList()
        {
            _games = new BindingList<Ps2IsoInfo>();
            _gamesOriginal = new List<Ps2IsoInfo>(_games);

            _db = new Ps2GameDatabase(DatabaseManager.DefaultPath);
            InitializeDgv();
            DgvSmoothScroll.EnableDoubleBuffer(dgvIsos);

        }

        private void button1_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select PS2 ISO Directory",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            _games.Clear();

            string ps2Dir = dialog.SelectedPath;
            SetStatus($"Scanning: {ps2Dir}");

            var isoFiles = GetDiscFiles(ps2Dir);
            int count = 0;
            foreach (var isoPath in isoFiles)
            {
                try
                {
                    using var reader = new Ps2IsoReader(isoPath, _db);
                    Ps2IsoInfo info = reader.Read(isoPath);
                    _games.Add(info);
                    AutoFixExtension(reader, info);
                }
                catch
                {
                    long size = 0;
                    try { size = new FileInfo(isoPath).Length; } catch { }
                    _games.Add(new Ps2IsoInfo
                    {
                        IsoPath = isoPath,
                        SizeBytes = size,
                        Validity = Ps2IsoValidity.ReadError
                    });
                }
                count++;
                if (count % 10 == 0)
                    SetStatus($"Scanning: {count} / {isoFiles.Count}");
            }

            SetStatus($"Scanned {_games.Count} files");
            DebugLog.Write($"Scanned {_games.Count} files from: {ps2Dir}");
            MessageBox.Show($"Scan complete. Found {_games.Count} file(s).");
        }

        private static List<string> GetDiscFiles(string directory)
        {
            string[] patterns = { "*.iso", "*.bin", "*.img" };
            var files = new List<string>();
            foreach (var pattern in patterns)
                files.AddRange(Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories));
            return files;
        }

        private static void AutoFixExtension(Ps2IsoReader reader, Ps2IsoInfo info)
        {
            if (!reader.WasReadAsCd) return;

            string path = reader.IsoPath;
            string ext = Path.GetExtension(path).ToLower();
            if (ext != ".iso") return;

            string newPath = Path.ChangeExtension(path, ".bin");
            if (File.Exists(newPath)) return;

            try
            {
                File.Move(path, newPath);
                info.IsoPath = newPath;
                DebugLog.Write($"Auto-fixed extension: {Path.GetFileName(path)} → {Path.GetFileName(newPath)}");
            }
            catch { }
        }

        public static void Export(IEnumerable<Ps2IsoInfo> games, string outputPath)
        {
            var sb = new StringBuilder(64 * 1024);

            foreach (var g in games)
            {
                sb.AppendLine($"Title        : {g.Title}");
                sb.AppendLine($"Title Source : {g.TitleSource}");
                sb.AppendLine($"Game ID      : {g.GameId}");
                sb.AppendLine($"Region       : {g.Region}");
                sb.AppendLine($"Disc         : {g.DiscNumber}");
                sb.AppendLine($"Multi-Disc   : {(g.IsMultiDisc ? "Yes" : "No")}");
                sb.AppendLine($"Size         : {g.SizeDisplay}");
                sb.AppendLine($"CRC32        : {(g.Crc32.HasValue ? g.Crc32.Value.ToString("X8") : "N/A")}");
                sb.AppendLine($"Validity     : {g.Validity}");
                sb.AppendLine($"ISO Path     : {g.IsoPath}");
                sb.AppendLine($"Scanned At   : {g.ScannedAt:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine(new string('-', 60));
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (_games.Count == 0)
            {
                MessageBox.Show("Nothing to export. Please scan first.");
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "Text File (*.txt)|*.txt",
                FileName = "ps2_library.txt",
                Title = "Export PS2 ISO List"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            Ps2TxtExporter.Export(_games, dialog.FileName);

            SetStatus($"Exported {_games.Count} games to TXT");
            DebugLog.Write($"Exported {_games.Count} games to TXT: {dialog.FileName}");
            MessageBox.Show("Export completed successfully.");
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void InitializeDgv()
        {
            // Binding
            dgvIsos.AutoGenerateColumns = false;
            dgvIsos.DataSource = _games;

            // Clean look
            dgvIsos.RowHeadersVisible = false;
            dgvIsos.AllowUserToAddRows = false;
            dgvIsos.AllowUserToDeleteRows = false;
            dgvIsos.AllowUserToResizeRows = false;

            dgvIsos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvIsos.MultiSelect = false;
            dgvIsos.ReadOnly = true;

            dgvIsos.BackgroundColor = SystemColors.Window;
            dgvIsos.BorderStyle = BorderStyle.FixedSingle;

            // 🔴 CRITICAL: disable system header theming
            dgvIsos.EnableHeadersVisualStyles = false;

            // Header styling (NO BLUE EVER)
            var headerStyle = dgvIsos.ColumnHeadersDefaultCellStyle;
            headerStyle.BackColor = Color.FromArgb(240, 240, 240);
            headerStyle.ForeColor = Color.Black;
            headerStyle.Font = new Font(dgvIsos.Font, FontStyle.Bold);
            headerStyle.SelectionBackColor = headerStyle.BackColor;
            headerStyle.SelectionForeColor = headerStyle.ForeColor;

            // Row selection colors
            dgvIsos.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
            dgvIsos.DefaultCellStyle.SelectionForeColor = Color.Black;

            dgvIsos.AlternatingRowsDefaultCellStyle.BackColor =
                Color.FromArgb(245, 245, 245);

            // Columns
            dgvIsos.Columns.Clear();

            dgvIsos.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "File Name",
                DataPropertyName = nameof(Ps2IsoInfo.FileName),
                Width = 260
            });

            dgvIsos.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Title",
                DataPropertyName = nameof(Ps2IsoInfo.Title),
                Width = 250
            });

            dgvIsos.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Game ID",
                DataPropertyName = nameof(Ps2IsoInfo.GameId),
                Width = 110
            });

            dgvIsos.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Region",
                DataPropertyName = nameof(Ps2IsoInfo.Region),
                Width = 70
            });

            dgvIsos.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Disc",
                DataPropertyName = nameof(Ps2IsoInfo.DiscNumber),
                Width = 50
            });

            dgvIsos.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Size",
                DataPropertyName = nameof(Ps2IsoInfo.SizeDisplay),
                Width = 90,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dgvIsos.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Directory",
                DataPropertyName = nameof(Ps2IsoInfo.Directory),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            // 🔑 Prevent header focus after sort/click
            dgvIsos.ColumnHeaderMouseClick += (s, e) =>
            {
                dgvIsos.ClearSelection();
            };
        }

        private bool IsFileMissing(Ps2IsoInfo game)
        {
            return !File.Exists(game.IsoPath);
        }


        private async void Form1_Shown(object sender, EventArgs e)
        {
            if (!DatabaseManager.Exists(DatabaseManager.DefaultPath))
            {
                var dl = MessageBox.Show(
                    "PS2 game database not found.\n\nDownload latest from GitHub?\n(github.com/niemasd/GameDB-PS2)",
                    "Database Missing",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (dl == DialogResult.Yes)
                {
                    SetStatus("Downloading database...");
                    string? result = await DatabaseManager.DownloadLatestAsync();
                    if (result != null)
                    {
                        LoadDatabase(result);
                        _db = new Ps2GameDatabase(result);
                        SetStatus("Database downloaded ✓");
                    }
                    else
                    {
                        SetStatus("Database download failed");
                        MessageBox.Show("Could not download database. The app will work but titles won't be available.", "Warning",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }

            var manifest = ManifestManager.LoadLatest();

            if (manifest != null)
            {
                var choice = MessageBox.Show(
                    $"A saved manifest was found ({manifest.Games.Count} games, saved {manifest.SavedAt:yyyy-MM-dd HH:mm}).\n\n" +
                    "Yes = Load from manifest (fast)\n" +
                    "No  = Scan ISO directory instead\n" +
                    "Cancel = Start with empty library",
                    "Load Manifest?",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (choice == DialogResult.Yes)
                {
                    _games.Clear();
                    foreach (var game in manifest.Games)
                        _games.Add(game);

                    EnrichFromDatabase();
                    _gamesOriginal = _games.ToList();

                    SetStatus($"Loaded {manifest.Games.Count} games from manifest");
                    DebugLog.Write($"Loaded {manifest.Games.Count} games from manifest cache (saved {manifest.SavedAt:yyyy-MM-dd HH:mm})");
                    _ = AutoDownloadCoversAsync();
                    return;
                }
                else if (choice == DialogResult.Cancel)
                {
                    DebugLog.Write("Startup: user chose empty library");
                    return;
                }
                // No = fall through to directory scan
            }

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select PS2 ISO Directory",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            SetStatus($"Scanning {dialog.SelectedPath}...");
            LoadIsosFromDirectory(dialog.SelectedPath);

            _gamesOriginal = _games.ToList();

            SetStatus($"Scanned {_games.Count} ISOs");
            DebugLog.Write($"Scanned {_games.Count} ISOs from directory: {dialog.SelectedPath}");
            _ = AutoDownloadCoversAsync();
        }


        private void LoadIsosFromDirectory(string ps2Dir)
        {
            _games.Clear();

            var isoFiles = GetDiscFiles(ps2Dir);
            int count = 0;
            foreach (var isoPath in isoFiles)
            {
                try
                {
                    using var reader = new Ps2IsoReader(isoPath, _db);
                    var info = reader.Read(isoPath);
                    _games.Add(info);
                    AutoFixExtension(reader, info);
                }
                catch
                {
                    long size = 0;
                    try { size = new FileInfo(isoPath).Length; } catch { }
                    _games.Add(new Ps2IsoInfo
                    {
                        IsoPath = isoPath,
                        SizeBytes = size,
                        Validity = Ps2IsoValidity.ReadError
                    });
                }
                count++;
                if (count % 10 == 0 || count == isoFiles.Count)
                    SetStatus($"Scanning: {count} / {isoFiles.Count}");
            }

            EnrichFromDatabase();
            _gamesOriginal = _games.ToList();
            SetStatus($"Scanned {_games.Count} files");
        }

        private void EnrichFromDatabase()
        {
            foreach (var game in _games)
            {
                var meta = _db.GetMetadata(game.GameId);
                if (meta != null)
                {
                    game.Region = meta.Region ?? game.Region;
                    game.Metadata = meta;
                }

                // Volume label fallback for games with no title
                if (game.Title == "Unknown" && game.IsValid && File.Exists(game.IsoPath))
                {
                    try
                    {
                        using var fs = File.OpenRead(game.IsoPath);
                        DiscUtils.Iso9660.CDReader cd;
                        try { cd = new DiscUtils.Iso9660.CDReader(fs, true); }
                        catch
                        {
                            var ss = new SectorStream(fs, forceCd: fs.Length % 2352 == 0);
                            cd = new DiscUtils.Iso9660.CDReader(ss, true);
                        }
                        using (cd)
                        {
                            string label = cd.VolumeLabel;
                            if (!string.IsNullOrWhiteSpace(label))
                            {
                                game.Title = Ps2IsoReader.HumanizeVolumeLabel(label);
                                game.TitleSource = Ps2TitleSource.VolumeLabel;
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private async Task AutoDownloadCoversAsync()
        {
            var gameIds = _games.Select(g => g.GameId)
                .Where(id => id != "Unknown")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (gameIds.Count == 0)
            {
                DebugLog.Write("Auto-download: no valid game IDs to process");
                return;
            }

            SetStatus("Checking covers...");
            DebugLog.Write($"Auto-download: checking covers for {gameIds.Count} game IDs");

            var missing = gameIds
                .Where(id => !File.Exists(Path.Combine(CoverManager.CoverDirectory, $"{id}.jpg")))
                .ToList();

            if (missing.Count == 0)
            {
                SetStatus("Ready");
                DebugLog.Write("Auto-download: all covers already cached");
                return;
            }

            SetStatus($"Downloading covers ({missing.Count} missing)...");
            DebugLog.Write($"Auto-download: downloading {missing.Count} missing covers");

            int ok = 0;
            var progress = new Progress<CoverDownloadProgress>(p =>
            {
                if (p.Success) ok++;
                SetStatus($"Downloading covers: {p.Completed} / {p.Total}");
            });

            await CoverManager.DownloadAllCoversAsync(missing, progress);

            SetStatus("Ready");
            DebugLog.Write($"Auto-download complete: {ok} downloaded, {missing.Count - ok} not found");

            // Refresh the currently displayed cover
            if (dgvIsos.CurrentRow?.DataBoundItem is Ps2IsoInfo info)
                ShowDetails(info);
        }

        private void dgvIsos_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var column = dgvIsos.Columns[e.ColumnIndex];

            if (column.HeaderText != "Size")
                return;

            var sorted = _sizeSortAscending
                ? _games.OrderBy(g => g.SizeBytes).ToList()
                : _games.OrderByDescending(g => g.SizeBytes).ToList();

            _sizeSortAscending = !_sizeSortAscending;

            // IMPORTANT: do NOT reassign _games
            _games.RaiseListChangedEvents = false;
            _games.Clear();

            foreach (var game in sorted)
                _games.Add(game);

            _games.RaiseListChangedEvents = true;
            _games.ResetBindings();
        }

        private void dgvIsos_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvIsos.CurrentRow?.DataBoundItem is Ps2IsoInfo info)
                ShowDetails(info);
            else
                ClearGameDetails();
        }

        private void ShowDetails(Ps2IsoInfo info)
        {
            // ===============================
            // File / ISO info (top summary)
            // ===============================
            tbTitle.Text = info.Title;                 // ✅ correct
            tbGameId.Text   = info.GameId;
            tbDisc.Text     = info.DiscNumber.ToString();
            tbSize.Text     = info.SizeDisplay;
            tbCrc.Text      = info.Pcsx2Crc?.ToString("X8") ?? "N/A";
            tbVersion.Text   = string.IsNullOrEmpty(info.Version) ? "Unknown" : info.Version;

            // ===============================
            // Metadata (from DB)
            // ===============================
            var meta = FindGameByID(tbGameId.Text);
            tbTitle.Text       = meta?.Title        ?? info.Title;
            tbRegion.Text      = meta?.Region       ?? info.Region;
            tbRelease.Text = meta?.ReleaseDate  ?? "Unknown";
            tbGenre.Text = meta != null && meta.Genres?.Length > 0
                ? string.Join(", ", meta.Genres)
                : "Unknown"; tbDeveloper.Text   = meta?.Developer    ?? "Unknown";
            tbPublisher.Text = meta != null && meta.Publisher?.Length > 0
                ? string.Join(", ", meta.Publisher)
                : "Unknown"; tbLanguage.Text    = meta?.Language     ?? "Unknown";
            tbVersion.Text   = string.IsNullOrEmpty(info.Version) ? "Unknown" : info.Version;

            // ===============================
            // Disc info
            // ===============================
            tbMultiDisc.Text = info.IsMultiDisc ? "Yes" : "No";

            // ===============================
            // Cover art
            // ===============================
            string? coverPath = CoverManager.GetCoverPath(info.GameId);
            if (coverPath != null)
            {
                try { gameCoverPb.Image = Image.FromFile(coverPath); }
                catch { gameCoverPb.Image = null; }
            }
            else
            {
                gameCoverPb.Image = null;
            }
        }


        private void ClearGameDetails()
        {
            foreach (Control c in gbDetails.Controls)
            {
                if (c is TextBox tb)
                    tb.Text = string.Empty;
            }
            gameCoverPb.Image = null;
        }

        private void btnCalcCrc_Click(object sender, EventArgs e)
        {
            string path = GetSelectedIsoPath();
            SetStatus("Calculating CRC32...");
            uint crc = Ps2CrcHelper.ComputeIsoCrc(path);
            tbCrc32.Text = crc.ToString("X8");
            SetStatus($"CRC32: {crc:X8}");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string fullIsoPath = GetSelectedIsoPath();
            MessageBox.Show(fullIsoPath);
        }

        private string GetSelectedIsoPath()
        {
            if (dgvIsos.CurrentRow == null) return "";

            string filename = dgvIsos.CurrentRow.Cells[0].Value?.ToString() ?? ""; // Filename column
            string folder = dgvIsos.CurrentRow.Cells[6].Value?.ToString() ?? ""; // Location column

            return Path.Combine(folder, filename);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (dgvIsos.CurrentRow?.DataBoundItem is Ps2IsoInfo info)
            {
                tbCrc32.Text = info.Pcsx2Crc?.ToString("X8") ?? "N/A"; // Fixed: convert uint? to string
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (_games.Count == 0)
            {
                MessageBox.Show("No game list to save.", "Warning");
                return;
            }

            var file = ManifestManager.Save(_games.ToList());
            SetStatus($"Manifest saved ({_games.Count} games)");
            DebugLog.Write($"Manifest saved: {file} ({_games.Count} games)");
            MessageBox.Show($"Manifest saved:\n{file}", "Saved", MessageBoxButtons.OK);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            var manifest = ManifestManager.LoadLatest();
            if (manifest == null)
            {
                SetStatus("No manifest found");
                MessageBox.Show("No manifest found.");
                return;
            }

            _games.Clear();
            foreach (var game in manifest.Games)
                _games.Add(game);

            EnrichFromDatabase();
            _gamesOriginal = _games.ToList();

            SetStatus($"Loaded {manifest.Games.Count} games from manifest");
            DebugLog.Write($"Loaded {manifest.Games.Count} games from manifest (saved {manifest.SavedAt:yyyy-MM-dd HH:mm})");
            _ = AutoDownloadCoversAsync();

            if (dgvIsos.CurrentRow?.DataBoundItem is Ps2IsoInfo info)
                ShowDetails(info);
        }

        private void saveManifestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_games.Count == 0)
            {
                MessageBox.Show("No game list to save.", "Warning");
                return;
            }

            var file = ManifestManager.Save(_gamesOriginal.ToList());
            SetStatus($"Manifest saved ({_games.Count} games)");
            DebugLog.Write($"Manifest saved: {file} ({_games.Count} games)");
            MessageBox.Show($"Manifest saved:\n{file}", "Saved", MessageBoxButtons.OK);
        }

        private void loadManifestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var manifest = ManifestManager.LoadLatest();
            if (manifest == null)
            {
                SetStatus("No manifest found");
                MessageBox.Show("No manifest found.");
                return;
            }

            _games.Clear();
            foreach (var game in manifest.Games)
                _games.Add(game);

            EnrichFromDatabase();
            _gamesOriginal = _games.ToList();

            SetStatus($"Loaded {manifest.Games.Count} games from manifest");
            DebugLog.Write($"Loaded {manifest.Games.Count} games from manifest (saved {manifest.SavedAt:yyyy-MM-dd HH:mm})");
            _ = AutoDownloadCoversAsync();

            if (dgvIsos.CurrentRow?.DataBoundItem is Ps2IsoInfo info)
                ShowDetails(info);
        }

        private void renameSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvIsos.CurrentRow?.DataBoundItem is not Ps2IsoInfo selected)
            {
                MessageBox.Show("Please select a game first.", "Rename ISO");
                return;
            }

            using var dialog = new RenameDialog(selected, _gamesOriginal);
            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            string template = dialog.Template;
            if (string.IsNullOrWhiteSpace(template))
                return;

            var targets = dialog.ApplyToAll
                ? _gamesOriginal.ToList()
                : new List<Ps2IsoInfo> { selected };

            int renamed = 0;
            int skipped = 0;
            int total = targets.Count;
            int done = 0;
            foreach (var game in targets.Where(g => File.Exists(g.IsoPath)))
            {
                string? result = Ps2FileRenamer.Rename(game, template);
                if (string.IsNullOrEmpty(result))
                    skipped++;
                else
                    renamed++;
                done++;
                if (dialog.ApplyToAll && done % 10 == 0)
                    SetStatus($"Renaming: {done} / {total}");
            }

            dgvIsos.Refresh();

            // Auto-save manifest so renamed paths persist
            if (renamed > 0)
                ManifestManager.Save(_gamesOriginal.ToList());

            DebugLog.Write($"Rename: {renamed} renamed, {skipped} skipped (template: {template})");
            SetStatus($"Renamed {renamed} files" + (skipped > 0 ? $", {skipped} skipped" : ""));

            if (dialog.ApplyToAll)
                MessageBox.Show($"Renamed {renamed} files." + (skipped > 0 ? $"\nSkipped {skipped} (target exists or unchanged)." : ""),
                    "Rename Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void moveSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvIsos.CurrentRow?.DataBoundItem is not Ps2IsoInfo selected)
            {
                MessageBox.Show("Please select a game first.", "Move ISO");
                return;
            }

            using var dialog = new MoveDialog(selected, _gamesOriginal);
            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            string template = dialog.Template;
            if (string.IsNullOrWhiteSpace(template))
                return;

            var targets = dialog.ApplyToAll
                ? _gamesOriginal.ToList()
                : new List<Ps2IsoInfo> { selected };

            int moved = 0;
            int skipped = 0;
            int total = targets.Count;
            int done = 0;
            foreach (var game in targets.Where(g => File.Exists(g.IsoPath)))
            {
                string? result = Ps2FileRenamer.MoveToFolder(game, template);
                if (string.IsNullOrEmpty(result))
                    skipped++;
                else
                    moved++;
                done++;
                if (dialog.ApplyToAll && done % 10 == 0)
                    SetStatus($"Moving: {done} / {total}");
            }

            dgvIsos.Refresh();

            if (moved > 0)
                ManifestManager.Save(_gamesOriginal.ToList());

            DebugLog.Write($"Move to folder: {moved} moved, {skipped} skipped (template: {template})");
            SetStatus($"Moved {moved} files" + (skipped > 0 ? $", {skipped} skipped" : ""));

            if (dialog.ApplyToAll)
                MessageBox.Show($"Moved {moved} files to separate folders." + (skipped > 0 ? $"\nSkipped {skipped}." : ""),
                    "Move Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void deleteISOToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvIsos.CurrentRow?.DataBoundItem is not Ps2IsoInfo game)
            {
                MessageBox.Show("Please select a game first.", "Delete ISO");
                return;
            }

            if (!File.Exists(game.IsoPath))
            {
                MessageBox.Show($"File not found:\n{game.IsoPath}", "Delete ISO");
                return;
            }

            var confirm = MessageBox.Show(
                $"Delete this ISO?\n\n{game.FileName}",
                "Delete ISO",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                File.Delete(game.IsoPath);
                _games.Remove(game);
                _gamesOriginal.Remove(game);
                dgvIsos.Refresh();
                ManifestManager.Save(_gamesOriginal.ToList());
                ClearGameDetails();
                SetStatus($"Deleted: {game.FileName}");
                DebugLog.Write($"Deleted ISO: {game.IsoPath}");
            }
            catch (Exception ex)
            {
                DebugLog.Write($"Delete failed: {game.IsoPath} — {ex.Message}");
                MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void viewISOInExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvIsos.CurrentRow?.DataBoundItem is not Ps2IsoInfo game)
            {
                MessageBox.Show("Please select a game first.", "View in Explorer");
                return;
            }

            string path = game.IsoPath;
            if (!File.Exists(path))
            {
                // File missing — try opening parent directory instead
                string? dir = Path.GetDirectoryName(path);
                if (dir != null && Directory.Exists(dir))
                    Process.Start("explorer.exe", $"/select,\"{dir}\"");
                else
                    MessageBox.Show($"File not found:\n{path}", "View in Explorer");
                return;
            }

            Process.Start("explorer.exe", $"/select,\"{path}\"");
            SetStatus($"Opened in Explorer: {Path.GetFileName(path)}");
        }

        private void dgvIsos_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvIsos.Rows[e.RowIndex].DataBoundItem is not Ps2IsoInfo game) return;

            bool missing = !File.Exists(game.IsoPath);

            dgvIsos.Rows[e.RowIndex].DefaultCellStyle.BackColor =
                missing ? Color.MistyRose : Color.White;

            dgvIsos.Rows[e.RowIndex].DefaultCellStyle.ForeColor =
                missing ? Color.DarkRed : Color.Black;
        }

        private void tbSearchIso_TextChanged(object sender, EventArgs e)
        {
            string q = tbSearchIso.Text.Trim().ToLower();

            // restore list when search is empty
            if (string.IsNullOrWhiteSpace(q))
            {
                _games.RaiseListChangedEvents = false;
                _games.Clear();
                foreach (var g in _gamesOriginal) _games.Add(g);
                _games.RaiseListChangedEvents = true;
                _games.ResetBindings();
                return;
            }

            // filtering
            _games.RaiseListChangedEvents = false;
            _games.Clear();

            foreach (var g in _gamesOriginal)
            {
                if (g.FileName.ToLower().Contains(q) ||
                    g.GameId.ToLower().Contains(q) ||
                    g.Title.ToLower().Contains(q))
                {
                    _games.Add(g);
                }
            }

            _games.RaiseListChangedEvents = true;
            _games.ResetBindings();
        }

        private void scanFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RescanGames();
        }

        private void exportMetadataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_games.Count == 0)
            {
                MessageBox.Show("No games to export. Please scan first.");
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"ps2_library_{DateTime.Now:yyyy-MM-dd}.xlsx",
                Title = "Export PS2 Library to Excel"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                SetStatus("Exporting to Excel...");
                Ps2ExcelExporter.Export(_games, dialog.FileName);
                SetStatus($"Exported {_games.Count} games to Excel");
                DebugLog.Write($"Exported {_games.Count} games to Excel: {dialog.FileName}");
                MessageBox.Show($"Exported {_games.Count} games to:\n{dialog.FileName}", "Export Complete");
            }
            catch (Exception ex)
            {
                SetStatus("Excel export failed");
                DebugLog.Write($"Excel export error: {ex.Message}");
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void coverDownloaderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_games.Count == 0)
            {
                MessageBox.Show("No games in library. Please scan first.", "Cover Downloader");
                return;
            }

            var confirm = MessageBox.Show(
                $"Download covers for {_games.Count} games?\n\nCovers will be saved to:\n{CoverManager.CoverDirectory}",
                "Cover Downloader",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            coverDownloaderToolStripMenuItem.Enabled = false;

            try
            {
                var gameIds = _games.Select(g => g.GameId).Where(id => id != "Unknown").ToList();
                int ok = 0;
                int fail = 0;

                DebugLog.Write($"Manual cover download started: {gameIds.Count} game IDs");

                var progress = new Progress<CoverDownloadProgress>(p =>
                {
                    if (p.Success) ok++; else fail++;
                    SetStatus($"Downloading covers: {p.Completed} / {p.Total}");
                });

                await CoverManager.DownloadAllCoversAsync(gameIds, progress);

                SetStatus("Ready");
                DebugLog.Write($"Manual cover download complete: {ok} ok, {fail} not found");
                MessageBox.Show(
                    $"Cover download complete.\n\n" +
                    $"Game IDs found: {gameIds.Count}\n" +
                    $"Covers obtained: {ok}\n" +
                    $"Not found (404): {fail}\n\n" +
                    $"Covers saved to:\n{CoverManager.CoverDirectory}",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (dgvIsos.CurrentRow?.DataBoundItem is Ps2IsoInfo info)
                    ShowDetails(info);
            }
            catch (Exception ex)
            {
                DebugLog.Write($"Cover download error: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                coverDownloaderToolStripMenuItem.Enabled = true;
            }
        }

        private async void downloadLatestDatabase_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                $"Download latest PS2 game database?\n\nFrom: github.com/niemasd/GameDB-PS2\nTo: {DatabaseManager.DefaultPath}",
                "Download Database",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetStatus("Downloading database...");
            DebugLog.Write("Downloading latest PS2 database...");

            try
            {
                string? result = await DatabaseManager.DownloadLatestAsync();
                if (result != null)
                {
                    // Reload database
                    LoadDatabase(result);
                    _db = new Ps2GameDatabase(result);
                    SetStatus("Database updated ✓");
                    DebugLog.Write($"Database downloaded to: {result}");
                    MessageBox.Show($"Database downloaded successfully.\n\n{result}", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    SetStatus("Database download failed");
                    MessageBox.Show("Download failed. Check your internet connection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Database download error");
                DebugLog.Write($"Database download error: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "PS2 Manager — Version 0.1\n\n" +
                "A desktop tool for managing PS2 ISO collections.\n" +
                "Scan, organize, rename, and browse your games\n" +
                "with cover art and metadata.\n\n" +
                "Database: github.com/niemasd/GameDB-PS2\n" +
                "Covers:   github.com/xlenore/ps2-covers",
                "About PS2 Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void flattenDirectory_Click(object sender, EventArgs e)
        {
            if (_games.Count == 0)
            {
                MessageBox.Show("No games loaded. Please scan first.", "Flatten Directory");
                return;
            }

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select target folder to move all disc images into",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != DialogResult.OK) return;

            string targetDir = dialog.SelectedPath;

            var confirm = MessageBox.Show(
                $"Move all disc images to:\n{targetDir}\n\n" +
                "Only files in subdirectories will be moved.\n" +
                "Files already in the target folder are skipped.",
                "Flatten Directory",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetStatus("Moving files...");
            int moved = 0;
            var errors = new List<string>();

            foreach (var game in _gamesOriginal.Where(g => File.Exists(g.IsoPath)))
            {
                string? currentDir = Path.GetDirectoryName(game.IsoPath);
                if (string.IsNullOrEmpty(currentDir)) continue;

                // Already in target — skip
                if (currentDir.Equals(targetDir, StringComparison.OrdinalIgnoreCase)) continue;

                string fileName = Path.GetFileName(game.IsoPath);
                string newPath = Path.Combine(targetDir, fileName);

                if (File.Exists(newPath))
                {
                    errors.Add($"{fileName} (target exists)");
                    continue;
                }

                try
                {
                    File.Move(game.IsoPath, newPath);
                    game.IsoPath = newPath;
                    moved++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{fileName}: {ex.Message}");
                }
            }

            dgvIsos.Refresh();
            if (moved > 0)
                ManifestManager.Save(_gamesOriginal.ToList());

            SetStatus($"Moved {moved} files" + (errors.Count > 0 ? $", {errors.Count} errors" : ""));
            DebugLog.Write($"Flatten directory: {moved} moved to {targetDir}, {errors.Count} errors");

            string msg = $"Moved {moved} files to:\n{targetDir}";
            if (errors.Count > 0)
                msg += $"\n\n{errors.Count} error(s):\n" + string.Join("\n", errors.Take(10));
            MessageBox.Show(msg, "Done", MessageBoxButtons.OK,
                errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void fixExtensions_Click(object sender, EventArgs e)
        {
            if (_games.Count == 0)
            {
                MessageBox.Show("No games loaded. Please scan first.", "Fix Extensions");
                return;
            }

            int changed = 0;
            foreach (var game in _gamesOriginal.Where(g => File.Exists(g.IsoPath)))
            {
                string currentExt = Path.GetExtension(game.IsoPath).ToLower();
                if (currentExt != ".iso" && currentExt != ".bin" && currentExt != ".img")
                    continue;

                // Detect real format by file size
                long size = 0;
                try { size = new FileInfo(game.IsoPath).Length; } catch { continue; }

                bool isCd = size % 2352 == 0 && size % 2048 != 0;
                string correctExt = isCd ? ".bin" : ".iso";

                if (currentExt == correctExt) continue;

                string newPath = Path.ChangeExtension(game.IsoPath, correctExt);
                if (File.Exists(newPath)) continue;

                try
                {
                    File.Move(game.IsoPath, newPath);
                    game.IsoPath = newPath;
                    changed++;
                }
                catch { }
            }

            dgvIsos.Refresh();
            if (changed > 0)
                ManifestManager.Save(_gamesOriginal.ToList());

            SetStatus($"Fixed {changed} file extensions");
            DebugLog.Write($"Fix extensions: {changed} corrected");
            MessageBox.Show($"Corrected {changed} file extension(s).", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RescanGames()
        {
            if (_games.Count == 0)
            {
                FullScanIsoDirectory();   // first time use
                return;
            }

            var choice = MessageBox.Show(
                "Choose scan type:\n\n" +
                "YES = Fast Rescan (Manifest)\n" +
                "NO  = Full Scan (Re-read all ISO files)\n" +
                "CANCEL = Abort",
                "Rescan Options",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (choice == DialogResult.Yes)
            {
                LoadFromManifest();
            }
            else if (choice == DialogResult.No)
            {
                FullScanIsoDirectory();
            }
        }

        private void LoadFromManifest()
        {
            var manifest = ManifestManager.LoadLatest();

            if (manifest == null)
            {
                MessageBox.Show("No saved manifest found. Running full ISO scan instead.");
                FullScanIsoDirectory();
                return;
            }

            _games.Clear();
            foreach (var g in manifest.Games)
                _games.Add(g);

            EnrichFromDatabase();
            MessageBox.Show($"Manifest loaded ✓ {_games.Count} games restored.");
        }

        private void FullScanIsoDirectory()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select PS2 ISO Directory",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            LoadIsosFromDirectory(dialog.SelectedPath);
            MessageBox.Show($"Full rescan complete. Found {_games.Count} games.");
        }

        private void button7_Click(object sender, EventArgs e)
        {
            var g = FindGameByID(tbGameId.Text);

            MessageBox.Show(g != null
                ? $"{g.Title}\n" +
                  $"{g.Developer}\n" +
                  $"{string.Join(", ", g.genre)}\n" +          // <-- Array to string
                  $"{string.Join(", ", g.language)}\n" +          // <-- Array to string
                  $"{string.Join(", ", g.publisher)}\n" +          // <-- Array to string
                  $"{g.ReleaseDate}"
                : "Not found"
            );
        }

        Dictionary<string, Ps2Game> ps2Db;

        public void LoadDatabase(string path)
        {
            if (!File.Exists(path))
            {
                ps2Db = new Dictionary<string, Ps2Game>();
                return;
            }

            string json = File.ReadAllText(path);
            ps2Db = JsonSerializer.Deserialize<Dictionary<string, Ps2Game>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        public Ps2Game FindGameByID(string gameID)
        {
            if (string.IsNullOrWhiteSpace(gameID) || ps2Db == null)
                return null;

            gameID = NormalizeID(gameID);

            foreach (var kv in ps2Db)
                if (NormalizeID(kv.Key) == gameID)
                    return kv.Value;

            return null;
        }

        private string NormalizeID(string id)
        {
            return new string(id.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
        }

        public class Ps2Game
        {
            public JsonElement language { get; set; }
            public JsonElement region { get; set; }
            public JsonElement serial { get; set; }
            public JsonElement title { get; set; }
            public JsonElement developer { get; set; }
            public JsonElement publisher { get; set; }
            public JsonElement release_date { get; set; }
            public JsonElement genre { get; set; }


            private string GetString(JsonElement el)
            {
                return el.ValueKind switch
                {
                    JsonValueKind.String => el.GetString(),
                    JsonValueKind.Number => el.GetRawText(),
                    JsonValueKind.Array => string.Join(", ", el.EnumerateArray().Select(x => x.GetString())),
                    _ => null
                };
            }

            [JsonIgnore] public string Title => GetString(title);
            [JsonIgnore] public string Region => GetString(region);
            [JsonIgnore] public string Language => GetString(language);
            [JsonIgnore] public string Serial => GetString(serial);
            [JsonIgnore] public string Developer => GetString(developer);
            [JsonIgnore] public string Publisher => GetString(publisher);
            [JsonIgnore] public string ReleaseDate => GetString(release_date);

            [JsonIgnore]
            public string[] Genres =>
                genre.ValueKind == JsonValueKind.Array
                ? genre.EnumerateArray().Select(x => x.GetString()).ToArray()
                : new[] { GetString(genre) };
        }

    }
}
