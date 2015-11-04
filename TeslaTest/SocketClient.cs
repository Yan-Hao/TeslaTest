using Microsoft.Win32.SafeHandles;
using System;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace TeslaTest
{

    // converted from VRED examples/external-connection1.py and external-connection-receiver.py
    // use SendVredCmd() and SendVredCmdR() to send commands to VRED via web server, or
    // instantiate the class to load and connect to a receiver in VRED and send commands directly
    class SocketClient : IDisposable
    {

        // Flag: Has Dispose already been called?
        bool disposed = false;
        // Instantiate a SafeHandle instance.
        SafeHandle handle = new SafeFileHandle(IntPtr.Zero, true);

        byte[] bytes = new byte[1024];
        Socket sender;

        public SocketClient(Uri uri, int port)
        {
            // load receiver script and start receiving on VRED via web server
            string receiverScript = Util.GetPathUri(ConfigurationManager.AppSettings["ReceiverScript"]);
            SendVredCmdR(uri.Host, uri.Port, string.Format("load(\"{0}\")", receiverScript));
            SendVredCmd(uri.Host, uri.Port, string.Format("receiver = VRReceiver({0})", port));
            SendVredCmd(uri.Host, uri.Port, @"receiver.setActive(true)");

            // connect to the receiver
            sender = Connect(uri.Host, port);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                // Free any other managed objects here.
                //
            }

            // Free any unmanaged objects here.
            //

            if (sender != null)
            {
                sender.Shutdown(SocketShutdown.Both);
                sender.Close();
                sender = null;
            }

            disposed = true;
        }

        ~SocketClient()
        {
            Dispose(false);
        }

        public void Send(string cmd)
        {
            // append cmd seperator and send
            byte[] msg = Encoding.ASCII.GetBytes(cmd + "|");
            int bytesSent = sender.Send(msg);
        }

        public bool IsConnected()
        {
            return sender.Connected;
        }
        
        private static Socket Connect(string hostName, int port)
        {
            // Establish the remote endpoint for the socket.
            IPHostEntry ipHostInfo = Dns.GetHostEntry(hostName);
            IPAddress ipAddress = Array.Find(ipHostInfo.AddressList, a => a.AddressFamily == AddressFamily.InterNetwork); // get ipv4 for hostname
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP  socket.
            Socket sender = new Socket(remoteEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Connect the socket to the remote endpoint. Catch any errors.
            sender.Connect(remoteEP);
            Console.WriteLine("Socket connected to {0}", sender.RemoteEndPoint.ToString());

            return sender;
        }

        private static void Send(string hostName, int port, string cmd)
        {
            // Data buffer for incoming data.
            byte[] bytes = new byte[1024];

            // Connect to a remote device.

            Socket sender = Connect(hostName, port);

            // Encode the data string into a byte array.
            byte[] msg = Encoding.ASCII.GetBytes(cmd);// Uri.EscapeDataString(cmd));

            // Send the data through the socket.
            int bytesSent = sender.Send(msg);

            // Receive the response from the remote device.
            int bytesRec = sender.Receive(bytes);
            Console.WriteLine("Echoed test = {0}", Encoding.ASCII.GetString(bytes, 0, bytesRec));

            // Release the socket.
            sender.Shutdown(SocketShutdown.Both);
            sender.Close();

        }

        public static void SendVredCmd(string hostName, int port, string cmd)
        {
            string requestString = string.Format("GET /python?value={0} HTTP/1.1\r\n\r\n", Uri.EscapeDataString(cmd));
            Send(hostName, port, requestString);
        }

        public static void SendVredCmdR(string hostName, int port, string cmd)
        {
            string requestString = string.Format("GET /pythoneval?value={0} HTTP/1.1\r\n\r\n", Uri.EscapeDataString(cmd));
            Send(hostName, port, requestString);
        }
    }
}
