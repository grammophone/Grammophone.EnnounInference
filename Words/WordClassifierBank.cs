using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.GenericContentModel;
using Gramma.Inference.Configuration;
using Gramma.LanguageModel;
using Gramma.LanguageModel.Grammar;
using Gramma.LanguageModel.Provision.EditCommands;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Threading;
using Gramma.LanguageModel.TrainingSources;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// A collection of classifiers for various classes of type <see cref="CommandSequenceClass"/>,
	/// as they appear in the training data.
	/// </summary>
	[Serializable]
	public class WordClassifierBank : InferenceResourceItem, IDeserializationCallback
	{
		#region Auxillary classes

		/// <summary>
		/// Score options for <see cref="WordClassifierBank.GetAnalogiesScoreBank"/> method.
		/// </summary>
		[Serializable]
		public class AnalogiesScoreOptions
		{
			#region Private fields

			private DistanceFalloffFunction distanceFalloffFunction;

			#endregion

			#region Construction

			/// <summary>
			/// Create.
			/// </summary>
			public AnalogiesScoreOptions()
			{
				this.MaxNormalizedEditDistance = 1.4;
				this.distanceFalloffFunction = new ReciprocalFalloffFunction(1.0);
			}

			#endregion

			#region Public properties

			/// <summary>
			/// The maximum normalized edit distance to search for analogies.
			/// Normalized edit distance is the absolute distance plus the reciprocal of the length of syllables of the 
			/// word being searched.
			/// Default is 1.4;
			/// </summary>
			public double MaxNormalizedEditDistance { get; set; }

			/// <summary>
			/// The gravity falloff function in terms of the edit distance.
			/// Default is <see cref="ReciprocalFalloffFunction"/> with <c>λ = 1.0</c>.
			/// </summary>
			public DistanceFalloffFunction DistanceFalloffFunction
			{
				get
				{
					return distanceFalloffFunction;
				}
				set
				{
					if (value == null) throw new ArgumentNullException("value");
					distanceFalloffFunction = value;
				}
			}

			#endregion
		}

		#endregion

		#region Private fields

		[NonSerialized]
		private IReadOnlyMultiMap<TagType, WordClassifier> classifiersByTagType;

		[NonSerialized]
		private IReadOnlyMultiDictionary<SyllabicWord, WordFeature> residualFeaturesDictionary;

		/// <summary>
		/// Indicates whether the word feature IDs in <see cref="WordsFeaturesDictionary"/>
		/// are compressed IDs (tag IDs) or
		/// expanded IDs (command sequence class IDs).
		/// </summary>
		private bool areDictionaryFeaturesCondensed;

		private int featureIDsCount;

		/// <summary>
		/// This is used to recover the word feature IDs when switching from compressed IDs (tag IDs) to
		/// expanded IDs (command sequence class IDs) in <see cref="WordsFeaturesDictionary"/>.
		/// </summary>
		private Dictionary<CommandSequenceClass, int> dictionaryClassesToExpandedIDs;

		#endregion

		#region Construction

		internal WordClassifierBank(
			InferenceResource inferenceResource,
			IEnumerable<WordClassifier> classifiers,
			TaggedWordFormTrainer.TrainDataArtifacts trainDataArtifacts)
			: base(inferenceResource)
		{
			if (classifiers == null) throw new ArgumentNullException("classifiers");
			if (trainDataArtifacts == null) throw new ArgumentNullException("trainDataArtifacts");

			var classifierFeatureSamples = trainDataArtifacts.ClassifierFeaturesSamples;
			var dictionaryFeatureSamples = trainDataArtifacts.DictionaryFeaturesSamples;

			// Force training by enumerating the classifiers LINQ projection.
			var classifiersArray = from classifier in classifiers.ToArray()
														 orderby classifier.Feature.ID
														 select classifier;

			this.Classifiers = new OrderedKeyedReadOnlyChildren<WordClassifierBank, CommandSequenceClass, WordClassifier>(
				this, classifiersArray);
			
			this.classifiersByTagType = new ReadOnlyMultiMap<TagType, WordClassifier>(classifiersArray);

			// Make sure that the items under a key remain in order.
			// For some reason, at least in .NET FRamework 4.0, 
			// the ReadOnlyBag<WordFeature>, which uses internally HashSet<WordFeature>,
			// keeps the items enumerable in insertion order, as long as no removal takes place.
			// But this behaviour is not documented and is prone to break when the code runs 
			// in later versions of the .NET framework.
			// So, use a ReadOnlySequence<WordFeature> instead.
			this.WordsFeaturesDictionary = new ReadOnlyMultiDictionary<SyllabicWord, WordFeature, ReadOnlySequence<WordFeature>>(
				dictionaryFeatureSamples.SelectMany(
					fs => fs.Samples, 
					(entry, sample) => new ReadOnlyKeyValuePair<SyllabicWord, WordFeature>(sample.WordSyllables, entry.Feature)
				)
			);

			this.areDictionaryFeaturesCondensed = false;

			this.featureIDsCount = trainDataArtifacts.ClassifierFeaturesCount + trainDataArtifacts.DictionaryFeaturesCount;

			this.dictionaryClassesToExpandedIDs = dictionaryFeatureSamples.ToDictionary(fs => fs.Feature.Class, fs => fs.Feature.ID);

			BuildDependentDictionaries();

			/*
#if DEBUG
			bool testWordFoundInSource =
				dictionaryFeatureSamples.SelectMany(
					fs => fs.Samples
				)
				.Any(sample => sample.WordSyllables.ToString() == "$ , ");

			if (!testWordFoundInSource) Debug.WriteLine("The word '$ , ' was not found in the supplied words data source.");

			SyllabicWord testWord =
				this.WordsFeaturesDictionary.Keys.FirstOrDefault(word => word.ToString() == "$ , ");

			if (testWord == null)
			{
				Debug.WriteLine("The word '$ , ' was not found in the dictionary.");
			}
			else
			{
				Debug.WriteLine("Hash code for '$ , ' in data source is {0}.", testWord.GetHashCode());

				if (!this.WordsFeaturesDictionary.ContainsKey(testWord))
					Debug.WriteLine("The test word '$ , ' was not retrieved successfully using the dictionary.");
			}
#endif
			*/
		}

		private void BuildDependentDictionaries()
		{
			this.residualFeaturesDictionary = new ReadOnlyMultiDictionary<SyllabicWord, WordFeature, ReadOnlySequence<WordFeature>>(
				this.WordsFeaturesDictionary
				.SelectMany(
					entry => entry.Value,
					(entry, feature) => new ReadOnlyKeyValuePair<SyllabicWord, WordFeature>(entry.Key, feature)
				)
				.Where(entry => !this.Classifiers.ContainsKey(entry.Value.Class))
			);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The classifiers of this bank.
		/// </summary>
		public IOrderedKeyedReadOnlyChildren<WordClassifierBank, CommandSequenceClass, WordClassifier> Classifiers { get; private set; }

		/// <summary>
		/// Dictionary of word forms, mapped to their corresponding <see cref="WordFeature"/>s.
		/// </summary>
		public IReadOnlyMultiDictionary<SyllabicWord, WordFeature> WordsFeaturesDictionary { get; private set; }

		/// <summary>
		/// Dictionary of word forms whose features are not scored by the <see cref="Classifiers"/>, 
		/// mapped to their corresponding <see cref="WordFeature"/>s.
		/// </summary>
		public IReadOnlyMultiDictionary<SyllabicWord, WordFeature> ResidualFeaturesDictionary
		{
			get
			{
				return residualFeaturesDictionary;
			}
		}

		/// <summary>
		/// If false, all command sequences in <see cref="WordsFeaturesDictionary"/>
		/// will have their unique feature id as 
		/// their non-exceptional conterparts. If true, their feature ID will 
		/// be changed to match their commonly shared tag.
		/// Default after training is false.
		/// </summary>
		public bool AreDictionaryFeaturesCondensed
		{
			get
			{
				return areDictionaryFeaturesCondensed;
			}
			set
			{
				if (areDictionaryFeaturesCondensed == value) return;

				if (value)
					CondenseDictionaryFeatures();
				else
					ExpandDictionaryFeatures();

				this.areDictionaryFeaturesCondensed = value;
			}
		}

		/// <summary>
		/// The number of distinct feature ID's provided by this bank.
		/// </summary>
		public int FeatureIDsCount
		{
			get { return featureIDsCount; }
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Validate the <see cref="Classifiers"/> against the word samples samples defined 
		/// in <see cref="Setup.ValidationSets"/>.
		/// </summary>
		/// <param name="degreeOfParallelism">The maximum amount of processors to use, or 0 to use all available.</param>
		/// <returns>Returns the mean BAC (BAlanced Accuracy) validation score.</returns>
		/// <remarks>
		/// This method uses parallelization, and produces tracing messages for the individual classifiers.
		/// </remarks>
		public double ValidateClassifiers(int degreeOfParallelism = 0)
		{
			var validationSet = this.GetValidationSet();

			return ValidateClassifiers(validationSet, degreeOfParallelism);
		}

		/// <summary>
		/// Validate the <see cref="Classifiers"/> against tagged word samples defined in a <see cref="ValidationSet"/>.
		/// </summary>
		/// <param name="validationSet">
		/// The set holding the <see cref="ValidationSet.TaggedWordTrainingSources"/> to validate the <see cref="Classifiers"/> against.
		/// </param>
		/// <param name="degreeOfParallelism">The maximum amount of processors to use, or 0 to use all available.</param>
		/// <returns>Returns the mean BAC (BAlanced Accuracy) validation score.</returns>
		/// <remarks>
		/// This method uses parallelization, and produces tracing messages for the individual classifiers.
		/// </remarks>
		public double ValidateClassifiers(ValidationSet validationSet, int degreeOfParallelism = 0)
		{
			if (validationSet == null) throw new ArgumentNullException("validationSet");

			if (validationSet.TaggedWordTrainingSources.Count == 0)
				throw new InferenceException("The validation set contains no tagged word sources.");

			TrainingSource<TaggedWordForm> validationSource;

			if (validationSet.TaggedWordTrainingSources.Count == 1)
				validationSource = validationSet.TaggedWordTrainingSources.First();
			else
				validationSource = new CompositeTrainingSource<TaggedWordForm>(validationSet.TaggedWordTrainingSources, this.InferenceResource.LanguageProvider);

			return ValidateClassifiers(validationSource, degreeOfParallelism);
		}

		/// <summary>
		/// Validate the <see cref="Classifiers"/> against tagged word samples defined in a <see cref="ValidationSet"/>.
		/// </summary>
		/// <param name="validationSource">
		/// The source of <see cref="TaggedWordForm"/>s to validate the <see cref="Classifiers"/> against.
		/// </param>
		/// <param name="degreeOfParallelism">The maximum amount of processors to use, or 0 to use all available.</param>
		/// <returns>Returns the mean BAC (BAlanced Accuracy) validation score.</returns>
		/// <remarks>
		/// This method uses parallelization, and produces tracing messages for the individual classifiers.
		/// </remarks>
		public double ValidateClassifiers(TrainingSource<TaggedWordForm> validationSource, int degreeOfParallelism = 0)
		{
			if (validationSource == null) throw new ArgumentNullException("validationSource");

			using (validationSource)
			{
				Trace.WriteLine("Opening the validation source.");

				validationSource.Open();

				return ValidateClassifiers(validationSource.GetData(), degreeOfParallelism);
			}
		}

		/// <summary>
		/// Validate the <see cref="Classifiers"/> against samples.
		/// </summary>
		/// <param name="validationData">The samples to validate the <see cref="Classifiers"/> against.</param>
		/// <param name="degreeOfParallelism">The maximum amount of processors to use, or 0 to use all available.</param>
		/// <returns>Returns the mean BAC (BAlanced Accuracy) validation score.</returns>
		/// <remarks>
		/// This method uses parallelization, and produces tracing messages for the individual classifiers.
		/// </remarks>
		public double ValidateClassifiers(IEnumerable<TaggedWordForm> validationData, int degreeOfParallelism = 0)
		{
			if (validationData == null) throw new ArgumentNullException("validationData");
			if (degreeOfParallelism < 0) throw new ArgumentException("degreeOfParallelism should not be negative.");

			if (degreeOfParallelism == 0) degreeOfParallelism = Environment.ProcessorCount;

			var trainer = new TaggedWordFormTrainer(this.InferenceResource);

			var validationSamples = from item in validationData.AsParallel().WithDegreeOfParallelism(degreeOfParallelism)
															select trainer.GetTrainingSample(item);

			return ValidateClassifiers(validationSamples.ToArray(), degreeOfParallelism);
		}

		/// <summary>
		/// Validate the <see cref="Classifiers"/> against samples.
		/// </summary>
		/// <param name="validationSamples">The samples to validate the <see cref="Classifiers"/> against.</param>
		/// <param name="degreeOfParallelism">The maximum amount of processors to use, or 0 to use all available.</param>
		/// <returns>Returns the mean BAC (BAlanced Accuracy) validation score.</returns>
		/// <remarks>
		/// This method uses parallelization, and produces tracing messages for the individual classifiers.
		/// </remarks>
		public double ValidateClassifiers(IEnumerable<TaggedWordFormTrainingSample> validationSamples, int degreeOfParallelism = 0)
		{
			if (validationSamples == null) throw new ArgumentNullException("validationSamples");
			if (degreeOfParallelism < 0) throw new ArgumentException("degreeOfParallelism should not be negative.");

			if (this.Classifiers.Count == 0) return 0.0;

			Trace.WriteLine("Validating word classifiers.");

			double totalSum = 0.0;

			var lok = new object();

			var parallelOptions = new System.Threading.Tasks.ParallelOptions();

			if (degreeOfParallelism > 0)
				parallelOptions.MaxDegreeOfParallelism = degreeOfParallelism;
			else
				parallelOptions.MaxDegreeOfParallelism = Environment.ProcessorCount;

			int currentClassifierCount = 0;

			System.Threading.Tasks.Parallel.ForEach(this.Classifiers, parallelOptions, () => 0.0, (classifier, state, localSum) =>
				{
					var commandSequenceClass = classifier.Feature.Class;

					var positiveWords = TaggedWordFormTrainer.GetPositiveWords(validationSamples, commandSequenceClass);

					var filteredSamples = from sample in validationSamples
																where commandSequenceClass.Equals(sample.Class) || !positiveWords.Contains(sample.WordSyllables)
																select sample;

					double score = classifier.Validate(filteredSamples);

					Trace.WriteLine(
						String.Format(
						"Validation score for classifier {0} of {1} for\n{2}is {3:F4}.", 
						Interlocked.Increment(ref currentClassifierCount),
						this.Classifiers.Count,
						classifier.Feature.Class.Tag, 
						score));

					return localSum + score;
				},
				delegate(double localSum)
				{
					lock (lok)
					{
						totalSum += localSum;
					}
				});

			return totalSum / (double)this.Classifiers.Count;
		}

		/// <summary>
		/// Get the bank of raw scores for a given word.
		/// Don't use correlation.
		/// </summary>
		/// <param name="word">The word to check.</param>
		/// <param name="dictionaryAppendingOption">Describes what additional scores to include from dictionaries.</param>
		/// <returns>Returns the bank of scores for each class.</returns>
		public ScoreBank GetScoreBank(
			SyllabicWord word, 
			DictionaryAppendingOption dictionaryAppendingOption = DictionaryAppendingOption.Full)
		{
			if (word == null) throw new ArgumentNullException("word");

#if DEBUG
			if (!this.WordsFeaturesDictionary.ContainsKey(word))
			{
				Debug.WriteLine("Unknown word: {0}", word);
			}
#endif

			var wordSyllables = word.ToArray();

			var scoresFromClassifiers = from classifier in this.Classifiers
																	let scoreValue = classifier.GetScoreValue(wordSyllables)
																	where scoreValue > 0.0
																	select new Score(classifier.Feature, scoreValue);

			IReadOnlyMultiDictionary<SyllabicWord, WordFeature> dictionary = GetDictionaryForAppendingOption(dictionaryAppendingOption);

			var scoresFromWordsFeaturesDictionary = from item in dictionary[word]
																							select new Score(item, 10.0);

			return new ScoreBank(word, scoresFromClassifiers, scoresFromWordsFeaturesDictionary);
		}

		/// <summary>
		/// Get the bank of raw scores for a given word focused only on a given tag type.
		/// Don't use correlation.
		/// </summary>
		/// <param name="word">The word to check.</param>
		/// <param name="tagType">The tag type to focus on.</param>
		/// <param name="dictionaryAppendingOption">Describes what additional scores to include from dictionaries.</param>
		/// <returns>Returns the bank of scores for each class having a tag with a type equal to <paramref name="tagType"/>.</returns>
		public ScoreBank GetScoreBank(
			SyllabicWord word, 
			TagType tagType, 
			DictionaryAppendingOption dictionaryAppendingOption = DictionaryAppendingOption.Full)
		{
			if (word == null) throw new ArgumentNullException("word");
			if (tagType == null) throw new ArgumentNullException("tagType");

#if DEBUG
			if (!this.WordsFeaturesDictionary.ContainsKey(word))
			{
				Debug.WriteLine("Unknown word: {0}", word);
			}
#endif

			var wordSyllables = word.ToArray();

			var scoresFromClassifiers = from classifier in classifiersByTagType[tagType]
																	let scoreValue = classifier.GetScoreValue(wordSyllables)
																	where scoreValue > 0.0
																	select new Score(classifier.Feature, scoreValue);

			IReadOnlyMultiDictionary<SyllabicWord, WordFeature> dictionary = GetDictionaryForAppendingOption(dictionaryAppendingOption);

			var scoresFromWordsFeaturesDictionary = from feature in dictionary[word]
																							where feature.Class.Tag.Type.Equals(tagType)
																							select new Score(feature, 10.0);

			return new ScoreBank(word, scoresFromClassifiers, scoresFromWordsFeaturesDictionary);
		}

		/// <summary>
		/// Get a bank of scores for a word, taking into account the analogies of the word's neighbors.
		/// </summary>
		/// <param name="word">The word to check.</param>
		/// <param name="options">The options for seeking and scoring the analogies of the word's neighbors.</param>
		/// <param name="dictionaryAppendingOption">Describes what additional scores to include from dictionaries.</param>
		/// <returns>Returns the bank of scores for each class.</returns>
		/// <remarks>
		/// Uses processor parallelization.
		/// </remarks>
		public ScoreBank GetAnalogiesScoreBank(SyllabicWord word, AnalogiesScoreOptions options, DictionaryAppendingOption dictionaryAppendingOption)
		{
			var wordFormsDictionary = this.InferenceResource.WordFormsDictionary;

			if (wordFormsDictionary == null)
				throw new InferenceException("The InferenceResource.WordFormsDictionary has not been defined.");

			// Get the basic scores with no correlation.
			var basicScoreBank = GetScoreBank(word, dictionaryAppendingOption);

			// Denormalize the maximum edit distance. Decrease the search radius for small words.
			double maxEditDistance = options.MaxNormalizedEditDistance - 1.0 / (double)Math.Max(word.Count - 1, 1);

			// Find the neighbors of the word.
			var neighborResults = wordFormsDictionary.GetNeighbours(word, maxEditDistance);

			// Correlate. For a score entry, if a neighbor word is classified to an edit command which leads to the same lemma of 
			// a basic score entry, reinforce its score with the neighbor score, attenuated by the DistanceWeight.
			foreach (var basicScore in basicScoreBank.Scores)
			{
				var proposedTag = basicScore.Feature.Class.Tag;

				if (proposedTag.Type.AreTagsUnrelated) continue;

				var proposedLemma = basicScore.Feature.Class.Sequence.Execute(word);

				foreach (var neighborResult in neighborResults)
				{
					if (neighborResult.Word.Equals(word)) continue;

					var neighborScoreBank = GetScoreBank(neighborResult.Word, proposedTag.Type, dictionaryAppendingOption);

					double distanceWeight = options.DistanceFalloffFunction.Compute(neighborResult.EditDistance);

					foreach (var neighborScore in neighborScoreBank.Scores)
					{
						var proposedLemmaByNeighbor = neighborScore.Feature.Class.Sequence.Execute(neighborScoreBank.Word);

						if (proposedLemma.Equals(proposedLemmaByNeighbor))
						{
							basicScore.ScoreValue += distanceWeight * neighborScore.ScoreValue;
						}
					}
				}
			}

			return basicScoreBank;
		}

		#endregion

		#region IDeserializationCallback implementation

		void IDeserializationCallback.OnDeserialization(object sender)
		{
			this.classifiersByTagType = new ReadOnlyMultiMap<TagType, WordClassifier>(this.Classifiers);

			BuildDependentDictionaries();
		}

		#endregion

		#region Private methods

		private void CondenseDictionaryFeatures()
		{
			int newFeatureID = this.Classifiers.Count;

			var tagsToFeatureIDs = new Dictionary<Tag, int>();

			foreach (var entry in this.WordsFeaturesDictionary)
			{
				foreach (var wordFeature in entry.Value)
				{
					var tag = wordFeature.Class.Tag;

					int existingFeatureID;

					if (tagsToFeatureIDs.TryGetValue(tag, out existingFeatureID))
					{
						wordFeature.ID = existingFeatureID;
					}
					else
					{
						wordFeature.ID = newFeatureID;
						tagsToFeatureIDs[tag] = newFeatureID;

						newFeatureID++;
					}
				}
			}

			this.featureIDsCount = newFeatureID;
		}

		private void ExpandDictionaryFeatures()
		{
			int singularFeaturesBaseID = this.Classifiers.Count;

			foreach (var entry in this.WordsFeaturesDictionary)
			{
				foreach (var wordFeature in entry.Value)
				{
					int id;

					if (dictionaryClassesToExpandedIDs.TryGetValue(wordFeature.Class, out id))
					{
						wordFeature.ID = id;
					}
					else
					{
						throw new InferenceException("Could not map singular word feature to back to expanded ID.");
					}
				}
			}

			this.featureIDsCount = this.Classifiers.Count + dictionaryClassesToExpandedIDs.Count;
		}

		private IReadOnlyMultiDictionary<SyllabicWord, WordFeature> GetDictionaryForAppendingOption(DictionaryAppendingOption dictionaryAppendingOption)
		{
			switch (dictionaryAppendingOption)
			{
				case DictionaryAppendingOption.Full:
					return this.WordsFeaturesDictionary;

				case DictionaryAppendingOption.ResidualOnly:
					return this.ResidualFeaturesDictionary;

				default:
					throw new ArgumentException(String.Format("Unsupported dictionary option: '{0}'.", dictionaryAppendingOption));
			}
		}

		#endregion
	}
}
