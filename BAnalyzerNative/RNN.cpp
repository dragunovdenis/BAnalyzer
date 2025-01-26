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

#include "RNN.h"
#include "Math/LinAlg4d.h"
#include "NeuralNet/InOutMData.h"
#include "NeuralNet/LazyVector.h"
#include "NeuralNet/MNet.h"
#include "DataConversionUtils.h"

using namespace DeepLearning;

namespace BAnalyzerNative
{
	RNN::RNN(const int time_depth, const int layer_item_sizes_count, const int* layer_item_sizes)
	{
		if (layer_item_sizes_count < 2)
			throw std::exception("Can't construct the net.");

		Index4d in_size{ {1, 1, layer_item_sizes[0]}, time_depth };

		for (auto layer_id = 1; layer_id < layer_item_sizes_count; ++layer_id)
		{
			in_size = _net.append_layer<RMLayer>(in_size, Index4d{ {1, 1, layer_item_sizes[layer_id]}, time_depth },
				FillRandomNormal, ActivationFunctionId::SIGMOID);
		}

		_context = _net.allocate_context();
		_plain_input_size = static_cast<int>(_net.in_size().xyz.coord_prod() * _net.in_size().w);
		_plain_output_size = static_cast<int>(_net.out_size().xyz.coord_prod() * _net.out_size().w);
	}

	void RNN::evaluate(const int size, const double* input, LazyVector<double>& output) const
	{

		if (size != _plain_input_size)
			throw std::exception("Invalid input data.");

		thread_local LazyVector<CpuDC::tensor_t> input_lazy{};
		const auto in_size = _net.in_size();
		input_lazy.resize(in_size.w);
		DataConversionUtils::fill_lazy_vector(in_size.xyz.coord_prod(), input, input_lazy);

		thread_local InOutMData<CpuDC> cache{};
		_net.act(input_lazy, cache);

		output.resize(_plain_output_size);
		DataConversionUtils::pack_lazy_vector(cache.out(), output.begin());
	}

	void RNN::train(const int in_aggregate_size, const double* input_aggregate, const int ref_aggregate_size,
		const double* reference_aggregate, const double learning_rate)
	{
		if (in_aggregate_size % _plain_input_size != 0 ||
			ref_aggregate_size % _plain_output_size != 0)
			throw std::exception("Invalid input.");

		const auto training_pair_count = in_aggregate_size / _plain_input_size;

		if (training_pair_count != ref_aggregate_size / _plain_output_size)
			throw std::exception("Invalid input.");

		thread_local LazyVector<LazyVector<CpuDC::tensor_t>> input_lazy{};
		input_lazy.resize(training_pair_count);

		thread_local LazyVector<LazyVector<CpuDC::tensor_t>> reference_lazy{};
		reference_lazy.resize(training_pair_count);

		auto raw_input_begin = input_aggregate;
		auto raw_reference_begin = reference_aggregate;

		const auto in_size = _net.in_size();
		const auto in_item_size = in_size.xyz.coord_prod();
		const auto out_size = _net.out_size();
		const auto ref_item_size = out_size.xyz.coord_prod();

		for (auto pair_id = 0; pair_id < training_pair_count; ++pair_id)
		{
			auto& in = input_lazy[pair_id];
			in.resize(in_size.w);
			DataConversionUtils::fill_lazy_vector(in_item_size, raw_input_begin, in);
			raw_input_begin += _plain_input_size;

			auto& ref = reference_lazy[pair_id];
			ref.resize(out_size.w);
			DataConversionUtils::fill_lazy_vector(ref_item_size, raw_reference_begin, ref);
			raw_reference_begin += _plain_output_size;
		}

		_net.learn(input_lazy, reference_lazy, CostFunction<CpuDC::tensor_t>(CostFunctionId::CROSS_ENTROPY),
			static_cast<Real>(learning_rate), _context);
	}

	Index4d RNN::in_size() const
	{
		return _net.in_size();
	}

	Index4d RNN::out_size() const
	{
		return _net.out_size();
	}

	int RNN::layer_count() const
	{
		return static_cast<int>(_net.layer_count());
	}
}
