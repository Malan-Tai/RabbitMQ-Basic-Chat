using System.Runtime.InteropServices;

struct Message
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string sender;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
    public string target;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 280)]
    public string message;
}