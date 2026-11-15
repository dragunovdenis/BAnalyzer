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

using BAnalyzerCore.TradeSim;

namespace BAnalyzer.Controllers;

/// <summary>
/// Owns the trade simulation and keeps it persisted.
/// </summary>
public class TradeSimController
{
    private const string FilePath = "trade-sim.xml";

    private readonly ITradeSimStore _store;

    private string _loadWarning;

    /// <summary>
    /// The simulation.
    /// </summary>
    public TradeSimulator Simulator { get; }

    /// <summary>
    /// Message of the last failed save or "null" if the last save succeeded.
    /// </summary>
    public string LastSaveError { get; private set; }

    /// <summary>
    /// Raised after the simulation has changed and the change has been (attempted to be) saved.
    /// </summary>
    public event EventHandler Updated;

    /// <summary>
    /// Private constructor to implement singleton pattern.
    /// </summary>
    private TradeSimController(ITradeSimStore store)
    {
        _store = store;

        var loaded = _store.Load();
        _loadWarning = loaded.Warning;
        Simulator = new TradeSimulator(loaded.State);
        Simulator.Changed += Simulator_Changed;
    }

    /// <summary>
    /// Returns the warning produced when the simulation was loaded (if any), only once.
    /// </summary>
    public string TakeLoadWarning()
    {
        var result = _loadWarning;
        _loadWarning = null;
        return result;
    }

    /// <summary>
    /// Saves the simulation if the last attempt to do so failed.
    /// </summary>
    public void Flush()
    {
        if (LastSaveError != null)
            Save();
    }

    /// <summary>
    /// Saves the simulation, recording the outcome in <see cref="LastSaveError"/>.
    /// </summary>
    private void Save()
    {
        try
        {
            _store.Save(Simulator.Snapshot());
            LastSaveError = null;
        }
        catch (Exception e)
        {
            LastSaveError = e.Message;
        }
    }

    /// <summary>
    /// Handles changes of the simulation.
    /// </summary>
    private void Simulator_Changed(object sender, EventArgs e)
    {
        Save();
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private static TradeSimController _instance;

    /// <summary>
    /// The only instance of the controller.
    /// </summary>
    public static TradeSimController Instance => _instance ??= new TradeSimController(new TradeSimFileStore(FilePath));
}
