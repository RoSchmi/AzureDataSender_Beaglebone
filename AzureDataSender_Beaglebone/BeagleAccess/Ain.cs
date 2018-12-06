using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Digithought.BlackNet;


namespace RoSchmi.BlackNet
{
    class Ain : PortBase
    {
        private static readonly Dictionary<BbbPort, string> AinTestMappings =
           new Dictionary<BbbPort, string>
           {
                { BbbPort.P9_33, "in_voltage4_raw" },     // AIN4
                { BbbPort.P9_35, "in_voltage6_raw" },     // AIN6
                { BbbPort.P9_36, "in_voltage5_raw" },     // AIN5
                { BbbPort.P9_37, "in_voltage2_raw" },     // AIN2
                { BbbPort.P9_38, "in_voltage3_raw" },     // AIN3
                { BbbPort.P9_39, "in_voltage0_raw" },     // AIN0
                { BbbPort.P9_40, "in_voltage1_raw" },     // AIN1
           };

        private const string AinPath = "/sys/bus/iio/devices/iio:device0/";       

        /// <summary> Wraps a digital I/O port. </summary>
        /// <param name="autoConfigure"> Whether to automatically configure the port.  Set to true unless you're certain the port is already configured. </param>              
        public Ain(BbbPort port, bool autoConfigure = true) : base(port, autoConfigure)
        {
           
        }

        public bool Available()
        {
            return File.Exists(GetGioDevicePath()) ? true : false;                   
        }

        public double? Read()
        {
            double returnValue;

            if (double.TryParse(ReadFromFile(GetGioDevicePath()), out returnValue))
            {
                return returnValue;
            }
            else
            {
                return null;
            }         
        }

        public override void Configure()
        {
            
        }

        public override void Unconfigure()
        {
           
        }

        private string GetGioDevicePath()
        {          
            return Path.Combine(AinPath + AinTestMappings[Port]);
        }
    }
}

