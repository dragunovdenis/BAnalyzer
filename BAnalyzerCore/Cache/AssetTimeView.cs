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

using Binance.Net.Enums;

namespace BAnalyzerCore.Cache;

/// <summary>
/// Binance exchange data of a certain granularity related to a certain asset.
/// </summary>
internal class AssetTimeView
{
    /// <summary>
    /// Grid data.
    /// </summary>
    private readonly Dictionary<KlineInterval, BlockGrid> _grid = new();

    /// <summary>
    /// Subscript operator.
    /// </summary>
    private BlockGrid this[KlineInterval granularity]
    {
        get
        {
            if (_grid.TryGetValue(granularity, out var block))
                return block;

            return _grid[granularity] = new BlockGrid(granularity);
        }

        set => _grid[granularity] = value;
    }

    /// <summary>
    /// Returns an instance of block-grid for the given <paramref name="granularity"/>.
    /// </summary>
    public BlockGrid GetGridThreadSafe(KlineInterval granularity)
    {
        lock(this) { return this[granularity]; }
    }

    /// <summary>
    /// Saves the "view" into the given folder.
    /// </summary>
    public void Save(string folderPath)
    {
        foreach (var g in _grid)
        {
            var subDir = Directory.CreateDirectory(Path.Combine(folderPath, g.Key.ToString()));
            g.Value.SaveThreadSafe(subDir.FullName);
        }
    }

    /// <summary>
    /// Loads an instance of "asset-view" from the
    /// data in the given <paramref name="folderPath"/>
    /// which was previously saved there by <see cref="Save"/>.
    /// </summary>
    public static AssetTimeView Load(string folderPath)
    {
        var result = new AssetTimeView();

        foreach (var dir in Directory.EnumerateDirectories(folderPath))
            if (Enum.TryParse(Path.GetFileName(dir), out KlineInterval granularity))
                result[granularity] = BlockGrid.Load(granularity, dir);

        return result;
    }
}