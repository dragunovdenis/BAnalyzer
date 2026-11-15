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

using BAnalyzerCore.Utils;

namespace BAnalyzerCore.TradeSim;

/// <summary>
/// The outcome of loading the simulator state.
/// </summary>
/// <param name="State">The loaded (or a fresh) state; never "null".</param>
/// <param name="Warning">Explanation of why a fresh state had to be used instead of the stored one or "null".</param>
public sealed record TradeSimLoadResult(TradeSimState State, string Warning);

/// <summary>
/// Persistence of the trade simulator state.
/// </summary>
public interface ITradeSimStore
{
    /// <summary>
    /// Loads the state; never throws.
    /// </summary>
    TradeSimLoadResult Load();

    /// <summary>
    /// Saves the given state.
    /// </summary>
    void Save(TradeSimState state);
}

/// <summary>
/// Stores the simulator state in an XML file.
/// </summary>
public sealed class TradeSimFileStore : ITradeSimStore
{
    /// <summary>
    /// Path to the file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Constructor.
    /// </summary>
    public TradeSimFileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
    }

    /// <inheritdoc/>
    public TradeSimLoadResult Load()
    {
        if (!File.Exists(FilePath))
            return new TradeSimLoadResult(new TradeSimState(), null);

        string problem;

        try
        {
            var state = DataContractSerializationUtils.LoadFromFile<TradeSimState>(FilePath);
            problem = TradeSimulator.Validate(state);

            if (problem == null)
                return new TradeSimLoadResult(state, null);
        }
        catch (Exception e)
        {
            problem = e.Message;
        }

        var backup = TryBackUpCorruptFile();
        var warning = $"The trade simulator data in \"{Path.GetFullPath(FilePath)}\" could not be loaded: {problem}" +
                      Environment.NewLine + "A new simulation has been started. " +
                      (backup != null ? $"The old file was preserved as \"{backup}\"." :
                          "The old file could not be preserved and will be overwritten.");

        return new TradeSimLoadResult(new TradeSimState(), warning);
    }

    /// <inheritdoc/>
    public void Save(TradeSimState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var xml = DataContractSerializationUtils.Serialize(state);
        var fullPath = Path.GetFullPath(FilePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = fullPath + ".tmp";
        File.WriteAllText(tempPath, xml);
        File.Move(tempPath, fullPath, overwrite: true);
    }

    /// <summary>
    /// Moves (or, failing that, copies) the unreadable file aside so that it is not overwritten;
    /// returns the full path of the backup or "null" if it could not be made.
    /// </summary>
    private string TryBackUpCorruptFile()
    {
        try
        {
            var fullPath = Path.GetFullPath(FilePath);
            var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(fullPath);
            var extension = Path.GetExtension(fullPath);
            var stem = Path.Combine(directory, $"{name}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}");

            var backup = stem + extension;

            for (var i = 1; File.Exists(backup); i++)
                backup = $"{stem}-{i}{extension}";

            try
            {
                File.Move(fullPath, backup);
            }
            catch (IOException)
            {
                File.Copy(fullPath, backup);
            }
            catch (UnauthorizedAccessException)
            {
                File.Copy(fullPath, backup);
            }

            return backup;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
