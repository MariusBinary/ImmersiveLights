using System;
using System.Diagnostics;
using System.IO.Ports;

namespace ImmersiveLights.Core
{
    class SafeSerialPort : SerialPort
    {
        public new void Open()
        {
            if (!base.IsOpen)
            {
                base.Open();
                GC.SuppressFinalize(this.BaseStream);
            }
        }

        public new void Close()
        {
            if (base.IsOpen)
            {
                GC.ReRegisterForFinalize(this.BaseStream);
                base.Close();
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
