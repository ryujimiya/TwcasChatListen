using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO; // File
using System.Threading;
using System.Text.RegularExpressions; //RegEx
using System.Windows.Forms;
using FNF.Utility; // BouyomiChanClient

namespace MyUtilLib
{
    // NOTE: 棒読みちゃん本体で下記設定を行ってください。
    //  (1) 配信者向け機能を有効にする
    //  (2) システム - 基本 02)読み上げ関連 - 02)行間の待機時間 を0に設定する
    class BouyomiChan : IDisposable
    {
        //////////////////////////////////////////////////////////////
        // 型
        //////////////////////////////////////////////////////////////
        private class TalkInfo
        {
            private string serif;
            public string Serif
            {
                get { return serif; }
            }
            public TalkInfo(string p_serif)
            {
                serif = p_serif;
            }
        }

        //////////////////////////////////////////////////////////////
        // 定数
        //////////////////////////////////////////////////////////////
        /// <summary>
        /// 速度最大値
        /// </summary>
        private const int MAX_SPEED   = 300;
        /// <summary>
        /// このライブラリの加速機能で使用する、加算速度[単位: 1/残件数]
        /// </summary>
        private const int SPEED_DELTA = 10;

        //////////////////////////////////////////////////////////////
        // 変数
        //////////////////////////////////////////////////////////////
        /// <summary>
        /// 破棄された？
        /// </summary>
        private bool disposed = false;
        /// <summary>
        /// 音声再生スレッド
        /// </summary>
        private Thread soundThread = null;
        /// <summary>
        /// スレッドを終了中?
        /// </summary>
        private bool terminating = false;
        /// <summary>
        /// 音声情報のキュー 
        /// </summary>
        private Queue<TalkInfo> serifQueue = new Queue<TalkInfo>();
       /// <summary>
       /// キュー排他用シグナル
       /// </summary>
        private AutoResetEvent queueLock = new AutoResetEvent(true);
        /// <summary>
        /// スレッド排他用シグナル
        /// </summary>
        private AutoResetEvent threadLock = new AutoResetEvent(false);
        /// <summary>
        /// 音量
        /// </summary>
        private int volume = -1 ;
        /// <summary>
        /// 棒読みちゃんクライアント
        /// </summary>
        private BouyomiChanClient bouyomiChanClient = null;
        /// <summary>
        /// 棒読みちゃんに未送信のキューをクリアする?
        /// </summary>
        private bool clearQueueFlag = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public BouyomiChan()
        {
            init();
        }
        
        /// <summary>
        /// ファイナライザ
        /// </summary>
        ~BouyomiChan()
        {
            Dispose(false);
        }

        /// <summary>
        /// 初期化
        /// </summary>
        private void init()
        {
            if (disposed)
            {
                return;
            }

            // 棒読みちゃんクライアント
            bouyomiChanClient = new BouyomiChanClient();
            Thread t = new Thread(new ParameterizedThreadStart(this.soundRun));
            t.Name = "BouyomiChan soundThread";
            t.Start();
            this.soundThread = t;
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
                if (this.soundThread != null)
                {
                    this.terminating = true;
                    if (this.soundThread.IsAlive)
                    {
                        this.queueLock.Set();
                        this.threadLock.Set();
                        this.soundThread.Join();
                    }
                    this.soundThread = null;
                    this.terminating = false;
                }

                // 棒読みちゃんクライアント破棄処理
                if (bouyomiChanClient != null)
                {
                    bouyomiChanClient.Dispose();
                    bouyomiChanClient = null;
                }

                disposed = true;
            }
        }

        /// <summary>
        /// 未実行の読み上げテキストをクリアする
        /// </summary>
        public void ClearText()
        {
            if (soundThread == null || !soundThread.IsAlive)
            {
                return;
            }

            this.queueLock.WaitOne();
            this.serifQueue.Clear();
            this.queueLock.Set();
            try
            {
                bouyomiChanClient.ClearTalkTasks();
            }
            //catch (System.Runtime.Remoting.RemotingException exception)
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
            }
            
            this.clearQueueFlag = true;
        }

        /// <summary>
        /// 指定されたテキストの音声を出力する
        /// （スレッドにタスクをキューイングする)
        /// </summary>
        /// <param name="text">テキスト</param>
        /// <returns></returns>
        public bool Talk(string text)
        {
            if (soundThread == null || !soundThread.IsAlive)
            {
                return false;
            }

            this.queueLock.WaitOne();
            System.Diagnostics.Debug.WriteLine("BouyomiChan::Talk:" + text);
            this.serifQueue.Enqueue(new TalkInfo(text));
            this.queueLock.Set();
            // 音声出力スレッドを起動する
            this.threadLock.Set();

            return true;
        }

        ////////////////////////////////////////////////////////////////////////
        // 音声出力スレッド
        ////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 音声出力スレッド
        /// </summary>
        /// <param name="param"></param>
        private void soundRun(object param)
        {
            Queue<TalkInfo> queue = new Queue<TalkInfo>();  //  ローカルキュー
            try
            {
                while (true)
                {
                    // 起されるまで待つ
                    // 他のスレッドからシグナルONにセットされて動く(動き出したらシグナルは自動でOFF)
                    this.threadLock.WaitOne();
                    if (this.terminating)
                    {
                        break;
                    }
                    // キュー排他制御開始 : ロックが解除される(シグナルがONになる)まで待つ
                    // 他のスレッドからシグナルONにセットされて動く(動き出したらシグナルは自動でOFF)
                    this.queueLock.WaitOne();
                    if (this.terminating)
                    {
                        break;
                    }
                    // 一旦キューからすべて取り出してローカルのキューに入れる
                    while (this.serifQueue.Count > 0)
                    {
                        queue.Enqueue(serifQueue.Dequeue());
                    }
                    // キュー排他制御終了:シグナルをONにする
                    this.queueLock.Set();
                    
                    ///////////////////////////////////////////////////////
                    // 以下このスレッド内部の処理
                    int queueCnt = queue.Count;  // ローカルキューの残り件数

                    while (queue.Count > 0)
                    {
                        // ローカルキューから取出し
                        TalkInfo talkInfo = queue.Dequeue();
                        if (!this.clearQueueFlag)
                        {
                            this.talkText(talkInfo.Serif);
                        }
                        if (this.terminating)
                        {
                            break;
                        }
                    }
                    try
                    {
                        if (this.clearQueueFlag)
                        {
                            bouyomiChanClient.ClearTalkTasks();
                        }
                    }
                    //catch (System.Runtime.Remoting.RemotingException exception)
                    catch (Exception exception)
                    {
                        //MyUtil.PRINT(exception.Message);
                        System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
                    }
                    if (this.terminating)
                    {
                        break;
                    }
                    this.clearQueueFlag = false;
                }
            }
            catch (ThreadAbortException exception)
            {
                Thread.ResetAbort();
                System.Diagnostics.Debug.WriteLine("BouyomiChan soundRun aborted");
                System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
            }

            System.Diagnostics.Debug.WriteLine("BouyomiChan soundRun end.");
        }

        /// <summary>
        /// 指定されたテキストの音声を出力する(ブロックする)
        /// </summary>
        /// <param name="text">テキスト</param>
        private void talkText(string text)
        {
            if (soundThread == null || !soundThread.IsAlive)
            {
                return;
            }

            int tone = -1;
            int speed = -1;
            VoiceType voiceType = VoiceType.Default;
            try
            {
                // 棒読みちゃん本体へタスク追加
                bouyomiChanClient.AddTalkTask(text, speed, tone, volume, voiceType);

                /*
                // 音声再生完了まで待つ
                //   棒読みちゃん本体のタスク数監視し、残り0になったら抜ける
                {
                    int taskCount = 1 ;
                    while (taskCount > 0)
                    {
                        Thread.Sleep(0);
                        taskCount = bouyomiChanClient.TalkTaskCount;
                        if (this.terminating)
                        {
                            break;
                        }
                    }
                }
                 */
            }
            //catch (System.Runtime.Remoting.RemotingException exception)
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.Message + " " + exception.StackTrace);
            }
        }
    }
}
