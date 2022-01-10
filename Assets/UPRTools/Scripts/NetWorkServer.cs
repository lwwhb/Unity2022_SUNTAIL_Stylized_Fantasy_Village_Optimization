using UnityEngine;
using UPRProfiler.Mail;
#if UNITY_2018_2_OR_NEWER
using UPRProfiler.OverdrawMonitor;
#endif

namespace UPRProfiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using UnityWebSocket;

    
    
#if UNITY_2018_2_OR_NEWER
    using Unity.Collections;

    public class UPRMessage
    {
        public int type;
        public NativeArray<byte> nativeRawBytes;
        public byte[] rawBytes;
        public int width;
        public int height;
        public string cameraName;
    }
#else
    public class UPRMessage
    {
        public int type;
        public byte[] rawBytes;
        public int width;
        public int height;
    }
#endif
    public enum DataType { Screenshot, PSS, Device, Customized, OverdrawScreenshot, GPUMali, CustomizedAction };
    public static class NetworkServer
    {
        private static readonly byte[] MagicTag = { 26, 8, 110, 123, 187, 39, 7, 0 };
        private static Queue<UPRMessage> m_sampleQueue = new Queue<UPRMessage>(256);
        private  static NetworkStream ns;
        private  static BinaryWriter bw;
        private  static BinaryReader br;
        public static bool screenFlag = false;
        public static bool isConnected = false;
        public static bool enableScreenShot = false;
        public static bool enableGPUProfiler = false;
        public static bool sendDeviceInfo = false;
        private static TcpClient m_client = null;
        private static string host = "0.0.0.0";
        private static Thread m_sendThread;
        private static Thread m_receiveThread;
        private static int screenShotFrequency = 1;
        private static int listenPort = 56000;
        private static byte[] dataType = new byte[2];
        private static JPGEncoder jpegEncoder;
        private static WebSocket _socket;
        private static bool linkFlag = false; 
        private static bool keepListen = true;
        private static bool keepreceive = true;
        private static bool keepsend = true;
        private static TcpListener _tcpListener;
        private static readonly byte[] OverdrawScreenshotMagicTag = {6, 52, 4, 197, 214, 65, 19, 36};
        #region public
        public static void ConnectTcpPort(int port)
        {
            
#if !UNITY_WEBGL
            listenPort = GetAvailablePort(port, 100, "tcp");
            Thread listenThead = new Thread(new ThreadStart(StartListening));
            listenThead.Start();
#else
            WebSocket(port + 1);
#endif
        }

        private static void WebSocket(int port)
        {
            try
            {
                // 创建实例
                string address = "ws://127.0.0.1:" + port;
                _socket = new WebSocket(address);

                // 注册回调
                _socket.OnOpen += Socket_OnOpen;
                _socket.OnClose += Socket_OnClose;
                _socket.OnMessage += Socket_OnMessage;
                _socket.OnError += Socket_OnError;
                
                _socket.ConnectAsync();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void StartListening()
        {
            if (m_client != null) return;

            keepListen = true;
            IPAddress myIP = IPAddress.Parse(host);
            _tcpListener = new TcpListener(myIP, listenPort);
            _tcpListener.Start();
           // Debug.LogError("Listening Package IP: " + host + " on port: " + listenPort);

            while (keepListen)
            {
                try
                {
                    m_client = _tcpListener.AcceptTcpClient();

                    if (_tcpListener == null)
                        return;
                    
                    if (m_client != null)
                    {
                        //Debug.LogError("<color=#00ff00>Package Connect Success</color>");
                        m_client.Client.SendTimeout = 30000;

                        ns = m_client.GetStream();
                        bw = new BinaryWriter(ns);
                        br = new BinaryReader(ns);

                        if (m_sendThread == null)
                        {
                            m_sendThread = new Thread(new ThreadStart(DoSendMessage));
                            m_sendThread.Priority = ThreadPriority.Highest;
                            m_sendThread.Start();
                        }
                        
                        if (m_receiveThread == null)
                        {
                            m_receiveThread = new Thread(new ThreadStart(DoReceiveMessage));
                            m_receiveThread.Priority = ThreadPriority.Lowest;
                            m_receiveThread.Start();
                        }

                        // break;
                    }

                }
                catch (Exception e)
                {
                    Debug.Log("Listening error:" + e);
                    Close();
                }
                Thread.Sleep(1000);
            }
        }
        public static void Close()
        {
            try
            {
                isConnected = false;
                enableScreenShot = false;
                linkFlag = false;
                keepreceive = false;
                keepsend = false;
                enableGPUProfiler = false;
                
#if UNITY_2018_2_OR_NEWER
                UPROverdrawMonitor.Enabled = false;
                UPRCameraOverdrawMonitor.EnableOverdrawScreenshot = false;
#endif
                
                if (m_client != null)
                {
                    if (m_client.Connected)
                    {
                        m_client.Close();
                    }
                    m_client = null;
                }
                
                lock (m_sampleQueue)
                {
                    m_sampleQueue.Clear();
                }

                m_receiveThread = null;
                m_sendThread = null;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private static void SaveAbortThread(Thread thread)
        {
            try
            {
                thread.Abort();
            }
            catch (Exception)
            {
                // ignored
            }
        }
        
        public static void SendMessage(UPRMessage sample)
        {
            if (m_client == null && _socket == null)
                return;

            lock (m_sampleQueue)
            {
                m_sampleQueue.Enqueue(sample);
            }
        }

        public static void SendMessage(byte[] rawBytes, int type, int width, int height)
        {
            
#if UNITY_WEBGL
            if (_socket == null || !linkFlag)
                return;
            switch (type)
            {
                case 0:
                    jpegEncoder.doEncoding(rawBytes, width, height);
                    byte[] image = jpegEncoder.GetBytes();
                    PackAndSendWebGL(image, (int)DataType.Screenshot);
                    screenFlag = false;
                    break;
                case 1:
                    PackAndSendWebGL(rawBytes, (int)DataType.PSS);
                    break;
                case 2:
                    PackAndSendWebGL(rawBytes, (int)DataType.Device);
                    break;
            }      
#else            
            UPRMessage sample = new UPRMessage
            {
                rawBytes = rawBytes,
                type = type,
                width = width,
                height = height
            };
            if (m_client == null)
                return;
            lock (m_sampleQueue)
            {
                m_sampleQueue.Enqueue(sample);
            }
#endif
        }
        
#if UNITY_2018_2_OR_NEWER
        public static void SendMessage(NativeArray<byte> nativeRawBytes, int type, int width, int height) //only used for image
        {
            
#if UNITY_WEBGL
            if (_socket == null || !linkFlag)
                return;
            jpegEncoder.doNativeEncoding(nativeRawBytes, width, height);   
            byte[] image = jpegEncoder.GetBytes();
            PackAndSendWebGL(image, (int)DataType.Screenshot);
            screenFlag = false;
#else            
            if (m_client == null)
                return;
            UPRMessage sample = new UPRMessage
            {
                nativeRawBytes = nativeRawBytes,
                type = type,
                width = width,
                height = height
            };
            lock (m_sampleQueue)
            {
                m_sampleQueue.Enqueue(sample);
            }
#endif
        }
        
        public static void SendOverdrawScreenshot(byte[] jpgBytes, string cameraName)
        {
            if (m_client == null)
                return;
            
            UPRMessage sample = new UPRMessage
            {
                rawBytes = jpgBytes,
                type = (int)DataType.OverdrawScreenshot,
                cameraName = cameraName
            };
            lock (m_sampleQueue)
            {
                m_sampleQueue.Enqueue(sample);
            }
        }
#endif
#endregion
        private static int GetAvailablePort(int beginPort, int maxIter, string type)
        {
            int availablePort = beginPort;
            for (int port = beginPort; port < beginPort + maxIter; port++)
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    socket.Bind(ep);
                    socket.Close();
                    availablePort = port;
                    break;
                    //Port available
                }
                catch (SocketException)
                {
                    Debug.LogError("Port not available " + port.ToString());
                }

            }
            return availablePort;
        }
 
        private static void DoSendMessage()
        {
            keepsend = true;
            while (keepsend)
            {
                try
                {
                    if (m_sendThread == null)
                    {
                        Debug.Log("<color=#ff0000>Package m_sendThread null</color>");
                        return;
                    }
                    if (m_sampleQueue.Count > 0)
                    {
                        while (m_sampleQueue.Count > 0)
                        {
                            UPRMessage s = null;
                            lock (m_sampleQueue)
                            {
                                s = m_sampleQueue.Dequeue();
                            }
                            switch (s.type)
                            {
                                case (int)DataType.Screenshot:
                                    
#if UNITY_2018_2_OR_NEWER
                                    jpegEncoder.doNativeEncoding(s.nativeRawBytes, s.width, s.height);
#else
                                    jpegEncoder.doEncoding(s.rawBytes, s.width, s.height);
#endif

                                    byte[] image = jpegEncoder.GetBytes();
                                    screenFlag = false;
                                    PackAndSend(image, (int)DataType.Screenshot);
                                    break;
                                case (int)DataType.PSS:
                                    PackAndSend(s.rawBytes, (int)DataType.PSS);
                                    break;
                                case (int)DataType.Device:
                                    PackAndSend(s.rawBytes, (int)DataType.Device);
                                    break;
                                case (int)DataType.Customized:
                                    PackAndSend(s.rawBytes, (int)DataType.Customized);
                                    break;
                                case (int)DataType.OverdrawScreenshot:
                                    PackAndSendOverdrawScreenshot(s.rawBytes, Encoding.UTF8.GetBytes(s.cameraName));
                                    break;
                                case (int)DataType.GPUMali:
                                    PackAndSend(s.rawBytes, (int)DataType.GPUMali);
                                    break;
                                case (int)DataType.CustomizedAction:
                                    PackAndSend(s.rawBytes, (int)DataType.CustomizedAction);
                                    break;
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
                catch (ThreadAbortException e)
                {
                    Debug.Log(e);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    Close();
                }
            }

        }

        private static void DoReceiveMessage()
        {
            string resultMess;
            keepreceive = true;
            while (keepreceive)
            {
                try
                {
                    if (m_receiveThread == null)
                    {
                        Debug.Log("<color=#ff0000>Package m_receiveThread null</color>");
                        return;
                    }
                    if (ns.CanRead && ns.DataAvailable)
                    {
                        resultMess = ParseMessage(br);
                        if (!DealReceivedMessage(resultMess))
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
                Thread.Sleep(1000);
            }
        }

        private static string ParseMessage(BinaryReader binaryReader)
        {
            int position = 0;
            string result = "";
            while (position < MagicTag.Length)
            {
                byte tmpByte = binaryReader.ReadByte();
                if (tmpByte == MagicTag[position])
                {
                    position++;
                }
                else if (tmpByte == MagicTag[0])
                {
                    position = 1;
                }
                else
                {
                    position = 0;
                }
            }
            try
            {
                int len = br.ReadInt32();
                byte[] data = br.ReadBytes(len);
                result = Encoding.UTF8.GetString(data, 0, len);
            }
            catch (Exception e)
            {
                Debug.Log("ParseMessage Error " + e);
            }
            return result;
        }

        private static void PackAndSend(byte[] bytes, int type)
        {
            bw.Write(MagicTag);

            dataType[1] = (byte)((type >> 8) & 0xFF);
            dataType[0] = (byte)(type & 0xFF);
            bw.Write(dataType);

            byte[] dataLen = BitConverter.GetBytes(bytes.Length);
            bw.Write(dataLen);
            bw.Write(bytes);
        }
        
        private static void PackAndSendOverdrawScreenshot(byte[] bytes, byte[] cameraNameBytes)
        {
            bw.Write(MagicTag);

            dataType[1] = (0 >> 8) & 0xFF;
            dataType[0] = 0 & 0xFF;
            bw.Write(dataType);

            // This has been converted to bigEndian by desktop
            byte[] dataLen = BitConverter.GetBytes(8 + 4 + cameraNameBytes.Length + bytes.Length);
            bw.Write(dataLen);
            
            bw.Write(OverdrawScreenshotMagicTag);

            byte[] nameLen = BitConverter.GetBytes(cameraNameBytes.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(nameLen);
            }
            
            bw.Write(nameLen);
            bw.Write(cameraNameBytes);
            
            bw.Write(bytes);
        }
        
        private static void PackAndSendWebGL(byte[] bytes, int type)
        {
            if (_socket == null)
                return;
            _socket.SendAsync(MagicTag);

            dataType[1] = (byte)((type >> 8) & 0xFF);
            dataType[0] = (byte)(type & 0xFF);
            _socket.SendAsync(dataType);
            byte[] dataLen = BitConverter.GetBytes(bytes.Length);
            _socket.SendAsync(dataLen);
            _socket.SendAsync(bytes);
        }

        private static bool DealReceivedMessage(string msg)
        {
            if (msg.Contains("Start Sending Message"))
            {
                sendDeviceInfo = false;
                isConnected = true;
            }
            else if (msg.Contains("Stop Sending Message") && isConnected)
            {
                isConnected = false;
                Close();
                return false;
            }
            else if (msg.Contains("Screen"))
            {
                string[] sess = msg.Split(':');
                if (sess.Length == 3)
                {
                    enableScreenShot = Convert.ToBoolean(sess[1]);
                    screenShotFrequency = Convert.ToInt32(sess[2]) > 3? Convert.ToInt32(sess[2]) : 3;
                    InnerPackageS.waitOneSeconds = new WaitForSeconds(screenShotFrequency);
                    jpegEncoder = new JPGEncoder(20);
                }
            }
#if UNITY_2018_2_OR_NEWER
            else if (msg.Contains("OverdrawMonitor"))
            {
                string[] sess = msg.Split(':');
                if (sess.Length == 4)
                {
                    bool enableOverdrawMonitor = Convert.ToBoolean(sess[1]);
                    if (enableOverdrawMonitor)
                    {
                        UPRCameraOverdrawMonitor.MonitorFrequency = Convert.ToInt32(sess[2]) < 30 ? 30 : Convert.ToInt32(sess[2]);
                        UPROverdrawMonitor.Cleaned = false;
                        UPROverdrawMonitor.NotSupportedFlagSent = false;
                        UPRCameraOverdrawMonitor.EnableOverdrawScreenshot = Convert.ToBoolean(sess[3]);
                        UPROverdrawMonitor.Enabled = true;
                    }
                }
            }
#endif
            else if (msg.Contains("GpuProfileEnabled"))
            {
                string[] sess = msg.Split(':');
                if (sess.Length == 2)
                {
                    enableGPUProfiler = Convert.ToBoolean(sess[1]);
                    HWCPipe.Sample();
                }
            }
            return true;
        }
        
        private static void Socket_OnOpen(object sender, OpenEventArgs e)
        {
            linkFlag = true;
        }

        private static void Socket_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.IsBinary)
            {
                DealReceivedMessage(Encoding.UTF8.GetString(e.RawData, 0, e.RawData.Length));
            }
            else if (e.IsText)
            {
                DealReceivedMessage(e.Data);
            }
        }

        private static void Socket_OnClose(object sender, CloseEventArgs e)
        {
            //Debug.Log(string.Format("Closed, StatusCode: {0}, Reason: {1}\n", e.StatusCode, e.Reason));
            Close();
        }

        private static void Socket_OnError(object sender, UnityWebSocket.ErrorEventArgs e)
        {
           //Debug.Log(string.Format("Error: {0}\n", e.Message));
        }
    }
}


