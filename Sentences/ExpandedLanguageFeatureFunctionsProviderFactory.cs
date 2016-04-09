using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.LanguageModel.Grammar;
using Grammophone.EnnounInference.Words;

namespace Grammophone.EnnounInference.Sentences
{
	/// <summary>
	/// This is a specialized <see cref="LanguageFeatureFunctionsProviderFactory"/>
	/// that only works when <see cref="WordClassifierBank.AreDictionaryFeaturesCondensed"/>.
	/// It performs better in this case and it is less heap-intensive.
	/// </summary>
	[Serializable]
	internal class ExpandedLanguageFeatureFunctionsProviderFactory : LanguageFeatureFunctionsProviderFactory
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="inferenceResource">The inference resource.</param>
		/// <param name="tagBiGrams">The allowed tag bi-grams.</param>
		/// <param name="wordScoringPolicy">Discriminates how a word is scored by a its <see cref="Words.ScoreBank"/>.</param>
		/// <param name="analogiesScoreOptions">The analogy score options or null for no analogy scoring.</param>
		public ExpandedLanguageFeatureFunctionsProviderFactory(
			InferenceResource inferenceResource, 
			Tuple<Tag, Tag>[] tagBiGrams, 
			WordScoringPolicy wordScoringPolicy, 
			WordClassifierBank.AnalogiesScoreOptions analogiesScoreOptions)
			: base(inferenceResource, tagBiGrams, wordScoringPolicy, analogiesScoreOptions)
		{
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Get the provider of feature functions.
		/// </summary>
		/// <param name="input">The input sequence.</param>
		/// <param name="scope">Gives a hint whether we are in training scope or not.</param>
		/// <returns>Returns a <see cref="LanguageFeatureFunctionsProvider"/>.</returns>
		/// <remarks>
		/// Depending on <paramref name="scope"/>, the <see cref="LanguageFeatureFunctionsProvider"/> is obtained
		/// by <see cref="LanguageFeatureFunctionsProviderFactory.RunningProvidersCache"/> or created directly.
		/// </remarks>
		public override CRF.LinearChainCRF<string[], Tag>.FeatureFunctionsProvider GetProvider(string[] input, CRF.EvaluationScope scope)
		{
			if (input == null) throw new ArgumentNullException("input");

			switch (scope)
			{
				case CRF.EvaluationScope.Training:
					return new ExpandedLanguageFeatureFunctionsProvider(input, this, CRF.EvaluationScope.Training);

				default:
					return this.RunningProvidersCache.Get(input);
			}
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Create a fresh provider for the given <paramref name="input"/>.
		/// Used by <see cref="LanguageFeatureFunctionsProviderFactory.RunningProvidersCache"/> during cache miss.
		/// </summary>
		/// <param name="input">The input sequence.</param>
		/// <returns>Returns the provider.</returns>
		/// <remarks>
		/// This returns an <see cref="ExpandedLanguageFeatureFunctionsProvider"/>.
		/// </remarks>
		protected override LanguageFeatureFunctionsProvider CreateRunningProvider(string[] input)
		{
			return new ExpandedLanguageFeatureFunctionsProvider(input, this, CRF.EvaluationScope.Running);
		}

		#endregion
	}
}
