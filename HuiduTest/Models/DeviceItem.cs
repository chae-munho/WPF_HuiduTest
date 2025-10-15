using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDKLibrary;
namespace HuiduTest.Models
{
    public class DeviceItem
{
    public Device Device { get; }
    public string Display { get; }

    public DeviceItem(Device device)
    {
        Device = device;
        var info = device.GetDeviceInfo();
        Display = $"{info.deviceID} ({info.deviceName})";
    }

    public override string ToString() => Display;
}
}