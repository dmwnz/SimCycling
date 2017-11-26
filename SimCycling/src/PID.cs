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

        public float sample_time { get; set; }
        public float windup_guard { get; set; }

        public float output { get; private set; }

        long current_time;
        long last_time;

        float PTerm;
        float ITerm;
        float DTerm;

        float last_error;
        // float int_error;



        public float SetPoint { get; set; }

        public PID(float P = 0.2f, float I = 0.0f, float D = 0.0f)
        {

            Kp = P;
            Ki = I;
            Kd = D;

            sample_time = 0.00f;
            current_time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            last_time = current_time;

            clear();
        }

        public void clear()
        {
            //"""Clears PID computations and coefficients"""
            SetPoint = 0.0f;

            PTerm = 0.0f;
            ITerm = 0.0f;
            DTerm = 0.0f;
            last_error = 0.0f;

            //# Windup Guard
            //int_error = 0.0f;
            windup_guard = 20.0f;

            output = 0.0f;
        }

        public void update(float feedback_value)
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
            var error = SetPoint - feedback_value;

            current_time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var delta_time = current_time - last_time;
            var delta_error = error - last_error;

            if (delta_time >= sample_time)
            {
                PTerm = Kp * error;
                ITerm += error * delta_time;

                if (ITerm < -windup_guard)
                {
                    ITerm = -windup_guard;
                }
                else if (ITerm > windup_guard)
                {
                    ITerm = windup_guard;
                }

                DTerm = 0.0f;
                if (delta_time > 0)
                {
                    DTerm = delta_error / delta_time;
                }

                //# Remember last time and last error for next calculation
                last_time = current_time;
                last_error = error;

                output = PTerm + (Ki * ITerm) + (Kd * DTerm);
            }
        }
    }
}
