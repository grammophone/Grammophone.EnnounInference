using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.GenericContentModel;
using Grammophone.LanguageModel;
using Grammophone.LanguageModel.Provision;
using Grammophone.EnnounInference.Configuration;

namespace Grammophone.EnnounInference
{
	/// <summary>
	/// Collection of <see cref="InferenceResourceProvider"/> items, 
	/// keyed by <see cref="LanguageProvider"/>.
	/// </summary>
	public class InferenceResourceProviders : ReadOnlyMap<LanguageProvider, InferenceResourceProvider>
	{
	}
}
