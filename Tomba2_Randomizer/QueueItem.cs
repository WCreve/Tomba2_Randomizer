using System;
using System.Collections.Generic;
using System.Text;

namespace Tomba2_Randomizer
{
    public class QueuedChange
    {
        public QueuedChange(int timeStamp, List<AddressValuePair> pairs)
        {
            EnqueueTimeStamp = timeStamp;
            AddressValuePairs = pairs;
        }

        public QueuedChange(int timeStamp, AddressValuePair pair)
        {
            EnqueueTimeStamp = timeStamp;
            AddressValuePairs = new List<AddressValuePair> { pair };
        }

        public QueuedChange(int timeStamp, string text)
        {
            EnqueueTimeStamp = timeStamp;
            Text = text;
        }

        public int EnqueueTimeStamp { get; private set; }

        public int DequeueTimeStamp { get; set; }

        public string Text { get; set; }

        public List<AddressValuePair> AddressValuePairs { get; private set; }
    }
}
