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

using BAnalyzerCore.Utils;

namespace BAnalyzerCore.Cache;

/// <summary>
/// Functionality allowing to cache "k-line" series.
/// </summary>
public class BinanceCache
{
    private readonly Dictionary<string, AssetTimeView> _data = new();

    /// <summary>
    /// Returns an existing asset-view object associated with the given pair of keys
    /// or a newly created one (if such an object does not exist yet).
    /// </summary>
    private AssetTimeView this[string symbol]
    {
        get
        {
            if (_data.TryGetValue(symbol, out var view))
                return view;

            return _data[symbol] = new AssetTimeView();
        }

        set => _data[symbol] = value;
    }

    /// <summary>
    /// Collection of all the symbols present in teh cache.
    /// </summary>
    public IReadOnlyCollection<string> CachedSymbols => _data.Keys;

    /// <summary>
    /// Returns instance of asset-view associated with the given <paramref name="symbol"/>.
    /// </summary>
    public AssetTimeView GetAssetViewThreadSafe(string symbol)
    {
        lock (this) { return this[symbol]; }
    }

    /// <summary>
    /// Saves the cache into the given folder.
    /// </summary>
    public void Save(string folderPath)
    {
        var dir = Directory.CreateDirectory(folderPath);
        dir.ClearDirectory();

        foreach (var v in _data)
        {
            var subDir = Directory.CreateDirectory(Path.Combine(folderPath, v.Key));
            v.Value.Save(subDir.FullName);
        }
    }

    /// <summary>
    /// Loads an instance of cache from the data in the given
    /// folder (which was previously saved there by <see cref="Save"/>)
    /// </summary>
    public static BinanceCache Load(string folderPath)
    {
        var result = new BinanceCache();

        foreach (var dir in Directory.GetDirectories(folderPath))
        {
            var symbol = Path.GetFileName(dir).ToUpper();
            result[symbol] = AssetTimeView.Load(dir) ??
                             throw new InvalidOperationException("Failed to load asset view data");
        }

        return result;
    }
}