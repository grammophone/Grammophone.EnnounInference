using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Grammophone.Indexing;
using Grammophone.EnnounInference.Configuration;
using Grammophone.LanguageModel;
using Grammophone.LanguageModel.Provision;
using Grammophone.LanguageModel.Provision.EditCommands;
using Grammophone.LanguageModel.TrainingSources;
using Grammophone.Parallel;

namespace Grammophone.EnnounInference.Words
{
	/// <summary>
	/// Provides training services for tagged word forms.
	/// </summary>
	internal class TaggedWordFormTrainer : InferenceResourceItem
	{
		#region Auxilliary types

		/// <summary>
		/// Represents a feature and known positive samples that possess it.
		/// </summary>
		internal class FeatureSamples
		{
			internal FeatureSamples(WordFeature feature, IEnumerable<TaggedWordFormTrainingSample> samples)
			{
				if (feature == null) throw new ArgumentNullException("feature");
				if (samples == null) throw new ArgumentNullException("samples");

				this.Feature = feature;
				this.Samples = samples;
			}

			/// <summary>
			/// The feature.
			/// </summary>
			public WordFeature Feature { get; private set; }

			/// <summary>
			/// The samples which are positive for the <see cref="Feature"/>.
			/// </summary>
			public IEnumerable<TaggedWordFormTrainingSample> Samples { get; private set; }
		}

		/// <summary>
		/// Used to split the total training samples into those non singular 
		/// whose class is above dropout frequency (for training classifiers) 
		/// and to singular ones (for building a simple dictionary).
		/// </summary>
		internal class TrainDataArtifacts
		{
			internal TrainDataArtifacts(TaggedWordFormTrainingSample[] totalSamples, double dropout, int decimation)
			{
				if (totalSamples == null) throw new ArgumentNullException("totalSamples");

				var uniqueSamples = totalSamples.Distinct();

				var filteredQuery = from sample in uniqueSamples
														group sample by sample.Class into classGroup
														let fraction = (double)classGroup.Count() / (double)totalSamples.Length
														where !classGroup.Key.Tag.Type.AreTagsUnrelated && fraction > dropout
														orderby fraction descending
														select new { CommandSequenceClass = classGroup.Key, ClassSamples = classGroup };

				var residualQuery = from sample in uniqueSamples
														group sample by sample.Class into classGroup
														let fraction = (double)classGroup.Count() / (double)totalSamples.Length
														where !(!classGroup.Key.Tag.Type.AreTagsUnrelated && fraction > dropout)
														orderby fraction descending
														select new { CommandSequenceClass = classGroup.Key, ClassSamples = classGroup };

				this.CommonSamples = filteredQuery.SelectMany(group => group.ClassSamples);

				this.ExceptionalSamples = residualQuery.SelectMany(group => group.ClassSamples);

				this.ClassifierFeaturesCount = filteredQuery.Count();

				this.DictionaryFeaturesCount = this.ClassifierFeaturesCount + residualQuery.Count();

				this.ClassifierFeaturesSamples = filteredQuery
					.Select((group, i) => new FeatureSamples(
						new WordFeature(i, group.CommandSequenceClass), group.ClassSamples));

				this.DictionaryFeaturesSamples = filteredQuery.Concat(residualQuery)
					.Select((group, i) => new FeatureSamples(
						new WordFeature(this.ClassifierFeaturesCount + i, group.CommandSequenceClass), group.ClassSamples));

				this.CommonClasses = filteredQuery.Select(group => group.CommandSequenceClass);

				this.ExceptionalClasses = residualQuery.Select(group => group.CommandSequenceClass);
			}

			/// <summary>
			/// The samples whose tags are not singular and have class frequency above dropout.
			/// </summary>
			public IEnumerable<TaggedWordFormTrainingSample> CommonSamples { get; private set; }

			/// <summary>
			/// The remaining samples when <see cref="CommonSamples"/> is deducted from the total samples.
			/// These are the samples whose tags are either singular or have class frequency below dropout.
			/// </summary>
			public IEnumerable<TaggedWordFormTrainingSample> ExceptionalSamples { get; private set; }

			/// <summary>
			/// The count of items in <see cref="ClassifierFeaturesSamples"/>.
			/// </summary>
			public int ClassifierFeaturesCount { get; private set; }

			/// <summary>
			/// The count of items in <see cref="DictionaryFeaturesSamples"/>.
			/// </summary>
			public int DictionaryFeaturesCount { get; private set; }

			/// <summary>
			/// The unique features and samples produced by <see cref="CommonSamples"/>.
			/// </summary>
			public IEnumerable<FeatureSamples> ClassifierFeaturesSamples { get; private set; }

			/// <summary>
			/// The features and samples produced 
			/// by both <see cref="CommonSamples"/> and <see cref="ExceptionalSamples"/>.
			/// </summary>
			public IEnumerable<FeatureSamples> DictionaryFeaturesSamples { get; private set; }

			/// <summary>
			/// The classes which are not singular and have class frequency above dropout.
			/// </summary>
			public IEnumerable<CommandSequenceClass> CommonClasses { get; private set; }

			/// <summary>
			/// The classes which are either singular or have class frequency below dropout.
			/// </summary>
			public IEnumerable<CommandSequenceClass> ExceptionalClasses { get; private set; }
		}

		#endregion

		#region Private fields

		private int totalClassifiersCount;

		private int trainedClassifiersCount;

		private ConcurrentDictionary<CommandSequenceClass, CommandSequenceClass> commandSequenceClassesCache;

		private ConcurrentDictionary<CommandSequence, CommandSequence> commandSequencesCache;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		internal TaggedWordFormTrainer(InferenceResource inferenceResource)
			: base(inferenceResource)
		{
			commandSequenceClassesCache = new ConcurrentDictionary<CommandSequenceClass, CommandSequenceClass>();

			commandSequencesCache = new ConcurrentDictionary<CommandSequence, CommandSequence>();
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Get the words which have the same command sequence to their lemma.
		/// </summary>
		/// <param name="totalSamples">The training samples containing the words and corresponding command sequences.</param>
		/// <param name="commandSequenceClass">The command sequence to test against.</param>
		/// <returns>Returns a set of the words having the given <paramref name="commandSequenceClass"/> for lemmatization.</returns>
		public static ISet<SyllabicWord> GetPositiveWords(IEnumerable<TaggedWordFormTrainingSample> totalSamples, CommandSequenceClass commandSequenceClass)
		{
			var positiveWordsQuery = from sample in totalSamples
															 where sample.Class.Equals(commandSequenceClass)
															 select sample.WordSyllables;

			var positiveWords = new HashSet<SyllabicWord>(positiveWordsQuery);

			return positiveWords;
		}

		/// <summary>
		/// Get the command sequence that transforms <paramref name="sourceForm"/>
		/// into <paramref name="targetLemma"/>.
		/// </summary>
		public CommandSequence GetSequence(SyllabicWord sourceForm, SyllabicWord targetLemma)
		{
			if (sourceForm == null) throw new ArgumentNullException("sourceForm");
			if (targetLemma == null) throw new ArgumentNullException("targetLemma");

			var syllabizer = this.InferenceResource.LanguageProvider.Syllabizer;

			var matrixCommands = DynamicMatrix.GetEditCommands(
				sourceForm.ToArray(),
				targetLemma.ToArray(),
				(sourceSyllable, targetSyllable) => syllabizer.GetDistance(sourceSyllable, targetSyllable).Cost);

			var indexedOperations = new IndexedOperation[matrixCommands.Length];

			for (int i = 0; i < matrixCommands.Length; i++)
			{
				var matrixCommand = matrixCommands[i];

				EditCommand editCommand;

				var replaceMatrixCommand = matrixCommand as DynamicMatrix.ReplaceCommand<string>;

				if (replaceMatrixCommand != null)
				{
					editCommand = syllabizer.GetDistance(replaceMatrixCommand.ExistingCharacter, replaceMatrixCommand.ReplacingCharacter);
				}
				else
				{
					var addMatrixCommand = matrixCommand as DynamicMatrix.AddCommand<string>;

					if (addMatrixCommand != null)
					{
						editCommand = new AddCommand(addMatrixCommand.AddedCharacter);
					}
					else
					{
						editCommand = new DeleteCommand();
					}
				}

				indexedOperations[i] = new IndexedOperation(editCommand, matrixCommand.SourceIndex);
			}

			var volatileCommandSequence = new CommandSequence(indexedOperations);

			return commandSequencesCache.GetOrAdd(volatileCommandSequence, volatileCommandSequence);
		}

		/// <summary>
		/// Convert a <see cref="TaggedWordForm"/> to a training sample 
		/// appropriate for <see cref="WordClassifier"/> training.
		/// </summary>
		/// <param name="form">The word form and lemma.</param>
		/// <returns>Returns a single training sample.</returns>
		public TaggedWordFormTrainingSample GetTrainingSample(TaggedWordForm form)
		{
			if (form == null) throw new ArgumentNullException("form");

			var syllabizer = this.InferenceResource.LanguageProvider.Syllabizer;

			var formSyllables = syllabizer.Segment(form.Text);

			var lemmaSyllables = syllabizer.Segment(form.Lemma);

			var commandSequence = this.GetSequence(formSyllables, lemmaSyllables);

#if DEBUG
			var lemmaVerification = commandSequence.Execute(formSyllables);

			if (!lemmaVerification.Equals(lemmaSyllables))
			{
				throw new InferenceException(String.Format("Failed to verify command execution from form '{0}' to lemma '{1}'.", form.Text, form.Lemma));
			}
#endif

			var volatileClass = new CommandSequenceClass(commandSequence, form.Tag);

			var wordClass = commandSequenceClassesCache.GetOrAdd(volatileClass, volatileClass);

			return new TaggedWordFormTrainingSample(formSyllables, wordClass);
		}

		/// <summary>
		/// Train a <see cref="WordClassifierBank"/> with a collection of word forms.
		/// </summary>
		/// <param name="wordForms">The collection of word forms.</param>
		/// <param name="trainingOptions">The training options.</param>
		/// <param name="dropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="decimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <returns>Returns the trained <see cref="WordClassifierBank"/>.</returns>
		public WordClassifierBank Train(
			IEnumerable<TaggedWordForm> wordForms, 
			WordClassifierTrainingOptions trainingOptions, 
			double dropout,
			int decimation,
			int degreeOfParallelism)
		{
			if (wordForms == null) throw new ArgumentNullException("wordForms");
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");
			if (dropout < 0.0) throw new ArgumentException("The dropout must not be negative.", "dropout");
			if (decimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "decimation");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var trainingSamples = from wordForm in wordForms
														select GetTrainingSample(wordForm);

			return this.Train(trainingSamples.ToArray(), trainingOptions, dropout, decimation, degreeOfParallelism);
		}

		/// <summary>
		/// Train a <see cref="WordClassifierBank"/> with the collection of word forms
		/// in <see cref="TrainingSet.TaggedWordTrainingSources"/> 
		/// specified inside <see cref="Setup.TrainingSets"/> 
		/// for the <see cref="LanguageProvider"/> of the <see cref="InferenceResourceItem.InferenceResource"/>.
		/// </summary>
		/// <param name="trainingOptions">The training options.</param>
		/// <param name="dropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="decimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <returns>Returns the trained <see cref="WordClassifierBank"/>.</returns>
		/// <exception cref="SetupException">
		/// When no <see cref="TrainingSet"/> is defined for the <see cref="ReadOnlyLanguageFacet.LanguageProvider"/>
		/// of the <see cref="InferenceResource"/>.
		/// </exception>
		public WordClassifierBank Train(
			WordClassifierTrainingOptions trainingOptions, 
			double dropout,
			int decimation,
			int degreeOfParallelism)
		{
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");
			if (dropout < 0.0) throw new ArgumentException("The dropout must not be negative.", "dropout");
			if (decimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "decimation");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var trainingSet = GetTrainingSet();

			if (trainingSet.TaggedWordTrainingSources.Count == 0)
				throw new InferenceException("The training set contains no tagged words sources.");

			TrainingSource<TaggedWordForm> trainingSource;

			if (trainingSet.TaggedWordTrainingSources.Count == 1)
				trainingSource = trainingSet.TaggedWordTrainingSources.First();
			else
				trainingSource = new CompositeTrainingSource<TaggedWordForm>(trainingSet.TaggedWordTrainingSources, this.InferenceResource.LanguageProvider);

			return Train(
				trainingSource, 
				trainingOptions,
				dropout,
				decimation,
				degreeOfParallelism);
		}

		/// <summary>
		/// Train a <see cref="WordClassifierBank"/> with the collection of word forms
		/// in a <see cref="TrainingSource{TaggedWordForm}"/>.
		/// </summary>
		/// <param name="trainingSource">The training source.</param>
		/// <param name="trainingOptions">The training options.</param>
		/// <param name="dropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="decimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <returns>Returns the trained <see cref="WordClassifierBank"/>.</returns>
		/// <exception cref="InferenceException"></exception>
		public WordClassifierBank Train(
			TrainingSource<TaggedWordForm> trainingSource, 
			WordClassifierTrainingOptions trainingOptions, 
			double dropout,
			int decimation,
			int degreeOfParallelism)
		{
			Trace.WriteLine("Training word classifiers.");

			using (trainingSource)
			{
				Trace.WriteLine("Opening tagged words training source.");

				trainingSource.Open();

				return Train(trainingSource.GetData(), trainingOptions, dropout, decimation, degreeOfParallelism);
			}
		}

		/// <summary>
		/// Train an optimal <see cref="WordClassifierBank"/> with the implied training sources using K-fold cross-validation
		/// by searching across a training options grid for each classifier in the bank.
		/// </summary>
		/// <param name="optionsGrid">The grid of training options used to search for the optimum training of each classifier.</param>
		/// <param name="foldCount">The fold count used in K-fold cross-validation.</param>
		/// <param name="dropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="decimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <returns>
		/// Returns a bank of word classifiers against all <see cref="CommandSequenceClass"/> variations extracted from the training set.
		/// </returns>
		/// <remarks>
		/// Training of classifiers takes place using parallel processing.
		/// </remarks>
		public WordClassifierBank OptimalTrain(
			IEnumerable<WordClassifierTrainingOptions> optionsGrid, 
			int foldCount, 
			double dropout,
			int decimation,
			int degreeOfParallelism)
		{
			if (optionsGrid == null) throw new ArgumentNullException("optionsGrid");
			if (foldCount < 2) throw new ArgumentException("foldCount must be at least 2.", "foldCount");
			if (dropout < 0.0) throw new ArgumentException("The dropout must not be negative.", "dropout");
			if (decimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "decimation");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var trainingSet = GetTrainingSet();

			if (trainingSet.TaggedWordTrainingSources.Count == 0)
				throw new InferenceException("The training set contains no tagged words sources.");

			TrainingSource<TaggedWordForm> trainingSource;

			if (trainingSet.TaggedWordTrainingSources.Count == 1)
				trainingSource = trainingSet.TaggedWordTrainingSources.First();
			else
				trainingSource = new CompositeTrainingSource<TaggedWordForm>(trainingSet.TaggedWordTrainingSources, this.InferenceResource.LanguageProvider);

			return OptimalTrain(trainingSource, optionsGrid, foldCount, dropout, decimation, degreeOfParallelism);

		}

		/// <summary>
		/// Train an optimal <see cref="WordClassifierBank"/> against a training source using K-fold cross-validation
		/// by searching across a training options grid for each classifier in the bank.
		/// </summary>
		/// <param name="trainingSource">The training source containing the tagged words.</param>
		/// <param name="optionsGrid">The grid of training options used to search for the optimum training of each classifier.</param>
		/// <param name="foldCount">The fold count used in K-fold cross-validation.</param>
		/// <param name="dropout">
		/// Omits from neural training the word samples with frequency less than the specified amount
		/// and puts them in a dictionary.
		/// </param>
		/// <param name="decimation">The interval of negative samples to omit from training set for each class.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <returns>
		/// Returns a bank of word classifiers against all <see cref="CommandSequenceClass"/> variations extracted from the training set.
		/// </returns>
		/// <remarks>
		/// Training of classifiers takes place using parallel processing.
		/// </remarks>
		public WordClassifierBank OptimalTrain(
			TrainingSource<TaggedWordForm> trainingSource, 
			IEnumerable<WordClassifierTrainingOptions> optionsGrid, 
			int foldCount, 
			double dropout, 
			int decimation, 
			int degreeOfParallelism)
		{
			Trace.WriteLine(String.Format("Training word classifiers with {0}-fold cross validation.", foldCount));

			using (trainingSource)
			{
				Trace.WriteLine("Opening tagged words training source.");

				trainingSource.Open();

				var trainingSamples = from wordForm in trainingSource.GetData()
															select GetTrainingSample(wordForm);

				return OptimalTrain(trainingSamples.ToArray(), optionsGrid, foldCount, dropout, decimation, degreeOfParallelism);
			}
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Train one <see cref="WordClassifier"/> for a <see cref="CommandSequenceClass"/>.
		/// </summary>
		/// <param name="feature">The word feature to be recognized.</param>
		/// <param name="totalSamples">The total samples available for training. Cross validation cuts will take place in this set.</param>
		/// <param name="options">The training options for the classifier.</param>
		/// <param name="decimation">The interval of negative samples to omit from training set for each class.</param>
		/// <returns>Returns the trained classiefier.</returns>
		private WordClassifier TrainClassifier(
			WordFeature feature,
			TaggedWordFormTrainingSample[] totalSamples,
			WordClassifierTrainingOptions options,
			int decimation)
		{
			if (feature == null) throw new ArgumentNullException("feature");
			if (totalSamples == null) throw new ArgumentNullException("totalSamples");
			if (options == null) throw new ArgumentNullException("options");
			if (decimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "decimation");

			var commandSequenceClass = feature.Class;

			var trainingSamples = new List<TaggedWordFormTrainingSample>(2 * totalSamples.Length / decimation);

			int decimationIndex = 0;

			Trace.WriteLine("Training for:");
			Trace.Write(commandSequenceClass.Tag);

			var positiveWords = GetPositiveWords(totalSamples, commandSequenceClass);

			for (int i = 0; i < totalSamples.Length; i++)
			{
				var sample = totalSamples[i];

				if (sample.Class.Equals(commandSequenceClass))
				{
					trainingSamples.Add(sample);
				}
				else
				{
					if (decimationIndex++ % decimation == 0 && !positiveWords.Contains(sample.WordSyllables))
					{
						trainingSamples.Add(sample);
					}
				}
			}

			var classifier = new WordClassifier(feature, trainingSamples.ToArray(), options);

			Trace.WriteLine(String.Format("Trained word classifier {0} of {1}.", Interlocked.Increment(ref trainedClassifiersCount), totalClassifiersCount));

			return classifier;
		}

		/// <summary>
		/// Train one optimum <see cref="WordClassifier"/> for a <see cref="CommandSequenceClass"/>
		/// using cross-validation.
		/// </summary>
		/// <param name="feature">The word feature to be recognized.</param>
		/// <param name="totalSamples">The total samples available for training. Cross validation cuts will take place in this set.</param>
		/// <param name="optionsGrid">Enumeration of all the training options to search for optimum classifier.</param>
		/// <param name="foldCount">The number of folds in the partitioning of the <paramref name="totalSamples"/> for validation.</param>
		/// <param name="decimation">The interval of negative samples to omit from training set for each class.</param>
		/// <returns>Returns the optimum classifier along with validation information.</returns>
		private ClassifierValidation<WordClassifier, WordClassifierTrainingOptions> TrainOptimalClassifier(
			WordFeature feature,
			TaggedWordFormTrainingSample[] totalSamples,
			IEnumerable<WordClassifierTrainingOptions> optionsGrid, 
			int foldCount,
			int decimation)
		{
			if (feature == null) throw new ArgumentNullException("feature");
			if (totalSamples == null) throw new ArgumentNullException("totalSamples");
			if (optionsGrid == null) throw new ArgumentNullException("optionsGrid");
			if (foldCount < 2) throw new ArgumentException("foldCount must be at least 2.", "foldCount");
			if (decimation < 1) throw new ArgumentException("wordDecimation must be at least 1.", "decimation");

			if (optionsGrid.Count() < 2) throw new ArgumentException("Two or more options should be countained in optionsGrid.", "optionsGrid");

			var commandSequenceClass = feature.Class;

			Fit<WordClassifierTrainingOptions> bestFit = null;

			Trace.WriteLine("Training for:");
			Trace.Write(commandSequenceClass.Tag);

			int decimationIndex;

			var positiveWords = GetPositiveWords(totalSamples, commandSequenceClass);

			foreach (var options in optionsGrid)
			{
				double meanValidationScore = 0.0;

				int foldsUsed = 0;

				decimationIndex = 0;

				for (int foldOffset = 0; foldOffset < foldCount; foldOffset++)
				{
					int validationSamplesCount = (totalSamples.Length + foldCount - 1 - foldOffset) / foldCount;

					var trainingSamples = new List<TaggedWordFormTrainingSample>((totalSamples.Length - validationSamplesCount) / decimation);
					var validationSamples = new List<TaggedWordFormTrainingSample>(validationSamplesCount);

					bool positiveFound = false;
					bool negativeFound = false;

					for (int i = 0; i < totalSamples.Length; i++)
					{
						var sample = totalSamples[i];

						if ((i - foldOffset) % foldCount == 0)
						{
							if (sample.Class.Equals(commandSequenceClass) || !positiveWords.Contains(sample.WordSyllables))
							{
								validationSamples.Add(sample);
							}
						}
						else
						{
							if (sample.Class.Equals(commandSequenceClass))
							{
								positiveFound = true;

								trainingSamples.Add(sample);
							}
							else
							{
								if (!positiveWords.Contains(sample.WordSyllables))
								{
									negativeFound = true;

									if (decimationIndex++ % decimation == 0)
									{
										trainingSamples.Add(sample);
									}
								}
							}
						}
					}

					if (!positiveFound && !negativeFound) continue;

					foldsUsed++;

					var classifier = new WordClassifier(feature, trainingSamples.ToArray(), options);

					var validationScore = classifier.Validate(validationSamples);

					meanValidationScore += validationScore;

				}

				if (foldsUsed == 0)
					throw new InferenceException("The samples are striped or insufficient modulo foldCount. Reorder samples or change the foldCount.");

				meanValidationScore /= (double)foldsUsed;

				var fit = new Fit<WordClassifierTrainingOptions>(meanValidationScore, options);

				if (fit.CompareTo(bestFit) > 0) bestFit = fit;
			}

			if (bestFit == null)
				throw new ArgumentException("The optionsGrid was empty.", "optionsGrid");

			Trace.WriteLine(String.Format("Best validation score is {0:F4} with training options {1} for:", bestFit.ValidationScore, bestFit.TrainingOptions));
			Trace.Write(commandSequenceClass.Tag);

			var decimatedSamples = new List<TaggedWordFormTrainingSample>(totalSamples.Length * 2 / decimation);

			decimationIndex = 0;

			for (int i = 0; i < totalSamples.Length; i++)
			{
				var sample = totalSamples[i];

				if (sample.Class.Equals(commandSequenceClass))
				{
					decimatedSamples.Add(sample);
				}
				else
				{
					if (decimationIndex++ % decimation == 0 && !positiveWords.Contains(sample.WordSyllables))
					{
						decimatedSamples.Add(sample);
					}
				}
			}

			var bestClassifier = new WordClassifier(feature, decimatedSamples.ToArray(), bestFit.TrainingOptions);

			Trace.WriteLine(String.Format("Trained word classifier {0} of {1}.", Interlocked.Increment(ref trainedClassifiersCount), totalClassifiersCount));

			return new ClassifierValidation<WordClassifier, WordClassifierTrainingOptions>(bestClassifier, bestFit);
		}

		private WordClassifierBank Train(
			TaggedWordFormTrainingSample[] totalSamples,
			WordClassifierTrainingOptions options,
			double dropout,
			int decimation,
			int degreeOfParallelism)
		{
			if (degreeOfParallelism == 0) degreeOfParallelism = Environment.ProcessorCount;

			Trace.WriteLine(String.Format("Loaded {0} tagged word word training samples.", totalSamples.Length));

			var trainDataArtifacts = new TrainDataArtifacts(totalSamples, dropout, decimation);

			trainedClassifiersCount = 0;
			totalClassifiersCount = trainDataArtifacts.ClassifierFeaturesCount;

			TraceInitialStatistics(trainDataArtifacts);

			var classifiers = from feature in trainDataArtifacts
													.ClassifierFeaturesSamples.Select(fs => fs.Feature)
													.AsLongParallel().WithDegreeOfParallelism(degreeOfParallelism)
												select TrainClassifier(feature, totalSamples, options, decimation);

			return new WordClassifierBank(
				this.InferenceResource,
				classifiers,
				trainDataArtifacts);
		}

		private WordClassifierBank OptimalTrain(
			TaggedWordFormTrainingSample[] totalSamples,
			IEnumerable<WordClassifierTrainingOptions> optionsGrid,
			int foldCount,
			double dropout,
			int decimation,
			int degreeOfParallelism)
		{
			if (degreeOfParallelism == 0) degreeOfParallelism = Environment.ProcessorCount;

			Trace.WriteLine(String.Format("Loaded {0} tagged word word training samples.", totalSamples.Length));

			var trainDataArtifacts = new TrainDataArtifacts(totalSamples, dropout, decimation);

			trainedClassifiersCount = 0;
			totalClassifiersCount = trainDataArtifacts.ClassifierFeaturesSamples.Count();

			TraceInitialStatistics(trainDataArtifacts);

			var classifiers = from feature in trainDataArtifacts
													.ClassifierFeaturesSamples.Select(fs => fs.Feature)
													.AsLongParallel().WithDegreeOfParallelism(degreeOfParallelism)
												select TrainOptimalClassifier(feature, totalSamples, optionsGrid, foldCount, decimation).Classifier;

			return new WordClassifierBank(
				this.InferenceResource, 
				classifiers, 
				trainDataArtifacts);
		}

		private void TraceInitialStatistics(TrainDataArtifacts trainDataArtifacts)
		{
			Trace.WriteLine(String.Format("The tagged word training samples after dropout are {0}.", trainDataArtifacts.CommonSamples.Count()));

			Trace.WriteLine(String.Format("Found {0} distinct command sequence classes after dropout.", totalClassifiersCount));

			Trace.WriteLine(String.Format("The residual singular samples are {0}.", trainDataArtifacts.ExceptionalSamples.Count()));

			Trace.WriteLine(
				String.Format(
					"The residual classes are {0}.",
					trainDataArtifacts.DictionaryFeaturesSamples.Count() - totalClassifiersCount));
		}

		#endregion
	}
}
