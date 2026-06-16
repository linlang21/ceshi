using Frida;

Console.WriteLine("FridaCLR probe");
try
{
    using var manager = new DeviceManager();
    var devices = manager.EnumerateDevices();
    Console.WriteLine("Devices: " + devices.Length);
    foreach (var device in devices)
    {
        Console.WriteLine($"{device.Id} | {device.Name} | {device.Type}");
        try
        {
            var processes = device.EnumerateProcesses();
            Console.WriteLine("  Processes: " + processes.Length);
            foreach (var process in processes.Take(10))
            {
                Console.WriteLine($"  PID={process.Pid} Name={process.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Enumerate failed: " + ex.Message);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}
