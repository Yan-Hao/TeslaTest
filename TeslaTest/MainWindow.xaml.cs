using System;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TeslaTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Controller controller = new Controller();

        DispatcherTimer timer;

        // TODO no hardcode
        const int updatesPerSec = 30;
        float dt = 1.0f / updatesPerSec;
        DateTime lastTime;

        Brush wBg;
        Brush aBg;
        Brush sBg;
        Brush dBg;
        Brush spaceBg;

        public MainWindow()
        {
            InitializeComponent();

            // start a game loop
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(dt);
            timer.Tick += Timer_Tick;
            timer.Start();
            lastTime = DateTime.Now;

            controller.OnLog += Controller_OnLog;
            controller.OnDisconnect += Controller_OnDisconnect;

            wBg = wBtn.Background;
            aBg = aBtn.Background;
            sBg = sBtn.Background;
            dBg = sBtn.Background;
            spaceBg = spaceBtn.Background;
        }

        private void Controller_OnDisconnect(object sender, EventArgs e)
        {
            connectBtn.Content = "Connect";
        }

        private void Controller_OnLog(string text)
        {
            consoleTBox.Text += "\n" /*+ DateTime.Now + ": "*/ + text;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            controller.Update(dt);
        }

        private void connectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (controller.IsConnected())
            {
                // disconnect from VRED
                controller.Disconnect();
                connectBtn.Content = "Connect";
            }
            else
            {
                // get URL of VRED web server
                Uri uri = GetVredUrl();
                if (uri == null)
                    return;

                // connect to VRED
                if (controller.Connect(uri))
                    connectBtn.Content = "Disconnect";
                else
                    Controller_OnLog("Connection failed");
            }
        }

        private Uri GetVredUrl()
        {
            // get URL from UI
            string urlText = url.Text;

            // validate URL
            Uri uri;
            if (!Uri.TryCreate(urlText, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttp)
            {
                // URL is invalid, reset it to default value
                MessageBox.Show("'" + urlText + "' is not a valid URL");
                url.Text = ConfigurationManager.AppSettings["DefaultUrl"];
                return null;
            }
            
            return uri;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            controller.UpdateInput(Controller.Input.Forward, false);
            controller.UpdateInput(Controller.Input.Backward, false);
            controller.UpdateInput(Controller.Input.TurnLeft, false);
            controller.UpdateInput(Controller.Input.TurnRight, false);
            controller.UpdateInput(Controller.Input.EBrake, false);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // key to input mapping
            switch (e.Key)
            {
                case Key.W:
                    controller.UpdateInput(Controller.Input.Forward, true);
                    wBtn.Background = Brushes.Orange;
                    break;
                case Key.S:
                    controller.UpdateInput(Controller.Input.Backward, true);
                    sBtn.Background = Brushes.Orange;
                    break;
                case Key.A:
                    controller.UpdateInput(Controller.Input.TurnLeft, true);
                    aBtn.Background = Brushes.Orange;
                    break;
                case Key.D:
                    controller.UpdateInput(Controller.Input.TurnRight, true);
                    dBtn.Background = Brushes.Orange;
                    break;
                case Key.LeftShift:
                    controller.UpdateInput(Controller.Input.EBrake, true);
                    spaceBtn.Background = Brushes.Orange;
                    break;
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            // key to input mapping
            switch (e.Key)
            {
                case Key.W:
                    controller.UpdateInput(Controller.Input.Forward, false);
                    wBtn.Background = wBg;
                    break;
                case Key.S:
                    controller.UpdateInput(Controller.Input.Backward, false);
                    sBtn.Background = sBg;
                    break;
                case Key.A:
                    controller.UpdateInput(Controller.Input.TurnLeft, false);
                    aBtn.Background = aBg;
                    break;
                case Key.D:
                    controller.UpdateInput(Controller.Input.TurnRight, false);
                    dBtn.Background = dBg;
                    break;
                case Key.LeftShift:
                    controller.UpdateInput(Controller.Input.EBrake, false);
                    spaceBtn.Background = spaceBg;
                    break;
            }
        }

        private void clearBtn_Click(object sender, RoutedEventArgs e)
        {
            consoleTBox.Text = "";
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(consoleTBox.Text))
                return;

            using (StreamWriter w = File.AppendText("log.txt"))
            {
                Log(consoleTBox.Text, w);
            }
            consoleTBox.Text = "";
            MessageBox.Show("log saved to log.txt in executable dir");
        }

        private static void Log(string logMessage, TextWriter w)
        {
            w.Write("\r\nLog Entry : ");
            w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                DateTime.Now.ToLongDateString());
            w.WriteLine("  :");
            w.WriteLine("  :{0}", logMessage);
            w.WriteLine("-------------------------------");
        }
    }
}
