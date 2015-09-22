using System;
using System.Collections.Generic;
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
    /// <summary>
    /// コメント受信イベントハンドラデリゲート
    /// </summary>
    /// <param name="sender">チャットクライアント</param>
    /// <param name="comment">受信したコメント構造体</param>
    public delegate void OnCommentReceiveEachDelegate(TwcasChatClient sender, CommentStruct comment);
    /// <summary>
    /// コメント受信完了イベントハンドラデリゲート
    /// <param name="sender">チャットクライアント</param>
    /// </summary>
    public delegate void OnCommentReceiveDoneDelegate(TwcasChatClient sender);
    /// <summary>
    /// 動画IDが変更されたときのイベントハンドラデリゲート
    /// </summary>
    /// <param name="sender">チャットクライアント</param>
    public delegate void OnMovieIdChangedDelegate(TwcasChatClient sender);

    /// <summary>
    /// コメント構造体
    /// </summary>
    public struct CommentStruct
    {
        /// <summary>
        /// コメントID
        /// </summary>
        //public uint Id;
        public ulong Id;
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
    /// Twitcasting.TVのチャットクライアント（機能はコメント受信のみ)
    /// </summary>
    public class TwcasChatClient : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////
        // 型
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 放送ステータス応答
        /// {"error":false,"status":0,"movieid":16337475,"title":"\u3088\u3063\u3061\u30e1\u30f3","duration":911}
        /// </summary>
        private class BcStatusResponse
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
        private class BcInfoResponse
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
        private class BcCmntResponse
        {
            //public uint id { get; set; }
            public ulong id { get; set; }
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
        private class BcCmntUpdateResponse
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
        /// オフライン時にコメントを取得する最大回数
        /// </summary>
        private const int MaxPendingCntForOffLine = -1;
        //private const int MaxPendingCntForOffLine = 5;

        ///////////////////////////////////////////////////////////////////////
        // フィールド
        ///////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 破棄された?
        /// </summary>
        private bool disposed = false;
        /// <summary>
        /// チャンネル名
        /// </summary>
        public string ChannelName { get; set; }
        /// <summary>
        /// 放送URL
        /// </summary>
        public string BcUrl { get; private set; }
        /// <summary>
        /// コメントリスト
        /// </summary>
        public IList<CommentStruct> CommentList { get; private set; }
        /// <summary>
        /// ユーザーステータス
        ///  0: 放送中
        ///  1, 2: オフライン
        /// </summary>
        public int BcStatus { get; private set; }
        /// <summary>
        /// 動画ID
        /// </summary>
        public int MovieId { get; private set; }
        /// <summary>
        /// 放送タイトル
        /// </summary>
        public string BcTitle { get; private set; }

        /// <summary>
        /// コメント受信イベントハンドラ
        /// </summary>
        public event OnCommentReceiveEachDelegate OnCommentReceiveEach = null;
        /// <summary>
        /// コメント受信完了イベントハンドラ
        /// </summary>
        public event OnCommentReceiveDoneDelegate OnCommentReceiveDone = null;
        /// <summary>
        /// 動画IDが変更された時のイベントハンドラ
        /// </summary>
        public event OnMovieIdChangedDelegate OnMovieIdChanged = null;

        /// <summary>
        /// コメント取得タイマー
        /// </summary>
        private System.Windows.Forms.Timer MainTimer = null;
        /// <summary>
        /// ステータス取得タイマー
        /// </summary>
        private System.Windows.Forms.Timer StatusTimer = null;
        /// <summary>
        /// タイマー処理実行中？
        /// </summary>
        private bool IsTimerProcRunning = false;
        /// <summary>
        /// オフライン後、タイマー処理をした回数
        /// </summary>
        private int PendingCntForOffLine = 0;
        /// <summary>
        /// 直近のコメント数
        /// </summary>
        private int LastBcCmntCnt = 0;
        /// <summary>
        /// 直近のコメントId
        /// </summary>
        //private uint LastBcCmntId = 0;
        private ulong LastBcCmntId = 0;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TwcasChatClient()
        {
            // 初期化
            CommentList = new List<CommentStruct>();
            MainTimer = new System.Windows.Forms.Timer();
            StatusTimer = new System.Windows.Forms.Timer();

            InitChannelInfo();

            // タイマーイベントハンドラの設定
            MainTimer.Tick += MainTimer_Tick;
            StatusTimer.Tick += StatusTimer_Tick;
            // タイマー間隔を設定する
            MainTimer.Interval = 5 * 1000;
            MainTimer.Enabled = false;
            StatusTimer.Interval = 30 * 1000;
            StatusTimer.Enabled = false;
        }

        /// <summary>
        /// ファイナライザ
        /// </summary>
        ~TwcasChatClient()
        {
            Dispose(false);
        }

        /// <summary>
        /// 終了
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                MainTimer.Enabled = false;

                disposed = true;
            }
        }

        /// <summary>
        /// チャンネル情報の初期化
        /// </summary>
        public void InitChannelInfo()
        {
            ChannelName = "";
            BcUrl = "";
            BcStatus = 0;
            MovieId = 0;
            LastBcCmntCnt = 0;
            LastBcCmntId = 0;
            PendingCntForOffLine = 0;
            CommentList.Clear();
        }

        /// <summary>
        /// コメント受信処理を開始する
        /// </summary>
        public bool Start()
        {
            // 放送URLを取得
            BcUrl = makeBcUrl(this.ChannelName);
            if (BcUrl == "")
            {
                return false;
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
                InitChannelInfo();
                return false;
            }

            // ステータスタイマーを開始
            StatusTimer.Enabled = true;

            // メインタイマー処理
            doMainTimerProc();
            // メインタイマーを開始
            MainTimer.Enabled = true;

            return true;

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

        /// <summary
        /// コメント受信処理を停止する>
        ///   タイマーが停止するまで待つ
        /// </summary>
        public void Stop()
        {
            // タイマーを停止する
            MainTimer.Enabled = false;
            StatusTimer.Enabled = false;

            // タイマー処理が終了するまで待つ
            System.Diagnostics.Debug.WriteLine("timer waiting...");
            while (IsTimerProcRunning)
            {
                Application.DoEvents();
            }
            System.Diagnostics.Debug.WriteLine("timer waiting... done");
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

                if (OnCommentReceiveEach != null)
                {
                    OnCommentReceiveEach(this, tagtComment);
                }
            }

            if (OnCommentReceiveDone != null)
            {
                OnCommentReceiveDone(this);
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
                System.Diagnostics.Debug.WriteLine("[Error]getBcCmntListAll: recvStr: " + recvStr);
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
                //uint id = bcCmntResponse.id;
                ulong id = bcCmntResponse.id;
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
                System.Diagnostics.Debug.WriteLine("[Error]getBcCmntListUpdate: recvStr: " + recvStr);
                return workCommentList;
            }
            return workCommentList;
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
                if (OnMovieIdChanged != null)
                {
                    OnMovieIdChanged(this);
                }
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
                    System.Diagnostics.Debug.WriteLine("skip BcStatus. recvStr: " + recvStr + " current MovieId: " + MovieId);
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

    }
}
