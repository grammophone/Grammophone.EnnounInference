using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.CRF;
using Grammophone.LanguageModel.Grammar;

namespace Grammophone.EnnounInference.Sentences
{
	/// <summary>
	/// Conditinal random field to be used in <see cref="SentenceClassifier"/>.
	/// </summary>
	[Serializable]
	internal class LanguageCRF : ConstrainedLinearChainCRF<string[], Tag>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="inferenceResource">The inference resource.</param>
		/// <param name="tagBiGrams">The allowed tag bi-grams.</param>
		/// <param name="trainingOptions">The training options for the sentences classifier.</param>
		public LanguageCRF(
			InferenceResource inferenceResource, 
			Tuple<Tag, Tag>[] tagBiGrams, 
			SentenceClassifierTrainingOptions trainingOptions)
			: base(
			tagBiGrams, 
			inferenceResource.LanguageProvider.StartTag, 
			inferenceResource.LanguageProvider.EndTag, 
			DecideOptimumFeatureFunctionsProviderFactory(inferenceResource, tagBiGrams, trainingOptions)
			)
		{

		}

		#endregion

		#region Private methods

		private static LanguageFeatureFunctionsProviderFactory DecideOptimumFeatureFunctionsProviderFactory(
			InferenceResource inferenceResource,
			Tuple<Tag, Tag>[] tagBiGrams,
			SentenceClassifierTrainingOptions trainingOptions)
		{
			if (inferenceResource == null) throw new ArgumentNullException("inferenceResource");
			if (tagBiGrams == null) throw new ArgumentNullException("tagBiGrams");
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");

			var wordScoringPolicy = trainingOptions.WordScoringPolicy;
			var analogiesScoreOptions = trainingOptions.AnalogiesScoreOptions;

			if (!inferenceResource.WordClassifierBank.AreDictionaryFeaturesCondensed)
				return new ExpandedLanguageFeatureFunctionsProviderFactory(inferenceResource, tagBiGrams, wordScoringPolicy, analogiesScoreOptions);
			else
				return new LanguageFeatureFunctionsProviderFactory(inferenceResource, tagBiGrams, wordScoringPolicy,analogiesScoreOptions);
		}

		#endregion
	}
}
