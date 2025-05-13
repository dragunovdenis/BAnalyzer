﻿//Copyright (c) 2024 Denys Dragunov, dragunovdenis@gmail.com
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

using System.IO;
using System.Windows;

namespace BAnalyzer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public App() => AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;

    private const string LogFileName = "BAnalyzerExceptionLog.txt";

    /// <summary>
    /// Logs unhandled exceptions.
    /// </summary>
    private static void LogUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Exception e = (Exception)args.ExceptionObject;
        using StreamWriter sw = File.AppendText(LogFileName);
        sw.WriteLine("/--------------------------------/");
        sw.WriteLine($"{DateTime.Now}: Unhandled Exception");
        sw.WriteLine($"With message: {e.Message}");
        sw.WriteLine($"Source: {e.Source}");
        sw.WriteLine($"Stack trace: {e.StackTrace}");
        sw.WriteLine("/--------------------------------/");
    }
}