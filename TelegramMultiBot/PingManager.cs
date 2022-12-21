using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot
{
    internal class PingManager
    {
        private bool InternetIsUp { get; set; }
        private bool run = true;

        public PingManager()
        {
            InternetIsUp = false;
        }

        public delegate void InternetStatusEventHandler(DateTime date, bool isInternetUp);
        public event InternetStatusEventHandler InternetUp;
        public event InternetStatusEventHandler InternetDown;

        public void Run()
        {
            Ping ping = new Ping();

            do
            {
                var r = ping.Send("google.com", 10000);
                if (r.Status != IPStatus.Success)
                {
                    if (InternetIsUp)
                    {
                        InternetDown?.Invoke(DateTime.Now, false);
                    }
                    InternetIsUp = false;
                }
                else
                {
                    if (!InternetIsUp)
                    {
                        InternetUp?.Invoke(DateTime.Now, true);
                    }
                    InternetIsUp = true;
                }
                
                Thread.Sleep(10000);
            } while (run);
        }

        internal void Abort()
        {
            run= false;
        }
    }
}
