using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Digithought.BlackNet;

namespace AzureDataSender_Beaglebone
{
    public class BeagleGpioReader
    {
        private bool invertPolarity = false;
        
        private InputSensorState oldState = InputSensorState.High;
        private InputSensorState actState = InputSensorState.High;
        private bool isStopped = true;
        private BbbPort port;
        DateTime dateTimeOfLastAction = DateTime.MinValue;

        Gpio gpio;

        string label = "undef";

        public enum InputSensorState
        {
            /// <summary>
            /// The state of InputSensor is low.
            /// </summary>
            Low = 0,
            /// <summary>
            /// The state of InputSensor is high.
            /// </summary>
            High = 1
        }

        public BeagleGpioReader(BbbPort pPort, string pLabel = "undef", bool pInitialState = true, bool pInvertPolarity = false)
        {
            port = pPort;
            label = pLabel;
            oldState = pInitialState ? InputSensorState.High : InputSensorState.Low;
            invertPolarity = pInvertPolarity;
            gpio = new Gpio(BbbPort.P8_43, true, true);
        }
        public void Start()
        {
            isStopped = false;
            Thread GpioReaderThread = new Thread(new ThreadStart(RunGpioReaderThread));
            GpioReaderThread.Start();
        }

        public void Stop()
        {
            isStopped = true;
        }

        private void RunGpioReaderThread()
        {
            Console.WriteLine("Reached RunGpioReaderThread");
            while (true)
            {
                if (!isStopped)
                {

                    if (gpio.Value == 1 ^ invertPolarity == false)
                    {
                        //Console.WriteLine("Reached = false");
                        Thread.Sleep(20);         // debouncing
                        if (gpio.Value == 1 ^ invertPolarity == false)
                        {
                            if (oldState == InputSensorState.High)
                            {
                                actState = InputSensorState.Low;
                                TimeSpan timeFromLastSend = DateTime.Now - dateTimeOfLastAction;
                                OnGpioStateChanged(this, new GpioChangedEventArgs(actState, oldState, label, DateTime.Now, timeFromLastSend, gpio));
                                dateTimeOfLastAction = DateTime.Now;
                                oldState = InputSensorState.Low;
                            }
                        }
                    }
                    else
                    {
                        //Console.WriteLine("Reached = true");
                        Thread.Sleep(20);             // (debouncing)
                        if (gpio.Value == 1 ^ invertPolarity == true)    // input still high                                     
                        {
                            if (oldState == InputSensorState.Low)
                            {
                                actState = InputSensorState.High;
                                TimeSpan timeFromLastSend = DateTime.Now - dateTimeOfLastAction;
                                OnGpioStateChanged(this, new GpioChangedEventArgs(actState, oldState, label, DateTime.Now, timeFromLastSend, gpio));
                                dateTimeOfLastAction = DateTime.Now;
                                oldState = InputSensorState.High;
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
        #region Delegate

        
        /// <summary>
        /// The delegate that is used to handle the Gpio change state event.
        /// </summary>
        /// <param name="sender">The <see cref=""/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>

        public delegate void gpioStateChangedEventhandler(BeagleGpioReader sender, GpioChangedEventArgs e);
        /// <summary>
        /// Raised when the input state has changed
        /// </summary>
        /// 

        public event gpioStateChangedEventhandler gpioStateChanged;

        private gpioStateChangedEventhandler onGpioStateChanged;



        private void OnGpioStateChanged(BeagleGpioReader sender, GpioChangedEventArgs e)
        {
            if (this.onGpioStateChanged == null)
            {
                this.onGpioStateChanged = this.OnGpioStateChanged;
            }
            this.gpioStateChanged(sender, e);

        }

        #endregion

        #region EventArgs
        public class GpioChangedEventArgs
        {
            /// <summary>
            /// State of the message
            /// </summary>
            /// 
            public bool ActState
            { get; private set; }

            /// <summary>
            /// Former State of the message
            /// </summary>
            /// 
            public bool OldState
            { get; private set; }


            /// <summary>
            /// Timestamp
            /// </summary>
            /// 
            public DateTime Timestamp
            { get; private set; }


            /// <summary>
            /// TimeFromLastSend
            /// </summary>
            /// 
            public TimeSpan TimeFromLastAction
            { get; private set; }


            /// <summary>
            /// SensorLabel
            /// </summary>
            /// 
            public string Label
            { get; private set; }

            

            // Not always all parameters used in a special App 
            internal GpioChangedEventArgs(InputSensorState pActState, InputSensorState pOldState, string pLabel, DateTime pTimeStamp, TimeSpan pTimeFromLastAction, Gpio pGpio)
            {
                this.ActState = pActState == InputSensorState.High ? true : false;
                this.OldState = pOldState == InputSensorState.High ? true : false;
                this.Timestamp = pTimeStamp;
                this.Label = pLabel;
                this.TimeFromLastAction = pTimeFromLastAction;               
            }
        }
        #endregion
    }
}
