using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA
{
    public class Payload
    {
        public uint Address { get; set; }
        public byte[] Data { get; set; }

        public Payload()
        {
            Address = 0;
            Data = new byte[0];
        }

        public Payload(uint address, byte[] data)
        {
            Address = address;
            Data = data;
        }
    }
}
