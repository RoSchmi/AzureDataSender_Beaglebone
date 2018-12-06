using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureDataSender.Models
{
    public class AnalogValueSet
    {
        int propertyNumber;
        DateTime sampleTime;        
        double measureValue;

        public AnalogValueSet() { }

        public AnalogValueSet(int pPropertyNumber, DateTime pSampleTime, double pMeasureValue)
        {
            propertyNumber = pPropertyNumber;
            sampleTime = pSampleTime;
            measureValue = pMeasureValue;
        }

        public double MeasureValue
        {
            get { return measureValue; }          
        }
        public DateTime SampleTime
        {
            get { return sampleTime; }
        }
        public int PropertyNumber
        { get
            { return propertyNumber; }
        }
    }
}
