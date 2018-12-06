using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace RoSchmi.BlackNet
{
    class Led
    {
        private const string LedPath = "/sys/class/leds/";
        private const string LedPrefix = "beaglebone:green:";

        private BbbLed _led;

        public Led(BbbLed led)
        {
            _led = led;          
        }

        public int Brightness
        {
            get
            {
                return int.Parse(ReadFromFile(BrightnessFileName()));
            }
            set
            {
                int writeValue = value == 1 ? 1 : 0;
                WriteToFile(BrightnessFileName(), writeValue.ToString());
            }
        }

        private string BrightnessFileName()
        {

            string theReturn = Path.Combine(GetLedDevicePath(), "brightness");
            return Path.Combine(GetLedDevicePath(), "brightness");
        }

        private string GetLedDevicePath()
        {
            string theReturn = Path.Combine(LedPath, LedPrefix + _led.ToString());
            return Path.Combine(LedPath, LedPrefix + _led.ToString());            
        }

        protected static void WriteToFile(string fileName, string value)
        {

            using (var writer = new StreamWriter(new FileStream(fileName, FileMode.Open, FileAccess.Write), Encoding.ASCII))
            {
                writer.Write(value);
            }
        }

        protected static string ReadFromFile(string fileName)
        {        
            var result = File.ReadAllText(fileName, Encoding.ASCII);
            return result;
        }


    }
}
