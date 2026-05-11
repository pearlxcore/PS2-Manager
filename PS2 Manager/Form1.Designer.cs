namespace PS2_ISO_Manager
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            panel1 = new Panel();
            toolStrip1 = new ToolStrip();
            lblStatus = new ToolStripStatusLabel();
            dgvIsos = new DataGridView();
            groupBox1 = new GroupBox();
            gameCoverPb = new PictureBox();
            label27 = new Label();
            tbSearchIso = new TextBox();
            gbDetails = new GroupBox();
            tbVersion = new TextBox();
            label28 = new Label();
            label29 = new Label();
            tbCrc = new TextBox();
            label25 = new Label();
            label26 = new Label();
            btnCalcCrc = new Button();
            tbCrc32 = new TextBox();
            tbSize = new TextBox();
            tbMultiDisc = new TextBox();
            tbDisc = new TextBox();
            label17 = new Label();
            label18 = new Label();
            label19 = new Label();
            label20 = new Label();
            label21 = new Label();
            label22 = new Label();
            label23 = new Label();
            label24 = new Label();
            panel2 = new Panel();
            tbLanguage = new TextBox();
            tbPublisher = new TextBox();
            tbDeveloper = new TextBox();
            tbGenre = new TextBox();
            tbRelease = new TextBox();
            tbRegion = new TextBox();
            tbGameId = new TextBox();
            tbTitle = new TextBox();
            label16 = new Label();
            label15 = new Label();
            label14 = new Label();
            label13 = new Label();
            label12 = new Label();
            label11 = new Label();
            label10 = new Label();
            label9 = new Label();
            label8 = new Label();
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            label4 = new Label();
            label3 = new Label();
            label2 = new Label();
            label1 = new Label();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            scanFolderToolStripMenuItem = new ToolStripMenuItem();
            rescanLibraryToolStripMenuItem = new ToolStripMenuItem();
            saveManifestToolStripMenuItem = new ToolStripMenuItem();
            loadManifestToolStripMenuItem = new ToolStripMenuItem();
            exportMetadataToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            renameSelectedToolStripMenuItem = new ToolStripMenuItem();
            moveSelectedToolStripMenuItem = new ToolStripMenuItem();
            deleteISOToolStripMenuItem = new ToolStripMenuItem();
            viewISOInExplorerToolStripMenuItem = new ToolStripMenuItem();
            calculateHashToolStripMenuItem = new ToolStripMenuItem();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            coverDownloaderToolStripMenuItem = new ToolStripMenuItem();
            checkForDuplicateToolStripMenuItem = new ToolStripMenuItem();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            addISOToolStripMenuItem = new ToolStripMenuItem();
            globalActionToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem2 = new ToolStripMenuItem();
            copyToolStripMenuItem = new ToolStripMenuItem();
            idToolStripMenuItem = new ToolStripMenuItem();
            titleToolStripMenuItem = new ToolStripMenuItem();
            renameISOToolStripMenuItem = new ToolStripMenuItem();
            panel1.SuspendLayout();
            toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvIsos).BeginInit();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gameCoverPb).BeginInit();
            gbDetails.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(toolStrip1);
            panel1.Controls.Add(dgvIsos);
            panel1.Controls.Add(groupBox1);
            panel1.Controls.Add(label27);
            panel1.Controls.Add(tbSearchIso);
            panel1.Controls.Add(gbDetails);
            panel1.Controls.Add(menuStrip1);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(1355, 897);
            panel1.TabIndex = 0;
            // 
            // toolStrip1
            // 
            toolStrip1.Dock = DockStyle.Bottom;
            toolStrip1.Items.AddRange(new ToolStripItem[] { lblStatus });
            toolStrip1.Location = new Point(0, 872);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(1355, 25);
            toolStrip1.TabIndex = 32;
            toolStrip1.Text = "toolStrip1";
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(39, 20);
            lblStatus.Text = "Ready";
            // 
            // dgvIsos
            // 
            dgvIsos.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvIsos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvIsos.Location = new Point(15, 65);
            dgvIsos.Name = "dgvIsos";
            dgvIsos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvIsos.Size = new Size(863, 796);
            dgvIsos.TabIndex = 0;
            dgvIsos.CellContentClick += dataGridView1_CellContentClick;
            dgvIsos.ColumnHeaderMouseClick += dgvIsos_ColumnHeaderMouseClick;
            dgvIsos.RowPrePaint += dgvIsos_RowPrePaint;
            dgvIsos.SelectionChanged += dgvIsos_SelectionChanged;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            groupBox1.Controls.Add(gameCoverPb);
            groupBox1.Location = new Point(884, 65);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(455, 315);
            groupBox1.TabIndex = 31;
            groupBox1.TabStop = false;
            groupBox1.Text = "Game Cover";
            // 
            // gameCoverPb
            // 
            gameCoverPb.Dock = DockStyle.Fill;
            gameCoverPb.Location = new Point(3, 19);
            gameCoverPb.Name = "gameCoverPb";
            gameCoverPb.Size = new Size(449, 293);
            gameCoverPb.SizeMode = PictureBoxSizeMode.Zoom;
            gameCoverPb.TabIndex = 0;
            gameCoverPb.TabStop = false;
            // 
            // label27
            // 
            label27.AutoSize = true;
            label27.Location = new Point(19, 40);
            label27.Name = "label27";
            label27.Size = new Size(191, 15);
            label27.TabIndex = 29;
            label27.Text = "Search File Name, Game Id or Title:";
            // 
            // tbSearchIso
            // 
            tbSearchIso.Location = new Point(215, 36);
            tbSearchIso.Name = "tbSearchIso";
            tbSearchIso.Size = new Size(239, 23);
            tbSearchIso.TabIndex = 10;
            tbSearchIso.TextChanged += tbSearchIso_TextChanged;
            // 
            // gbDetails
            // 
            gbDetails.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            gbDetails.Controls.Add(tbVersion);
            gbDetails.Controls.Add(label28);
            gbDetails.Controls.Add(label29);
            gbDetails.Controls.Add(tbCrc);
            gbDetails.Controls.Add(label25);
            gbDetails.Controls.Add(label26);
            gbDetails.Controls.Add(btnCalcCrc);
            gbDetails.Controls.Add(tbCrc32);
            gbDetails.Controls.Add(tbSize);
            gbDetails.Controls.Add(tbMultiDisc);
            gbDetails.Controls.Add(tbDisc);
            gbDetails.Controls.Add(label17);
            gbDetails.Controls.Add(label18);
            gbDetails.Controls.Add(label19);
            gbDetails.Controls.Add(label20);
            gbDetails.Controls.Add(label21);
            gbDetails.Controls.Add(label22);
            gbDetails.Controls.Add(label23);
            gbDetails.Controls.Add(label24);
            gbDetails.Controls.Add(panel2);
            gbDetails.Controls.Add(tbLanguage);
            gbDetails.Controls.Add(tbPublisher);
            gbDetails.Controls.Add(tbDeveloper);
            gbDetails.Controls.Add(tbGenre);
            gbDetails.Controls.Add(tbRelease);
            gbDetails.Controls.Add(tbRegion);
            gbDetails.Controls.Add(tbGameId);
            gbDetails.Controls.Add(tbTitle);
            gbDetails.Controls.Add(label16);
            gbDetails.Controls.Add(label15);
            gbDetails.Controls.Add(label14);
            gbDetails.Controls.Add(label13);
            gbDetails.Controls.Add(label12);
            gbDetails.Controls.Add(label11);
            gbDetails.Controls.Add(label10);
            gbDetails.Controls.Add(label9);
            gbDetails.Controls.Add(label8);
            gbDetails.Controls.Add(label7);
            gbDetails.Controls.Add(label6);
            gbDetails.Controls.Add(label5);
            gbDetails.Controls.Add(label4);
            gbDetails.Controls.Add(label3);
            gbDetails.Controls.Add(label2);
            gbDetails.Controls.Add(label1);
            gbDetails.Location = new Point(884, 386);
            gbDetails.Name = "gbDetails";
            gbDetails.Size = new Size(455, 475);
            gbDetails.TabIndex = 5;
            gbDetails.TabStop = false;
            gbDetails.Text = "Title Details";
            // 
            // tbVersion
            // 
            tbVersion.Location = new Point(126, 253);
            tbVersion.Name = "tbVersion";
            tbVersion.ReadOnly = true;
            tbVersion.Size = new Size(311, 23);
            tbVersion.TabIndex = 43;
            // 
            // label28
            // 
            label28.AutoSize = true;
            label28.Location = new Point(93, 257);
            label28.Name = "label28";
            label28.Size = new Size(10, 15);
            label28.TabIndex = 42;
            label28.Text = ":";
            // 
            // label29
            // 
            label29.AutoSize = true;
            label29.Location = new Point(17, 257);
            label29.Name = "label29";
            label29.Size = new Size(45, 15);
            label29.TabIndex = 41;
            label29.Text = "Version";
            // 
            // tbCrc
            // 
            tbCrc.Location = new Point(126, 397);
            tbCrc.Name = "tbCrc";
            tbCrc.ReadOnly = true;
            tbCrc.Size = new Size(311, 23);
            tbCrc.TabIndex = 40;
            // 
            // label25
            // 
            label25.AutoSize = true;
            label25.Location = new Point(93, 401);
            label25.Name = "label25";
            label25.Size = new Size(10, 15);
            label25.TabIndex = 39;
            label25.Text = ":";
            // 
            // label26
            // 
            label26.AutoSize = true;
            label26.Location = new Point(17, 401);
            label26.Name = "label26";
            label26.Size = new Size(30, 15);
            label26.TabIndex = 38;
            label26.Text = "CRC";
            // 
            // btnCalcCrc
            // 
            btnCalcCrc.Location = new Point(362, 424);
            btnCalcCrc.Name = "btnCalcCrc";
            btnCalcCrc.Size = new Size(75, 23);
            btnCalcCrc.TabIndex = 6;
            btnCalcCrc.Text = "Calculate";
            btnCalcCrc.UseVisualStyleBackColor = true;
            btnCalcCrc.Click += btnCalcCrc_Click;
            // 
            // tbCrc32
            // 
            tbCrc32.Location = new Point(126, 424);
            tbCrc32.Name = "tbCrc32";
            tbCrc32.ReadOnly = true;
            tbCrc32.Size = new Size(230, 23);
            tbCrc32.TabIndex = 36;
            // 
            // tbSize
            // 
            tbSize.Location = new Point(126, 369);
            tbSize.Name = "tbSize";
            tbSize.ReadOnly = true;
            tbSize.Size = new Size(311, 23);
            tbSize.TabIndex = 35;
            // 
            // tbMultiDisc
            // 
            tbMultiDisc.Location = new Point(126, 341);
            tbMultiDisc.Name = "tbMultiDisc";
            tbMultiDisc.ReadOnly = true;
            tbMultiDisc.Size = new Size(311, 23);
            tbMultiDisc.TabIndex = 34;
            // 
            // tbDisc
            // 
            tbDisc.Location = new Point(126, 313);
            tbDisc.Name = "tbDisc";
            tbDisc.ReadOnly = true;
            tbDisc.Size = new Size(311, 23);
            tbDisc.TabIndex = 33;
            // 
            // label17
            // 
            label17.AutoSize = true;
            label17.Location = new Point(93, 373);
            label17.Name = "label17";
            label17.Size = new Size(10, 15);
            label17.TabIndex = 32;
            label17.Text = ":";
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.Location = new Point(93, 345);
            label18.Name = "label18";
            label18.Size = new Size(10, 15);
            label18.TabIndex = 31;
            label18.Text = ":";
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.Location = new Point(93, 428);
            label19.Name = "label19";
            label19.Size = new Size(10, 15);
            label19.TabIndex = 30;
            label19.Text = ":";
            // 
            // label20
            // 
            label20.AutoSize = true;
            label20.Location = new Point(93, 317);
            label20.Name = "label20";
            label20.Size = new Size(10, 15);
            label20.TabIndex = 29;
            label20.Text = ":";
            // 
            // label21
            // 
            label21.AutoSize = true;
            label21.Location = new Point(17, 428);
            label21.Name = "label21";
            label21.Size = new Size(42, 15);
            label21.TabIndex = 28;
            label21.Text = "CRC32";
            // 
            // label22
            // 
            label22.AutoSize = true;
            label22.Location = new Point(17, 373);
            label22.Name = "label22";
            label22.Size = new Size(27, 15);
            label22.TabIndex = 27;
            label22.Text = "Size";
            // 
            // label23
            // 
            label23.AutoSize = true;
            label23.Location = new Point(17, 345);
            label23.Name = "label23";
            label23.Size = new Size(62, 15);
            label23.TabIndex = 26;
            label23.Text = "Multi-Disc";
            // 
            // label24
            // 
            label24.AutoSize = true;
            label24.Location = new Point(17, 317);
            label24.Name = "label24";
            label24.Size = new Size(29, 15);
            label24.TabIndex = 25;
            label24.Text = "Disc";
            // 
            // panel2
            // 
            panel2.BackColor = SystemColors.ControlDark;
            panel2.Location = new Point(17, 295);
            panel2.Name = "panel2";
            panel2.Size = new Size(420, 1);
            panel2.TabIndex = 24;
            // 
            // tbLanguage
            // 
            tbLanguage.Location = new Point(126, 224);
            tbLanguage.Name = "tbLanguage";
            tbLanguage.ReadOnly = true;
            tbLanguage.Size = new Size(311, 23);
            tbLanguage.TabIndex = 23;
            // 
            // tbPublisher
            // 
            tbPublisher.Location = new Point(126, 196);
            tbPublisher.Name = "tbPublisher";
            tbPublisher.ReadOnly = true;
            tbPublisher.Size = new Size(311, 23);
            tbPublisher.TabIndex = 22;
            // 
            // tbDeveloper
            // 
            tbDeveloper.Location = new Point(126, 168);
            tbDeveloper.Name = "tbDeveloper";
            tbDeveloper.ReadOnly = true;
            tbDeveloper.Size = new Size(311, 23);
            tbDeveloper.TabIndex = 21;
            // 
            // tbGenre
            // 
            tbGenre.Location = new Point(126, 140);
            tbGenre.Name = "tbGenre";
            tbGenre.ReadOnly = true;
            tbGenre.Size = new Size(311, 23);
            tbGenre.TabIndex = 20;
            // 
            // tbRelease
            // 
            tbRelease.Location = new Point(126, 112);
            tbRelease.Name = "tbRelease";
            tbRelease.ReadOnly = true;
            tbRelease.Size = new Size(311, 23);
            tbRelease.TabIndex = 19;
            // 
            // tbRegion
            // 
            tbRegion.Location = new Point(126, 84);
            tbRegion.Name = "tbRegion";
            tbRegion.ReadOnly = true;
            tbRegion.Size = new Size(311, 23);
            tbRegion.TabIndex = 18;
            // 
            // tbGameId
            // 
            tbGameId.Location = new Point(126, 56);
            tbGameId.Name = "tbGameId";
            tbGameId.ReadOnly = true;
            tbGameId.Size = new Size(311, 23);
            tbGameId.TabIndex = 17;
            // 
            // tbTitle
            // 
            tbTitle.Location = new Point(126, 28);
            tbTitle.Name = "tbTitle";
            tbTitle.ReadOnly = true;
            tbTitle.Size = new Size(311, 23);
            tbTitle.TabIndex = 16;
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Location = new Point(93, 200);
            label16.Name = "label16";
            label16.Size = new Size(10, 15);
            label16.TabIndex = 15;
            label16.Text = ":";
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new Point(93, 172);
            label15.Name = "label15";
            label15.Size = new Size(10, 15);
            label15.TabIndex = 14;
            label15.Text = ":";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Location = new Point(93, 228);
            label14.Name = "label14";
            label14.Size = new Size(10, 15);
            label14.TabIndex = 13;
            label14.Text = ":";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Location = new Point(93, 144);
            label13.Name = "label13";
            label13.Size = new Size(10, 15);
            label13.TabIndex = 12;
            label13.Text = ":";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new Point(93, 116);
            label12.Name = "label12";
            label12.Size = new Size(10, 15);
            label12.TabIndex = 11;
            label12.Text = ":";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(93, 88);
            label11.Name = "label11";
            label11.Size = new Size(10, 15);
            label11.TabIndex = 10;
            label11.Text = ":";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(93, 60);
            label10.Name = "label10";
            label10.Size = new Size(10, 15);
            label10.TabIndex = 9;
            label10.Text = ":";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(93, 32);
            label9.Name = "label9";
            label9.Size = new Size(10, 15);
            label9.TabIndex = 8;
            label9.Text = ":";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(17, 228);
            label8.Name = "label8";
            label8.Size = new Size(59, 15);
            label8.TabIndex = 7;
            label8.Text = "Language";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(17, 200);
            label7.Name = "label7";
            label7.Size = new Size(56, 15);
            label7.TabIndex = 6;
            label7.Text = "Publisher";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(17, 172);
            label6.Name = "label6";
            label6.Size = new Size(60, 15);
            label6.TabIndex = 5;
            label6.Text = "Developer";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(17, 144);
            label5.Name = "label5";
            label5.Size = new Size(38, 15);
            label5.TabIndex = 4;
            label5.Text = "Genre";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(17, 116);
            label4.Name = "label4";
            label4.Size = new Size(73, 15);
            label4.TabIndex = 3;
            label4.Text = "Release Date";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(17, 88);
            label3.Name = "label3";
            label3.Size = new Size(44, 15);
            label3.TabIndex = 2;
            label3.Text = "Region";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(17, 60);
            label2.Name = "label2";
            label2.Size = new Size(52, 15);
            label2.TabIndex = 1;
            label2.Text = "Game ID";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(17, 32);
            label1.Name = "label1";
            label1.Size = new Size(30, 15);
            label1.TabIndex = 0;
            label1.Text = "Title";
            // 
            // menuStrip1
            // 
            menuStrip1.GripStyle = ToolStripGripStyle.Visible;
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, toolsToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.RenderMode = ToolStripRenderMode.System;
            menuStrip1.Size = new Size(1355, 24);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { scanFolderToolStripMenuItem, rescanLibraryToolStripMenuItem, saveManifestToolStripMenuItem, loadManifestToolStripMenuItem, exportMetadataToolStripMenuItem, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // scanFolderToolStripMenuItem
            // 
            scanFolderToolStripMenuItem.Name = "scanFolderToolStripMenuItem";
            scanFolderToolStripMenuItem.Size = new Size(160, 22);
            scanFolderToolStripMenuItem.Text = "Scan PS2 Iso";
            scanFolderToolStripMenuItem.Click += scanFolderToolStripMenuItem_Click;
            // 
            // rescanLibraryToolStripMenuItem
            // 
            rescanLibraryToolStripMenuItem.Enabled = false;
            rescanLibraryToolStripMenuItem.Name = "rescanLibraryToolStripMenuItem";
            rescanLibraryToolStripMenuItem.Size = new Size(160, 22);
            rescanLibraryToolStripMenuItem.Text = "Rescan Library";
            // 
            // saveManifestToolStripMenuItem
            // 
            saveManifestToolStripMenuItem.Name = "saveManifestToolStripMenuItem";
            saveManifestToolStripMenuItem.Size = new Size(160, 22);
            saveManifestToolStripMenuItem.Text = "Save Manifest";
            saveManifestToolStripMenuItem.Click += saveManifestToolStripMenuItem_Click;
            // 
            // loadManifestToolStripMenuItem
            // 
            loadManifestToolStripMenuItem.Name = "loadManifestToolStripMenuItem";
            loadManifestToolStripMenuItem.Size = new Size(160, 22);
            loadManifestToolStripMenuItem.Text = "Load Manifest";
            loadManifestToolStripMenuItem.Click += loadManifestToolStripMenuItem_Click;
            // 
            // exportMetadataToolStripMenuItem
            // 
            exportMetadataToolStripMenuItem.Name = "exportMetadataToolStripMenuItem";
            exportMetadataToolStripMenuItem.Size = new Size(160, 22);
            exportMetadataToolStripMenuItem.Text = "Export Metadata";
            exportMetadataToolStripMenuItem.Click += exportMetadataToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Enabled = false;
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(160, 22);
            exitToolStripMenuItem.Text = "Exit";
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { renameSelectedToolStripMenuItem, moveSelectedToolStripMenuItem, deleteISOToolStripMenuItem, viewISOInExplorerToolStripMenuItem, calculateHashToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(39, 20);
            editToolStripMenuItem.Text = "Edit";
            // 
            // renameSelectedToolStripMenuItem
            // 
            renameSelectedToolStripMenuItem.Name = "renameSelectedToolStripMenuItem";
            renameSelectedToolStripMenuItem.Size = new Size(223, 22);
            renameSelectedToolStripMenuItem.Text = "Rename ISO";
            renameSelectedToolStripMenuItem.Click += renameSelectedToolStripMenuItem_Click;
            // 
            // moveSelectedToolStripMenuItem
            // 
            moveSelectedToolStripMenuItem.Name = "moveSelectedToolStripMenuItem";
            moveSelectedToolStripMenuItem.Size = new Size(223, 22);
            moveSelectedToolStripMenuItem.Text = "Move ISO to Seperate Folder";
            moveSelectedToolStripMenuItem.Click += moveSelectedToolStripMenuItem_Click;
            // 
            // deleteISOToolStripMenuItem
            // 
            deleteISOToolStripMenuItem.Name = "deleteISOToolStripMenuItem";
            deleteISOToolStripMenuItem.Size = new Size(223, 22);
            deleteISOToolStripMenuItem.Text = "Delete ISO";
            deleteISOToolStripMenuItem.Click += deleteISOToolStripMenuItem_Click;
            // 
            // viewISOInExplorerToolStripMenuItem
            // 
            viewISOInExplorerToolStripMenuItem.Name = "viewISOInExplorerToolStripMenuItem";
            viewISOInExplorerToolStripMenuItem.Size = new Size(223, 22);
            viewISOInExplorerToolStripMenuItem.Text = "View ISO in Explorer";
            viewISOInExplorerToolStripMenuItem.Click += viewISOInExplorerToolStripMenuItem_Click;
            // 
            // calculateHashToolStripMenuItem
            // 
            calculateHashToolStripMenuItem.Enabled = false;
            calculateHashToolStripMenuItem.Name = "calculateHashToolStripMenuItem";
            calculateHashToolStripMenuItem.Size = new Size(223, 22);
            calculateHashToolStripMenuItem.Text = "Calculate Hash";
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { coverDownloaderToolStripMenuItem, checkForDuplicateToolStripMenuItem, settingsToolStripMenuItem });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new Size(47, 20);
            toolsToolStripMenuItem.Text = "Tools";
            // 
            // coverDownloaderToolStripMenuItem
            // 
            coverDownloaderToolStripMenuItem.Name = "coverDownloaderToolStripMenuItem";
            coverDownloaderToolStripMenuItem.Size = new Size(199, 22);
            coverDownloaderToolStripMenuItem.Text = "Cover Downloader";
            coverDownloaderToolStripMenuItem.Click += coverDownloaderToolStripMenuItem_Click;
            // 
            // checkForDuplicateToolStripMenuItem
            // 
            checkForDuplicateToolStripMenuItem.Enabled = false;
            checkForDuplicateToolStripMenuItem.Name = "checkForDuplicateToolStripMenuItem";
            checkForDuplicateToolStripMenuItem.Size = new Size(199, 22);
            checkForDuplicateToolStripMenuItem.Text = "Check for Duplicate ISO";
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.Enabled = false;
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(199, 22);
            settingsToolStripMenuItem.Text = "Settings";
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(44, 20);
            helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(107, 22);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // addISOToolStripMenuItem
            // 
            addISOToolStripMenuItem.Enabled = false;
            addISOToolStripMenuItem.Name = "addISOToolStripMenuItem";
            addISOToolStripMenuItem.Size = new Size(160, 22);
            addISOToolStripMenuItem.Text = "Add ISO";
            // 
            // globalActionToolStripMenuItem
            // 
            globalActionToolStripMenuItem.Enabled = false;
            globalActionToolStripMenuItem.Name = "globalActionToolStripMenuItem";
            globalActionToolStripMenuItem.Size = new Size(223, 22);
            globalActionToolStripMenuItem.Text = "Global Action";
            // 
            // toolStripMenuItem2
            // 
            toolStripMenuItem2.Enabled = false;
            toolStripMenuItem2.Name = "toolStripMenuItem2";
            toolStripMenuItem2.Size = new Size(223, 22);
            toolStripMenuItem2.Text = "...";
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { idToolStripMenuItem, titleToolStripMenuItem });
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.Size = new Size(223, 22);
            copyToolStripMenuItem.Text = "Copy";
            // 
            // idToolStripMenuItem
            // 
            idToolStripMenuItem.Name = "idToolStripMenuItem";
            idToolStripMenuItem.Size = new Size(97, 22);
            idToolStripMenuItem.Text = "Id";
            // 
            // titleToolStripMenuItem
            // 
            titleToolStripMenuItem.Name = "titleToolStripMenuItem";
            titleToolStripMenuItem.Size = new Size(97, 22);
            titleToolStripMenuItem.Text = "Title";
            // 
            // renameISOToolStripMenuItem
            // 
            renameISOToolStripMenuItem.Enabled = false;
            renameISOToolStripMenuItem.Name = "renameISOToolStripMenuItem";
            renameISOToolStripMenuItem.Size = new Size(223, 22);
            renameISOToolStripMenuItem.Text = "Rename ISO";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1355, 897);
            Controls.Add(panel1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "PS2 Manager";
            Shown += Form1_Shown;
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvIsos).EndInit();
            groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gameCoverPb).EndInit();
            gbDetails.ResumeLayout(false);
            gbDetails.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panel1;
        private DataGridView dgvIsos;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem scanFolderToolStripMenuItem;
        private ToolStripMenuItem addISOToolStripMenuItem;
        private ToolStripMenuItem rescanLibraryToolStripMenuItem;
        private ToolStripMenuItem exportMetadataToolStripMenuItem;
        private ToolStripMenuItem saveManifestToolStripMenuItem;
        private ToolStripMenuItem loadManifestToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem globalActionToolStripMenuItem;
        private ToolStripMenuItem renameSelectedToolStripMenuItem;
        private ToolStripMenuItem moveSelectedToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem2;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem idToolStripMenuItem;
        private ToolStripMenuItem titleToolStripMenuItem;
        private ToolStripMenuItem deleteISOToolStripMenuItem;
        private ToolStripMenuItem renameISOToolStripMenuItem;
        private ToolStripMenuItem calculateHashToolStripMenuItem;
        private ToolStripMenuItem viewISOInExplorerToolStripMenuItem;
        private ToolStripMenuItem toolsToolStripMenuItem;
        private ToolStripMenuItem checkForDuplicateToolStripMenuItem;
        private ToolStripMenuItem coverDownloaderToolStripMenuItem;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private GroupBox gbDetails;
        private Label label8;
        private Label label7;
        private Label label6;
        private Label label5;
        private Label label4;
        private Label label3;
        private Label label2;
        private Label label1;
        private TextBox tbTitle;
        private Label label16;
        private Label label15;
        private Label label14;
        private Label label13;
        private Label label12;
        private Label label11;
        private Label label10;
        private Label label9;
        private TextBox tbCrc32;
        private TextBox tbSize;
        private TextBox tbMultiDisc;
        private TextBox tbDisc;
        private Label label17;
        private Label label18;
        private Label label19;
        private Label label20;
        private Label label21;
        private Label label22;
        private Label label23;
        private Label label24;
        private Panel panel2;
        private TextBox tbLanguage;
        private TextBox tbPublisher;
        private TextBox tbDeveloper;
        private TextBox tbGenre;
        private TextBox tbRelease;
        private TextBox tbRegion;
        private TextBox tbGameId;
        private Button btnCalcCrc;
        private TextBox tbCrc;
        private Label label25;
        private Label label26;
        private TextBox tbSearchIso;
        private Label label27;
        private GroupBox groupBox1;
        private PictureBox gameCoverPb;
        private ToolStrip toolStrip1;
        private ToolStripStatusLabel lblStatus;
        private TextBox tbVersion;
        private Label label28;
        private Label label29;
    }
}
