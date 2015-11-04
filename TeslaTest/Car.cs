
using System;
using System.Windows.Media.Media3D;

namespace TeslaTest
{
    class Car
    {
        const float GRAVITY = 9.81f;                // m/s^2

        public float mass { get; set; }             // kg
        public float inertiaScale { get; set; }     // Multiply by mass for inertia
        public float halfWidth { get; set; }        // Centre to side of chassis (metres)
        public float cgToFront { get; set; }        // Centre of gravity to front of chassis (metres)
        public float cgToRear { get; set; }         // Centre of gravity to rear of chassis
        public float cgToFrontAxle { get; set; }    // Centre gravity to front axle
        public float cgToRearAxle { set; get; }     // Centre gravity to rear axle
        public float cgHeight { set; get; }         // Centre gravity height
        public float wheelRadius { set; get; }      // Includes tire (also represents height of axle)
        public float tireGrip { set; get; }         // How much grip tires have
        public float lockGrip { set; get; }         // % of grip available when wheel is locked
        public float engineForce { get; set; }
        public float brakeForce { get; set; }
        public float eBrakeForce { get; set; }
        public float weightTransfer { get; set; }   // How much weight is transferred during acceleration/braking
        public float maxSteer { get; set; }         // degrees
        public float cornerStiffnessFront { get; set; }
        public float cornerStiffnessRear { get; set; }
        public float airResist { get; set; }        // air resistance (* vel)
        public float rollResist { get; set; }
        
        //  Other static values to be computed from config
        private float inertia = 0.0f;
        private float wheelBase = 0.0f;
        private float axleWeightRatioFront = 0.0f;  // % car weight on the front axle
        private float axleWeightRatioRear = 0.0f;   // % car weight on the rear axle
        private float yawRate = 0.0f;               // angular velocity in radians
        private float speed = 0.0f;

        //  Car state variables
        public float throttle { get; set; }
        public float brake { get; set; }
        public float ebrake { get; set; }
        public float steerAngle { get; private set; }   // front wheel angle (-maxSteer..maxSteer)
        private float steer = 0.0f;                     // (-1..1)
        private float heading = 0.0f;

        // world
        public Vector3D p_w { get; set; }
        public Vector3D v_w { get; set; }
        public Vector3D a_w { get; set; }
        
        // local
        public Vector3D v { get; set; }
        public Vector3D a { get; set; }

        public Matrix3D transform
        {
            get
            {
                Matrix3D transform = Matrix3D.Identity;
                transform.Rotate(new Quaternion(new Vector3D(0, 0, 1), heading * 180 / Math.PI + 180)); // add 180 rotation to reconsiliate model's forward direction pointing at -x
                transform.Translate(p_w * 1000); // meter to milimeter
                return transform;
            }
        }

        public Car()
        {
            v_w = new Vector3D();
        }

        public void RecalcStats()
        {
            inertia = mass * inertiaScale;
            wheelBase = cgToFrontAxle + cgToRearAxle;
            axleWeightRatioFront = cgToRearAxle / wheelBase;
            axleWeightRatioRear = cgToFrontAxle / wheelBase;
        }

        public void Update(float dt)
        {
            var sn = Math.Sin(heading);
            var cs = Math.Cos(heading);

            v = new Vector3D(cs * v_w.X + sn * v_w.Y, cs * v_w.Y - sn * v_w.X, 0);

            // Weight on axles based on centre of gravity and weight shift due to forward/reverse acceleration
            var axleWeightFront = mass * (axleWeightRatioFront * GRAVITY - weightTransfer * a.X * cgHeight / wheelBase);
            var axleWeightRear = mass * (axleWeightRatioRear * GRAVITY + weightTransfer * a.X * cgHeight / wheelBase);

            // Resulting velocity of the wheels as result of the yaw rate of the car body.
            // v = yawrate * r where r is distance from axle to CG and yawRate (angular velocity) in rad/s.
            var yawSpeedFront = cgToFrontAxle * yawRate;
            var yawSpeedRear = -cgToRearAxle * yawRate;

            // Calculate slip angles for front and rear wheels (a.k.a. alpha)
            var slipAngleFront = Math.Atan2(v.Y + yawSpeedFront, Math.Abs(v.X)) - Math.Sign(v.X) * steerAngle; // TODO x dir
            var slipAngleRear = Math.Atan2(v.Y + yawSpeedRear, Math.Abs(v.X));

            var tireGripFront = tireGrip;
            var tireGripRear =tireGrip * (1.0 - ebrake * (1.0 - lockGrip)); // reduce rear grip when ebrake is on

            var frictionForceFront_cy = Util.Clamp(-cornerStiffnessFront * slipAngleFront, -tireGripFront, tireGripFront) * axleWeightFront;
            var frictionForceRear_cy = Util.Clamp(-cornerStiffnessRear * slipAngleRear, -tireGripRear, tireGripRear) * axleWeightRear;

            //  Get amount of brake/throttle from our inputs
            var brake = Math.Min(this.brake * brakeForce + ebrake * eBrakeForce, brakeForce);
            var throttle = this.throttle * engineForce;

            //  Resulting force in local car coordinates.
            //  This is implemented as a RWD car only.
            var tractionForce_cx = throttle - brake * Math.Sign(v.X);
            var tractionForce_cy = 0;

            var dragForce_cx = -rollResist * v.X - airResist * v.X * Math.Abs(v.X);
            var dragForce_cy = -rollResist * v.Y - airResist * v.Y * Math.Abs(v.Y);

            // total force in car coordinates
            var totalForce_cx = dragForce_cx + tractionForce_cx;
            var totalForce_cy = dragForce_cy + tractionForce_cy + Math.Cos(steerAngle) * frictionForceFront_cy + frictionForceRear_cy;

            // acceleration along car axes
            a = new Vector3D(totalForce_cx / mass, totalForce_cy / mass, 0);

            // acceleration in world coordinates
            a_w = new Vector3D(cs * a.X - sn * a.Y, sn * a.X + cs * a.Y, 0);
            
            // update velocity
            v_w += a_w * dt;

            speed = (float)v_w.Length;

            // calculate rotational forces
            var angularTorque = (frictionForceFront_cy + tractionForce_cy) * cgToFrontAxle - frictionForceRear_cy * cgToRearAxle;

            //  Sim gets unstable at very slow speeds, so just stop the car
            if (Math.Abs(speed) < 0.5f && throttle == 0.0f)
            {
                speed = 0;
                v_w = new Vector3D();
                angularTorque = yawRate = 0;
            }

            var angularAccel = angularTorque / inertia;

            yawRate += (float)angularAccel * dt;
            
            heading += yawRate * dt;

            //  finally we can update position
            p_w += v_w * dt;// * 1000; // m to mm

        }

        public void Steer(float steerInput, float dt)
        {
            // apply smooth steer
            if (steerInput != 0)
            {
                // move towards steering input
                steer = (steer + steerInput * dt * 2).Clamp(-1, 1);
            }
            else
            {
                // no input, re-center the sterring
                if (steer > 0)
                {
                    steer = Math.Max(steer - dt * 1, 0);
                }
                else if (steer < 0)
                {
                    steer = Math.Min(steer + dt * 1, 0);
                }
            }

            // apply safe steer
            var avel = Math.Min(speed, 250.0f);  // m/s
            steer = steer * (1.0f - (avel / 280.0f));

            steerAngle = steer * maxSteer;
        }


    }
}
