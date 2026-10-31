//Copyright (c) 2026 Denys Dragunov, dragunovdenis@gmail.com
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files(the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and /or sell
//copies of the Software, and to permit persons to whom the Software is furnished
//to do so, subject to the following conditions :

//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
//INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
//PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
//HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Runtime.InteropServices;
using NAudio.Wave;

namespace BAnalyzer.Utils;

/// <summary>
/// A recording device.
/// </summary>
/// <param name="Number">WinMM number of the device.</param>
/// <param name="Name">Display name of the device.</param>
public sealed record MicrophoneDevice(int Number, string Name)
{
    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <summary>
    /// Returns the recording devices of the system.
    /// </summary>
    public static IReadOnlyList<MicrophoneDevice> GetAll()
    {
        var result = new List<MicrophoneDevice>();

        try
        {
            for (var i = 0; i < WaveIn.DeviceCount; i++)
                result.Add(new MicrophoneDevice(i, WaveIn.GetCapabilities(i).ProductName));
        }
        catch (Exception)
        {
            // The devices that could not be enumerated are not offered.
        }

        return result;
    }

    /// <summary>
    /// Returns the WinMM number of the default recording device of Windows,
    /// or "null" if it can't be determined.
    /// </summary>
    public static int? GetDefaultNumber()
    {
        try
        {
            return waveInMessage(new IntPtr(WaveMapper), DrvmMapperPreferredGet,
                out var deviceNumber, out _) == 0 && deviceNumber >= 0 ? deviceNumber : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// WinMM number of the "sound mapper" that records the default device of Windows.
    /// </summary>
    private const int WaveMapper = -1;

    /// <summary>
    /// Message that asks the "sound mapper" for the device it records from.
    /// </summary>
    private const uint DrvmMapperPreferredGet = 0x2015;

    [DllImport("winmm.dll")]
    private static extern int waveInMessage(IntPtr waveIn, uint message, out int deviceNumber, out int flags);
}
