namespace KahoiiMusic
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.lblLogo = new System.Windows.Forms.Label();
            this.panelPlayerCard = new System.Windows.Forms.Panel();
            this.panelTopBar = new System.Windows.Forms.Panel();
            this.btnPlaylist = new KahoiiMusic.Controls.PlaylistButton();
                        this.searchPillBox = new KahoiiMusic.Controls.SearchPillBox();
            this.gradientPanel = new KahoiiMusic.Controls.GradientPanel();
            this.volumePopup = new KahoiiMusic.Controls.VolumePopup();
            this.artworkBox = new KahoiiMusic.Controls.ArtworkBox();
            this.lyricsPanel = new KahoiiMusic.Controls.LyricsPanel();
            this.marqueeTitle = new KahoiiMusic.Controls.MarqueeLabel();
            this.marqueeAuthor = new KahoiiMusic.Controls.MarqueeLabel();
            this.btnFavorite = new KahoiiMusic.Controls.FavoriteButton();
            this.panelBottomControls = new System.Windows.Forms.Panel();
            this.btnLyrics = new KahoiiMusic.Controls.LyricsButton();
            this.btnVolume = new KahoiiMusic.Controls.VolumeButton();
            this.btnPrevious = new KahoiiMusic.Controls.DirectionalButton();
            this.btnPlayPause = new KahoiiMusic.Controls.PlayPauseButton();
            this.btnNext = new KahoiiMusic.Controls.DirectionalButton();
            this.btnLoop = new KahoiiMusic.Controls.LoopButton();
            this.btnTimer = new KahoiiMusic.Controls.TimerButton();
            this.btnQuickDownload = new KahoiiMusic.Controls.QuickDownloadButton();
            this.musicTrackBar = new KahoiiMusic.Controls.MusicTrackBar();
            this.lblCopyright = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Label();
            this.btnMinimize = new System.Windows.Forms.Label();
            this.btnUser = new KahoiiMusic.Controls.UserAccountButton();
            this.playbackTimer = new System.Windows.Forms.Timer(this.components);
            this.panelPlayerCard.SuspendLayout();
            this.panelTopBar.SuspendLayout();
            this.gradientPanel.SuspendLayout();
            this.panelBottomControls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            this.SuspendLayout();
            // 
            // picLogo
            // 
            this.picLogo.BackColor = System.Drawing.Color.Transparent;
            this.picLogo.Location = new System.Drawing.Point(4, 3);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new System.Drawing.Size(18, 18);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picLogo.TabIndex = 0;
            this.picLogo.TabStop = false;
            // 
            // lblLogo
            // 
            this.lblLogo.AutoSize = false;
            this.lblLogo.BackColor = System.Drawing.Color.Transparent;
            this.lblLogo.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLogo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(95)))), ((int)(((byte)(195)))));
            this.lblLogo.Location = new System.Drawing.Point(24, 4);
            this.lblLogo.Name = "lblLogo";
            this.lblLogo.Size = new System.Drawing.Size(120, 18);
            this.lblLogo.TabIndex = 4;
            this.lblLogo.Text = "Kahoii Music";
            // 
            // panelPlayerCard
            // 
            this.panelPlayerCard.Controls.Add(this.panelTopBar);
            this.panelPlayerCard.Controls.Add(this.gradientPanel);
            this.panelPlayerCard.Controls.Add(this.panelBottomControls);
            this.panelPlayerCard.Location = new System.Drawing.Point(2, 24);
            this.panelPlayerCard.Name = "panelPlayerCard";
            this.panelPlayerCard.Size = new System.Drawing.Size(476, 336);
            this.panelPlayerCard.TabIndex = 3;
            // 
            // panelTopBar
            // 
            this.panelTopBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(47)))), ((int)(((byte)(20)))));
            this.panelTopBar.Controls.Add(this.btnPlaylist);
            this.panelTopBar.Controls.Add(this.searchPillBox);
            this.panelTopBar.Controls.Add(this.btnUser);
            this.panelTopBar.Location = new System.Drawing.Point(0, 0);
            this.panelTopBar.Name = "panelTopBar";
            this.panelTopBar.Size = new System.Drawing.Size(476, 30);
            this.panelTopBar.TabIndex = 0;
            // 
            // btnPlaylist
            // 
            this.btnPlaylist.BackColor = System.Drawing.Color.Transparent;
            this.btnPlaylist.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPlaylist.Location = new System.Drawing.Point(12, 3);
            this.btnPlaylist.Name = "btnPlaylist";
            this.btnPlaylist.Size = new System.Drawing.Size(28, 24);
            this.btnPlaylist.TabIndex = 1;
            // 
            // searchPillBox
            // 
            this.searchPillBox.BackColor = System.Drawing.Color.Transparent;
            this.searchPillBox.Location = new System.Drawing.Point(46, 4);
            this.searchPillBox.Name = "searchPillBox";
            this.searchPillBox.PlaceholderText = "link youtube...";
            this.searchPillBox.Size = new System.Drawing.Size(384, 22);
            this.searchPillBox.TabIndex = 0;
            // 
            // gradientPanel
            // 
            this.gradientPanel.BottomColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(26)))), ((int)(((byte)(94)))));
            this.gradientPanel.Controls.Add(this.volumePopup);
            this.gradientPanel.Controls.Add(this.artworkBox);
            this.gradientPanel.Controls.Add(this.lyricsPanel);
            this.gradientPanel.Controls.Add(this.marqueeTitle);
            this.gradientPanel.Controls.Add(this.marqueeAuthor);
            this.gradientPanel.Location = new System.Drawing.Point(0, 30);
            this.gradientPanel.Name = "gradientPanel";
            this.gradientPanel.Size = new System.Drawing.Size(476, 206);
            this.gradientPanel.TabIndex = 1;
            this.gradientPanel.TopColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(16)))), ((int)(((byte)(92)))));
            // 
            // lyricsPanel
            // 
            this.lyricsPanel.BackColor = System.Drawing.Color.Transparent;
            this.lyricsPanel.Location = new System.Drawing.Point(176, 8);
            this.lyricsPanel.Name = "lyricsPanel";
            this.lyricsPanel.Size = new System.Drawing.Size(292, 190);
            this.lyricsPanel.TabIndex = 4;
            this.lyricsPanel.Visible = false;
            // 
            // volumePopup
            // 
            this.volumePopup.BackColor = System.Drawing.Color.Transparent;
            this.volumePopup.Location = new System.Drawing.Point(120, 50);
            this.volumePopup.Name = "volumePopup";
            this.volumePopup.Size = new System.Drawing.Size(36, 136);
            this.volumePopup.TabIndex = 1;
            this.volumePopup.Visible = false;
            this.volumePopup.Volume = 1F;
            // 
            // artworkBox
            // 
            this.artworkBox.BackColor = System.Drawing.Color.Transparent;
            this.artworkBox.Image = null;
            this.artworkBox.IsSpinning = false;
            this.artworkBox.Location = new System.Drawing.Point(156, 0);
            this.artworkBox.Name = "artworkBox";
            this.artworkBox.Size = new System.Drawing.Size(164, 162);
            this.artworkBox.TabIndex = 0;
            this.artworkBox.TimeText = "";
            // 
            // marqueeTitle
            // 
            this.marqueeTitle.BackColor = System.Drawing.Color.Transparent;
            this.marqueeTitle.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.marqueeTitle.Location = new System.Drawing.Point(23, 162);
            this.marqueeTitle.Name = "marqueeTitle";
            this.marqueeTitle.Size = new System.Drawing.Size(430, 20);
            this.marqueeTitle.TabIndex = 2;
            this.marqueeTitle.Text = "";
            this.marqueeTitle.TextColor = System.Drawing.Color.White;
            // 
            // marqueeAuthor
            // 
            this.marqueeAuthor.BackColor = System.Drawing.Color.Transparent;
            this.marqueeAuthor.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.marqueeAuthor.Location = new System.Drawing.Point(23, 184);
            this.marqueeAuthor.Name = "marqueeAuthor";
            this.marqueeAuthor.Size = new System.Drawing.Size(430, 18);
            this.marqueeAuthor.TabIndex = 3;
            this.marqueeAuthor.Text = "";
            this.marqueeAuthor.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(195)))), ((int)(((byte)(215)))));
            // 
            // panelBottomControls
            // 
            this.panelBottomControls.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(54)))), ((int)(((byte)(88)))), ((int)(((byte)(6)))));
            this.panelBottomControls.Controls.Add(this.btnFavorite);
            this.panelBottomControls.Controls.Add(this.btnQuickDownload);
            this.panelBottomControls.Controls.Add(this.btnVolume);
            this.panelBottomControls.Controls.Add(this.btnPrevious);
            this.panelBottomControls.Controls.Add(this.btnPlayPause);
            this.panelBottomControls.Controls.Add(this.btnNext);
            this.panelBottomControls.Controls.Add(this.btnLoop);
            this.panelBottomControls.Controls.Add(this.btnTimer);
            this.panelBottomControls.Controls.Add(this.btnLyrics);
            this.panelBottomControls.Controls.Add(this.musicTrackBar);
            this.panelBottomControls.Controls.Add(this.lblCopyright);
            this.panelBottomControls.Controls.Add(this.lblVersion);
            this.panelBottomControls.Location = new System.Drawing.Point(0, 236);
            this.panelBottomControls.Name = "panelBottomControls";
            this.panelBottomControls.Size = new System.Drawing.Size(476, 100);
            this.panelBottomControls.TabIndex = 2;
            // 
            // btnFavorite
            // 
            this.btnFavorite.BackColor = System.Drawing.Color.Transparent;
            this.btnFavorite.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnFavorite.Location = new System.Drawing.Point(40, 47);
            this.btnFavorite.Name = "btnFavorite";
            this.btnFavorite.Size = new System.Drawing.Size(28, 24);
            this.btnFavorite.TabIndex = 8;
            // 
            // btnQuickDownload
            // 
            this.btnQuickDownload.ActiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(85)))), ((int)(((byte)(247)))));
            this.btnQuickDownload.BackColor = System.Drawing.Color.Transparent;
            this.btnQuickDownload.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnQuickDownload.InactiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(220)))), ((int)(((byte)(220)))));
            this.btnQuickDownload.Location = new System.Drawing.Point(84, 47);
            this.btnQuickDownload.Name = "btnQuickDownload";
            this.btnQuickDownload.Size = new System.Drawing.Size(28, 24);
            this.btnQuickDownload.TabIndex = 7;
            // 
            // btnVolume
            // 
            this.btnVolume.BackColor = System.Drawing.Color.Transparent;
            this.btnVolume.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnVolume.IconColor = System.Drawing.Color.White;
            this.btnVolume.Location = new System.Drawing.Point(128, 47);
            this.btnVolume.Name = "btnVolume";
            this.btnVolume.Size = new System.Drawing.Size(28, 24);
            this.btnVolume.TabIndex = 4;
            this.btnVolume.Volume = 1F;
            // 
            // btnPrevious
            // 
            this.btnPrevious.BackColor = System.Drawing.Color.Transparent;
            this.btnPrevious.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPrevious.Location = new System.Drawing.Point(176, 47);
            this.btnPrevious.Name = "btnPrevious";
            this.btnPrevious.OutlineColor = System.Drawing.Color.White;
            this.btnPrevious.Size = new System.Drawing.Size(24, 24);
            this.btnPrevious.TabIndex = 1;
            // 
            // btnPlayPause
            // 
            this.btnPlayPause.BackColor = System.Drawing.Color.Transparent;
            this.btnPlayPause.CircleColor = System.Drawing.Color.FromArgb(((int)(((byte)(217)))), ((int)(((byte)(217)))), ((int)(((byte)(217)))));
            this.btnPlayPause.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPlayPause.IconColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.btnPlayPause.Location = new System.Drawing.Point(223, 44);
            this.btnPlayPause.Name = "btnPlayPause";
            this.btnPlayPause.Size = new System.Drawing.Size(30, 30);
            this.btnPlayPause.TabIndex = 2;
            // 
            // btnNext
            // 
            this.btnNext.BackColor = System.Drawing.Color.Transparent;
            this.btnNext.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnNext.Direction = KahoiiMusic.Controls.DirectionType.Next;
            this.btnNext.Location = new System.Drawing.Point(276, 47);
            this.btnNext.Name = "btnNext";
            this.btnNext.OutlineColor = System.Drawing.Color.White;
            this.btnNext.Size = new System.Drawing.Size(24, 24);
            this.btnNext.TabIndex = 3;
            // 
            // btnLoop
            // 
            this.btnLoop.ActiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(85)))), ((int)(((byte)(247)))));
            this.btnLoop.BackColor = System.Drawing.Color.Transparent;
            this.btnLoop.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLoop.InactiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(220)))), ((int)(((byte)(220)))));
            this.btnLoop.Location = new System.Drawing.Point(320, 47);
            this.btnLoop.Name = "btnLoop";
            this.btnLoop.Size = new System.Drawing.Size(28, 24);
            this.btnLoop.TabIndex = 5;
            // 
            // btnTimer
            // 
            this.btnTimer.ActiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(85)))), ((int)(((byte)(247)))));
            this.btnTimer.BackColor = System.Drawing.Color.Transparent;
            this.btnTimer.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnTimer.InactiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(220)))), ((int)(((byte)(220)))));
            this.btnTimer.Location = new System.Drawing.Point(364, 47);
            this.btnTimer.Name = "btnTimer";
            this.btnTimer.Size = new System.Drawing.Size(28, 24);
            this.btnTimer.TabIndex = 6;
            // 
            // btnLyrics
            // 
            this.btnLyrics.BackColor = System.Drawing.Color.Transparent;
            this.btnLyrics.Cursor = System.Windows.Forms.Cursors.Default;
            this.btnLyrics.Enabled = false;
            this.btnLyrics.Location = new System.Drawing.Point(408, 47);
            this.btnLyrics.Name = "btnLyrics";
            this.btnLyrics.Size = new System.Drawing.Size(28, 24);
            this.btnLyrics.TabIndex = 9;
            // 
            // lblCopyright
            // 
            this.lblCopyright.BackColor = System.Drawing.Color.Transparent;
            this.lblCopyright.Font = new System.Drawing.Font("Segoe UI", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCopyright.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(90)))), ((int)(((byte)(160)))));
            this.lblCopyright.Location = new System.Drawing.Point(100, 78);
            this.lblCopyright.Name = "lblCopyright";
            this.lblCopyright.Size = new System.Drawing.Size(276, 15);
            this.lblCopyright.TabIndex = 6;
            this.lblCopyright.Text = "copyright by Kahoii-San";
            this.lblCopyright.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblVersion
            // 
            this.lblVersion.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lblVersion.BackColor = System.Drawing.Color.Transparent;
            this.lblVersion.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblVersion.Font = new System.Drawing.Font("Segoe UI", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblVersion.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(185)))), ((int)(((byte)(235)))));
            this.lblVersion.Location = new System.Drawing.Point(380, 78);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(86, 15);
            this.lblVersion.TabIndex = 10;
            this.lblVersion.Text = "v1.0.19";
            this.lblVersion.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // musicTrackBar
            // 
            this.musicTrackBar.BackColor = System.Drawing.Color.Transparent;
            this.musicTrackBar.Cursor = System.Windows.Forms.Cursors.Hand;
            this.musicTrackBar.Location = new System.Drawing.Point(23, 16);
            this.musicTrackBar.Name = "musicTrackBar";
            this.musicTrackBar.PlayedColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(15)))), ((int)(((byte)(15)))));
            this.musicTrackBar.Size = new System.Drawing.Size(430, 20);
            this.musicTrackBar.TabIndex = 0;
            this.musicTrackBar.UnplayedColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClose.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClose.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnClose.Location = new System.Drawing.Point(454, 3);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(22, 18);
            this.btnClose.TabIndex = 2;
            this.btnClose.Text = "✕";
            this.btnClose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnUser
            // 
            this.btnUser.BackColor = System.Drawing.Color.Transparent;
            this.btnUser.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnUser.Location = new System.Drawing.Point(436, 3);
            this.btnUser.Name = "btnUser";
            this.btnUser.Size = new System.Drawing.Size(28, 24);
            this.btnUser.TabIndex = 2;
            this.btnUser.Click += new System.EventHandler(this.btnUser_Click);
            // 
            // btnMinimize
            // 
            this.btnMinimize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMinimize.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnMinimize.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnMinimize.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnMinimize.Location = new System.Drawing.Point(430, 3);
            this.btnMinimize.Name = "btnMinimize";
            this.btnMinimize.Size = new System.Drawing.Size(22, 18);
            this.btnMinimize.TabIndex = 1;
            this.btnMinimize.Text = "─";
            this.btnMinimize.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // playbackTimer
            // 
            this.playbackTimer.Interval = 200;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.ClientSize = new System.Drawing.Size(480, 362);
            this.Controls.Add(this.btnMinimize);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.panelPlayerCard);
            this.Controls.Add(this.picLogo);
            this.Controls.Add(this.lblLogo);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Kahoii Music";
            this.panelPlayerCard.ResumeLayout(false);
            this.panelTopBar.ResumeLayout(false);
            this.gradientPanel.ResumeLayout(false);
            this.panelBottomControls.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox picLogo;
        private System.Windows.Forms.Label lblLogo;
        private KahoiiMusic.Controls.MarqueeLabel marqueeTitle;
        private KahoiiMusic.Controls.MarqueeLabel marqueeAuthor;
        private KahoiiMusic.Controls.FavoriteButton btnFavorite;
        private System.Windows.Forms.Label btnClose;
        private System.Windows.Forms.Label btnMinimize;
        private KahoiiMusic.Controls.UserAccountButton btnUser;
        private System.Windows.Forms.Panel panelPlayerCard;
        private System.Windows.Forms.Panel panelTopBar;
        private KahoiiMusic.Controls.SearchPillBox searchPillBox;
        private KahoiiMusic.Controls.GradientPanel gradientPanel;
        private KahoiiMusic.Controls.ArtworkBox artworkBox;
        private KahoiiMusic.Controls.LyricsPanel lyricsPanel;
        private System.Windows.Forms.Panel panelBottomControls;
        private KahoiiMusic.Controls.MusicTrackBar musicTrackBar;
        private KahoiiMusic.Controls.DirectionalButton btnPrevious;
        private KahoiiMusic.Controls.PlayPauseButton btnPlayPause;
        private KahoiiMusic.Controls.DirectionalButton btnNext;
        private KahoiiMusic.Controls.VolumeButton btnVolume;
        private KahoiiMusic.Controls.LoopButton btnLoop;
        private KahoiiMusic.Controls.TimerButton btnTimer;
        private KahoiiMusic.Controls.QuickDownloadButton btnQuickDownload;
        private KahoiiMusic.Controls.VolumePopup volumePopup;
        private KahoiiMusic.Controls.PlaylistButton btnPlaylist;
        private KahoiiMusic.Controls.LyricsButton btnLyrics;
        private System.Windows.Forms.Label lblCopyright;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Timer playbackTimer;
    }
}
