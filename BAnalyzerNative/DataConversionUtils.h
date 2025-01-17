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

#include <defs.h>
#include <NeuralNet/LazyVector.h>
#include <NeuralNet/DataContext.h>

namespace BAnalyzerNative
{
	/// <summary>
	/// Data conversion utilities.
	/// </summary>
	struct DataConversionUtils
	{
		/// <summary>
		/// Fills the given instance <paramref name="dest"/> of lazy vector with the content of <paramref name="arr"/>.
		/// </summary>
		static void fill_lazy_vector(const long long item_size, const double* arr,
			DeepLearning::LazyVector<DeepLearning::CpuDC::tensor_t>& dest);

		/// <summary>
		/// Packs given "lazy vector" <paramref name="src"/> into the given plain array <paramref name="dest"/>.
		/// </summary>
		static void pack_lazy_vector(const DeepLearning::LazyVector<DeepLearning::CpuDC::tensor_t>& src, double* dest);
	};
}
