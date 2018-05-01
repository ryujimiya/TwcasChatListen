using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MyUtilLib;

namespace TwcasChatListen
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// タイトルのベース
        /// </summary>
        private string titleBase = "";
        /// <summary>
        /// 棒読みちゃん
        /// </summary>
        private MyUtilLib.BouyomiChan bouyomiChan = new MyUtilLib.BouyomiChan();

        /// <summary>
        /// ふわっちクライアント
        /// </summary>
        private TwcasChatClient twcasChatClient;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // GUI初期処理
            titleBase = this.Title + " " + MyUtil.GetFileVersion();
            this.Title = titleBase;
        }

        /// <summary>
        /// ウィンドウが開かれた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            twcasChatClient = new TwcasChatClient();
            twcasChatClient.OnCommentReceiveEach += twcasChatClient_OnCommentReceiveEach;
            twcasChatClient.OnCommentReceiveDone += twcasChatClient_OnCommentReceiveDone;
            twcasChatClient.OnMovieIdChanged += twcasChatClient_OnMovieIdChanged;
        }

        /// <summary>
        /// ウィンドウが閉じられようとしている
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            twcasChatClient.Stop();
            bouyomiChan.ClearText();
            bouyomiChan.Dispose();
        }

        /// <summary>
        /// ウィンドウのサイズが変更された
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // ウィンドウの高さ Note:最大化のときthis.Heightだと値がセットされない
            double height = this.RenderSize.Height;
            // データグリッドの高さ変更
            stackPanel1.Height = height - SystemParameters.CaptionHeight;
            dataGrid.Height = stackPanel1.Height - wrapPanel1.Height;
        }

        /// <summary>
        /// コメント受信イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="comment"></param>
        private void twcasChatClient_OnCommentReceiveEach(TwcasChatClient sender, CommentStruct comment)
        {
            // コメントの追加
            UiCommentData uiCommentData = new UiCommentData();
            uiCommentData.UserThumbUrl = comment.UserThumbUrl;
            uiCommentData.UserName = comment.UserName;
            uiCommentData.CommentStr = comment.Text;

            System.Diagnostics.Debug.WriteLine("UserThumbUrl " + uiCommentData.UserThumbUrl);
            System.Diagnostics.Debug.WriteLine("UserName " + uiCommentData.UserName);
            System.Diagnostics.Debug.WriteLine("CommentStr " + uiCommentData.CommentStr);

            ViewModel viewModel = this.DataContext as ViewModel;
            ObservableCollection<UiCommentData> uiCommentDataList = viewModel.UiCommentDataCollection;
            uiCommentDataList.Add(uiCommentData);

            // コメントログを記録
            writeLog(uiCommentData.UserName, uiCommentData.CommentStr);

            // 棒読みちゃんへ送信
            string sendText = comment.Text;
            string bcTitle = twcasChatClient.BcTitle;
            if (bcTitle != "")
            {
                StringBuilder sendTextSb = new StringBuilder(sendText);
                sendTextSb.Replace("(" + bcTitle + ")", "");
                sendText = sendTextSb.ToString();
            }
            bouyomiChan.Talk(sendText);
        }

        /// <summary>
        /// コメントログを記録する
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="commentText"></param>
        private void writeLog(string userName, string commentText)
        {
            string logText = userName + "\t" + commentText;
            System.IO.StreamWriter sw = new System.IO.StreamWriter(
                @"comment.txt",
                true, // append : true
                System.Text.Encoding.GetEncoding("UTF-8"));
            sw.WriteLine(logText);
            sw.Close();
        }

        /// <summary>
        /// コメント受信完了イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        private void twcasChatClient_OnCommentReceiveDone(TwcasChatClient sender)
        {
            // データグリッドを自動スクロール
            dataGridScrollToEnd();
        }

        /// <summary>
        /// データグリッドを自動スクロール
        /// </summary>
        private void dataGridScrollToEnd()
        {
            if (dataGrid.Items.Count > 0)
            {
                var border = VisualTreeHelper.GetChild(dataGrid, 0) as Decorator;
                if (border != null)
                {
                    var scroll = border.Child as ScrollViewer;
                    if (scroll != null) scroll.ScrollToEnd();
                }
            }

        }

        /// <summary>
        /// 動画IDが変更された時のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        private void twcasChatClient_OnMovieIdChanged(TwcasChatClient sender)
        {
            doOpen();
        }

        /// <summary>
        /// ツイキャスボタンがクリックされた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void twcasBtn_Click(object sender, EventArgs e)
        {
            // 既定ブラウザで放送ページを開く
            if (twcasChatClient.BcUrl == "")
            {
                return;
            }
            string url = twcasChatClient.BcUrl;

            // ブラウザを開く
            System.Diagnostics.Process.Start(url);
        }

        /// <summary>
        /// 更新ボタンがクリックされた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void updateBtn_Click(object sender, RoutedEventArgs e)
        {
            doOpen();
        }

        /// <summary>
        /// ライブIDテキストボックスのキーアップイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void channelNameTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                doOpen();
            }
        }

        /// <summary>
        /// フォームのタイトルを設定する
        /// </summary>
        private void setTitle()
        {
            // チャンネル名をGUIに設定
            string channelName = twcasChatClient.ChannelName;
            if (channelName != null && channelName.Length != 0)
            {
                this.Title = channelName + " - " + titleBase;
            }
            else
            {
                this.Title = titleBase;
            }
        }

        /// <summary>
        /// チャンネル情報の初期化
        /// </summary>
        private void initChannelInfo()
        {
            twcasChatClient.InitChannelInfo();

            // タイトルを設定
            setTitle();
        }

        /// <summary>
        /// ページを開く
        /// </summary>
        private void doOpen()
        {

            // タイマーが停止するまで待つ
            twcasChatClient.Stop();

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
            twcasChatClient.ChannelName = channelName;
            // タイトルを設定
            setTitle();

            // チャットをオープンする
            bool ret = twcasChatClient.Start();
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
            bouyomiChan.ClearText();

            ViewModel viewModel = this.DataContext as ViewModel;
            ObservableCollection<UiCommentData> uiCommentDataList = viewModel.UiCommentDataCollection;
            uiCommentDataList.Clear();
        }

        /// <summary>
        /// チャンネル名をGUIから取得する
        /// </summary>
        /// <returns></returns>
        private string getChannelNameFromGui()
        {
            // チャンネル欄にURLが指定されてもOKとする
            string[] tokens = channelNameTextBox.Text.Split('/');
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
    }

}
