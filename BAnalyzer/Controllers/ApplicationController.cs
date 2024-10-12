//Copyright (c) 2024 Denys Dragunov, dragunovdenis@gmail.com
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

using BAnalyzer.DataStructures;
using BAnalyzer.Utils;

namespace BAnalyzer.Controllers;

/// <summary>
/// Application controller
/// </summary>
public class ApplicationController
{
    /// <summary>
    /// Application settings.
    /// </summary>
    public IApplicationSettings ApplicationSettings { get; }

    private string _settingsFilePath = "settings.xml";

    /// <summary>
    /// Private constructor to implement singleton pattern.
    /// </summary>
    private ApplicationController()
    {
        try
        {
            ApplicationSettings = DataContractSerializationUtils.LoadFromFile<ApplicationSettings>(_settingsFilePath);
        }
        catch (Exception)
        {
            ApplicationSettings = new ApplicationSettings();
        }
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public void SaveSettings() => DataContractSerializationUtils.SaveToFile(_settingsFilePath, ApplicationSettings);

    private static ApplicationController _instance;

    /// <summary>
    /// The only instance of the controller.
    /// </summary>
    public static ApplicationController Instance => _instance ??= new ApplicationController(); 
}