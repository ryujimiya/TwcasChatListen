using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO; // Stream
using System.Net; // WebClient
using System.Text.RegularExpressions; // Regex
using System.Threading;
using System.Web.Script.Serialization; // JavaScriptSerializer (System.Web.Extensionsを参照追加)

namespace TwcasChatter
{
    public partial class MainFrm : Form
    {
        ///////////////////////////////////////////////////////////////////////
        // 型
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// コメント構造体
        /// </summary>
        private struct CommentStruct
        {
            /// <summary>
            /// コメントテキスト
            /// </summary>
            public string Text;
            /// <summary>
            /// ユーザー名
            /// </summary>
            public string UserName;
            /// <summary>
            /// 時刻
            /// </summary>
            public string TimeStr;
            /// <summary>
            /// サムネールURL
            /// </summary>
            public string ThumbUrl;
        }

        /// <summary>
        /// 放送ステータス構造体
        /// </summary>
        private struct BcStatusStruct
        {
            //{"error":false,"status":0,"movieid":16337475,"title":"\u3088\u3063\u3061\u30e1\u30f3","duration":911}
            public bool error;
            public int status;
            public int movieid;
            public string title;
            public int duration;
        }
        
        ///////////////////////////////////////////////////////////////////////
        // 定数
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// TwitCastingTVのURL
        /// </summary>
        private const string TwcastUrl = "http://twitcasting.tv/";
        /// <summary>
        /// 最大保持コメント数
        /// </summary>
        private const int MaxStoredCommentCnt = 20;
        /// <summary>
        /// 最大表示コメント数
        /// </summary>
        private const int MaxShowCommentCnt = 20;
        /// <summary>
        /// オフライン時にコメントを取得する最大回数
        /// </summary>
        private const int MaxPendingCntForOffLine = -1;
        //private const int MaxPendingCntForOffLine = 5;

        ///////////////////////////////////////////////////////////////////////
        // フィールド
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// タイトルのベース
        /// </summary>
        private string TitleBase = "";
        /// <summary>
        /// チャンネル名
        /// </summary>
        private string ChannelName = "";
        /// <summary>
        /// 放送URL
        /// </summary>
        private string BcUrl = "";
        /// <summary>
        /// チャット窓Url
        /// </summary>
        //private string ChatUrl = "";
        /// <summary>
        /// コメントリスト
        /// </summary>
        private IList<CommentStruct> CommentList = new List<CommentStruct>();
        /// <summary>
        /// 棒読みちゃん
        /// </summary>
        private MyUtilLib.BouyomiChan BouyomiChan = new MyUtilLib.BouyomiChan();
        /// <summary>
        /// タイマー処理実行中？
        /// </summary>
        private bool IsTimerProcRunning = false;

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
        /// ユーザーステータス
        ///  0: 放送中
        ///  1, 2: オフライン
        /// </summary>
        private int BcStatus = 0;
        /// <summary>
        /// 動画ID
        /// </summary>
        private int MovieId = 0;
        /// <summary>
        /// 放送タイトル
        /// </summary>
        private string BcTitle = "";
        /// <summary>
        /// オフライン後、タイマー処理をした回数
        /// </summary>
        private int PendingCntForOffLine = 0;
        /// <summary>
        /// キーダウンイベントを受けたコントロール
        /// </summary>
        private Control CtrlOfKeyDown = null;

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

                // ユーザーラベル
                Label userLabel = new Label();
                this.MainPanel.Controls.Add(userLabel);
                UserLabelList[i] = userLabel;
                userLabel.AutoSize = true;
                userLabel.BackColor = System.Drawing.Color.DimGray;
                userLabel.Font = new System.Drawing.Font("MS UI Gothic", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
                userLabel.ForeColor = System.Drawing.Color.SpringGreen;
                userLabel.Location = new System.Drawing.Point(72, 5 + distanceY * i);
                userLabel.Size = new System.Drawing.Size(39, 19);
                userLabel.Text = "";

                TextBox commentTextBox = new TextBox();
                this.MainPanel.Controls.Add(commentTextBox);
                CommentTextBoxList[i] = commentTextBox;
                commentTextBox.AutoSize = false;
                commentTextBox.BackColor = System.Drawing.Color.DimGray;
                commentTextBox.Font = new System.Drawing.Font("MS UI Gothic", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
                commentTextBox.ForeColor = System.Drawing.Color.White;
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
            // タイマー間隔を設定する
            MainTimer.Interval = 1000;
            MainTimer.Enabled = false;
            StatusTimer.Interval = 30 * 1000;
            StatusTimer.Enabled = false;

            //DEBUG
            //tbChannelName.Text = "teirufeari12077";
            //tbChannelName.Text = "blackrock1925";
            //tbChannelName.Text = "kazucheru_mgg";
        }

        /// <summary>
        /// フォーをの閉じる前のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            MainTimer.Enabled = false;

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
            if (ChannelName != null && ChannelName.Length != 0)
            {
                this.Text = ChannelName + " - " + TitleBase;
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
            ChannelName = "";
            BcUrl = "";
            //ChatUrl = "";
            BcStatus = 0;
            MovieId = 0;
            PendingCntForOffLine = 0;
            // タイトルを設定
            setTitle();
        }

        /// <summary>
        /// ページを開く
        /// </summary>
        private void doOpen()
        {
            // 初期化
            // タイマーを停止する
            MainTimer.Enabled = false;
            StatusTimer.Enabled = false;
            System.Diagnostics.Debug.WriteLine("timer waiting...");
            while (IsTimerProcRunning)
            {
                Application.DoEvents();
            }
            System.Diagnostics.Debug.WriteLine("timer waiting... done");

            // チャンネルの初期化
            initChannelInfo();

            // チャット窓の初期化
            WebBrowser.Url = null;
            initChatWindow();

            // チャンネル名を取得
            string channelName = getChannelNameFromGui();
            if (channelName == "")
            {
                return;
            }
            // チャンネル名のセット
            ChannelName = channelName;
            // タイトルを設定
            setTitle();

            // 放送ステータス取得
            getBcStatus();

            // 動画IDが取得できているかチェック
            if (MovieId == 0)
            {
                new Thread(new ThreadStart(delegate()
                    {
                        MessageBox.Show("番組が見つかりませんでした");
                    })).Start();
                // チャンネルの初期化
                initChannelInfo();
                return;
            }
            
            // 放送URLを取得
            BcUrl = makeBcUrl(channelName);
            if (BcUrl == "")
            {
                // チャンネルの初期化
                initChannelInfo();
                return;
            }
            /*
            // チャット窓URLを取得
            ChatUrl = makeChatUrl(channelName, MovieId);
            if (ChatUrl == "")
            {
                // チャンネルの初期化
                initChannelInfo();
                return;
            }
             */

            // 放送ページのチャットを開く
            //WebBrowser.Url = new Uri(ChatUrl);
            WebBrowser.Url = new Uri(BcUrl);

            // ステータスタイマーを開始
            StatusTimer.Enabled = true;
        }

        /// <summary>
        /// チャット窓の初期化
        /// </summary>
        private void initChatWindow()
        {
            CommentList.Clear();
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
        /// 放送URLを作成する
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        private static string makeBcUrl(string channelName)
        {
            if (channelName.Length == 0)
            {
                return "";
            }
            return TwcastUrl + channelName;
        }

        /*
        /// <summary>
        /// チャット窓のURLを作成する
        /// </summary>
        /// <param name="channelName"></param>
        /// <param name="movieId"></param>
        /// <returns></returns>
        private static string makeChatUrl(string channelName, int movieId)
        {
            if (channelName.Length == 0 || movieId == 0)
            {
                return "";
            }
            return TwcastUrl + channelName + "/windowcomment/" + string.Format("{0}", movieId);
        }
         */

        /// <summary>
        /// タイマーイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainTimer_Tick(object sender, EventArgs e)
        {
            if (!IsTimerProcRunning)
            {
                IsTimerProcRunning = true;
                if (BcStatus == 0 
                    || (BcStatus != 0 && (MaxPendingCntForOffLine == -1 || (PendingCntForOffLine < MaxPendingCntForOffLine)))
                    )
                {
                    doMainTimerProc();
                    if (BcStatus != 0)
                    {
                        PendingCntForOffLine++;
                    }
                }
                IsTimerProcRunning = false;
            }
        }

        /// <summary>
        /// メインタイマー処理
        /// </summary>
        private void doMainTimerProc()
        {
            if (WebBrowser.Url == null || WebBrowser.Url.Host == "")
            {
                return;
            }

            //System.Diagnostics.Debug.WriteLine(WebBrowser.Document.Body.InnerHtml);

            IList<CommentStruct> workCommentList = new List<CommentStruct>();
            HtmlElement elementTBodyComment = WebBrowser.Document.GetElementById("comment");
            if (elementTBodyComment == null)
            {
                return;
            }
            //System.Diagnostics.Debug.WriteLine(elementCommentBox.InnerHtml);
            // コメントを取得する
            HtmlElementCollection tdTagElements = elementTBodyComment.GetElementsByTagName("TD");
            HtmlElementCollection imgTagElements = elementTBodyComment.GetElementsByTagName("IMG");
            if (tdTagElements.Count == 0)
            {
                return;
            }
            if (imgTagElements.Count == 0)
            {
                return;
            }
            // 画像の並びチェック
            {
                HtmlElement workElement = imgTagElements[0];
                string imgSrc = workElement.GetAttribute("src");
                if (imgSrc.IndexOf("profile") >= 0  // プロフィール画像 "profile_images"
                    || imgSrc.IndexOf("graph.facebook.com") >= 0 // フェイスブック
                    || imgSrc.IndexOf("twitter_normal_") >= 0 // 卵の画像
                    )
                {
                    // ユーザー画像
                }
                else
                {
                    // 放送画像サムネール
                    // プレミアムユーザー画像
                    System.Diagnostics.Debug.WriteLine("image invalid");
                    return;
                }
            }

            // コメント
            foreach (HtmlElement element in tdTagElements)
            {
                IList<string> workCommentTokens = new List<string>();
                string commentText = element.OuterText;
                StringBuilder worksb = new StringBuilder(commentText);
                worksb.Replace("\r", "");
                commentText = worksb.ToString();
                //System.Diagnostics.Debug.WriteLine(commentText);
                if (commentText != null && commentText.Length != 0)
                {
                    string[] tokens = commentText.Split('\n');
                    foreach (string token in tokens)
                    {
                        //System.Diagnostics.Debug.WriteLine(token);
                        if (token != null && token.Length != 0)
                        {
                            workCommentTokens.Add(token);
                        }
                    }
                }
                if (workCommentTokens.Count >= 3)
                {
                    // コメントを格納
                    CommentStruct workComment = new CommentStruct();
                    workComment.UserName = workCommentTokens[0];
                    // コメント本文は複数行の場合あり
                    for (int i = 1; i < (workCommentTokens.Count - 1); i++)
                    {
                        if (workComment.Text != null && workComment.Text.Length != 0)
                        {
                            workComment.Text += " ";
                        }
                        workComment.Text += workCommentTokens[i];
                    }
                    workComment.TimeStr = workCommentTokens[workCommentTokens.Count - 1];

                    workCommentList.Add(workComment);
                    //System.Diagnostics.Debug.WriteLine("{0} {1} {2}", workComment.UserName, workComment.Text, workComment.DateTime);
                }

            }

            for (int iThumb = 0, iComment = 0; iThumb < imgTagElements.Count; iThumb++)
            {
                HtmlElement workElement = imgTagElements[iThumb];
                string imgSrc = workElement.GetAttribute("src");
                if (imgSrc.IndexOf("profile") >= 0  // プロフィール画像 "profile_images"
                    || imgSrc.IndexOf("graph.facebook.com") >= 0 // フェイスブック
                    || imgSrc.IndexOf("twitter_normal_") >= 0 // 卵の画像
                    )
                {
                    // ユーザー画像
                    if (iComment < workCommentList.Count)
                    {
                        CommentStruct workComment = workCommentList[iComment];
                        workComment.ThumbUrl = imgSrc;
                        //System.Diagnostics.Debug.WriteLine("ThumbUrl " + workComment.ThumbUrl);
                        workCommentList[iComment] = workComment;
                        iComment++;
                    }
                }
                else
                {
                    // 放送画像サムネール
                    // プレミアムユーザー画像
                }
            }

            // 全体のコメントリストへ登録
            CommentStruct prevComment = new CommentStruct();
            if (CommentList.Count > 0)
            {
                prevComment = CommentList[CommentList.Count - 1];
            }
            int iLastComment = workCommentList.Count - 1;
            for (int iComment = 0; iComment < workCommentList.Count; iComment++)
            {
                CommentStruct tagtComment = workCommentList[iComment];

                // 登録済みかチェック
                if (tagtComment.UserName == prevComment.UserName &&
                    tagtComment.Text == prevComment.Text &&
                    tagtComment.TimeStr == prevComment.TimeStr)
                {
                    iLastComment = iComment - 1;
                    System.Diagnostics.Debug.WriteLine("found stored comment.");
                    break;
                }
            }

            for (int iComment = iLastComment; iComment >= 0; iComment--)
            {
                CommentStruct tagtComment = workCommentList[iComment];

                // 新規のコメントの場合、リストに追加する
                CommentList.Add(tagtComment);
                System.Diagnostics.Debug.WriteLine("■{0} {1} {2}", tagtComment.UserName, tagtComment.Text, tagtComment.TimeStr);
                System.Diagnostics.Debug.WriteLine("■ThumbUrl " + tagtComment.ThumbUrl);

                // 最大コメント数チェック
                if (CommentList.Count > MaxStoredCommentCnt)
                {
                    CommentList.RemoveAt(0);
                    System.Diagnostics.Debug.Assert(CommentList.Count == MaxStoredCommentCnt);
                }

                // コメントのGUIへのセットと棒読みちゃんへの送信
                // 棒読みちゃんへ送信
                string sendText = "";
                if (BcTitle != "")
                {
                    StringBuilder sendTextSb = new StringBuilder(tagtComment.Text);
                    sendTextSb.Replace("(" + BcTitle + ")", "");
                    sendText = sendTextSb.ToString();
                }
                BouyomiChan.Talk(sendText);

                // GUIへセット
                for (int iLabel = CommentTextBoxList.Length - 1; iLabel >= 1; iLabel--)
                {
                    // コメントラベル
                    {
                        TextBox textBox1 = CommentTextBoxList[iLabel];
                        TextBox textBox2 = CommentTextBoxList[iLabel - 1];
                        textBox1.Text = textBox2.Text;
                        textBox1.Refresh();
                    }
                    // ユーザーラベル
                    {
                        Label label1 = UserLabelList[iLabel];
                        Label label2 = UserLabelList[iLabel - 1];
                        label1.Text = label2.Text;
                        label1.Refresh();
                    }
                    // ユーザーピクチャーボックス
                    {
                        PictureBox pictBox1 = UserPictBoxList[iLabel];
                        PictureBox pictBox2 = UserPictBoxList[iLabel - 1];
                        pictBox1.ImageLocation = pictBox2.ImageLocation;
                        pictBox1.Refresh();
                    }

                }
                // コメントラベル
                CommentTextBoxList[0].Text = tagtComment.Text;
                CommentTextBoxList[0].Refresh();
                // ユーザーラベル
                UserLabelList[0].Text = tagtComment.UserName;
                UserLabelList[0].Refresh();
                // ユーザーピクチャーボックス
                UserPictBoxList[0].ImageLocation = tagtComment.ThumbUrl;
                UserPictBoxList[0].Refresh();

                // ここでイベントを処理
                Application.DoEvents();
            }
        }

        /// <summary>
        /// WebBrowserのドキュメント処理完了イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("WebBrowser_DocumentCompleted");
            if (WebBrowser.Url == null || WebBrowser.Url.Host == "")
            {
                return;
            }
            //System.Diagnostics.Debug.WriteLine(WebBrowser.Url.Host + WebBrowser.Url.AbsolutePath);
            if (!MainTimer.Enabled)
            {
                MainTimer.Enabled = true;
                IsTimerProcRunning = true;
                new Thread(new ThreadStart(delegate()
                    {
                        // ドキュメント処理完了イベントが複数回発生するので、最後のイベントが完了するのを待つ
                        //Thread.Sleep(3 * 1000);
                        this.Invoke(new MethodInvoker(() =>
                            {
                                int retryCnt = 0;
                                bool isCommentReady = false;
                                int saveVolume = MyUtilLib.MyUtil.WaveOutGetVolume();
                                // 消音する(動画が再生されるときの音を消す)
                                MyUtilLib.MyUtil.WaveOutSetVolume(0);

                                while (retryCnt < 30 && !isCommentReady)
                                {
                                    Thread.Sleep(1000);
                                    Application.DoEvents();// これが必要

                                    // 1つでもコメントが取得できているかチェック
                                    HtmlElement elementTBodyComment = WebBrowser.Document.GetElementById("comment");
                                    if (elementTBodyComment != null)
                                    {
                                        HtmlElementCollection tdTagElements = elementTBodyComment.GetElementsByTagName("TD");
                                        foreach (HtmlElement element in tdTagElements)
                                        {
                                            string commentText = element.OuterText;
                                            StringBuilder sb = new StringBuilder(commentText);
                                            sb.Replace("\r", "");
                                            sb.Replace("\n", "");
                                            commentText = sb.ToString();
                                            if (commentText.Length > 0)
                                            {
                                                isCommentReady = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (isCommentReady)
                                    {
                                        // ループを抜ける
                                        break;
                                    }
                                    retryCnt++;
                                    System.Diagnostics.Debug.WriteLine("retryCnt:{0}", retryCnt);
                                }

                                // 音量を元に戻す
                                MyUtilLib.MyUtil.WaveOutSetVolume(saveVolume);
                            }));


                        this.Invoke(new MethodInvoker(() =>
                            {
                                // 動画再生タグの削除
                                deleteMovieTag();
                                // コメントの取得
                                doMainTimerProc();
                                IsTimerProcRunning = false;
                            }));
                        System.Diagnostics.Debug.WriteLine("WebBrowser_DocumentCompleted Thread end.");
                    })).Start();
            }
        }

        /// <summary>
        /// 動画再生タグを削除する
        /// </summary>
        private void deleteMovieTag()
        {
            // 初回処理
            string allStr = WebBrowser.Document.Body.InnerHtml;
            if (allStr == null)
            {
                return;
            }
            // 改行を削除
            StringBuilder sb = new StringBuilder(allStr);
            sb.Replace("\r", "");
            sb.Replace("\n", "");
            allStr = sb.ToString();
            // 変更前の文字の長さを後の比較のためにとっておく
            int len = allStr.Length;

            // 動画再生タグを削除する
            MatchCollection matches = Regex.Matches(allStr, "<OBJECT.*/OBJECT>"); // タグは大文字
            if (matches != null && matches.Count > 0)
            {
                string delStr = matches[0].Value;
                int pos = allStr.IndexOf(delStr);
                if (pos >= 0)
                {
                    allStr = allStr.Remove(pos, delStr.Length);
                    System.Diagnostics.Debug.Assert(allStr.IndexOf(delStr) < 0);
                }
            }

            if (len != allStr.Length)
            {
                // HTMLの内容が変更されたとき
                WebBrowser.Document.Body.InnerHtml = allStr;
                System.Diagnostics.Debug.WriteLine("movie Tag deleted!!!!!!!!!");
            }

        }

        /// <summary>
        /// テキストボックスのキーダウンイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtBoxChannelName_KeyDown(object sender, KeyEventArgs e)
        {
            // キーダウンを受けたコントロールを格納
            CtrlOfKeyDown = sender as Control;
        }

        /// <summary>
        /// テキストボックスのキーアップイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtBoxChannelName_KeyUp(object sender, KeyEventArgs e)
        {
            // キーダウンを受けたコントロールのイベントかチェックする
            // メッセージボックスのキーダウンの後のキーアップイベントを除外する
            if (sender != CtrlOfKeyDown)
            {
                return;
            }
            CtrlOfKeyDown = null;

            if (e.KeyCode == Keys.Enter)
            {
                doOpen();
            }
        }

        /// <summary>
        /// 放送ステータスを取得する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            // 前のステータスを退避
            int prevBcStatus = BcStatus;
            int prevMovieId = MovieId;

            // 放送ステータスを取得
            getBcStatus();

            // 動画IDが変更されたらページを再読み込みする
            if (prevMovieId != 0  // 初回は除外
                && prevMovieId != MovieId)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("movieId changed:{0} -> {1}", prevMovieId, MovieId));
                doOpen();
            }

        }

        /// <summary>
        /// 放送ステータスを取得する
        /// </summary>
        private void getBcStatus()
        {
            if (ChannelName == null || ChannelName == "")
            {
                return;
            }
            // ステータスを取得する
            // http://twitcasting.tv/userajax.php?c=status&u=0424cchi
            //{"error":false,"status":0,"movieid":16337475,"title":"\u3088\u3063\u3061\u30e1\u30f3","duration":911}
            string url = TwcastUrl + "/userajax.php?c=status&u=" + ChannelName;
            using (WebClient webClient = new WebClient())
            {
                Stream stream = null;
                try
                {
                    stream = webClient.OpenRead(url);
                }
                catch (Exception exception)
                {
                    // 接続エラー
                    System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
                    return;
                }
                StreamReader sr = new StreamReader(stream);
                string recvStr = sr.ReadToEnd();
                /*
                MatchCollection matches = Regex.Matches(recvStr, "\"status\":([0-9]),\"movieid\":([0-9]+),\"title\":\"([^\"]+)\",");
                if (matches != null && matches.Count > 0)
                {
                    if (matches[0].Groups.Count >= 4)
                    {
                        string statusStr = matches[0].Groups[1].Value; // Note:Groups[0]は検索された対象 Groups[1]がグループ化した部分
                        BcStatus = int.Parse(statusStr);

                        string movieIdStr = matches[0].Groups[2].Value; // グループ $2
                        MovieId = int.Parse(movieIdStr);

                        string titleStr = matches[0].Groups[3].Value; // グループ $3
                        BcTitle = titleStr;

                        System.Diagnostics.Debug.WriteLine("BcStatus = {0} MovieId = {1} BcTitle = {2}", BcStatus, MovieId, BcTitle);
                    }
                }
                 */
                try
                {
                    // JSON形式から放送ステータス構造体オブジェクトに変換
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    BcStatusStruct bcStatusStruct = ser.Deserialize<BcStatusStruct>(recvStr);
                    BcStatus = bcStatusStruct.status;
                    MovieId = bcStatusStruct.movieid;
                    BcTitle = bcStatusStruct.title;
                    System.Diagnostics.Debug.WriteLine("BcStatus = {0} MovieId = {1} BcTitle = {2}", BcStatus, MovieId, BcTitle);
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
                }
            }
        }

        /// <summary>
        /// 既定ブラウザで放送ページを開く
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnWeb_Click(object sender, EventArgs e)
        {
            if (BcUrl == "")
            {
                return;
            }
            string url = BcUrl;

            // ブラウザを開く
            System.Diagnostics.Process.Start(url);
        }
    }
}
