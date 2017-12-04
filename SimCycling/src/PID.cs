using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimCycling
{
    public class PID
    {
        public float Kp { get; set; }
        public float Ki { get; set; }
        public float Kd { get; set; }

        public float SampleTime { get; set; }
        public float WindupGuard { get; set; }

        public float Output { get; private set; }

        long currentTime;
        long lastTime;

        float PTerm;
        float ITerm;
        float DTerm;

        float lastError;
        // float int_error;



        public float SetPoint { get; set; }

        public PID(float P = 0.2f, float I = 0.0f, float D = 0.0f)
        {

            Kp = P;
            Ki = I;
            Kd = D;

            SampleTime = 0.00f;
            currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            lastTime = currentTime;

            Clear();
        }

        public void Clear()
        {
            //"""Clears PID computations and coefficients"""
            SetPoint = 0.0f;

            PTerm = 0.0f;
            ITerm = 0.0f;
            DTerm = 0.0f;
            lastError = 0.0f;

            //# Windup Guard
            //int_error = 0.0f;
            WindupGuard = 20.0f;

            Output = 0.0f;
        }

        public void Update(float feedbackValue)
        {
            /*"""Calculates PID value for given reference feedback

                    .. math::
                        u(t) = K_p e(t) + K_i \int_{ 0}^{t
                } e(t)dt + K_d {de
            }/{dt}

                    .. figure::images/pid_1.png
                       :align:   center

                       Test PID with Kp=1.2, Ki=1, Kd=0.001 (test_pid.py)

                    """*/
            var error = SetPoint - feedbackValue;

            currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var deltaTime = currentTime - lastTime;
            var deltaError = error - lastError;

            if (deltaTime >= SampleTime)
            {
                PTerm = Kp * error;
                ITerm += error * deltaTime;

                if (ITerm < -WindupGuard)
                {
                    ITerm = -WindupGuard;
                }
                else if (ITerm > WindupGuard)
                {
                    ITerm = WindupGuard;
                }

                DTerm = 0.0f;
                if (deltaTime > 0)
                {
                    DTerm = deltaError / deltaTime;
                }

                //# Remember last time and last error for next calculation
                lastTime = currentTime;
                lastError = error;

                Output = PTerm + (Ki * ITerm) + (Kd * DTerm);
            }
        }
    }
}
