using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Drawing.Imaging;

namespace MovingObjectServer
{
    public partial class Form1 : Form
    {
        // Graphics objects for moving object
        Pen red = new Pen(Color.Red);
        Rectangle rect = new Rectangle(20, 20, 30, 30);
        SolidBrush fillBlue = new SolidBrush(Color.Blue);
        int slide = 10;

        private Socket serverSocket;
        private List<Socket> clientSockets = new List<Socket>();
        private const int PORT = 1111;
        private const int BUFFER_SIZE = 1024;

        public Form1()
        {
            InitializeComponent();
            timer1.Interval = 60;
            timer1.Enabled = true;
            
            InitializeServer();
        }

        private void InitializeServer()
        {
            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
                serverSocket.Listen(10);
                serverSocket.BeginAccept(OnClientConnect, null);
            }
            catch (Exception ex)
            {
                UpdateStatus("Server error: " + ex.Message);
            }
        }

        private void OnClientConnect(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = serverSocket.EndAccept(ar);
                
                lock (clientSockets)
                {
                    clientSockets.Add(clientSocket);
                }
                
                UpdateStatus($"Client connected: {clientSocket.RemoteEndPoint}. Total clients: {clientSockets.Count}");
                serverSocket.BeginAccept(OnClientConnect, null);
            }
            catch (Exception ex)
            {
                UpdateStatus("Error accepting client: " + ex.Message);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            back();
            rect.X += slide;
            Invalidate();
            CaptureAndSendFrame();
        }

        private void CaptureAndSendFrame()
        {
            try
            {
                Bitmap frame = new Bitmap(this.Width, this.Height);
                using (Graphics g = Graphics.FromImage(frame))
                {
                    g.Clear(this.BackColor);
                    g.DrawRectangle(red, rect);
                    g.FillRectangle(fillBlue, rect);
                }
                byte[] imageData = BitmapToByteArray(frame);
                frame.Dispose();
                BroadcastFrame(imageData);
            }
            catch (Exception ex)
            {
                UpdateStatus("Error capturing frame: " + ex.Message);
            }
        }

        private byte[] BitmapToByteArray(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private void BroadcastFrame(byte[] imageData)
        {
            List<Socket> disconnectedClients = new List<Socket>();

            lock (clientSockets)
            {
                foreach (Socket client in clientSockets)
                {
                    try
                    {
                        if (client.Connected)
                        {
                            byte[] sizeData = BitConverter.GetBytes(imageData.Length);
                            client.BeginSend(sizeData, 0, sizeData.Length, SocketFlags.None, OnSendComplete, client);
                            client.BeginSend(imageData, 0, imageData.Length, SocketFlags.None, OnSendComplete, client);
                        }
                        else
                        {
                            disconnectedClients.Add(client);
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error sending to client {client.RemoteEndPoint}: {ex.Message}");
                        disconnectedClients.Add(client);
                    }
                }
                foreach (Socket client in disconnectedClients)
                {
                    clientSockets.Remove(client);
                    try
                    {
                        client.Close();
                    }
                    catch { }
                }

                if (disconnectedClients.Count > 0)
                {
                    UpdateStatus($"Removed {disconnectedClients.Count} disconnected clients. Active clients: {clientSockets.Count}");
                }
            }
        }

        private void OnSendComplete(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                int bytesSent = client.EndSend(ar);
            }
            catch (Exception ex)
            {
                UpdateStatus("Send complete error: " + ex.Message);
            }
        }

        private void back()
        {
            if (rect.X >= this.Width - rect.Width * 2)
                slide = -10;
            else if (rect.X <= rect.Width / 2)
                slide = 10;
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawRectangle(red, rect);
            g.FillRectangle(fillBlue, rect);
        }

        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateStatus), message);
                return;
            }
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                lock (clientSockets)
                {
                    foreach (Socket client in clientSockets)
                    {
                        try
                        {
                            client.Shutdown(SocketShutdown.Both);
                            client.Close();
                        }
                        catch { }
                    }
                    clientSockets.Clear();
                }
                
                if (serverSocket != null)
                {
                    serverSocket.Close();
                }
            }
            catch { }
            
            base.OnFormClosing(e);
        }
    }
}