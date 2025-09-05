using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace MovingObjectClient
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SimpleClientForm());
        }
    }

    public partial class SimpleClientForm : Form
    {
        private Socket clientSocket;
        private PictureBox pictureBox;
        private byte[] buffer = new byte[4096];

        // Image receiving state
        private byte[] receivedImageData = new byte[1024 * 1024];
        private int currentImageSize = 0;
        private int expectedImageSize = 0;
        private int bytesReceived = 0;
        private bool receivingHeader = true;

        public SimpleClientForm()
        {
            InitializeUI();
            ConnectToServer();
        }

        private void InitializeUI()
        {
            this.Size = new Size(600, 400);
            this.Text = "Moving Object Client";
            this.StartPosition = FormStartPosition.CenterScreen;

            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.BackColor = Color.White;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;

            this.Controls.Add(pictureBox);
        }

        private void ConnectToServer()
        {
            try
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1111));
                
                this.Text = "Moving Object Client - Connected";
                BeginReceive();
            }
            catch (Exception ex)
            {
                this.Text = "Moving Object Client - Connection Failed";
                MessageBox.Show("Could not connect to server: " + ex.Message);
            }
        }

        private void BeginReceive()
        {
            try
            {
                clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, OnDataReceived, null);
            }
            catch (Exception ex)
            {
                this.Text = "Moving Object Client - Disconnected";
            }
        }

        private void OnDataReceived(IAsyncResult ar)
        {
            try
            {
                int received = clientSocket.EndReceive(ar);
                
                if (received > 0)
                {
                    ProcessData(buffer, received);
                    BeginReceive();
                }
            }
            catch
            {
                this.Text = "Moving Object Client - Disconnected";
            }
        }

        private void ProcessData(byte[] data, int length)
        {
            int offset = 0;
            
            while (offset < length)
            {
                if (receivingHeader)
                {
                    int headerBytesNeeded = 4 - bytesReceived;
                    int headerBytesAvailable = Math.Min(headerBytesNeeded, length - offset);
                    
                    Array.Copy(data, offset, receivedImageData, bytesReceived, headerBytesAvailable);
                    bytesReceived += headerBytesAvailable;
                    offset += headerBytesAvailable;
                    
                    if (bytesReceived >= 4)
                    {
                        expectedImageSize = BitConverter.ToInt32(receivedImageData, 0);
                        currentImageSize = 0;
                        receivingHeader = false;
                        bytesReceived = 0;
                    }
                }
                else
                {
                    int imageBytesNeeded = expectedImageSize - currentImageSize;
                    int imageBytesAvailable = Math.Min(imageBytesNeeded, length - offset);
                    
                    Array.Copy(data, offset, receivedImageData, currentImageSize, imageBytesAvailable);
                    currentImageSize += imageBytesAvailable;
                    offset += imageBytesAvailable;
                    
                    if (currentImageSize >= expectedImageSize)
                    {
                        DisplayImage();
                        receivingHeader = true;
                        bytesReceived = 0;
                        currentImageSize = 0;
                        expectedImageSize = 0;
                    }
                }
            }
        }

        private void DisplayImage()
        {
            try
            {
                using (var ms = new MemoryStream(receivedImageData, 0, expectedImageSize))
                {
                    var image = Image.FromStream(ms);
                    
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => {
                            if (pictureBox.Image != null) pictureBox.Image.Dispose();
                            pictureBox.Image = image;
                        }));
                    }
                    else
                    {
                        if (pictureBox.Image != null) pictureBox.Image.Dispose();
                        pictureBox.Image = image;
                    }
                }
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                clientSocket?.Close();
            }
            catch { }
            base.OnFormClosing(e);
        }
    }
}