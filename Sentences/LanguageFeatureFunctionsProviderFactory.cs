using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grammophone.Caching;
using Grammophone.CRF;
using Grammophone.EnnounInference.Words;
using Grammophone.LanguageModel.Grammar;
using System.Runtime.Serialization;
using Grammophone.Vectors;

namespace Grammophone.EnnounInference.Sentences
{
	/// <summary>
	/// Provides <see cref="LanguageFeatureFunctionsProvider"/> instances
	/// for <see cref="LanguageCRF"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The feature functions are:
	/// </para>
	/// <para>
	/// <list type="bullet">
	/// <item>
	/// <description>Command sequence unigram functions from neurons and exception dictionaries.</description>
	/// </item>
	/// <item>
	/// <description>END indicator.</description>
	/// </item>
	/// <item>
	/// <description>Bigram indicators.</description>
	/// </item>
	/// <item>
	/// <description>Command sequence unigram biases.</description>
	/// </item>
	/// <item>
	/// <description>END bias.</description>
	/// </item>
	/// <item>
	/// <description>Bigram biases.</description>
	/// </item>
	/// </list>
	/// </para>
	/// </remarks>
	[Serializable]
	internal class LanguageFeatureFunctionsProviderFactory : LinearChainCRF<string[], Tag>.FeatureFunctionsProviderFactory, IDeserializationCallback
	{
		#region Private fields

		private int featureFunctionsCount;

		private WordClassifierBank.AnalogiesScoreOptions analogiesScoreOptions;

		[NonSerialized]
		private MRUCache<string[], LanguageFeatureFunctionsProvider> runningProvidersCache;

		[NonSerialized]
		private MRUCache<string, ScoreBank> banksCache;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="inferenceResource">The inference resource.</param>
		/// <param name="tagBiGrams">The allowed tag bi-grams.</param>
		/// <param name="wordScoringPolicy">Discriminates how a word is scored by a its <see cref="Words.ScoreBank"/>.</param>
		/// <param name="analogiesScoreOptions">The analogy score options or null for no analogy scoring.</param>
		public LanguageFeatureFunctionsProviderFactory(
			InferenceResource inferenceResource, 
			Tuple<Tag, Tag>[] tagBiGrams, 
			WordScoringPolicy wordScoringPolicy, 
			WordClassifierBank.AnalogiesScoreOptions analogiesScoreOptions)
		{
			if (inferenceResource == null) throw new ArgumentNullException("inferenceResource");
			if (tagBiGrams == null) throw new ArgumentNullException("tagBiGrams");

			var wordClassifierBank = inferenceResource.WordClassifierBank;

			if (wordClassifierBank == null)
				throw new InferenceException("Tagged words have not been trained.");

			if (analogiesScoreOptions != null && inferenceResource.WordFormsDictionary == null)
				throw new InferenceException("Untagged words have not been trained or loaded. These should be present when analogies core options are specified.");

			this.InferenceResource = inferenceResource;
			this.TagBiGrams = tagBiGrams;
			this.analogiesScoreOptions = analogiesScoreOptions;
			this.WordScoringPolicy = wordScoringPolicy;

			this.EndTag = inferenceResource.LanguageProvider.EndTag;

			// Feature function space segmentation.
			this.UnigramIndicatorsOffset = 0;

			this.EndIndicatorOffset = this.UnigramIndicatorsOffset + wordClassifierBank.FeatureIDsCount;

			this.BigramIndicatorsOffset = this.EndIndicatorOffset + 1;

			this.BiasesOffset = this.BigramIndicatorsOffset + tagBiGrams.Length;

			this.UnigramBiasesOffset = this.BiasesOffset + this.UnigramIndicatorsOffset;

			this.EndBiasOffset = this.EndIndicatorOffset + this.EndIndicatorOffset;

			this.BigramBiasesOffset = this.BiasesOffset + this.BigramIndicatorsOffset;

			// Bigram feature functions indexing.
			this.BiGramFeatureIndices = new Dictionary<Tuple<Tag, Tag>, int>();

			for (int i = 0; i < tagBiGrams.Length; i++)
			{
				var biGram = tagBiGrams[i];

				this.BiGramFeatureIndices[biGram] = this.BigramIndicatorsOffset + i;
			}

			this.featureFunctionsCount = 
				2 * (wordClassifierBank.FeatureIDsCount + this.TagBiGrams.Length + 1) + 1;

			InitializeCaches();
		}

		private void InitializeCaches()
		{
			this.runningProvidersCache = new MRUCache<string[], LanguageFeatureFunctionsProvider>(this.CreateRunningProvider, 128);

			this.banksCache = new MRUCache<string, ScoreBank>(this.CreateScoreBank, 1024);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The number of feature functions.
		/// </summary>
		/// <remarks>
		/// This is equal to the count of the word classifiers plus
		/// the count of singular words features plus
		/// the number of tag tuples.
		/// </remarks>
		public override int FeatureFunctionsCount
		{
			get { return featureFunctionsCount; }
		}

		/// <summary>
		/// The inference resource associated with this factory.
		/// </summary>
		public InferenceResource InferenceResource
		{
			get;
			private set;
		}

		/// <summary>
		/// The allowed tag bi-grams.
		/// </summary>
		public Tuple<Tag, Tag>[] TagBiGrams
		{
			get; 
			private set;
		}

		/// <summary>
		/// Dictionary mapping bi-grams to their feature index.
		/// </summary>
		public IDictionary<Tuple<Tag, Tag>, int> BiGramFeatureIndices { get; private set; }

		/// <summary>
		/// The cache of <see cref="LanguageFeatureFunctionsProvider"/>s used during running.
		/// Its default size is 128, and can be changed by setting <see cref="MRUCache{K, V}.MaxCount"/>.
		/// </summary>
		public MRUCache<string[], LanguageFeatureFunctionsProvider> RunningProvidersCache 
		{
			get
			{
				return runningProvidersCache;
			}
		}

		/// <summary>
		/// The cache of <see cref="ScoreBank"/>s.
		/// Its default size is 1024, and can be changed by setting <see cref="MRUCache{K, V}.MaxCount"/>.
		/// </summary>
		public MRUCache<string, ScoreBank> BanksCache 
		{
			get
			{
				return banksCache;
			}
		}

		/// <summary>
		/// The offset where all bias feature functions start.
		/// </summary>
		public int BiasesOffset { get; private set; }

		/// <summary>
		/// The offset where unigram indicator fucntions start.
		/// </summary>
		public int UnigramIndicatorsOffset { get; private set; }

		/// <summary>
		/// The offset where bigram indicator fucntions start.
		/// </summary>
		public int BigramIndicatorsOffset { get; private set; }

		/// <summary>
		/// The offset of the END indicator.
		/// </summary>
		public int EndIndicatorOffset { get; private set; }

		/// <summary>
		/// The offset where unigram biases start.
		/// </summary>
		public int UnigramBiasesOffset { get; private set; }

		/// <summary>
		/// The offset where bigram biases start.
		/// </summary>
		public int BigramBiasesOffset { get; private set; }

		/// <summary>
		/// The offset of the END bias.
		/// </summary>
		public int EndBiasOffset { get; private set; }

		/// <summary>
		/// The END tag.
		/// </summary>
		public Tag EndTag { get; private set; }

		/// <summary>
		/// Discriminates how a word is scored by a its <see cref="Words.ScoreBank"/>. 
		/// </summary>
		public WordScoringPolicy WordScoringPolicy { get; private set; }

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
		/// by <see cref="RunningProvidersCache"/> or created directly.
		/// </remarks>
		public override LinearChainCRF<string[], Tag>.FeatureFunctionsProvider GetProvider(string[] input, EvaluationScope scope)
		{
			if (input == null) throw new ArgumentNullException("input");

			switch (scope)
			{
				case EvaluationScope.Training:
					return new LanguageFeatureFunctionsProvider(input, this, EvaluationScope.Training);

				default:
					return this.RunningProvidersCache.Get(input);
			}
		}

		/// <summary>
		/// Get initial weights suggestion to start optimization algorithms.
		/// The weights are set to a small positive value.
		/// </summary>
		public override Vector GetInitialWeights()
		{
			var initialWeights = new double[this.FeatureFunctionsCount];

			double normalizedPositive = 10.0 / this.FeatureFunctionsCount;

			for (int i = 0; i < this.BiasesOffset; i++)
			{
				initialWeights[i] = normalizedPositive;
			}

			return initialWeights;
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Create a fresh provider for the given <paramref name="input"/>.
		/// Used by <see cref="RunningProvidersCache"/> during cache miss.
		/// </summary>
		/// <param name="input">The input sequence.</param>
		/// <returns>Returns the provider.</returns>
		protected virtual LanguageFeatureFunctionsProvider CreateRunningProvider(string[] input)
		{
			return new LanguageFeatureFunctionsProvider(input, this, EvaluationScope.Running);
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Create a fresh <see cref="ScoreBank"/>.
		/// Used by <see cref="BanksCache"/>
		/// during cache miss.
		/// </summary>
		/// <param name="word">The word to be scored.</param>
		/// <returns>Returns the score bank.</returns>
		private ScoreBank CreateScoreBank(string word)
		{
			if (word == null) throw new ArgumentNullException("word");

			var syllabicWord = this.InferenceResource.LanguageProvider.Syllabizer.Segment(word);

			var wordClassifierBank = this.InferenceResource.WordClassifierBank;

			if (analogiesScoreOptions != null)
			{
				switch (this.WordScoringPolicy)
				{
					case WordScoringPolicy.Proportional:
						return wordClassifierBank.GetAnalogiesScoreBank(syllabicWord, analogiesScoreOptions, DictionaryAppendingOption.ResidualOnly);

					default:
						return wordClassifierBank.GetAnalogiesScoreBank(syllabicWord, analogiesScoreOptions, DictionaryAppendingOption.Full);
				}
			}
			else
			{
				switch (this.WordScoringPolicy)
				{
					case WordScoringPolicy.Proportional:
						return wordClassifierBank.GetScoreBank(syllabicWord, DictionaryAppendingOption.ResidualOnly);

					default:
						return wordClassifierBank.GetScoreBank(syllabicWord, DictionaryAppendingOption.Full);
				}

			}
		}

		#endregion

		void IDeserializationCallback.OnDeserialization(object sender)
		{
			InitializeCaches();
		}
	}
}
