using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.CRF;
using Gramma.Vectors;
using Gramma.LanguageModel.Grammar;

namespace Gramma.Inference.Sentences
{
	/// <summary>
	/// A faster and less heap-intensive feature functions provider that only
	/// works with expanded feature ID space,
	/// ie when the <see cref="Words.WordClassifierBank.AreDictionaryFeaturesCondensed"/> 
	/// property is false.
	/// </summary>
	internal class ExpandedLanguageFeatureFunctionsProvider : LanguageFeatureFunctionsProvider
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="input">The input sequence.</param>
		/// <param name="factory">The factory which creates this provider.</param>
		/// <param name="scope">Gives a hint whether we are in training scope or not.</param>
		public ExpandedLanguageFeatureFunctionsProvider(
			string[] input,
			LanguageFeatureFunctionsProviderFactory factory,
			EvaluationScope scope)
			: base(input, factory, scope)
		{

		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Provides the unigram feature functions.
		/// </summary>
		protected override LinearChainCRF<string[], Tag>.UnigramVectorFeatureFunction CreateUnigramVectorFeatureFunction()
		{
			return this.UnigramFeatureFunction;
		}

		#endregion

		#region Private methods

		private SparseVector UnigramFeatureFunction(Tag yi, string[] x, int i)
		{
			int biasesOffset = factory.BiasesOffset;

			if (i < x.Length)
			{
				var scores = GetScores(i, yi);

				if (scores.Count > 0)
				{
					var entries = new SparseVector.Entry[2 * scores.Count + 1];

					int j = 0;

					foreach (var score in scores)
					{
						int featureID = score.Feature.ID;

						entries[j] = new SparseVector.Entry(featureID, score.ScoreValue * normalizer);
						entries[scores.Count + j] = new SparseVector.Entry(biasesOffset + featureID, normalizer);

						j++;
					}

					// Global bias.
					entries[2 * scores.Count] = new SparseVector.Entry(factory.FeatureFunctionsCount - 1, normalizer);

					return new SparseVector(entries);
				}
			}
			else // i == x.Length
			{
				var entries = new SparseVector.Entry[3];

				entries[0] = new SparseVector.Entry(factory.EndIndicatorOffset, normalizer);
				entries[1] = new SparseVector.Entry(factory.EndBiasOffset, normalizer);

				// Global bias.
				entries[2] = new SparseVector.Entry(factory.FeatureFunctionsCount - 1, normalizer);

				return new SparseVector(entries);
			}

			return new SparseVector();
		}

		#endregion
	}
}
