
using System;
using System.Collections.Generic;
using System.Text;

namespace Horizon.Plugin.UYA
{
    public class RoboAccount
    {

        public string Username { get; set; }
        public string Password { get; set; }
        public int AppId { get; set; }
        public int[] Stats { get; set; }

        public override string ToString()
        {
            return base.ToString() + " " +
                $"Username:{Username} " +
                $"Password:{Password}";
        }
    }
}
