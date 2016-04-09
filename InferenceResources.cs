using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.GenericContentModel;
using Grammophone.LanguageModel.Provision;

namespace Grammophone.EnnounInference
{
	/// <summary>
	/// Collection of <see cref="InferenceResource"/> items, indexed by <see cref="ReadOnlyLanguageFacet.LanguageProvider"/>.
	/// </summary>
	public class InferenceResources : Map<LanguageProvider, InferenceResource>
	{
	}
}
