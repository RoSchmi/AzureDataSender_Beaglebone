using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace AzureDataSender
{
    class OnOffDigitalSensorMgr
    {
        private bool input = false;

        private InputSensorState actState = InputSensorState.Low;
        private InputSensorState oldState = InputSensorState.Low;

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

        DateTime dateTimeOfLastSend;

        bool _stopped = true;

        bool invertPolarity = false;

        TimeSpan onTimeDay = new TimeSpan(0, 0, 0);

        // To mange DayLightSavingsTime (not needed on this platform)
        int dstOffset = 0;
        string dstStart = "";
        string dstEnd = "";

        // Store some attributes of the instances of this class
        string sensorLabel = "";
        string sensorLocation = "";
        string measuredQuantity = "";
        string destinationTable = "";
        string channel = "";

        public string DestinationTable
        {
            get { return destinationTable;}
            set {destinationTable = value;}
        }


        Thread ReadSensorThread;

        #region Constructor
        public OnOffDigitalSensorMgr(int pDstOffset, string pDstStart, string pDstEnd, string pDestinationTable = "undef", bool pInvertInputPolarity = false, string pSensorLabel = "undef", string pSensorLocation = "undef", string pMeasuredQuantity = "undef",  string pChannel = "000")
        {
            dstOffset = pDstOffset;
            dstStart = pDstStart;
            dstEnd = pDstEnd;

            sensorLabel = pSensorLabel;
            sensorLocation = pSensorLocation;
            measuredQuantity = pMeasuredQuantity;
            destinationTable = pDestinationTable;
            channel = pChannel;

            dateTimeOfLastSend = DateTime.Now;

            invertPolarity = pInvertInputPolarity;
            _stopped = true;
            ReadSensorThread = new Thread(runReadSensorThread);
            ReadSensorThread.Start();
        }
        #endregion

        #region runReadSensorThread
        private void runReadSensorThread()
        {
            while (true)
            { 
                if (!_stopped)
                {
                    if (input ^ invertPolarity== false)
                    {
                        Thread.Sleep(20);         // debouncing
                        if (input ^ invertPolarity == false)
                        {
                            if (oldState == InputSensorState.High)
                            {
                                actState = InputSensorState.Low;
                                TimeSpan timeFromLastSend = DateTime.Now - dateTimeOfLastSend;                                
                                OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, DateTime.Now, timeFromLastSend, onTimeDay, sensorLabel, sensorLocation, measuredQuantity, destinationTable, channel, false));
                                dateTimeOfLastSend = DateTime.Now;
                                oldState = InputSensorState.Low;
                            }
                        }
                    }
                    else
                        Thread.Sleep(20);             // (debouncing)
                    if (input ^ invertPolarity == true)    // input still high                                     
                    {
                        if (oldState == InputSensorState.Low)
                        {
                            actState = InputSensorState.High;
                            TimeSpan timeFromLastSend = DateTime.Now - dateTimeOfLastSend;
                            onTimeDay += timeFromLastSend;
                            OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, DateTime.Now, timeFromLastSend, onTimeDay, sensorLabel, sensorLocation, measuredQuantity, destinationTable, channel, false));
                            dateTimeOfLastSend = DateTime.Now;
                            oldState = InputSensorState.High;
                        }
                    }
                    // Send an input high event (means burner is off) in the last 30 seconds of each day and wait on the next day
                    DateTime actTime = DateTime.Now;
                    if (actTime.Hour == 23 && actTime.Minute == 59 && actTime.Second > 30)
                    {
                        actState = InputSensorState.High;
                        TimeSpan timeFromLastSend = DateTime.Now - dateTimeOfLastSend;
                        onTimeDay += timeFromLastSend;
                        OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, DateTime.Now, timeFromLastSend, onTimeDay, sensorLabel, sensorLocation, measuredQuantity, destinationTable, channel, true));
                        oldState = InputSensorState.High;

                        /*
                        // wait on the next minute (for tests)
                        while (actTime.Minute   == DateTime.Now.Minute)
                        {
                            Thread.Sleep(100);
                        }
                        */
                       
                        // wait on the next day
                        while (actTime.Day == DateTime.Today.Day)
                        {
                            Thread.Sleep(100);
                        }
                        
                        onTimeDay = new TimeSpan(0, 0, 0);
                    }

                }
                Thread.Sleep(200);   // Read Sensor every 200 ms
            }
        }
        #endregion

        #region public method Start
        public void Start()
        {
            oldState = input ^ invertPolarity ? InputSensorState.Low : InputSensorState.High;
            _stopped = false;
        }
        #endregion

        #region public method Stop
        public void Stop()
        {
             oldState = input ^ invertPolarity ? InputSensorState.Low : InputSensorState.High;

            _stopped = true;
        }
        #endregion

        #region public Input
        public bool Input
        {
            get
            {
                return input;
            }
            set
            {
                input = value;
            } 
        }
        #endregion

        #region Delegate
        /// <summary>
        /// The delegate that is used to handle the data message.
        /// </summary>
        /// <param name="sender">The <see cref=""/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>

        
        public delegate void digitalOnOffSensorEventhandler(OnOffDigitalSensorMgr sender, OnOffSensorEventArgs e);

        /// <summary>
        /// Raised when the input state has changed
        /// </summary>
        public event digitalOnOffSensorEventhandler digitalOnOffSensorSend;
       
        private digitalOnOffSensorEventhandler onDigitalOnOffSensorSend;
       
        private void OnDigitalOnOffSensorSend(OnOffDigitalSensorMgr sender, OnOffSensorEventArgs e)
        {
            if (this.onDigitalOnOffSensorSend == null)
            {
                this.onDigitalOnOffSensorSend = this.OnDigitalOnOffSensorSend;
            }
            this.digitalOnOffSensorSend(sender, e);
        }
        #endregion

        #region EventArgs
        public class OnOffSensorEventArgs
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
            public TimeSpan TimeFromLastSend
            { get; private set; }


            /// <summary>
            /// OnTimeDay
            /// </summary>
            /// 
            public TimeSpan OnTimeDay
            { get; private set; }


            /// <summary>
            /// SensorLabel
            /// </summary>
            /// 
            public string SensorLabel
            { get; private set; }

            /// <summary>
            /// SensorLocation
            /// </summary>
            /// 
            public string SensorLocation
            { get; private set; }

            /// <summary>
            /// MeasuredQuantity
            /// </summary>
            /// 
            public string MeasuredQuantity
            { get; private set; }

            /// <summary>
            /// DestinationTable
            /// </summary>
            /// 
            public string DestinationTable
            { get; private set; }

            /// <summary>
            /// Channel
            /// </summary>
            /// 
            public string Channel
            { get; private set; }

            /// <summary>
            /// LastOfDay
            /// </summary>
            /// 
            public bool LastOfDay
            { get; private set; }

            /// <summary>
            /// Val_1
            /// </summary>
            /// 
            public string Val_1
            { get; private set; }

            /// <summary>
            /// Val_1
            /// </summary>
            /// 
            public string Val_2
            { get; private set; }


            /// <summary>
            /// Val_1
            /// </summary>
            /// 
            public string Val_3
            { get; private set; }

            // Not always all parameters used in a special App 
            internal OnOffSensorEventArgs(InputSensorState pActState, InputSensorState pOldState, DateTime pTimeStamp, TimeSpan pTimeFromLastSend, TimeSpan pOnTimeDay, string pSensorLabel, string pSensorLocation, string pMeasuredQuantity, string pDestinationTable, string pChannel, bool pLastOfDay, string pVal_1 = "0000", string pVal_2 = "0000", string pVal_3 = "0000")
            {
                this.ActState = pActState == InputSensorState.High ? true : false;
                this.OldState = pOldState == InputSensorState.High ? true : false;
                this.Timestamp = pTimeStamp;
                this.TimeFromLastSend = pTimeFromLastSend;
                this.OnTimeDay = pOnTimeDay;
                this.DestinationTable = pDestinationTable;
                this.MeasuredQuantity = pMeasuredQuantity;
                this.SensorLabel = pSensorLabel;
                this.SensorLocation = pSensorLocation;
                this.Channel = pChannel;
                this.LastOfDay = pLastOfDay;
                this.Val_1 = pVal_1;
                this.Val_2 = pVal_2;
                this.Val_3 = pVal_3;
            }
        }
        #endregion
    }
}
