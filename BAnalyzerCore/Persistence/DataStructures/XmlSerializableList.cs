//Copyright (c) 2025 Denys Dragunov, dragunovdenis@gmail.com
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

using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using BAnalyzerCore.DataConversionUtils;

namespace BAnalyzerCore.Persistence.DataStructures;

/// <summary>
///A list of structs that can be efficiently serialized into XML format.
/// </summary>
[Obfuscation(Exclude = true)]
public class XmlSerializableList<T> : List<T>, IXmlSerializable
    where T : struct
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    public XmlSerializableList() { }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="source"></param>
    public XmlSerializableList(IEnumerable<T> source) => AddRange(source);

    private readonly string _currentVersion = "1.0";

    /// <summary>
    /// Enumerates xml elements.
    /// </summary>
    [Obfuscation(Exclude = true)]
    private enum XmlNames
    {
        Version,
        Data,
    }

    /// <inheritdoc />
    public XmlSchema GetSchema() => null;

    /// <inheritdoc />
    public void ReadXml(XmlReader reader)
    {
        reader.ReadToDescendant(XmlNames.Data.ToString());
        Clear();
        AddRange(reader.ReadElementString(XmlNames.Data.ToString()).Base64StringToArray<T>());
        reader.ReadEndElement();
    }

    /// <inheritdoc />
    public void WriteXml(XmlWriter writer)
    {
        writer.WriteAttributeString(XmlNames.Version.ToString(), _currentVersion);
        writer.WriteElementString(XmlNames.Data.ToString(), ToArray().ToBase64String());
    }
}