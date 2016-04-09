using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.CRF;
using Grammophone.LanguageModel.Grammar;
using Grammophone.EnnounInference.Words;
using Grammophone.Vectors;
using System.Collections.Concurrent;

namespace Grammophone.EnnounInference.Sentences
{
	/// <summary>
	/// Provides the feature functions for use with the <see cref="LanguageCRF"/>
	/// in <see cref="SentenceClassifier"/>.
	/// </summary>
	internal class LanguageFeatureFunctionsProvider : ConstrainedLinearChainCRF<string[], Tag>.FeatureFunctionsProvider
	{
		#region Protected fields

		/// <summary>
		/// The feature functions provider factory.
		/// </summary>
		protected readonly LanguageFeatureFunctionsProviderFactory factory;

		/* 
		 * Scale down the values of the feature function vector in order to prevent overflow and underflow
		 * when exponentiating the expression gi(., .) = w' * f(x, ., ., i).
		 */
		/// <summary>
		/// Used for normalizing feature function values in order to keep 
		/// exponentations exp gi(., .) = exp(w' * f(x, ., ., i)) within reasonable arithmetic bounds.
		/// </summary>
		protected readonly double normalizer;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="input">The input sequence.</param>
		/// <param name="factory">The factory which creates this provider.</param>
		/// <param name="scope">Gives a hint whether we are in training scope or not.</param>
		public LanguageFeatureFunctionsProvider(
			string[] input,
			LanguageFeatureFunctionsProviderFactory factory,
			EvaluationScope scope)
			: base(input)
		{
			if (input == null) throw new ArgumentNullException("inputSequence");
			if (factory == null) throw new ArgumentNullException("factory");

			this.factory = factory;
			this.Scope = scope;

			this.ScoreBanks = new ScoreBank[input.Length];

			switch (scope)
			{
				case EvaluationScope.Running:
					{
						var inputPartitioner = Partitioner.Create(0, input.Length, 1);

						System.Threading.Tasks.Parallel.ForEach(inputPartitioner, range =>
							{
								for (int i = range.Item1; i < range.Item2; i++)
								{
									this.ScoreBanks[i] = factory.BanksCache.Get(input[i]);
								}
							});
					}
					break;
				
				default:
					for (int i = 0; i < input.Length; i++)
					{
						this.ScoreBanks[i] = factory.BanksCache.Get(input[i]);
					}
					break;
			}

			this.normalizer = 10.0 / factory.FeatureFunctionsCount;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Gives a hint whether we are in training scope or not.
		/// </summary>
		public EvaluationScope Scope { get; private set; }

		/// <summary>
		/// The score banks corresponding to each word of the input.
		/// </summary>
		public ScoreBank[] ScoreBanks { get; private set; }

		#endregion

		#region Public methods

		/// <summary>
		/// Get the scores of a word in the sentence for a given tag, using the implied scoring policy.
		/// </summary>
		/// <param name="i">The index of the word within the sentence.</param>
		/// <param name="yi">The tag for which to return the scores.</param>
		/// <returns>Returns the appropriate collection of scores.</returns>
		public GenericContentModel.IReadOnlyBag<Score> GetScores(int i, Tag yi)
		{
			var scoreBank = this.ScoreBanks[i];

			GenericContentModel.IReadOnlyBag<Score> scores;

			switch (factory.WordScoringPolicy)
			{
				case WordScoringPolicy.Prioritized:
					scores = scoreBank.GetPrioritizedScoresByTag(yi);
					break;

				case WordScoringPolicy.Proportional:
				case WordScoringPolicy.Mixed:
					scores = scoreBank.GetMixedScoresByTag(yi);
					break;

				default:
					throw new InferenceException(String.Format("Unsupported scoring type '{0}'", factory.WordScoringPolicy));
			}

			return scores;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Provides the bigram feature functions.
		/// </summary>
		protected override LinearChainCRF<string[], Tag>.BigramVectorFeatureFunction CreateBigramVectorFeatureFunction()
		{
			return this.BigramFeatureFunction;
		}

		/// <summary>
		/// Provides the unigram feature functions.
		/// </summary>
		protected override LinearChainCRF<string[], Tag>.UnigramVectorFeatureFunction CreateUnigramVectorFeatureFunction()
		{
			return this.UnigramFeatureFunction;
		}

		/// <summary>
		/// Returns the length of the output sequence as 
		/// the length of the Input.
		/// </summary>
		protected override int GetOutputLength()
		{
			return this.Input.Length;
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
					var entries = new List<SparseVector.Entry>(2 * scores.Count + 1);

					int previousFeatureID;

					int j = 0;

					previousFeatureID = -1;

					foreach (var score in scores)
					{
						int featureID = score.Feature.ID;

						if (featureID != previousFeatureID)
						{
							entries.Add(new SparseVector.Entry(featureID, score.ScoreValue * normalizer));
							j++;
							previousFeatureID = featureID;
						}
						else
						{
							entries[j - 1] = new SparseVector.Entry(featureID, entries[j - 1].Value + score.ScoreValue * normalizer);
						}

					}

					j = 0;

					previousFeatureID = -1;

					foreach (var score in scores)
					{
						int featureID = score.Feature.ID;

						if (featureID != previousFeatureID)
						{
							entries.Add(new SparseVector.Entry(biasesOffset + featureID, normalizer));
							j++;
							previousFeatureID = featureID;
						}
						else
						{
							entries[j - 1] = new SparseVector.Entry(featureID, entries[j - 1].Value + normalizer);
						}

					}

					// Global bias.
					entries.Add(new SparseVector.Entry(factory.FeatureFunctionsCount - 1, normalizer));

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

		private SparseVector BigramFeatureFunction(Tag yim1, Tag yi, string[] x, int i)
		{
			var entries = new List<SparseVector.Entry>(2);

			int biasesOffset = factory.BiasesOffset;

			int biGramFeatureID;

			if (factory.BiGramFeatureIndices.TryGetValue(new Tuple<Tag, Tag>(yim1, yi), out biGramFeatureID))
			{
				bool matchedFirst, matchedSecond;

				if (i == 0)
				{
					matchedFirst = true;
				}
				else
				{
					var firstScores = GetScores(i - 1, yim1);

					// Positive singular features are covered by this because they always have a score of 10.
					matchedFirst = firstScores.Any(score => score.ScoreValue >= 1.0);
				}

				if (i == x.Length)
				{
					matchedSecond = true;
				}
				else
				{
					var secondScores = GetScores(i, yi);

					// Positive singular features are covered by this because they always have a score of 10.
					matchedSecond = secondScores.Any(score => score.ScoreValue >= 1.0);
				}

				if (matchedFirst && matchedSecond)
				{
					entries.Add(new SparseVector.Entry(biGramFeatureID, normalizer));
				}
				else if (!matchedFirst && !matchedSecond)
				{
					entries.Add(new SparseVector.Entry(biGramFeatureID, -normalizer));
				}

				entries.Add(new SparseVector.Entry(biasesOffset + biGramFeatureID, normalizer));
			}

			return new SparseVector(entries);
		}

		#endregion
	}
}
