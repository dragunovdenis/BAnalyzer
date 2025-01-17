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

#include "DataConversionUtils.h"

namespace BAnalyzerNative
{
	void DataConversionUtils::fill_lazy_vector(const long long item_size, const double* arr,
		DeepLearning::LazyVector<DeepLearning::CpuDC::tensor_t>& dest)
	{
		auto begin = arr;
		for (auto item_id = 0ull; item_id < dest.size(); ++item_id)
		{
			dest[item_id].resize(1, 1, item_size);

			const auto end = begin + item_size;
#ifdef USE_SINGLE_PRECISION
#pragma warning(push)
#pragma warning(disable: 4244)
#endif
			std::ranges::copy(begin, end, dest[item_id].begin());
#ifdef USE_SINGLE_PRECISION
#pragma warning(pop)
#endif
			begin = end;
		}
	}

	void DataConversionUtils::pack_lazy_vector(const DeepLearning::LazyVector<DeepLearning::CpuDC::tensor_t>& src,
		double* dest)
	{
		auto begin = dest;
		for (const auto& item : src)
		{
			std::ranges::copy(item, begin);
			begin += item.size();
		}
	}
}
