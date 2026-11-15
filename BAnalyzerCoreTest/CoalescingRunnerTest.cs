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
using FluentAssertions;

namespace BAnalyzerCoreTest;

/// <summary>
/// Tests of <see cref="CoalescingRunner"/>.
/// </summary>
[TestClass]
public class CoalescingRunnerTest
{
    [TestMethod]
    public async Task RequestsDuringRunAreCoalescedIntoOneMoreRun()
    {
        // The continuations of these tasks run synchronously, within "SetResult".
        var gates = new List<TaskCompletionSource>();
        var runner = new CoalescingRunner(() =>
        {
            gates.Add(new TaskCompletionSource());
            return gates[^1].Task;
        });

        var first = runner.RunAsync();
        runner.RunAsync().IsCompleted.Should().BeTrue();
        runner.RunAsync().IsCompleted.Should().BeTrue();
        gates.Should().HaveCount(1);

        gates[0].SetResult();
        gates.Should().HaveCount(2);
        first.IsCompleted.Should().BeFalse();

        gates[1].SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        gates.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task FailedRunPropagatesExceptionAndDoesNotBlockLaterRuns()
    {
        var runs = 0;
        var runner = new CoalescingRunner(() => ++runs == 1 ?
            Task.FromException(new InvalidOperationException()) : Task.CompletedTask);

        await runner.Invoking(x => x.RunAsync()).Should().ThrowAsync<InvalidOperationException>();
        await runner.RunAsync();

        runs.Should().Be(2);
    }
}
