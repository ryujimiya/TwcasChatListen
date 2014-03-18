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
using HtmlAgilityPack;
using Newtonsoft.Json;

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
            /// コメントID
            /// </summary>
            public int Id;
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
            /// ユーザーサムネールURL
            /// </summary>
            public string UserThumbUrl;
            /// <summary>
            /// 放送スクリーンサムネールURL
            /// </summary>
            public string ScreenThumbUrl;
        }

        /// <summary>
        /// 放送ステータス応答
        /// {"error":false,"status":0,"movieid":16337475,"title":"\u3088\u3063\u3061\u30e1\u30f3","duration":911}
        /// </summary>
        public class BcStatusResponse
        {
            public bool error { get; set; }
            public int status { get; set; }
            public int movieid { get; set; }
            public string title { get; set; }
            public int duration { get; set; }
        }

        /// <summary>
        /// 放送情報応答
        /// </summary>
        public class BcInfoResponse
        {
            public int maxchars { get; set; }
            public int comment_update_interval { get; set; }
            public int cnum { get; set; }
            public string posttitle { get; set; }
            public string postmessage { get; set; }
            public int duration { get; set; }
            public string movietitle { get; set; }
        }

        /// <summary>
        /// 放送コメント応答
        /// </summary>
        public class BcCmntResponse
        {
            public int id { get; set; }
            public string @class { get; set; }
            public string html { get; set; }
            public string date { get; set; }
            public string dur { get; set; }
            public string uid { get; set; }
            public string screen { get; set; }
            public string statusid { get; set; }
            public int lat { get; set; }
            public int lng { get; set; }
            public bool show { get; set; }
            public string yomi { get; set; }
        }

        /// <summary>
        /// 放送コメント応答(更新)
        /// </summary>
        public class BcCmntUpdateResponse
        {
            public List<BcCmntResponse> comment { get; set; }
            public int cnum { get; set; }
        }

        ///////////////////////////////////////////////////////////////////////
        // 定数
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// TwitCastingTVのURL
        /// </summary>
        private const string TwcastUrl = "http://twitcasting.tv";
        /// <summary>
        /// 最大保持コメント数
        /// </summary>
        private const int MaxStoredCommentCnt = 40;
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
        /// 直近のコメント数
        /// </summary>
        private int LastBcCmntCnt = 0;
        /// <summary>
        /// 直近のコメントId
        /// </summary>
        private int LastBcCmntId = 0;

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
            MainTimer.Interval = 5 * 1000;
            MainTimer.Enabled = false;
            StatusTimer.Interval = 30 * 1000;
            StatusTimer.Enabled = false;
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
            BcStatus = 0;
            MovieId = 0;
            PendingCntForOffLine = 0;
            LastBcCmntCnt = 0;
            LastBcCmntId = 0;

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

            // 放送URLを取得
            BcUrl = makeBcUrl(channelName);
            if (BcUrl == "")
            {
                // チャンネルの初期化
                initChannelInfo();
                return;
            }

            // 放送ページから動画ID等を取得する
            getBcInfoFromBcPage();
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
            
            // ステータスタイマーを開始
            StatusTimer.Enabled = true;

            // メインタイマー処理
            doMainTimerProc();
            // メインタイマーを開始
            MainTimer.Enabled = true;

            System.Diagnostics.Debug.WriteLine("doOpen end");
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
            return TwcastUrl + "/" + channelName;
        }

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
            IList<CommentStruct> workCommentList = null;
            if (LastBcCmntId == 0)
            {
                // コメント一覧を取得する
                workCommentList = getBcCmntListAll();
                if (workCommentList.Count == 0)
                {
                    return;
                }

            }
            else
            {
                // コメント更新一覧を取得する
                workCommentList = getBcCmntListUpdate();
            }
            if (workCommentList == null)
            {
                return;
            }

            // コメントをGUIに登録する
            setCmntToGui(workCommentList);
        }

        /// <summary>
        /// コメントをGUIに登録する
        /// </summary>
        /// <param name="workCommentList"></param>
        private void setCmntToGui(IList<CommentStruct> workCommentList)
        {
            // 登録済みの最新コメントを取得
            CommentStruct prevComment = new CommentStruct();
            if (CommentList.Count > 0)
            {
                prevComment = CommentList[CommentList.Count - 1];
            }
            // 新しいコメントから順にチェック
            int iStPos = 0; // 未登録のコメントの開始位置
            for (int iComment = workCommentList.Count - 1; iComment >= 0; iComment--)
            {
                CommentStruct tagtComment = workCommentList[iComment];

                // 登録済みかチェック
                if (tagtComment.Id == prevComment.Id)
                {
                    iStPos = iComment + 1; // 登録済みのコメントの次のコメントが未登録の開始位置
                    System.Diagnostics.Debug.WriteLine("found stored comment.");
                    break;
                }
            }
            if (iStPos == workCommentList.Count)
            {
                // すべて登録済み
                return;
            }

            // 新規分だけ登録
            for (int iComment = iStPos; iComment < workCommentList.Count; iComment++)
            {
                CommentStruct tagtComment = workCommentList[iComment];

                // 新規のコメントの場合、リストに追加する
                CommentList.Add(tagtComment);
                System.Diagnostics.Debug.WriteLine("■{0} {1} {2}", tagtComment.UserName, tagtComment.Text, tagtComment.TimeStr);
                System.Diagnostics.Debug.WriteLine("■ThumbUrl " + tagtComment.UserThumbUrl);

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
                UserPictBoxList[0].ImageLocation = tagtComment.UserThumbUrl;
                UserPictBoxList[0].Refresh();

                // ここでイベントを処理
                Application.DoEvents();
            }

            // 放送スクリーン画像の表示
            if (CommentList.Count > 0)
            {
                CommentStruct lastCmnt = CommentList[CommentList.Count - 1];
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
        /// 放送の動画情報を取得可能か？
        /// </summary>
        /// <returns></returns>
        private bool IsBcMovieValid()
        {
            if (ChannelName == null || ChannelName == "")
            {
                return false;
            }
            if (MovieId == 0)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// コメント一覧を取得する
        /// </summary>
        /// <returns></returns>
        private IList<CommentStruct> getBcCmntListAll()
        {
            IList<CommentStruct> workCommentList = new List<CommentStruct>();

            if (!IsBcMovieValid())
            {
                return workCommentList;
            }

            // コメント一覧の取得
            // http://ja.twitcasting.tv/アカウント/userajax.php?c=listall&m=動画ID&k=0&f=0&n=10
            // [{"id":,"class":,"html":,"date":,"dur":,"uid":,"screen":,"statusid":"","lat":0,"lng":0,"show":true,"yomi":""},
            //     ...
            // ]
            string url = TwcastUrl + "/" + ChannelName + "/userajax.php?c=listall&m=" + MovieId + "&k=0&f=0&n=10";
            string recvStr = doHttpRequest(url);
            if (recvStr == null)
            {
                // 接続エラー
                return workCommentList;
            }
            try
            {
                // JSON形式からコメント応答オブジェクトに変換
                IList<BcCmntResponse> cmnts = JsonConvert.DeserializeObject<List<BcCmntResponse>>(recvStr);

                // コメント応答リストからコメントを取り出す
                workCommentList = parseBcCmntResponse(cmnts);

                // 直近の取得開始コメントIDをセットする
                if (workCommentList.Count > 0)
                {
                    LastBcCmntId = workCommentList[workCommentList.Count - 1].Id;
                }
                else
                {
                    LastBcCmntId = 0;
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
                return workCommentList;
            }
            return workCommentList;
        }

        /// <summary>
        /// コメント応答のパース
        /// </summary>
        /// <param name="cmnts"></param>
        private IList<CommentStruct> parseBcCmntResponse(IList<BcCmntResponse> cmnts)
        {
            IList<CommentStruct> workCommentList = new List<CommentStruct>();

            if (cmnts == null)
            {
                return workCommentList;
            }

            // コメント応答を取得
            //  日付順
            foreach (BcCmntResponse bcCmntResponse in cmnts)
            {
                int id = bcCmntResponse.id;
                string htmlStr = bcCmntResponse.html;
                string dateStr = bcCmntResponse.date;
                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(htmlStr);
                // 最初のimgタグ：プロフィール画像
                HtmlNode profImgTag = doc.DocumentNode.SelectSingleNode(@"//img[1]");
                if (profImgTag == null)
                {
                    System.Diagnostics.Debug.WriteLine("profImgTag is null. id: [" + id + "] html: [" + htmlStr + "]");
                    continue;
                }
                string profImgSrc = profImgTag.GetAttributeValue("src", "");
                // ユーザーノード
                HtmlNode userSpanTag = doc.DocumentNode.SelectSingleNode(@"//span[@class=""user""]");
                if (userSpanTag == null)
                {
                    System.Diagnostics.Debug.WriteLine("userSpanTag is null. id: [" + id + "] html: [" + htmlStr + "]");
                    continue;
                }
                string userName = userSpanTag.InnerText;
                // コメントノード
                HtmlNode cmntTdTag = doc.DocumentNode.SelectSingleNode(@"//td[@class=""comment""]");
                if (cmntTdTag == null)
                {
                    System.Diagnostics.Debug.WriteLine("cmntTdTag is null. id: [" + id + "] html: [" + htmlStr + "]");
                    continue;
                }
                // テキストノードだけ抽出(spanノードは除外する)
                string cmntStr = "";
                foreach (HtmlNode workChild in cmntTdTag.ChildNodes)
                {
                    if (workChild.GetType() == typeof(HtmlTextNode))
                    {
                        cmntStr += workChild.InnerText;
                    }
                }
                //cmntStr = cmntStr.Replace("<br>", System.Environment.NewLine);
                HtmlNode subTitleNode = cmntTdTag.SelectSingleNode(@"//span[@class=""smallsubtitle""]");
                if (subTitleNode != null)
                {
                    cmntStr += subTitleNode.InnerText;
                }
                cmntStr = System.Web.HttpUtility.HtmlDecode(cmntStr);
                // 放送スクリーン画像
                string screenImgSrc = "";
                HtmlNode screenImgTag = doc.DocumentNode.SelectSingleNode(@"//img[@class=""commentthumb""]");
                if (screenImgTag != null)
                {
                    screenImgSrc = screenImgTag.GetAttributeValue("src", "");
                }

                CommentStruct workComment = new CommentStruct();
                workComment.Id = id;
                workComment.UserThumbUrl = profImgSrc;
                workComment.UserName = userName;
                workComment.TimeStr = dateStr;
                workComment.Text = cmntStr;
                workComment.ScreenThumbUrl = screenImgSrc;
                //System.Diagnostics.Debug.WriteLine("Id " + workComment.Id);
                //System.Diagnostics.Debug.WriteLine("UserThumbUrl " + workComment.UserThumbUrl);
                //System.Diagnostics.Debug.WriteLine("UserName " + workComment.UserName);
                //System.Diagnostics.Debug.WriteLine("TimeStr " + workComment.TimeStr);
                //System.Diagnostics.Debug.WriteLine("Text " + workComment.Text);
                //System.Diagnostics.Debug.WriteLine("ScreenThumbUrl " + workComment.ScreenThumbUrl);
                workCommentList.Add(workComment);
            }
            return workCommentList;
        }

        /// <summary>
        /// コメント更新一覧を取得する
        /// </summary>
        /// <returns></returns>
        private IList<CommentStruct> getBcCmntListUpdate()
        {
            IList<CommentStruct> workCommentList = new List<CommentStruct>();

            if (!IsBcMovieValid())
            {
                return workCommentList;
            }

            // コメント更新一覧の取得
            // http://ja.twitcasting.tv/アカウント/userajax.php?c=listupdate&m=動画ID&n=直近のコメント数&k=直近のコメントID
            // {comment:[{"id":,"class":,"html":,"date":,"dur":,"uid":,"screen":,"statusid":"","lat":0,"lng":0,"show":true,"yomi":""},
            //     ...
            //          ],
            //  cnum:}
            string url = TwcastUrl + "/" + ChannelName + "/userajax.php?c=listupdate&m=" + MovieId + "&n=" + LastBcCmntCnt + "&k=" + LastBcCmntId;
            string recvStr = doHttpRequest(url);
            if (recvStr == null)
            {
                // 接続エラー
                return workCommentList;
            }
            if (recvStr == "[]" || recvStr == "{\"edit\":\"\"}")
            {
                // 空の場合
                return workCommentList;
            }
            try
            {
                // JSON形式からコメント応答オブジェクトに変換
                BcCmntUpdateResponse bcCmntUpdateResponse = JsonConvert.DeserializeObject<BcCmntUpdateResponse>(recvStr);
                IList<BcCmntResponse> cmnts = bcCmntUpdateResponse.comment;

                // コメント応答リストからコメントを取り出す
                workCommentList = parseBcCmntResponse(cmnts);

                // 直近の取得開始コメントIDをセットする
                if (workCommentList.Count > 0)
                {
                    LastBcCmntId = workCommentList[workCommentList.Count - 1].Id;
                }
                else
                {
                    LastBcCmntId = 0;
                }
                // 直近のコメント数をセットする
                LastBcCmntCnt = bcCmntUpdateResponse.cnum;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
                return workCommentList;
            }
            return workCommentList;
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
        /// 放送ページから動画ID等を取得する
        /// </summary>
        private void getBcInfoFromBcPage()
        {
            if (BcUrl == "")
            {
                return;
            }
            // 放送ページから動画ID等を取得する
            string url = BcUrl;
            string recvStr = doHttpRequest(url);
            if (recvStr == null)
            {
                // 接続エラー
                return;
            }
            int maxchars = 0;
            {
                MatchCollection matches = Regex.Matches(recvStr, "var maxchars = ([0-9]+);");
                if (matches != null && matches.Count == 1)
                {
                    maxchars = Convert.ToInt32(matches[0].Groups[1].Value);
                }
            }
            int interval = 0;
            {
                MatchCollection matches = Regex.Matches(recvStr, "var comment_update_interval = ([0-9]+);");
                if (matches != null && matches.Count == 1)
                {
                    interval = Convert.ToInt32(matches[0].Groups[1].Value);
                }
            }
            int cnum = 0;
            {
                MatchCollection matches = Regex.Matches(recvStr, "var movie_cnum = ([0-9]+);");
                if (matches != null && matches.Count == 1)
                {
                    cnum = Convert.ToInt32(matches[0].Groups[1].Value);
                }
            }
            int movieid = 0;
            {
                MatchCollection matches = Regex.Matches(recvStr, "var movieid = \"([0-9]+)\";");
                if (matches != null && matches.Count >= 1) // movieidの宣言が2つあるので条件を1以上としている
                {
                    movieid = Convert.ToInt32(matches[0].Groups[1].Value);
                }
            }
            MovieId = movieid;
            LastBcCmntId = 0;
            LastBcCmntCnt = cnum;
            System.Diagnostics.Debug.WriteLine("LastBcCmntCnt " + LastBcCmntCnt);
            System.Diagnostics.Debug.WriteLine("MovieId " + MovieId);
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
            string recvStr = doHttpRequest(url);
            if (recvStr == null)
            {
                // 接続エラー
                return;
            }
            try
            {
                // JSON形式から放送ステータス応答オブジェクトに変換
                BcStatusResponse bcStatusResponse = JsonConvert.DeserializeObject<BcStatusResponse>(recvStr);
                bool error = bcStatusResponse.error;
                if (error
                    || bcStatusResponse.movieid < MovieId  // 最新動画ID(放送ページ上の動画ID)より古いものを取得した場合
                    )
                {
                    // 動画ID等の情報がとれない場合がある
                }
                else
                {
                    BcStatus = bcStatusResponse.status;
                    MovieId = bcStatusResponse.movieid;
                    BcTitle = bcStatusResponse.title;
                    System.Diagnostics.Debug.WriteLine("BcStatus = {0} MovieId = {1} BcTitle = {2}", BcStatus, MovieId, BcTitle);
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
            }
        }

        /// <summary>
        /// HTTPリクエストを送信する
        /// </summary>
        /// <param name="url"></param>
        /// <returns>null:接続エラー または、recvStr:受信文字列</returns>
        private static string doHttpRequest(string url)
        {
            string recvStr = null;
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
                    return recvStr;
                }
                StreamReader sr = new StreamReader(stream);
                recvStr = sr.ReadToEnd();
            }
            return recvStr;
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
