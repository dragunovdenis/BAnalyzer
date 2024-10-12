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


using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace BAnalyzer.Utils;

/// <summary>
/// Functionality that facilitates serialization of data
/// </summary>
internal class DataContractSerializationUtils
{
    /// <summary>
    /// Method to serialize.
    /// </summary>
    public static string Serialize(object obj)
    {
        using MemoryStream memoryStream = new MemoryStream();
        new DataContractSerializer(obj.GetType()).WriteObject(memoryStream, obj);
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    /// <summary>
    /// Method to de-serialize.
    /// </summary>
    public static T Deserialize<T>(string xml)
    {
        using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var deserializer = new DataContractSerializer(typeof(T));
        return (T)deserializer.ReadObject(stream);
    }

    /// <summary>
    /// Loads instance of "T" from the given file.
    /// </summary>
    public static T LoadFromFile<T>(string fileName) => Deserialize<T>(File.ReadAllText(fileName));

    /// <summary>
    /// Saves the given instance to the given file on disk.
    /// </summary>
    public static void SaveToFile(string fileName, object obj) => File.WriteAllText(fileName, Serialize(obj));
}