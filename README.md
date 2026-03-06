### J2534-Sharp ###

This is now a legacy project.  The current project is now J2534-Sharp.Core in the main/master branch.

J2534-Sharp handles all the details of operating with unmanaged SAE J2534 spec library and lets you deal with the important stuff.

Available on NuGet! [NuGet Gallery: J2534-Sharp]

## Features ##
- No 'Unsafe' code.  All unmanaged memory references are done using the marshaller
- Thread safe design.   Locking is done on API calls to allow concurrent access.
- Simplified API.  Most API calls have had redundant data removed and offer method signatures for common use cases
- Support for v2.02 and v4.04 J2534 standards.  v2.02 libraries are detected and 'shimmed' to a v4.04 interface seamlessly
- Support for v5.00.  v5 J2534 support has been started, but I need more info to complete it.
- Support for DrewTech API.  Support has been included for undocumented DrewTech API calls

## TODO's ##
- Test the FivebaudInit and Fastinit methods.
- Test with an actual v2.02 driver.
- Finish the v5.00 implementation (can anyone send me the spec please??!!)

This project is very active and still somewhat fluid.  Breaking changes may occur and I wont apologize for them.

## Feedback ##
I want to hear from you if you have ideas or thoughts about this library.  Anything from features to critiques are welcome.  Also, I would love to know what kinds of things its being used for!

## Usage Paradigm ##
## Traditional usage ##
The traditional usage will use explicit filter definition and using disposables within using's'.
```csharp
using System;
using System.Linq;
using SAE.J2534;

namespace J5234Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            MessageFilter FlowControlFilter = new MessageFilter()
            {
                FilterType = Filter.FLOW_CONTROL_FILTER,
                Mask = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF },
                Pattern = new byte[] { 0x00, 0x00, 0x07, 0xE8 },
                FlowControl = new byte[] { 0x00, 0x00, 0x07, 0xE0 }
            };

            string DllFileName = APIFactory.GetAPIinfo().First().Filename;

            using (API API = APIFactory.GetAPI(DllFileName))
            using (Device Device = API.GetDevice())
            using (Channel Channel = Device.GetChannel(Protocol.ISO15765, Baud.ISO15765, ConnectFlag.NONE))
            {
                Channel.StartMsgFilter(FlowControlFilter);
                Console.WriteLine($"Voltage is {Channel.MeasureBatteryVoltage() / 1000}");
                Channel.SendMessage(new byte[] { 0x00, 0x00, 0x07, 0xE0, 0x01, 0x00 });
                GetMessageResults Response = Channel.GetMessage();
            }
        }
    }
}
```

## Simplified usage ##
The simplified usage take advantage of built in 'templates' for creating the filters
```csharp
using System;
using System.Linq;
using SAE.J2534;

namespace J5234Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            string DllFileName = APIFactory.GetAPIinfo().First().Filename;

            using (API API = APIFactory.GetAPI(DllFileName))
            using (Device Device = API.GetDevice())
            using (Channel Channel = Device.GetChannel(Protocol.ISO15765, Baud.ISO15765, ConnectFlag.NONE))
            {
                Channel.StartMsgFilter(new MessageFilter(UserFilterType.STANDARDISO15765, new byte[] { 0x00, 0x00, 0x07, 0xE0}));
                Console.WriteLine($"Voltage is {Channel.MeasureBatteryVoltage() / 1000}");
                Channel.SendMessage(new byte[] { 0x00, 0x00, 0x07, 0xE0, 0x01, 0x00 });
                GetMessageResults Response = Channel.GetMessage();
            }
        }
    }
}
```

## Alternate usage of the APIFactory ##
Alternately, the API factory can be instantiated as an instance, and when disposed, will dispose all children with it.  This negates the need for explicit using's'
except for the initial one for the APIFactory.  NOTE:  The APIFactory instance is only used to facilitate the disposal, and the instance does not need to be passed
around.
```csharp
using System;
using System.Linq;
using SAE.J2534;

namespace J5234Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var factory = new APIFactory())
            {
                Run();
            }
        }
        static void Run()
        {
            string DllFileName = APIFactory.GetAPIinfo().First().Filename;

            Channel Channel = APIFactory.GetAPI(DllFileName).GetDevice().GetChannel(Protocol.ISO15765, Baud.ISO15765, ConnectFlag.NONE);

            Channel.StartMsgFilter(new MessageFilter(UserFilterType.STANDARDISO15765, new byte[] { 0x00, 0x00, 0x07, 0xE0 }));
            Console.WriteLine($"Voltage is {Channel.MeasureBatteryVoltage() / 1000}");
            Channel.SendMessage(new byte[] { 0x00, 0x00, 0x07, 0xE0, 0x01, 0x00 });
            GetMessageResults Response = Channel.GetMessage();
        }
    }
}
```
[NuGet Gallery: J2534-Sharp]: http://www.nuget.org/packages/J2534-Sharp