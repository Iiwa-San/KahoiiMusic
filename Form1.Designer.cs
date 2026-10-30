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
            this.marqueeTitle = new KahoiiMusic.Controls.MarqueeLabel();
            this.panelBottomControls = new System.Windows.Forms.Panel();
            this.btnVolume = new KahoiiMusic.Controls.VolumeButton();
            this.btnPrevious = new KahoiiMusic.Controls.DirectionalButton();
            this.btnPlayPause = new KahoiiMusic.Controls.PlayPauseButton();
            this.btnNext = new KahoiiMusic.Controls.DirectionalButton();
            this.btnLoop = new KahoiiMusic.Controls.LoopButton();
            this.btnTimer = new KahoiiMusic.Controls.TimerButton();
            this.musicTrackBar = new KahoiiMusic.Controls.MusicTrackBar();
            this.lblCopyright = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Label();
            this.btnMinimize = new System.Windows.Forms.Label();
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
            this.panelPlayerCard.Size = new System.Drawing.Size(476, 322);
            this.panelPlayerCard.TabIndex = 3;
            // 
            // panelTopBar
            // 
            this.panelTopBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(47)))), ((int)(((byte)(20)))));
            this.panelTopBar.Controls.Add(this.btnPlaylist);
            this.panelTopBar.Controls.Add(this.searchPillBox);
            this.panelTopBar.Location = new System.Drawing.Point(0, 0);
            this.panelTopBar.Name = "panelTopBar";
            this.panelTopBar.Size = new System.Drawing.Size(476, 30);
            this.panelTopBar.TabIndex = 0;
            // 
            // btnPlaylist
            // 
            this.btnPlaylist.BackColor = System.Drawing.Color.Transparent;
            this.btnPlaylist.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPlaylist.Location = new System.Drawing.Point(14, 3);
            this.btnPlaylist.Name = "btnPlaylist";
            this.btnPlaylist.Size = new System.Drawing.Size(28, 24);
            this.btnPlaylist.TabIndex = 1;
            // 
            // searchPillBox
            // 
            this.searchPillBox.BackColor = System.Drawing.Color.Transparent;
            this.searchPillBox.Location = new System.Drawing.Point(58, 5);
            this.searchPillBox.Name = "searchPillBox";
            this.searchPillBox.PlaceholderText = "link youtube...";
            this.searchPillBox.Size = new System.Drawing.Size(360, 20);
            this.searchPillBox.TabIndex = 0;
            // 
            // gradientPanel
            // 
            this.gradientPanel.BottomColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(38)))), ((int)(((byte)(128)))));
            this.gradientPanel.Controls.Add(this.volumePopup);
            this.gradientPanel.Controls.Add(this.artworkBox);
            this.gradientPanel.Controls.Add(this.marqueeTitle);
            this.gradientPanel.Location = new System.Drawing.Point(0, 30);
            this.gradientPanel.Name = "gradientPanel";
            this.gradientPanel.Size = new System.Drawing.Size(476, 192);
            this.gradientPanel.TabIndex = 1;
            this.gradientPanel.TopColor = System.Drawing.Color.FromArgb(((int)(((byte)(43)))), ((int)(((byte)(16)))), ((int)(((byte)(127)))));
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
            this.marqueeTitle.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.marqueeTitle.Location = new System.Drawing.Point(23, 166);
            this.marqueeTitle.Name = "marqueeTitle";
            this.marqueeTitle.Size = new System.Drawing.Size(430, 22);
            this.marqueeTitle.TabIndex = 2;
            this.marqueeTitle.Text = "";
            this.marqueeTitle.TextColor = System.Drawing.Color.White;
            // 
            // panelBottomControls
            // 
            this.panelBottomControls.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(54)))), ((int)(((byte)(88)))), ((int)(((byte)(6)))));
            this.panelBottomControls.Controls.Add(this.btnVolume);
            this.panelBottomControls.Controls.Add(this.btnPrevious);
            this.panelBottomControls.Controls.Add(this.btnPlayPause);
            this.panelBottomControls.Controls.Add(this.btnNext);
            this.panelBottomControls.Controls.Add(this.btnLoop);
            this.panelBottomControls.Controls.Add(this.btnTimer);
            this.panelBottomControls.Controls.Add(this.musicTrackBar);
            this.panelBottomControls.Controls.Add(this.lblCopyright);
            this.panelBottomControls.Location = new System.Drawing.Point(0, 222);
            this.panelBottomControls.Name = "panelBottomControls";
            this.panelBottomControls.Size = new System.Drawing.Size(476, 100);
            this.panelBottomControls.TabIndex = 2;
            // 
            // btnVolume
            // 
            this.btnVolume.BackColor = System.Drawing.Color.Transparent;
            this.btnVolume.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnVolume.IconColor = System.Drawing.Color.White;
            this.btnVolume.Location = new System.Drawing.Point(124, 47);
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
            this.btnLoop.InactiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnLoop.Location = new System.Drawing.Point(324, 47);
            this.btnLoop.Name = "btnLoop";
            this.btnLoop.Size = new System.Drawing.Size(28, 24);
            this.btnLoop.TabIndex = 5;
            // 
            // btnTimer
            // 
            this.btnTimer.ActiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(85)))), ((int)(((byte)(247)))));
            this.btnTimer.BackColor = System.Drawing.Color.Transparent;
            this.btnTimer.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnTimer.InactiveColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnTimer.Location = new System.Drawing.Point(368, 47);
            this.btnTimer.Name = "btnTimer";
            this.btnTimer.Size = new System.Drawing.Size(28, 24);
            this.btnTimer.TabIndex = 6;
            // 
            // lblCopyright
            // 
            this.lblCopyright.BackColor = System.Drawing.Color.Transparent;
            this.lblCopyright.Font = new System.Drawing.Font("Segoe UI", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCopyright.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(43)))), ((int)(((byte)(16)))), ((int)(((byte)(127)))));
            this.lblCopyright.Location = new System.Drawing.Point(0, 78);
            this.lblCopyright.Name = "lblCopyright";
            this.lblCopyright.Size = new System.Drawing.Size(476, 15);
            this.lblCopyright.TabIndex = 6;
            this.lblCopyright.Text = "copyright by Kahoii-San";
            this.lblCopyright.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
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
            this.ClientSize = new System.Drawing.Size(480, 348);
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
        private System.Windows.Forms.Label btnClose;
        private System.Windows.Forms.Label btnMinimize;
        private System.Windows.Forms.Panel panelPlayerCard;
        private System.Windows.Forms.Panel panelTopBar;
        private KahoiiMusic.Controls.SearchPillBox searchPillBox;
        private KahoiiMusic.Controls.GradientPanel gradientPanel;
        private KahoiiMusic.Controls.ArtworkBox artworkBox;
        private System.Windows.Forms.Panel panelBottomControls;
        private KahoiiMusic.Controls.MusicTrackBar musicTrackBar;
        private KahoiiMusic.Controls.DirectionalButton btnPrevious;
        private KahoiiMusic.Controls.PlayPauseButton btnPlayPause;
        private KahoiiMusic.Controls.DirectionalButton btnNext;
        private KahoiiMusic.Controls.VolumeButton btnVolume;
        private KahoiiMusic.Controls.LoopButton btnLoop;
        private KahoiiMusic.Controls.TimerButton btnTimer;
        private KahoiiMusic.Controls.VolumePopup volumePopup;
        private KahoiiMusic.Controls.PlaylistButton btnPlaylist;
        private System.Windows.Forms.Label lblCopyright;
        private System.Windows.Forms.Timer playbackTimer;
    }
}
