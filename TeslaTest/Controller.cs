using System;
using System.Net.Sockets;
using System.Windows.Media.Media3D;

namespace TeslaTest
{
    delegate void LogToConsole(string text);
    
    class Controller
    {
        public enum Input { Forward, Backward, TurnLeft, TurnRight, EBrake };
        public event LogToConsole OnLog;
        public event EventHandler OnDisconnect;

        bool[] inputState = new bool[Enum.GetNames(typeof(Input)).Length];
        SocketClient sender = null;
        int port = 8890;

        Car car;
        double wheelRot;

        private void Log(string text)
        {
            if (OnLog != null)
                OnLog(text);
        }

        public bool IsConnected()
        {
            return sender != null;
        }

        public bool Connect(Uri uri)
        {
            try
            {
                // start with a new scene in VRED
                SocketClient.SendVredCmd(uri.Host, uri.Port, @"newScene()");
                SocketClient.SendVredCmd(uri.Host, uri.Port, string.Format("load(\"{0}\")", Util.GetPathUri("Aeromax.vpb")));
                
                sender = new SocketClient(uri, port);

                // set up car
                car = new Car();
                car.mass = 1200;    
                car.inertiaScale = 2.0f;
                car.halfWidth = 0.8f;
                car.cgToFront = 2.0f;
                car.cgToRear = 2.0f;
                car.cgToFrontAxle = 1.25f;
                car.cgToRearAxle = 1.25f;
                car.cgHeight = 0.55f;
                car.wheelRadius = 0.55f;
                car.tireGrip = 2.0f;
                car.lockGrip = 0.7f;
                car.engineForce = 4000.0f;
                car.brakeForce = 12000.0f;
                car.eBrakeForce = car.brakeForce / 2.5f;
                car.weightTransfer = 0.2f;
                car.maxSteer = 40;
                car.cornerStiffnessFront = 5.0f;
                car.cornerStiffnessRear = 5.2f;
                car.airResist = 2.5f;
                car.rollResist = 8.0f;
                car.RecalcStats();

                // set up car
                sender.Send(@"car = findNode(""Alias Shape Rep"")");
                sender.Send(@"front_left_wheel = findNode(""node#166502"")");
                sender.Send(@"front_right_wheel = findNode(""node#167187"")");
                sender.Send(@"rear_left_wheel = findNode(""node#166503"")");
                sender.Send(@"rear_right_wheel = findNode(""node#166504"")");

                //sender.Send("camera_node = getCamNode(0)");

                return true;
            }
            catch (Exception e)
            {
                //Console.WriteLine("Unexpected exception : {0}", e.ToString());
                Log(e.ToString());
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                sender.Dispose();
                sender = null;
                car = null;
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }

            if (OnDisconnect != null)
                OnDisconnect(this, null);
        }

        public void UpdateInput(Input input, bool state)
        {
            if (!IsConnected())
                return;

            if (inputState[(int)input] != state)
            {
                inputState[(int)input] = state;
            }
        }

        private void UpdateInput(float dt)
        {
            car.ebrake = inputState[(int)Input.EBrake] ? 1.0f : 0.0f;

            car.throttle = inputState[(int)Input.Forward] ? 1.0f : 0.0f;
            car.brake = inputState[(int)Input.Forward] ? 0.0f : 1.0f;

            if (inputState[(int)Input.TurnLeft])
                car.Steer(1, dt);
            else if (inputState[(int)Input.TurnRight])
                car.Steer(-1, dt);
            else 
                car.Steer(0, dt);
            
        }
        
        private void UpdateRED()
        {
            try
            {
                const string identity = @"1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1";
                Matrix3D transform = car.transform;

                // update car pos
                sender.Send(string.Format(@"car.setTransformMatrix([{0}], true)", transform.IsIdentity ? identity : transform.ToString()));

                // wheel rotation based on distance traveled 
                var vX = car.v.X;
                wheelRot -= vX / car.wheelRadius;

                // update wheel rot
                sender.Send(string.Format(@"front_left_wheel.setRotation(0,{0},{1})", wheelRot, car.steerAngle));
                sender.Send(string.Format(@"front_right_wheel.setRotation(0,{0},{1})", wheelRot, car.steerAngle));
                sender.Send(string.Format(@"rear_left_wheel.setRotation(0,{0},0)", wheelRot));
                sender.Send(string.Format(@"rear_right_wheel.setRotation(0,{0},0)", wheelRot));
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == 10054)
                {
                    Disconnect(); // handle VRED ending connection
                    Log("Connection terminated by VRED");
                }    
                else
                {
                    Log(e.ToString());
                } 
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
        }

        public void Update(float dt)
        {
            if (!IsConnected())
                return;

            UpdateInput(dt);
            car.Update(dt);
            UpdateRED();
            
        }
    }
}
