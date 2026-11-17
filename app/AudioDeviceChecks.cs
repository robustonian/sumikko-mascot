using System;

namespace SumikkoCompanion
{
    internal static class AudioDeviceChecks
    {
        internal static void Run(Action<bool,string> assert)
        {
            var first=new AudioDeviceInfo("endpoint-A","仮想 Audio = 入力");
            var second=new AudioDeviceInfo("endpoint-B","仮想 Audio = 入力");
            var choices=AudioDeviceWindow.BuildChoices(new[] { first,second },second.Id,second.Name);
            assert(choices.Count==3 && choices[0].Id=="" && choices[0].Available,
                "Windows default remains an explicit device choice");
            assert(choices[1].Label!=choices[2].Label && choices[1].Id!=choices[2].Id,
                "identically named virtual devices stay individually selectable");
            var reordered=AudioDeviceWindow.BuildChoices(new[] { second,first },second.Id,second.Name);
            assert(reordered.Find(c=>c.Id==second.Id).Available,
                "saved device identity survives enumeration order changes");
            var missing=AudioDeviceWindow.BuildChoices(new[] { first },second.Id,second.Name);
            assert(missing.Count==3 && missing[2].Id==second.Id && !missing[2].Available,
                "missing selected device is preserved rather than replaced by a namesake");
            var disconnected=AudioDeviceWindow.BuildChoices(new AudioDeviceInfo[0],second.Id,second.Name);
            assert(disconnected.Count==2 && disconnected[1].Name==second.Name && !disconnected[1].Available,
                "empty device list retains the saved device for reconnection");
            var reconnected=AudioDeviceWindow.BuildChoices(new[] { second },second.Id,"old name");
            assert(reconnected.Count==2 && reconnected[1].Available && reconnected[1].Name==second.Name,
                "reconnected endpoint uses its current name without a missing duplicate");
            var usbA=new AudioDeviceInfo("{0.0.1.00000000}.{11111111-2222-3333-4444-5555aaaa0001}","USB マイク");
            var usbB=new AudioDeviceInfo("{0.0.1.00000000}.{11111111-2222-3333-4444-5555aaaa0002}","USB マイク");
            string inOrder=AudioDeviceWindow.BuildChoices(new[] { usbA,usbB },"","").Find(c=>c.Id==usbB.Id).Label;
            string swapped=AudioDeviceWindow.BuildChoices(new[] { usbB,usbA },"","").Find(c=>c.Id==usbB.Id).Label;
            assert(AudioDeviceWindow.ShortId(usbB.Id)=="0002" && inOrder.Contains("ID 0002") && swapped.Contains("ID 0002"),
                "namesakes show the same part of their ID whatever the list order");
            assert(AudioDeviceWindow.BuildChoices(new[] { usbA },"","")[1].Label=="USB マイク",
                "a device without a namesake is shown by its name alone");
        }
    }
}
