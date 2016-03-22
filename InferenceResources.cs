using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.GenericContentModel;
using Gramma.LanguageModel.Provision;

namespace Gramma.Inference
{
	/// <summary>
	/// Collection of <see cref="InferenceResource"/> items, indexed by <see cref="ReadOnlyLanguageFacet.LanguageProvider"/>.
	/// </summary>
	public class InferenceResources : Map<LanguageProvider, InferenceResource>
	{
	}
}
