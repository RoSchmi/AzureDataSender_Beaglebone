using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using RoSchmi.DayLihtSavingTime;

namespace AzureDataSender
{
    class OnOffDigitalSensorMgr
    {
        private bool input = true;
        private bool InitialInputState = true;

        private InputSensorState actState = InputSensorState.High;
        private InputSensorState oldState = InputSensorState.High;

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

        int timeZoneOffset = 0;       
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
            get { return destinationTable; }
            set { destinationTable = value; }
        }

        Thread ReadSensorThread;

        #region Constructor
        public OnOffDigitalSensorMgr(int pTimeZoneOffset, int pDstOffset, string pDstStart, string pDstEnd, string pDestinationTable = "undef", string pSensorLabel = "undef", bool pInitialInputState = true,  bool pInvertInputPolarity = false, string pSensorLocation = "undef", string pMeasuredQuantity = "undef", string pChannel = "000")
        {
            dstOffset = pDstOffset;
            dstStart = pDstStart;
            dstEnd = pDstEnd;           
            input = pInitialInputState;
            oldState = pInitialInputState ? InputSensorState.High : InputSensorState.Low;

            timeZoneOffset = pTimeZoneOffset;
            sensorLabel = pSensorLabel;
            sensorLocation = pSensorLocation;
            measuredQuantity = pMeasuredQuantity;
            destinationTable = pDestinationTable;
            channel = pChannel;
            DateTime timeZoneCorrectedDateTime = DateTime.Now.AddMinutes(timeZoneOffset);                      
            dateTimeOfLastSend = timeZoneCorrectedDateTime.AddMinutes(GetDlstOffset.Get(timeZoneCorrectedDateTime, pDstStart, pDstEnd, pDstOffset));
            invertPolarity = pInvertInputPolarity;
            _stopped = true;
            ReadSensorThread = new Thread(new ThreadStart(runReadSensorThread));
            ReadSensorThread.Start();
        }
        #endregion

        #region runReadSensorThread
        private void runReadSensorThread()
        {
            DateTime timeZoneCorrectedDateTime; 
            DateTime actDateTime;

            while (true)
            {
                if (!_stopped)
                {
                    if (input ^ invertPolarity == false)
                    {
                        Thread.Sleep(20);         // debouncing
                        if (input ^ invertPolarity == false)
                        {                          
                            if (oldState == InputSensorState.High)
                            {                              
                                timeZoneCorrectedDateTime = DateTime.Now.AddMinutes(timeZoneOffset);                                
                                actDateTime = timeZoneCorrectedDateTime.AddMinutes(GetDlstOffset.Get(timeZoneCorrectedDateTime, dstStart, dstEnd, dstOffset));
                                actState = InputSensorState.Low;
                                TimeSpan timeFromLastSend = actDateTime - dateTimeOfLastSend;
                                OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, actDateTime, timeFromLastSend, onTimeDay, sensorLabel, sensorLocation, measuredQuantity, destinationTable, channel, false));
                                dateTimeOfLastSend = actDateTime;
                                oldState = InputSensorState.Low;
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(20);             // (debouncing)
                        if (input ^ invertPolarity == true)    // input still high                                     
                        {                          
                            if (oldState == InputSensorState.Low)
                            {                               
                                timeZoneCorrectedDateTime = DateTime.Now.AddMinutes(timeZoneOffset);                               
                                actDateTime = timeZoneCorrectedDateTime.AddMinutes(GetDlstOffset.Get(timeZoneCorrectedDateTime, dstStart, dstEnd, dstOffset));
                                actState = InputSensorState.High;
                                TimeSpan timeFromLastSend = actDateTime - dateTimeOfLastSend;
                                onTimeDay += timeFromLastSend;
                                OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, actDateTime, timeFromLastSend, onTimeDay, sensorLabel, sensorLocation, measuredQuantity, destinationTable, channel, false));
                                dateTimeOfLastSend = actDateTime;
                                oldState = InputSensorState.High;
                            }
                        }
                    }

                    // Send an input high event (means: is off) in the last 30 seconds of each day and wait on the next day                                     
                    timeZoneCorrectedDateTime = DateTime.Now.AddMinutes(timeZoneOffset);
                    actDateTime = timeZoneCorrectedDateTime.AddMinutes(GetDlstOffset.Get(timeZoneCorrectedDateTime, dstStart, dstEnd, dstOffset));
                    if (actDateTime.Hour == 23 && actDateTime.Minute == 59 && actDateTime.Second > 30)
                    {
                        actState = InputSensorState.High;
                        TimeSpan timeFromLastSend = actDateTime - dateTimeOfLastSend;
                        onTimeDay += timeFromLastSend;
                        OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, actDateTime, timeFromLastSend, onTimeDay, sensorLabel, sensorLocation, measuredQuantity, destinationTable, channel, true));
                        oldState = InputSensorState.High;

                        // wait on the next day
                        while (actDateTime.Day == DateTime.Now.AddMinutes(timeZoneOffset).AddMinutes(GetDlstOffset.Get(DateTime.Now.AddMinutes(timeZoneOffset), dstStart, dstEnd, dstOffset)).Day)
                        {
                            Thread.Sleep(100);
                        }

                        onTimeDay = new TimeSpan(0, 0, 0);
                    }                   
                    Thread.Sleep(20);   // Read Sensor every 20 ms
                }
                else
                {
                    
                    Thread.Sleep(200);   // When stopped, Read Sensor every 200 ms
                }                
            }
        }
        #endregion

        #region public method Start
        public void Start()
        {
            oldState = InitialInputState ^ invertPolarity ? InputSensorState.High : InputSensorState.Low;
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

        #region Class GetDlstOffset
        public static class GetDlstOffset
        {
            public static int Get(DateTime pDateTime, string pDstStart, string pDstEnd, int pDstOffset)
            {
                return DayLihtSavingTime.DayLightTimeOffset(pDstStart, pDstEnd, pDstOffset, pDateTime, true);
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