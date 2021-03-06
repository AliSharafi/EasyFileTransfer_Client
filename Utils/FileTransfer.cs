﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyFileTransfer.Utils;

namespace EasyFileTransfer
{
    public class FileTransfer
    {
        #region Constants
        const int _portReceive = 2346;
        const int _portSend = 2345;
        #endregion

        #region fields
        Thread _listenThread;
        public CircularProgressBar.CircularProgressBar pb;
        int _flag = 0;
        string _receivedPath;
        public delegate void ReceiveDelegate();
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        #endregion

        public FileTransfer(bool listen)
        {
            
            if (listen)
            {
                _listenThread = new Thread(new ThreadStart(StartListening));
                _listenThread.Start();
            }
        }

        #region Send File
        public void Send(string FilePath)
        {
            string _fName = string.Concat(Path.GetFileName(FilePath), "■", AppConfigs.Load().DomainUsername); // append username to filename 
            Socket clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            byte[] fileName = Encoding.UTF8.GetBytes(_fName); //file name
            byte[] fileData = File.ReadAllBytes(FilePath); //file
            byte[] fileNameLen = BitConverter.GetBytes(fileName.Length); //lenght of file name

            byte[] m_clientData = new byte[4 + fileName.Length + fileData.Length];

            fileNameLen.CopyTo(m_clientData, 0);
            fileName.CopyTo(m_clientData, 4);
            fileData.CopyTo(m_clientData, 4 + fileName.Length);

            clientSock.Connect(AppConfigs.Load().ServerIP, _portSend); //target machine's ip address and the port number
            clientSock.Send(m_clientData);
            clientSock.Close();
        }
        #endregion

        #region Receive file
        private void StartListening()
        {
            //byte[] bytes = new Byte[1024];
            IPEndPoint ipEnd = new IPEndPoint(IPAddress.Any, _portReceive);
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(ipEnd);
                listener.Listen(32);
                while (true)
                {
                    allDone.Reset();
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    allDone.WaitOne();
                }
            }
            catch (Exception ex)
            {
            }
        }
        public void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
            _flag = 0;
        }
        public void ReadCallback(IAsyncResult ar)
        {
            pb.Invoke(new ReceiveDelegate(pbShow));

            int fileNameLen = 1;
            String content = String.Empty;
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;
            int bytesRead = handler.EndReceive(ar);
            if (bytesRead > 0)
            {
                if (_flag == 0)
                {
                    fileNameLen = BitConverter.ToInt32(state.buffer, 0);
                    string fileName = Encoding.UTF8.GetString(state.buffer, 4, fileNameLen);
                    _receivedPath = AppConfigs.Load().SavePath + "\\" + fileName;
                    _flag++;
                }
                if (_flag >= 1)
                {
                    BinaryWriter writer = new BinaryWriter(File.Open(_receivedPath , FileMode.Create));
                    if (_flag == 1)
                    {
                        writer.Write(state.buffer, 4 + fileNameLen, bytesRead - (4 + fileNameLen));
                        _flag++;
                    }
                    else
                        writer.Write(state.buffer, 0, bytesRead);
                    writer.Close();
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
            }
            else
            {
                pb.Invoke(new ReceiveDelegate(pbHide));
            }
        }
        public void pbShow()
        {
            pb.Visible = true;
        }
        public void pbHide()
        {
            pb.Visible = false;
        }
        #endregion

        public void Stop()
        {
            _listenThread.Abort();
        }
    }

    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;

        public const int BufferSize = 1024 * 1024;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
    }
}
