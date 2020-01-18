using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using LitJson;

public class ThreadDownloadList : MonoBehaviour
{
    public static ThreadDownloadList instance;

    private static string rootpath
    {
        get
        {
            string _rootpath = Path.Combine(Application.persistentDataPath, Application.platform.ToString());
            return _rootpath;
        }
    }
    private Thread thread;

    [SerializeField] Button m_downloadButton;
    [SerializeField] Slider m_singleSlider;
    [SerializeField] Slider m_totalSlider;
    [SerializeField] Text m_singleText;
    [SerializeField] Text m_totalText;

    public int index = 0; //只能通过按钮启动
    private int singlePercent;
    public List<string> urlList;
    public List<string> pathList;

    #region Inner Methods

    void Awake()
    {
        instance = this;

        Debug.unityLogger.logEnabled = true;
        
        m_downloadButton.onClick.AddListener(StartDownload);
    }

    void OnDestroy()
    {
        m_downloadButton.onClick.RemoveListener(StartDownload);

        if (thread != null)
        {
            thread.Abort();
            thread = null;
        }
    }

    void Start()
    {
        // 模拟创建下载列表
        string patch = Resources.Load<TextAsset>("patch").text;
        var array = JsonMapper.ToObject<PatchClass[]>(patch);
        Debug.LogFormat("共{0}个资源", array.Length);

        urlList = new List<string>();
        for (int i = 0; i < array.Length; i++)
        {
            urlList.Add(array[i].url);
        }

        pathList = new List<string>();
        for (int i = 0; i < urlList.Count; i++)
        {
            string path = Url2Path(urlList[i]);
            pathList.Add(path);
        }

        //逐条检查：(文件是否存在)-> (md5)->下载
    }

    void Update()
    {
        m_singleText.text = string.Format("{0}%", singlePercent);
        m_totalText.text = string.Format("{0}/{1}", index, urlList.Count);
        m_singleSlider.value = singlePercent / 100f;
        m_totalSlider.value = (float)index / (float)urlList.Count;
    }

    #endregion

    #region 按钮监听

    void StartDownload()
    {
        if (!Directory.Exists(rootpath))
            Directory.CreateDirectory(rootpath);

        index = 0;

        DownloadNext();
    }

    void ShowLogin() 
    {
        Debug.Log("更新完成后登陆");
    }

    #endregion

    #region 下载监听

    void DownloadNext()
    {
        if (index >= urlList.Count)
        {
            if (thread != null)
            {
                thread.Abort();
                thread = null;
            }
            Debug.LogFormat("<color=green>全部下载完成，文件数：{0}</color>", index);

            ShowLogin();

            return;
        }

        Debug.Log("开始下载：" + index);
        MyThread mt = new MyThread(urlList[index], pathList[index], OnProgressChanged, OnCompleted);
        thread = new Thread(new ThreadStart(mt.DownLoadFile));
        thread.Start();
    }

    void OnProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        singlePercent = e.ProgressPercentage;
        //string progress = string.Format("正在下载文件，完成进度{0}%  {1}/{2}(字节)", singlePercent, e.BytesReceived, e.TotalBytesToReceive);
    }

    void OnCompleted(object sender, AsyncCompletedEventArgs e)
    {
        //string log = string.Format("主线程接收回调 OnCompleted " + e.UserState);
        //Debug.Log(log);
        index++;

        if (thread != null)
        {
            thread.Abort();
            thread = null;
        }

        DownloadNext();
    }

    #endregion

    #region 工具类

    // 获取下载文件的大小
    public static long GetLength(string url)
    {
        Debug.Log(url);

        //HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
        HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
        request.Method = "HEAD";

        //如果是发送HTTPS请求
        if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
        {
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
            request.ProtocolVersion = HttpVersion.Version10;
        }

        HttpWebResponse response = request.GetResponse() as HttpWebResponse;
        return response.ContentLength;
    }

    private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
    {
        return true; //总是接受
    }

    private static string Url2Path(string url) 
    {
        string[] array = url.Split('/');
        string filename = array[array.Length - 1];
        string filepath = Path.Combine(rootpath, filename);
        //Debug.LogFormat("<color=green>{0}</color>", filepath);
        return filepath;
    }

    private static string Path2Url(string path)
    {
        return "";
    }

    #endregion
}

public class MyThread
{
    public string _url;
    public string _filePath;
    public float _progress { get; private set; } //下载进度
    public bool _isDone { get; private set; } //是否下载完成
    public Action<object, DownloadProgressChangedEventArgs> _onProgressChanged;
    public Action<object, AsyncCompletedEventArgs> _onFileComplete;

    public MyThread(string url, string filePath, Action<object, DownloadProgressChangedEventArgs> progress, Action<object, AsyncCompletedEventArgs> complete)
    {
        _url = url;
        _filePath = filePath;
        _onProgressChanged = progress;
        _onFileComplete = complete;
    }

    public void DownLoadFile()
    {
        if (File.Exists(_filePath))
        {
            Debug.LogWarning("文件已存在");
            //return; //这里会终止，要保证下载列表文件全部不存在
            File.Delete(_filePath);
        }

        //Debug.Log("DownLoadFile：" + _filePath);

        WebClient webClient = new WebClient();
        webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(_onProgressChanged);
        webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(_onFileComplete);

        //webClient.DownloadFile(_url, _filePath); //同步
        Uri _uri = new Uri(_url);
        webClient.DownloadFileAsync(_uri, _filePath); //异步
    }

    // 流写入本地
    private static byte[] SaveBytes(string filepath)
    {
        using (FileStream fs = new FileStream(filepath, FileMode.OpenOrCreate))
        {
            byte[] bytes = new byte[(int)fs.Length];
            int read = fs.Read(bytes, 0, bytes.Length);

            fs.Dispose();
            fs.Close();

            return bytes;
        }
    }
}

public class PatchClass 
{
    public string url;
}
