using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Configuration;

namespace TwcasChatter
{
    public partial class MainFrm : Form
    {
        ///////////////////////////////////////////////////////////////////////
        // 型
        ///////////////////////////////////////////////////////////////////////

        ///////////////////////////////////////////////////////////////////////
        // 定数
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 最大表示コメント数
        /// </summary>
        private const int MaxShowCommentCnt = 20;

        ///////////////////////////////////////////////////////////////////////
        // フィールド
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// タイトルのベース
        /// </summary>
        private string TitleBase = "";
        /// <summary>
        /// 棒読みちゃん
        /// </summary>
        private MyUtilLib.BouyomiChan BouyomiChan = new MyUtilLib.BouyomiChan();

        private TwcasChatClient ChatClient = null;

        /// <summary>
        /// ユーザーピクチャーボックスリスト
        /// </summary>
        private PictureBox[] UserPictBoxList = null;
        /// <summary>
        /// ユーザーラベルリスト
        /// </summary>
        private Label[] UserLabelList = null;
        /// <summary>
        /// コメントラベルリスト
        /// </summary>
        private TextBox[] CommentTextBoxList = null;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainFrm()
        {
            InitializeComponent();

            // GUI初期処理
            TitleBase = this.Text + " " + MyUtilLib.MyUtil.getAppVersion();
            this.Text = TitleBase;

            UserPictBoxList = new PictureBox[MaxShowCommentCnt];
            UserLabelList = new Label[MaxShowCommentCnt];
            CommentTextBoxList = new TextBox[MaxShowCommentCnt];
            // 後ろから追加
            const int distanceY = 45;
            for (int i = MaxShowCommentCnt - 1; i >= 0; i--)
            {
                // ユーザーピクチャーボックス
                PictureBox userPictBox = new PictureBox();
                this.MainPanel.Controls.Add(userPictBox);
                UserPictBoxList[i] = userPictBox;
                userPictBox.ImageLocation = "";
                userPictBox.Location = new System.Drawing.Point(10, 5 + distanceY * i);
                userPictBox.Size = new System.Drawing.Size(40, 40);
                userPictBox.SizeMode = PictureBoxSizeMode.Zoom; // 拡縮

                // ユーザーラベル
                Label userLabel = new Label();
                this.MainPanel.Controls.Add(userLabel);
                UserLabelList[i] = userLabel;
                userLabel.AutoSize = true;
                userLabel.BackColor = System.Drawing.Color.WhiteSmoke;
                userLabel.Font = new System.Drawing.Font("MS UI Gothic", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
                userLabel.ForeColor = System.Drawing.Color.DarkBlue;
                userLabel.Location = new System.Drawing.Point(72, 5 + distanceY * i);
                userLabel.Size = new System.Drawing.Size(39, 19);
                userLabel.Text = "";

                TextBox commentTextBox = new TextBox();
                this.MainPanel.Controls.Add(commentTextBox);
                CommentTextBoxList[i] = commentTextBox;
                commentTextBox.AutoSize = false;
                commentTextBox.BackColor = System.Drawing.Color.WhiteSmoke;
                commentTextBox.Font = new System.Drawing.Font("MS UI Gothic", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
                commentTextBox.ForeColor = System.Drawing.Color.Black;
                commentTextBox.BorderStyle = BorderStyle.None;
                commentTextBox.Location = new System.Drawing.Point(72, 24 + distanceY * i);
                commentTextBox.Size = new System.Drawing.Size(1200, 30);
                commentTextBox.Text = "";
                commentTextBox.ReadOnly = true;
            }
            System.Diagnostics.Debug.Assert(UserPictBoxList.Length == CommentTextBoxList.Length);
            System.Diagnostics.Debug.Assert(UserLabelList.Length == CommentTextBoxList.Length);

        }

        /// <summary>
        /// フォームのロードイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainFrm_Load(object sender, EventArgs e)
        {
            ChatClient = new TwcasChatClient();
            ChatClient.OnCommentReceiveEach += ChatClient_OnCommentReceiveEach;
            ChatClient.OnCommentReceiveDone += ChatClient_OnCommentReceiveDone;
            ChatClient.OnMovieIdChanged += ChatClient_OnMovieIdChanged;
        }

        /// <summary>
        /// フォーをの閉じる前のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ChatClient.Dispose();
            BouyomiChan.ClearText();
            BouyomiChan.Dispose();
        }

        /// <summary>
        /// フォームを閉じたときのイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainFrm_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        /// <summary>
        /// 開くボタンクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpen_Click(object sender, EventArgs e)
        {
            doOpen();
        }

        /// <summary>
        /// フォームのタイトルを設定する
        /// </summary>
        private void setTitle()
        {
            // チャンネル名をGUIに設定
            string channelName = ChatClient.ChannelName;
            if (channelName != null && channelName.Length != 0)
            {
                this.Text = channelName + " - " + TitleBase;
            }
            else
            {
                this.Text = TitleBase;
            }
        }

        /// <summary>
        /// チャンネル情報の初期化
        /// </summary>
        private void initChannelInfo()
        {
            ChatClient.InitChannelInfo();

            // タイトルを設定
            setTitle();

            // 放送スクリーン画像クリア
            pictBoxScreenThumb.ImageLocation = "";
            pictBoxScreenThumb.Refresh();
        }

        /// <summary>
        /// ページを開く
        /// </summary>
        private void doOpen()
        {

            // タイマーが停止するまで待つ
            ChatClient.Stop();

            // 初期化
            // チャンネルの初期化
            initChannelInfo();

            // チャット窓の初期化
            initChatWindow();

            // チャンネル名を取得
            string channelName = getChannelNameFromGui();
            if (channelName == "")
            {
                return;
            }
            // チャンネル名のセット
            ChatClient.ChannelName = channelName;
            // タイトルを設定
            setTitle();

            // チャットをオープンする
            bool ret = ChatClient.Start();
            if (!ret)
            {
                // チャンネルの初期化
                initChannelInfo();
                return;
            }

            System.Diagnostics.Debug.WriteLine("doOpen end");
        }

        /// <summary>
        /// チャット窓の初期化
        /// </summary>
        private void initChatWindow()
        {
            BouyomiChan.ClearText();
            foreach (TextBox textBox in CommentTextBoxList)
            {
                textBox.Text = "";
            }
            foreach (Label label in UserLabelList)
            {
                label.Text = "";
            }
            foreach (PictureBox pictBox in UserPictBoxList)
            {
                pictBox.ImageLocation = "";
            }
        }

        /// <summary>
        /// チャンネル名をGUIから取得する
        /// </summary>
        /// <returns></returns>
        private string getChannelNameFromGui()
        {
            // チャンネル欄にURLが指定されてもOKとする
            string[] tokens = txtBoxChannelName.Text.Split('/');
            if (tokens.Length == 0)
            {
                return "";
            }
            string channelName = "";
            for (int i = tokens.Length - 1; i >= 0; i--)
            {
                channelName = tokens[i];
                if (channelName.Length != 0)
                {
                    break;
                }
            }
            return channelName;
        }

        /// <summary>
        /// テキストボックスのキーダウンイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtBoxChannelName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                doOpen();
            }
        }

        /// <summary>
        /// テキストボックスのキーアップイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtBoxChannelName_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // メッセージボックスのキーダウンの後のキーアップイベントやIMEの変換確定のイベントもここにくるので
                // エンターキーの処理はキーダウンに移動した
            }

        }

        /// <summary>
        /// コメント受信イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="comment"></param>
        private void ChatClient_OnCommentReceiveEach(TwcasChatClient sender, CommentStruct comment)
        {
            // コメントのGUIへのセットと棒読みちゃんへの送信
            // 棒読みちゃんへ送信
            string sendText = comment.Text;
            string bcTitle = ChatClient.BcTitle;
            if (bcTitle != "")
            {
                StringBuilder sendTextSb = new StringBuilder(sendText);
                sendTextSb.Replace("(" + bcTitle + ")", "");
                sendText = sendTextSb.ToString();
            }
            BouyomiChan.Talk(sendText);

            // 音声送信の処理が行われるようにイベント処理する
            Application.DoEvents();
        }

        /// <summary>
        /// コメント受信完了イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        private void ChatClient_OnCommentReceiveDone(TwcasChatClient sender)
        {
            IList<CommentStruct> commentList = ChatClient.CommentList;
            // GUIへセット
            for (int iLabel = 0, iComment = commentList.Count - 1; iLabel < CommentTextBoxList.Length && iComment >= 0; iLabel++, iComment--)
            {
                CommentStruct tagtComment = commentList[iComment];
                // コメントラベル
                CommentTextBoxList[iLabel].Text = tagtComment.Text;
                CommentTextBoxList[iLabel].Refresh();
                // ユーザーラベル
                UserLabelList[iLabel].Text = tagtComment.UserName;
                UserLabelList[iLabel].Refresh();
                // ユーザーピクチャーボックス
                UserPictBoxList[iLabel].ImageLocation = tagtComment.UserThumbUrl;
                UserPictBoxList[iLabel].Refresh();
            }

            // 放送スクリーン画像の表示
            if (commentList.Count > 0)
            {
                CommentStruct lastCmnt = commentList[commentList.Count - 1];
                if (pictBoxScreenThumb.ImageLocation != lastCmnt.ScreenThumbUrl)
                {
                    pictBoxScreenThumb.ImageLocation = lastCmnt.ScreenThumbUrl;
                    pictBoxScreenThumb.Refresh();
                }
            }
            else
            {
                pictBoxScreenThumb.ImageLocation = "";
                pictBoxScreenThumb.Refresh();
            }
        }

        /// <summary>
        /// 動画IDが変更された時のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        private void ChatClient_OnMovieIdChanged(TwcasChatClient sender)
        {
            doOpen();
        }

        /// <summary>
        /// 既定ブラウザで放送ページを開く
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnWeb_Click(object sender, EventArgs e)
        {
            if (ChatClient.BcUrl == "")
            {
                return;
            }
            string url = ChatClient.BcUrl;

            // ブラウザを開く
            System.Diagnostics.Process.Start(url);
        }

        /// <summary>
        /// 「設定」ボタンがクリックされた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsButton_Click(object sender, EventArgs e)
        {
            // 一旦TopMostを解除する
            this.TopMost = false;

            // 設定ダイアログを表示する
            var settingsForm = new SettingsForm();
            settingsForm.Owner = this;
            DialogResult result = settingsForm.ShowDialog();

            // 設定を反映する
            string topMost = ConfigurationSettings.AppSettings["TopMost"];
            this.TopMost = (topMost == "1");
        }
    }
}
