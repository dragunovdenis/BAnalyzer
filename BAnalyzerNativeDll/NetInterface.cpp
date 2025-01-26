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

#include "NetInterface.h"
#include <RNN.h>

__declspec(dllexport) RNN* RnnConstruct(const int time_depth,
	const int layer_item_sizes_count, const int* layer_item_sizes)
{
	RNN* result{};

	try
	{
		result = new RNN(time_depth, layer_item_sizes_count, layer_item_sizes);

	} catch (...)
	{
		if (result)
			delete result;
		result = nullptr;
	}

	return result;
}

__declspec(dllexport) bool RnnFree(const RNN* net_ptr)
{
	if (!net_ptr)
		return false;

	try
	{
		delete net_ptr;
	} catch(...)
	{
		return false;
	}

	return true;
}

int RnnGetInputItemSize(const RNN* net_ptr)
{
	if (net_ptr)
		return static_cast<int>(net_ptr->in_size().xyz.coord_prod());

	return -1;
}

int RnnGetOutputItemSize(const RNN* net_ptr)
{
	if (net_ptr)
		return static_cast<int>(net_ptr->out_size().xyz.coord_prod());

	return -1;
}

int RnnGetLayerCount(const RNN* net_ptr)
{
	if (net_ptr)
		return static_cast<int>(net_ptr->layer_count());

	return -1;
}

int RnnGetDepth(const RNN* net_ptr)
{
	if (net_ptr)
		return static_cast<int>(net_ptr->in_size().w);

	return -1;
}

bool RnnEvaluate(const RNN* net_ptr, const int size, const double* input,
	const GetArrayCallBack get_result_callback)
{
	if (!net_ptr)
		return false;

	thread_local DeepLearning::LazyVector<double> output{};

	try
	{
		net_ptr->evaluate(size, input, output);
		get_result_callback(static_cast<int>(output.size()), output.begin());
	} catch (...)
	{
		return false;
	}

	return true;
}

bool RnnBatchTrain(RNN* net_ptr, const int in_aggregate_size,
	const double* input_aggregate, const int ref_aggregate_size,
	const double* reference_aggregate, const double learning_rate)
{
	if (!net_ptr)
		return false;

	try
	{
		net_ptr->train(in_aggregate_size, input_aggregate, ref_aggregate_size,
			reference_aggregate, learning_rate);
	} catch(...)
	{
		return false;
	}

	return true;
}

bool IsSinglePrecision()
{
	return std::is_same_v<DeepLearning::Real, float>;
}
