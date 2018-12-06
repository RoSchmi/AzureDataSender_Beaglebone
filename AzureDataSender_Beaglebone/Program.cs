﻿// Copyright RoSchmi 2018, License MIT
// Version 1.1.1 06.12.2018
// The C# Wrapper classes to access GPIOs were taken from  https://github.com/Digithought/BlackNet
// This program is an Application for Beaglebone green (should run on -Black or other Beaglebones as well) 
// The App writes Sample Data to Azure Storage Tables.
// The Cloud data are provided in special format, so that they can be graphically visualized with
// the iOS App: Charts4Azure (available in the App Store)
// Created are 5 Tables. Every year new tables are created (TableName + yyyy):
// 1 Table for analog values of 4 sensors (Values must be in the range -40.0 to 140.0, not valid values are epressed as 999.9)
// In the example the values are calculated to display sinus curves
// For a real application you must read analog sensors an take their values (method readAnalogSensors)

// 4 Tables for On/Off values for 1 digital Input each
// In the example in a timer event the input for each table is toggled, where 
// the timer interval for the Off-State is twice the timer interval of the On-State
// In a real application you must toggle the inputs according to the state of e.g. a GPIO pin
// You can send the states repeatedly, only a change of the state has an effect
// At the end of each day this App automatically sends an Entry with an 'Off' state to the cloud


//#define UseTestValues  // if UseTestValues is active test values are transmitted to the cloud, otherwise readings from the analogPorts/digitalPorts

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using TableStorage;
using AzureDataSender.Models;
using System.Globalization;
using AzureDataSender;
using RoSchmi.BlackNet;
using Digithought.BlackNet;

namespace AzureDataSender_Beaglebone
{
    class Program
    {
        //******************  Settings to be changed by user   *********************************************************************

        // Set your Azure Storage Account Credentials here
        static string storageAccount = "roxxxzzz";
        static string storageKey = "WmymeaAfDvPtLcSPdmEeLydIFf3kOqtVeOJBJMhcKR3RYyiB5gD0Slm0kU9sjZQgjnXXK6Ni/U2v513DgAC6Ng==";

        // Set the name of the table for analog values (name must be conform to special rules: see Azure)
        static string analogTable = "AnaRolTable";

        static string analog_Property_1 = "T_1";  // (name must conform to special rules: see Azure)
        static string analog_Property_2 = "T_2";
        static string analog_Property_3 = "T_3";
        static string analog_Property_4 = "T_4";

        // Set parameter for 4 tables for On/Off-values (name must conform to special rules: see Azure)
        // the 4th parameter defines the name of table
        static OnOffDigitalSensorMgr OnOffSensor_01 = new OnOffDigitalSensorMgr(dstOffset, dstStart, dstEnd, "Burner2", false, "OnOffSensor01", "undef", "undef", "undef");
        static OnOffDigitalSensorMgr OnOffSensor_02 = new OnOffDigitalSensorMgr(dstOffset, dstStart, dstEnd, "Boiler2", false, "OnOffSensor02", "undef", "undef", "undef");
        static OnOffDigitalSensorMgr OnOffSensor_03 = new OnOffDigitalSensorMgr(dstOffset, dstStart, dstEnd, "Pump2", false, "OnOffSensor03", "undef", "undef", "undef");
        static OnOffDigitalSensorMgr OnOffSensor_04 = new OnOffDigitalSensorMgr(dstOffset, dstStart, dstEnd, "Heater2", false, "OnOffSensor04", "undef", "undef", "undef");

        // Set intervals (in seconds)
        static int readInterval = 4;            // in this interval analog sensors are read
        static int writeToCloudInterval = 10;   // in this interval the analog data are stored to the cloud
        static int OnOffToggleInterval = 11;    // in this interval the On/Off state is toggled (test values)
        static int invalidateInterval = 900;    // if analog values ar not actualized in this interval, they are set to invalid (999.9)

        //*******************************************************************************************************************************

        //DayLightSavingTimeSettings  (not used in this App)
        // Europe       
        private static int dstOffset = 60; // 1 hour (Europe 2016)
        private static string dstStart = "Mar lastSun @2";
        private static string dstEnd = "Oct lastSun @3";
        /*  USA
        private static int dstOffset = 60; // 1 hour (US 2013)
        private static string dstStart = "Mar Sun>=8"; // 2nd Sunday March (US 2013)
        private static string dstEnd = "Nov Sun>=1"; // 1st Sunday Nov (US 2013)
        */

        // Define Beaglebone 4 analog inputs to read data from the ports
        public static Ain aIn_0 = new Ain(BbbPort.P9_39);   // AIN0 - P9_39
        public static Ain aIn_1 = new Ain(BbbPort.P9_40);   // AIN1 - P9_40
        public static Ain aIn_2 = new Ain(BbbPort.P9_37);   // AIN2 - P9_37
        public static Ain aIn_3 = new Ain(BbbPort.P9_38);   // AIN3 - P9_38

        private static BeagleGpioReader BeagleGpioReader_01 = new BeagleGpioReader(BbbPort.P8_43, "UserButton");


        public static System.Threading.Timer getSensorDataTimer = new System.Threading.Timer(new TimerCallback(getSensorDataTimer_tick), null, readInterval * 1000, Timeout.Infinite);
        public static System.Threading.Timer writeAnalogToCloudTimer = new System.Threading.Timer(new TimerCallback(writeAnalogToCloudTimer_tick), null, writeToCloudInterval * 1000, Timeout.Infinite);
        public static System.Threading.Timer toggleInputTimer = new System.Threading.Timer(new TimerCallback(onOffToggleTimer_tick), null, OnOffToggleInterval * 1000, Timeout.Infinite);

        static DataContainer dataContainer = new DataContainer(new TimeSpan(0, 15, 0));

        static ManualResetEvent manualResetEvent = new ManualResetEvent(false);

        static string connectionString = "DefaultEndpointsProtocol=https;AccountName=" + storageAccount + ";AccountKey=" + storageKey;

        static bool AnalogCloudTableExists = false;

        const double inValidValue = 999.9;

        static List<string> existingOnOffTables = new List<string>();

        #region Main Method
        static void Main(string[] args)
        {          
            OnOffSensor_01.digitalOnOffSensorSend += OnOffSensor_01_digitalOnOffSensorSend;
            OnOffSensor_02.digitalOnOffSensorSend += OnOffSensor_02_digitalOnOffSensorSend;
            OnOffSensor_03.digitalOnOffSensorSend += OnOffSensor_03_digitalOnOffSensorSend;
            OnOffSensor_04.digitalOnOffSensorSend += OnOffSensor_04_digitalOnOffSensorSend;

            OnOffSensor_01.Start();
            OnOffSensor_02.Start();
            OnOffSensor_03.Start();
            OnOffSensor_04.Start();

            BeagleGpioReader_01.gpioStateChanged += BeagleGpioReader_01_gpioStateChanged;

            BeagleGpioReader_01.Start();

            dataContainer.DataInvalidateTime = new TimeSpan(0, 0, invalidateInterval);
                                 
            AnalogCloudTableExists = false;
            
            Console.WriteLine("All commands of Main ready, halted at ManualResetEvent, Thread No.: " + Thread.CurrentThread.ManagedThreadId);
            manualResetEvent = new ManualResetEvent(false);
            manualResetEvent.WaitOne();
            Console.WriteLine("Main is ending in 3 sec, Thread No.: " + Thread.CurrentThread.ManagedThreadId);
            Thread.Sleep(3000);
        }

        private static void BeagleGpioReader_01_gpioStateChanged(BeagleGpioReader sender, BeagleGpioReader.GpioChangedEventArgs e)
        {
            Console.WriteLine("GpioEvent happened");
            OnOffSensor_01.Input = e.ActState;
        }
        #endregion

        // Change this method for a real application
        #region Timer Event onOffToggleTimer_tick
        private static void onOffToggleTimer_tick(object state)
        {
#if UseTestValues
            Console.WriteLine("OnOff toggled");
            // In this example the digital input states are toggled by a timer event
            // In a real appliction the digital input states must be set e.g. according to the state of a GPIO input

            OnOffSensor_01.Input = !OnOffSensor_01.Input;
            OnOffSensor_02.Input = !OnOffSensor_02.Input;
            OnOffSensor_03.Input = !OnOffSensor_03.Input;
            OnOffSensor_04.Input = !OnOffSensor_04.Input;
#endif
            // use different intervals for 'On'- and 'Off' states
            if (OnOffSensor_01.Input == true)
            {
                toggleInputTimer.Change(OnOffToggleInterval * 2 * 1000, Timeout.Infinite);
            }
            else
            {
                toggleInputTimer.Change(OnOffToggleInterval * 1000, Timeout.Infinite);
               
            }           
        }
        #endregion

        // When the timer fires an Entity containing the 4 analog values is stored in the Azure Cloud Table
        #region Timer Event writeAnalogToCloudTimer_tick
        private async static void writeAnalogToCloudTimer_tick(object state)
        {          
            bool validStorageAccount = false;
            CloudStorageAccount storageAccount = null;
            Exception CreateStorageAccountException = null;
            try
            {
                storageAccount = Common.CreateStorageAccountFromConnectionString(connectionString);
                validStorageAccount = true;
            }
            catch (Exception ex0)
            {
                CreateStorageAccountException = ex0;
            }
            if (!validStorageAccount)
            {
                // MessageBox.Show("Storage Account not valid\r\nEnter valid Storage Account and valid Key", "Alert", MessageBoxButton.OK);
                writeAnalogToCloudTimer.Change(writeToCloudInterval * 1000, Timeout.Infinite);
                return;
            }
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();


            // Create analog table if not existing           
            CloudTable cloudTable = tableClient.GetTableReference(analogTable + DateTime.Today.Year);

            if (!AnalogCloudTableExists)
            {
                try
                {
                    await cloudTable.CreateIfNotExistsAsync();
                    AnalogCloudTableExists = true;
                }
                catch
                {
                    Console.WriteLine("Could not create Analog Table with name: \r\n" + cloudTable.Name + "\r\nCheck your Internet Connection.\r\nAction aborted.");

                    writeAnalogToCloudTimer.Change(writeToCloudInterval * 1000, Timeout.Infinite);
                    return;
                }
            }


            // Populate Analog Table with Sinus Curve values for the actual day
            cloudTable = tableClient.GetTableReference(analogTable + DateTime.Today.Year);

            // formatting the PartitionKey this way to have the tables sorted with last added row upmost
            string partitionKey = "Y2_" + DateTime.Today.Year + "-" + (12 - DateTime.Now.Month).ToString("D2");

            DateTime actDate = DateTime.Now;

            // formatting the RowKey (= revereDate) this way to have the tables sorted with last added row upmost
            string reverseDate = (10000 - actDate.Year).ToString("D4") + (12 - actDate.Month).ToString("D2") + (31 - actDate.Day).ToString("D2")
                       + (23 - actDate.Hour).ToString("D2") + (59 - actDate.Minute).ToString("D2") + (59 - actDate.Second).ToString("D2");

            string[] propertyNames = new string[4] { analog_Property_1, analog_Property_2, analog_Property_3, analog_Property_4 };
            Dictionary<string, EntityProperty> entityDictionary = new Dictionary<string, EntityProperty>();
            string sampleTime = actDate.Month.ToString("D2") + "/" + actDate.Day.ToString("D2") + "/" + actDate.Year + " " + actDate.Hour.ToString("D2") + ":" + actDate.Minute.ToString("D2") + ":" + actDate.Second.ToString("D2");
            //string sampleTime = actDate.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

            entityDictionary.Add("SampleTime", EntityProperty.GeneratePropertyForString(sampleTime));
            for (int i = 1; i < 5; i++)
            {
                double measuredValue = dataContainer.GetAnalogValueSet(i).MeasureValue;
                // limit measured values to the allowed range of -40.0 to +140.0, exception: 999.9 (not valid value)
                if ((measuredValue < 999.89) || (measuredValue > 999.91))  // want to be careful with decimal numbers
                {
                    measuredValue = (measuredValue < -40.0) ? -40.0 : (measuredValue > 140.0 ? 140.0 : measuredValue);
                }
                else
                {
                    measuredValue = 999.9;
                }

                entityDictionary.Add(propertyNames[i - 1], EntityProperty.GeneratePropertyForString(measuredValue.ToString("f1", System.Globalization.CultureInfo.InvariantCulture)));
            }
            DynamicTableEntity sendEntity = new DynamicTableEntity(partitionKey, reverseDate, null, entityDictionary);

            DynamicTableEntity dynamicTableEntity = await Common.InsertOrMergeEntityAsync(cloudTable, sendEntity);
          
            writeAnalogToCloudTimer.Change(writeToCloudInterval * 1000, Timeout.Infinite);

            Console.WriteLine("Analog data written to Cloud");
        }
        #endregion

        // When the timer fires, 4 analog inputs are read, the values and a timestamp are stored in the data container
        #region TimerEvent getSensorDataTimer_tick
        private static void getSensorDataTimer_tick(object state)
        {
            DateTime actDate = DateTime.Now;
            dataContainer.SetNewAnalogValue(1, actDate, ReadAnalogSensor(aIn_0));
            dataContainer.SetNewAnalogValue(2, actDate, ReadAnalogSensor(aIn_1));
            dataContainer.SetNewAnalogValue(3, actDate, ReadAnalogSensor(aIn_2));
            dataContainer.SetNewAnalogValue(4, actDate, ReadAnalogSensor(aIn_3));          
            Console.WriteLine("Got Sensor Data");

            getSensorDataTimer.Change(readInterval * 1000, Timeout.Infinite);
        }
        #endregion
        
        
        // Events for 4 digital On/Off Sensors. The events are fired when the input changes its state
        #region Event OnOffSensor_01_digitalOnOffSensorSend
        private static async void OnOffSensor_01_digitalOnOffSensorSend(OnOffDigitalSensorMgr sender, OnOffDigitalSensorMgr.OnOffSensorEventArgs e)
        {
            await WriteOnOffEntityToCloud(e);
        }
        #endregion

        #region Event OnOffSensor_02_digitalOnOffSensorSend
        private static async void OnOffSensor_02_digitalOnOffSensorSend(OnOffDigitalSensorMgr sender, OnOffDigitalSensorMgr.OnOffSensorEventArgs e)
        {
            await WriteOnOffEntityToCloud(e);
        }
        #endregion

        #region Event OnOffSensor_03_digitalOnOffSensorSend
        private static async void OnOffSensor_03_digitalOnOffSensorSend(OnOffDigitalSensorMgr sender, OnOffDigitalSensorMgr.OnOffSensorEventArgs e)
        {
            await WriteOnOffEntityToCloud(e);
        }
        #endregion

        #region Event OnOffSensor_04_digitalOnOffSensorSend
        private static async void OnOffSensor_04_digitalOnOffSensorSend(OnOffDigitalSensorMgr sender, OnOffDigitalSensorMgr.OnOffSensorEventArgs e)
        {
            await WriteOnOffEntityToCloud(e);
        }
        #endregion

        #region ReadAnalogSensors
        private static double ReadAnalogSensor(Ain pAin)
        {
#if !UseTestValues
            // Use values read from the analogInput ports
            if (pAin.Available())
            {
                double? theRead = pAin.Read();  //range 0 - 4095
                return theRead.HasValue ? ((double)theRead / 40.0) : inValidValue;
            }
            else
            {
                return inValidValue;
            }
#else
            // Only as an example we here return values which draw a sinus curve
            int frequDeterminer = 4;
            int y_offset = 1;
            // different frequency and y_offset for aIn_0 to aIn_3
            if (pAin == aIn_0)
            { frequDeterminer = 4; y_offset = 1; }
            if (pAin == aIn_1)
            { frequDeterminer = 8; y_offset = 10; }
            if (pAin == aIn_2)
            { frequDeterminer = 12; y_offset = 20; }
            if (pAin == aIn_3)
            { frequDeterminer = 16; y_offset = 30; }

            int secondsOnDayElapsed = DateTime.Now.Second + DateTime.Now.Minute * 60 + DateTime.Now.Hour * 60 * 60;
            return Math.Round(2.5f * (double)Math.Sin(Math.PI / 2.0 + (secondsOnDayElapsed * ((frequDeterminer * Math.PI) / (double)86400))), y_offset);
#endif
        }
        #endregion
       
        #region Task WriteOnOffEntityToCloud
        static async Task WriteOnOffEntityToCloud(OnOffDigitalSensorMgr.OnOffSensorEventArgs e)
        {

            Console.WriteLine("WriteOnOff to Cloud Common-method Thread-Id: " + e.DestinationTable + " "+ Thread.CurrentThread.ManagedThreadId.ToString());

            bool validStorageAccount = false;
            CloudStorageAccount storageAccount = null;
            Exception CreateStorageAccountException = null;
            try
            {
                storageAccount = Common.CreateStorageAccountFromConnectionString(connectionString);
                validStorageAccount = true;
            }
            catch (Exception ex0)
            {
                CreateStorageAccountException = ex0;
            }
            if (!validStorageAccount)
            {
                return;
            }
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable cloudTable = tableClient.GetTableReference(e.DestinationTable + DateTime.Today.Year);

            if (!existingOnOffTables.Contains(cloudTable.Name))
            {
                try
                {
                    await cloudTable.CreateIfNotExistsAsync();
                    existingOnOffTables.Add(cloudTable.Name);
                }
                catch (Exception exc)
                {
                    Console.WriteLine("Could not create On/Off Table with name: \r\n" + cloudTable.Name + "\r\nCheck your Internet Connection.\r\nAction aborted.");
                    return;
                }
            }

            // formatting the PartitionKey this way to have the tables sorted with last added row upmost
            string partitionKey = "Y3_" + DateTime.Today.Year + "-" + (12 - DateTime.Now.Month).ToString("D2");

            DateTime actDate = DateTime.Now;
            // formatting the RowKey this way to have the tables sorted with last added row upmost
            string rowKey = (10000 - actDate.Year).ToString("D4") + (12 - actDate.Month).ToString("D2") + (31 - actDate.Day).ToString("D2")
                       + (23 - actDate.Hour).ToString("D2") + (59 - actDate.Minute).ToString("D2") + (59 - actDate.Second).ToString("D2");


            //string sampleTime = actDate.Month.ToString("D2") + "/" + actDate.Day.ToString("D2") + "/" + actDate.Year + " " + actDate.Hour.ToString("D2") + ":" + actDate.Minute.ToString("D2") + ":" + actDate.Second.ToString("D2");
            string sampleTime = actDate.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            TimeSpan tflSend = e.TimeFromLastSend;
            string timeFromLastSendAsString = tflSend.Days.ToString("D3") + "-" + tflSend.Hours.ToString("D2") + ":" + tflSend.Minutes.ToString("D2") + ":" + tflSend.Seconds.ToString("D2");

            string onTimeDayAsString = e.OnTimeDay.ToString(@"ddd\-hh\:mm\:ss", CultureInfo.InvariantCulture);

            Dictionary<string, EntityProperty> entityDictionary = new Dictionary<string, EntityProperty>();
            entityDictionary.Add("SampleTime", EntityProperty.GeneratePropertyForString(sampleTime));
            entityDictionary.Add("ActStatus", EntityProperty.GeneratePropertyForString(e.ActState ? "Off" : "On"));
            entityDictionary.Add("LastStatus", EntityProperty.GeneratePropertyForString(e.OldState ? "Off" : "On"));
            entityDictionary.Add("TimeFromLast", EntityProperty.GeneratePropertyForString(timeFromLastSendAsString));
            entityDictionary.Add("OnTimeDay", EntityProperty.GeneratePropertyForString(onTimeDayAsString));

            DynamicTableEntity dynamicTableEntity = await Common.InsertOrMergeEntityAsync(cloudTable, new DynamicTableEntity(partitionKey, rowKey, null, entityDictionary));
        }
#endregion
    }
}
