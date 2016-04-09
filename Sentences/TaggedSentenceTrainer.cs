using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Grammophone.LanguageModel.Provision;
using Grammophone.LanguageModel.TrainingSources;
using Grammophone.EnnounInference.Configuration;
using Grammophone.LanguageModel.Grammar;
using Grammophone.Linq;
using Grammophone.CRF;

namespace Grammophone.EnnounInference.Sentences
{
	/// <summary>
	/// Provides training services for tagged sentences.
	/// </summary>
	internal class TaggedSentenceTrainer : InferenceResourceItem
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		internal TaggedSentenceTrainer(InferenceResource inferenceResource)
			: base(inferenceResource)
		{

		}

		#endregion

		#region Public methods

		/// <summary>
		/// Train a <see cref="SentenceClassifier"/> with the collection of <see cref="TaggedSentence"/>s
		/// specified inside <see cref="Setup.TrainingSets"/> 
		/// for the <see cref="LanguageProvider"/> of the <see cref="InferenceResourceItem.InferenceResource"/>.
		/// </summary>
		/// <param name="trainingOptions">The training options.</param>
		/// <param name="tagBiGramsDropout">
		/// Excludes tag bi-grams with frequency below the specified amount.
		/// </param>
		/// <param name="sentencesStride">The step size of picking samples within the sentences training set.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <returns>Returns the trained sentence classifier.</returns>
		public SentenceClassifier Train(
			SentenceClassifierTrainingOptions trainingOptions,
			double tagBiGramsDropout,
			int sentencesStride,
			int degreeOfParallelism)
		{
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var trainingSet = GetTrainingSet();

			if (trainingSet.SentenceTrainingSources.Count == 0)
				throw new InferenceException("The training set contains no tagged sentences sources.");

			TrainingSource<TaggedSentence> trainingSource;

			if (trainingSet.SentenceTrainingSources.Count == 1)
				trainingSource = trainingSet.SentenceTrainingSources.First();
			else
				trainingSource = new CompositeTrainingSource<TaggedSentence>(trainingSet.SentenceTrainingSources, this.InferenceResource.LanguageProvider);

			return Train(trainingSource, trainingOptions, tagBiGramsDropout, sentencesStride, degreeOfParallelism);
		}

		/// <summary>
		/// Train a <see cref="SentenceClassifier"/> with the collection of <see cref="TaggedSentence"/>s
		/// provided by the <paramref name="trainingSource"/>.
		/// </summary>
		/// <param name="trainingSource">The training source.</param>
		/// <param name="trainingOptions">The training options.</param>
		/// <param name="tagBiGramsDropout">
		/// Excludes tag bi-grams with frequency below the specified amount.
		/// </param>
		/// <param name="sentencesStride">The step size of picking samples within the sentences training set.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <returns>Returns the trained sentence classifier.</returns>
		public SentenceClassifier Train(
			TrainingSource<TaggedSentence> trainingSource,
			SentenceClassifierTrainingOptions trainingOptions,
			double tagBiGramsDropout,
			int sentencesStride,
			int degreeOfParallelism)
		{
			if (trainingSource == null) throw new ArgumentNullException("trainingSource");
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");
			if (degreeOfParallelism < 0) throw new ArgumentException("The degree of parallelism must not be negative.", "degreeOfParallelism");

			var wordClassifierBank = this.InferenceResource.WordClassifierBank;

			if (wordClassifierBank == null)
				throw new InferenceException("Tagged words have not been trained.");

			if (trainingOptions.AnalogiesScoreOptions != null && this.InferenceResource.WordFormsDictionary == null)
				throw new InferenceException("Untagged words have not been trained or loaded. These should be present when analogies core options are specified.");

			wordClassifierBank.AreDictionaryFeaturesCondensed = trainingOptions.CondenseFeatures;

			if (degreeOfParallelism < 0 || degreeOfParallelism > Environment.ProcessorCount) degreeOfParallelism = Environment.ProcessorCount;

			Trace.WriteLine("Training sentences.");

			using (trainingSource)
			{
				Trace.WriteLine("Opening tagged sentences training source.");

				trainingSource.Open();

				var trainingSamples = trainingSource.GetData().ToArray();

				Trace.WriteLine(String.Format("Sentence training samples: {0}.", trainingSamples.Length));

				switch (trainingOptions.TrainingMethod)
				{
					case SentenceClassifierTrainingMethod.Offline:
						return OfflineTrain(trainingSamples, trainingOptions, tagBiGramsDropout, sentencesStride, degreeOfParallelism);

					default:
						return OnlineTrain(trainingSamples, trainingOptions, tagBiGramsDropout, sentencesStride, degreeOfParallelism);
				}
			}
		}

		#endregion

		#region Private methods

		private Tuple<Tag, Tag>[] GetTagBiGrams(TaggedSentence[] sentences, double tagBiGramsDropout)
		{
			var tagTuplesFrequencies = new Dictionary<Tuple<Tag, Tag>, int>();

			var startTag = this.InferenceResource.LanguageProvider.StartTag;
			var endTag = this.InferenceResource.LanguageProvider.EndTag;

			int totalTagTuplesCount = 0;

			for (int i = 0; i < sentences.Length; i++)
			{
				var sentence = sentences[i];

				var sentenceWordForms = sentence.WordForms;

				if (sentenceWordForms.Length > 0)
				{
					var previousTaggedWord = sentenceWordForms[0];

					var startTuple = new Tuple<Tag, Tag>(startTag, previousTaggedWord.Tag);

					totalTagTuplesCount++;

					UpdateTagTupleHistogram(tagTuplesFrequencies, startTuple);

					for (int j = 1; j < sentenceWordForms.Length; j++)
					{
						totalTagTuplesCount++;

						var nextTaggedWord = sentenceWordForms[j];

						var tagTuple = new Tuple<Tag, Tag>(previousTaggedWord.Tag, nextTaggedWord.Tag);

						UpdateTagTupleHistogram(tagTuplesFrequencies, tagTuple);

						previousTaggedWord = nextTaggedWord;
					}

					var endTuple = new Tuple<Tag, Tag>(previousTaggedWord.Tag, endTag);

					totalTagTuplesCount++;

					UpdateTagTupleHistogram(tagTuplesFrequencies, endTuple);
				}
			}

			Trace.WriteLine(String.Format("Total tag tuples in sentences training source, including repetitions: {0}", totalTagTuplesCount));

			Trace.WriteLine(String.Format("Total unique tag tuples in sentences : {0}", tagTuplesFrequencies.Keys.Count));

			int tagTuplesThresholdCount = (int)(tagBiGramsDropout * (double)totalTagTuplesCount);

			var importantTagTuplesQuery = from entry in tagTuplesFrequencies
																		where entry.Value >= tagTuplesThresholdCount
																		select entry.Key;

			var importantTagTuples = importantTagTuplesQuery.ToArray();

			Trace.WriteLine(String.Format("Important tag tuples in sentences training source after dropout: {0}", importantTagTuples.Length));

			return importantTagTuples;
		}

		private static void UpdateTagTupleHistogram(Dictionary<Tuple<Tag, Tag>, int> tagTuplesFrequencies, Tuple<Tag, Tag> tagTuple)
		{
			int tagTupleCount;

			if (!tagTuplesFrequencies.TryGetValue(tagTuple, out tagTupleCount))
			{
				tagTupleCount = 0;
			}

			tagTuplesFrequencies[tagTuple] = ++tagTupleCount;
		}

		private SentenceClassifier OfflineTrain(TaggedSentence[] trainingSamples,
			SentenceClassifierTrainingOptions trainingOptions,
			double tagBiGramsDropout,
			int sentencesStride,
			int degreeOfParallelism)
		{
			var tagTuples = GetTagBiGrams(trainingSamples, tagBiGramsDropout);

			var conditionalRandomField =
				new LanguageCRF(
					this.InferenceResource, 
					tagTuples,
					trainingOptions);

			var trainingPairs = GetTrainingPairs(trainingSamples, sentencesStride);

			var computationFactory = trainingOptions.OfflineTrainingOptions.GoalComputationFactory;

			computationFactory.DegreeOfParallelism = degreeOfParallelism;

			conditionalRandomField.OfflineTrain(trainingPairs, trainingOptions.OfflineTrainingOptions);

			return new SentenceClassifier(this.InferenceResource,  conditionalRandomField);
		}

		private SentenceClassifier OnlineTrain(TaggedSentence[] trainingSamples,
			SentenceClassifierTrainingOptions trainingOptions,
			double tagBiGramsDropout,
			int sentencesStride,
			int degreeOfParallelism)
		{
			var tagTuples = GetTagBiGrams(trainingSamples, tagBiGramsDropout);

			var conditionalRandomField =
				new LanguageCRF(
					this.InferenceResource,
					tagTuples,
					trainingOptions);

			var trainingPairs = GetTrainingPairs(trainingSamples, sentencesStride);

			var infiniteTrainingPairsSequence =
				trainingOptions.ShuffleTrainingSamples ? trainingPairs.RandomPickSequence(42) : trainingPairs.CycleSequence();

			conditionalRandomField.OnlineTrain(infiniteTrainingPairsSequence, trainingOptions.OnlineTrainingOptions, degreeOfParallelism);

			return new SentenceClassifier(this.InferenceResource, conditionalRandomField);
		}

		private static LinearChainCRF<string[], Tag>.TrainingPair[] GetTrainingPairs(TaggedSentence[] trainingSamples, int sentencesStride)
		{
			var trainingPairsQuery = from trainingSample in trainingSamples.Where((_, i) => i % sentencesStride == 0)
															 select new LinearChainCRF<string[], Tag>.TrainingPair(
																 trainingSample.WordForms.Select(wordForm => wordForm.Text).ToArray(),
																 trainingSample.WordForms.Select(wordForm => wordForm.Tag).ToArray()
															 );

			var trainingPairs = trainingPairsQuery.ToArray();

			Trace.WriteLine(String.Format("Training with {0} sample sentences.", trainingPairs.Length));

			return trainingPairs;
		}

		#endregion
	}
}
