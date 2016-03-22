using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.GenericContentModel;
using Gramma.LanguageModel;
using Gramma.LanguageModel.Provision;
using Gramma.Inference.Configuration;

namespace Gramma.Inference
{
	/// <summary>
	/// Collection of <see cref="InferenceResourceProvider"/> items, 
	/// keyed by <see cref="LanguageProvider"/>.
	/// </summary>
	public class InferenceResourceProviders : ReadOnlyMap<LanguageProvider, InferenceResourceProvider>
	{
	}
}
