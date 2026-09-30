#nullable enable
using System;
using Foundation;
using ObjCRuntime;

namespace BaristaNotes.Native.iOS.Ailoha;

[BaseType(typeof(NSObject), Name = "BNAilohaBridge")]
[DisableDefaultCtor]
interface NativeAgentBridge
{
    [Static]
    [Export("startWithCompletion:")]
    void Start(Action<nint, NSError?> completion);
}
