using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceChangeFix
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DEV_BROADCAST_HDR
    {
        public uint dbch_size;
        public uint dbch_devicetype;
        public uint dbch_reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DEV_BROADCAST_DEVICEINTERFACE
    {
        public uint dbcc_size;
        public uint dbcc_devicetype;
        public uint dbcc_reserved;
        public Guid dbcc_classguid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=255)]
        public string dbcc_name;
    }
}
