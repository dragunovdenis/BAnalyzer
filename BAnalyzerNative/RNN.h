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

#pragma once
#include "NeuralNet/DataContext.h"
#include "NeuralNet/MNet.h"

namespace BAnalyzerNative
{
	/// <summary>
	/// A wrapper for an instance of MNet.
	/// </summary>
	class RNN
	{
		DeepLearning::MNet<DeepLearning::CpuDC> _net{};
		DeepLearning::MNet<DeepLearning::CpuDC>::Context _context{};
		int _plain_input_size{ -1 };
		int _plain_output_size{ -1 };

	public:

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="time_depth">Recursive depth the RNN. Constant for all the level (in the current implementation)</param>
		/// <param name="layer_item_sizes_count">Number of items in the array <paramref name="layer_item_sizes"/> </param>
		/// <param name="layer_item_sizes">An array containing linear sizes of time-point input items for each layer as wel as the
		/// size of time-point output item for the last layer at the end of the array.</param>
		RNN(const int time_depth, const int layer_item_sizes_count, const int* layer_item_sizes);

		/// <summary>
		/// Evaluates net at the given <paramref name="input"/> and stores the
		/// result into the given <paramref name="output"/> container.
		/// </summary>
		/// <param name="size">Size of the <paramref name="input"/> </param>
		/// <param name="input">Input</param>
		/// <param name="output">Container to store the result.</param>
		void evaluate(const int size, const double* input, DeepLearning::LazyVector<double>& output) const;

		/// <summary>
		/// Performs a single-batch training iteration based on the given set of input-reference data.
		/// </summary>
		/// <param name="in_aggregate_size">Total number of elements in <paramref name="input_aggregate"/> array.</param>
		/// <param name="input_aggregate">Array of input data.</param>
		/// <param name="ref_aggregate_size">Total number of elements in <paramref name="reference_aggregate"/> array.</param>
		/// <param name="reference_aggregate">Array of reference data.</param>
		/// <param name="learning_rate">Factor determining aggressiveness of the training.</param>
		void train(const int in_aggregate_size, const double* input_aggregate, const int ref_aggregate_size,
			const double* reference_aggregate, const double learning_rate);

		/// <summary>
		/// Returns input size of the net.
		/// </summary>
		DeepLearning::Index4d in_size() const;

		/// <summary>
		/// Returns output size of the net.
		/// </summary>
		DeepLearning::Index4d out_size() const;

		/// <summary>
		/// Returns number of layers constituting the net.
		/// </summary>
		int layer_count() const;
	};
}
