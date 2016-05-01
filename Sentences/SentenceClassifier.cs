using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Grammophone.GenericContentModel;
using Grammophone.EnnounInference.Configuration;
using Grammophone.LanguageModel.Grammar;
using Grammophone.LanguageModel.TrainingSources;
using Grammophone.Linq;
using Grammophone.Vectors;

namespace Grammophone.EnnounInference.Sentences
{
	/// <summary>
	/// Classifier for sentences, yielding the part-of-speech of the words and 
	/// their inferred lemmata.
	/// </summary>
	[Serializable]
	public class SentenceClassifier : InferenceResourceItem
	{
		#region Auxilliary classes

		/// <summary>
		/// Result of validation against tagged sentences.
		/// </summary>
		public class ValidationResult
		{
			#region Construction

			internal ValidationResult(
				int totalSentencesCount, 
				int totalWordsCount, 
				int correctlyLematizedSentencesCount, 
				int correctlyLemmatizedWordsCount,
				int correctlyTaggedSentencesCount,
				int correctlyTaggedWordsCount)
			{
				if (totalSentencesCount <= 0) throw new ArgumentException("totalSentencesCount must be positive.", "totalSentencesCount");
				if (totalWordsCount <= 0) throw new ArgumentException("totalWordsCount must be positive.", "totalWordsCount");

				this.TotalSentencesCount = totalSentencesCount;
				this.TotalWordsCount = totalWordsCount;
				this.CorrectlyLemmatizedSentencesCount = correctlyLematizedSentencesCount;
				this.CorrectlyLemmatizedWordsCount = correctlyLemmatizedWordsCount;
				this.CorrectlyTaggedSentencesCount = correctlyTaggedSentencesCount;
				this.CorrectlyTaggedWordsCount = correctlyTaggedWordsCount;
			}

			#endregion

			#region Public properties

			/// <summary>
			/// The total number of sentences found in the validation set.
			/// </summary>
			public int TotalSentencesCount { get; private set; }

			/// <summary>
			/// The total number of words in all te sentences of the validation set.
			/// </summary>
			public int TotalWordsCount { get; private set; }

			/// <summary>
			/// The number of sentences whose words have all been classified and lemmatized correctly.
			/// </summary>
			public int CorrectlyLemmatizedSentencesCount { get; private set; }

			/// <summary>
			/// The number of words which have been classified and lemmatized correctly among all sentences.
			/// </summary>
			public int CorrectlyLemmatizedWordsCount { get; private set; }

			/// <summary>
			/// The number of sentences whose words have all been classified correctly.
			/// </summary>
			public int CorrectlyTaggedSentencesCount { get; private set; }

			/// <summary>
			/// The number of words which have been classified correctly among all sentences.
			/// </summary>
			public int CorrectlyTaggedWordsCount { get; private set; }

			/// <summary>
			/// The rate of successfully tagged and lemmatized sentences.
			/// </summary>
			public double SentencesLemmatizationAccuracy
			{
				get
				{
					return (double)this.CorrectlyLemmatizedSentencesCount / (double)this.TotalSentencesCount;
				}
			}

			/// <summary>
			/// The rate of successfully tagged and lemmatized words.
			/// </summary>
			public double WordsLemmatizationAccurracy
			{
				get
				{
					return (double)this.CorrectlyLemmatizedWordsCount / (double)this.TotalWordsCount;
				}
			}

			/// <summary>
			/// The rate of successfully tagged sentences.
			/// </summary>
			public double SentencesTaggingAccuracy
			{
				get
				{
					return (double)this.CorrectlyTaggedSentencesCount / (double)this.TotalSentencesCount;
				}
			}

			/// <summary>
			/// The rate of successfully tagged words.
			/// </summary>
			public double WordsTaggingAccuracy
			{
				get
				{
					return (double)this.CorrectlyTaggedWordsCount / (double)this.TotalWordsCount;
				}
			}

			#endregion

			#region Public methods

			/// <summary>
			/// Get a string summarizing the validation result.
			/// </summary>
			public override string ToString()
			{
				var builder = new StringBuilder();

				builder.AppendFormat("Total sentences: {0}, total words: {1}\n", this.TotalSentencesCount, this.TotalWordsCount);
				builder.AppendFormat("Sentences lemmatization accuracy: {0:00.00}%\n", this.SentencesLemmatizationAccuracy * 100.0);
				builder.AppendFormat("Sentences tagging accuracy: {0:00.00}%\n", this.SentencesTaggingAccuracy * 100.0);
				builder.AppendFormat("Words lemmatization accuracy: {0:00.00}%\n", this.WordsLemmatizationAccurracy * 100.0);
				builder.AppendFormat("Words tagging accuracy: {0:00.00}%\n", this.WordsTaggingAccuracy * 100.0);

				return builder.ToString();
			}

			#endregion
		}

		#endregion

		#region Private fields

		private LanguageCRF conditionalRandomField;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="inferenceResource">The inference resource.</param>
		/// <param name="conditionalRandomField">The CRF which this classifier relies on.</param>
		internal SentenceClassifier(InferenceResource inferenceResource, LanguageCRF conditionalRandomField)
			: base(inferenceResource)
		{
			if (conditionalRandomField == null) throw new ArgumentNullException("conditionalRandomField");

			this.conditionalRandomField = conditionalRandomField;
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Infer the tags for each word of a sentence.
		/// If no inference is possible because the sentence is estimated as impossible, return null.
		/// </summary>
		/// <param name="words">The words of a sentence, in order. Depending on the implementation, they include punctuation as words.</param>
		/// <returns>Returns the corresponding sequence of tags or null if the sentence is considered as impossible.</returns>
		public IReadOnlySequence<Tag> InferTags(IReadOnlySequence<string> words)
		{
			if (words == null) throw new ArgumentNullException("words");

			return InferTags(words.ToArray());
		}

		/// <summary>
		/// Infer the tags for each word of a sentence.
		/// If no inference is possible because the sentence is estimated as impossible, return null.
		/// </summary>
		/// <param name="words">The words of a sentence, in order. Depending on the implementation, they include punctuation as words.</param>
		/// <returns>Returns the corresponding sequence of tags or null if the sentence is considered as impossible.</returns>
		public IReadOnlySequence<Tag> InferTags(string[] words)
		{
			if (words == null) throw new ArgumentNullException("words");

			var sequenceEvaluator = conditionalRandomField.GetSequenceEvaluator(words);

			var tags = sequenceEvaluator.Y;

			return new ReadOnlySequence<Tag>(tags);
		}

		/// <summary>
		/// Infer the tags and the lemmata for each word of a sentence.
		/// If no inference is possible because the sentence is estimated as impossible, return null.
		/// </summary>
		/// <param name="words">The words of a sentence, in order. Depending on the implementation, they include punctuation as words.</param>
		/// <returns>
		/// Returns a sequence of <see cref="LemmaInference"/> items, which contain the tags and the lemmata corresponding
		/// to the given words, or null of the sequence of the given words is estimated as impossible.
		/// </returns>
		public IReadOnlySequence<LemmaInference> InferLemmata(IReadOnlySequence<string> words)
		{
			if (words == null) throw new ArgumentNullException("words");

			return InferLemmata(words.ToArray());
		}

		/// <summary>
		/// Infer the tags and the lemmata for each word of a sentence. 
		/// If no inference is possible because the sentence is estimated as impossible, return null.
		/// </summary>
		/// <param name="words">The words of a sentence, in order. Depending on the implementation, they include punctuation as words.</param>
		/// <returns>
		/// Returns a sequence of <see cref="LemmaInference"/> items, which contain the tags and the lemmata corresponding
		/// to the given words, or null of the sequence of the given words is estimated as impossible.
		/// </returns>
		public IReadOnlySequence<LemmaInference> InferLemmata(string[] words)
		{
			if (words == null) throw new ArgumentNullException(nameof(words));

			CRF.LinearChainCRF<string[], Tag>.SequenceEvaluator sequenceEvaluator;

			LemmaInference[] lemmaInferences;

			InferLemmata(words, out sequenceEvaluator, out lemmaInferences);

			if (lemmaInferences != null)
				return new ReadOnlySequence<LemmaInference>(lemmaInferences);
			else
				return null;
		}

		/// <summary>
		/// Infer the tags and the lemmata for each word of a sentence. 
		/// If no inference is possible because the sentence is estimated as impossible, return null.
		/// </summary>
		/// <param name="sentence">String of a complete sentence including punctuation.</param>
		/// <returns>
		/// Returns a sequence of <see cref="LemmaInference"/> items, which contain the tags and the lemmata corresponding
		/// to the given words, or null of the sequence of the given words is estimated as impossible.
		/// </returns>
		public IReadOnlySequence<LemmaInference> InferLemmata(string sentence)
		{
			if (sentence == null) throw new ArgumentNullException("sentence");

			var sentenceBreaker = this.InferenceResource.LanguageProvider.SentenceBreaker;

			string[] words = sentenceBreaker.Break(sentence);

			return InferLemmata(words);
		}

		/// <summary>
		/// Estimate the probability of a sentence and infer the tags and 
		/// the lemmata for each word in it.
		/// </summary>
		/// <param name="words">The words of a sentence, in order. Depending on the implementation, they include punctuation as words.</param>
		/// <returns>Returns the sentence inference.</returns>
		public SentenceInference InferSentence(string[] words)
		{
			if (words == null) throw new ArgumentNullException(nameof(words));

			CRF.LinearChainCRF<string[], Tag>.SequenceEvaluator sequenceEvaluator;

			LemmaInference[] lemmaInferences;

			this.InferLemmata(words, out sequenceEvaluator, out lemmaInferences);

			if (lemmaInferences == null) return new SentenceInference(null, 0.0);

			double probability = Math.Exp(
				sequenceEvaluator.ComputeLogConditionalLikelihood(lemmaInferences.Select(li => li.Tag).ToArray()));

			return new SentenceInference(new ReadOnlySequence<LemmaInference>(lemmaInferences), probability);
		}

		/// <summary>
		/// Estimate the probability of a sentence and infer the tags and 
		/// the lemmata for each word in it.
		/// </summary>
		/// <param name="sentence">String of a complete sentence including punctuation.</param>
		/// <returns>Returns the sentence inference.</returns>
		public SentenceInference InferSentence(string sentence)
		{
			if (sentence == null) throw new ArgumentNullException(nameof(sentence));

			var sentenceBreaker = this.InferenceResource.LanguageProvider.SentenceBreaker;

			string[] words = sentenceBreaker.Break(sentence);

			return InferSentence(words);
		}

		/// <summary>
		/// Validate the classifier against the tagged sentences specified in <see cref="Setup.ValidationSets"/>
		/// for the language provider.
		/// </summary>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <exception cref="SetupException">
		/// When no validation set exists in <see cref="Setup.ValidationSets"/> defined for this language.
		/// </exception>
		/// <exception cref="InferenceException">
		/// When there are no sentences or words in the validation set.
		/// </exception>
		public ValidationResult Validate(int degreeOfParallelism = 0)
		{
			var validationSet = this.GetValidationSet();

			return Validate(validationSet, degreeOfParallelism);
		}

		/// <summary>
		/// Validate the classifier against a validation set.
		/// </summary>
		/// <param name="validationSet">The validation set to use.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <exception cref="SetupException">
		/// When no validation set exists in <see cref="Setup.ValidationSets"/> defined for this language.
		/// </exception>
		/// <exception cref="InferenceException">
		/// When there are no sentences or words in the validation set.
		/// </exception>
		public ValidationResult Validate(ValidationSet validationSet, int degreeOfParallelism = 0)
		{
			if (validationSet == null) throw new ArgumentNullException("validationSet");

			if (validationSet.SentenceValidationSources.Count == 0)
				throw new InferenceException("The validation set contains no tagged sentences sources.");

			TrainingSource<TaggedSentence> validationSource;

			if (validationSet.SentenceValidationSources.Count == 1)
				validationSource = validationSet.SentenceValidationSources.First();
			else
				validationSource = new CompositeTrainingSource<TaggedSentence>(validationSet.SentenceValidationSources, this.InferenceResource.LanguageProvider);

			return Validate(validationSource, degreeOfParallelism);
		}

		/// <summary>
		/// Validate the generalization capabilities of the classifier against tagged sentences.
		/// </summary>
		/// <param name="validationSource">The validation source.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <exception cref="InferenceException">
		/// When there are no sentences or words in the validation set.
		/// </exception>
		public ValidationResult Validate(TrainingSource<TaggedSentence> validationSource, int degreeOfParallelism = 0)
		{
			if (validationSource == null) throw new ArgumentNullException("validationSource");

			using (validationSource)
			{
				validationSource.Open();

				return Validate(validationSource.GetData(), degreeOfParallelism);
			}
		}

		/// <summary>
		/// Validate the generalization capabilities of the classifier against tagged sentences.
		/// </summary>
		/// <param name="taggedSentences">The sentences validation set.</param>
		/// <param name="degreeOfParallelism">
		/// The number of parallel tasks to employ for processing. 
		/// If zero, the number of tasks is the number of the available processor cores.
		/// </param>
		/// <exception cref="InferenceException">
		/// When there are no sentences or words in the validation set.
		/// </exception>
		public ValidationResult Validate(IEnumerable<TaggedSentence> taggedSentences,	int degreeOfParallelism = 0)
		{
			if (taggedSentences == null) throw new ArgumentNullException("taggedSentences");

			int totalSentencesCount = 0, totalWordsCount = 0;

			int correctlyLematizedSentencesCount = 0, totalCorrectlyLemmatizedWordsCount = 0;
			int correctlyTaggedSentencesCount = 0, totalCorrectlyTaggedWordsCount = 0;

			var parallelizationOptions = new System.Threading.Tasks.ParallelOptions();

			if (degreeOfParallelism > 0)
			{
				parallelizationOptions.MaxDegreeOfParallelism = degreeOfParallelism;
			}
			else
			{
				parallelizationOptions.MaxDegreeOfParallelism = Environment.ProcessorCount;
			}

			var languageProvider = this.InferenceResource.LanguageProvider;

			System.Threading.Tasks.Parallel.ForEach(taggedSentences, parallelizationOptions, taggedSentence =>
			{
				int correctlyLemmatizedWordsCount = 0;
				int correctlyTaggedWordsCount = 0;

				int currentSentencesCount = Interlocked.Increment(ref totalSentencesCount);

				Trace.WriteLine(String.Format("Evaluating sentence #{0}:", currentSentencesCount));

				var wordsQuery = from taggedWord in taggedSentence.WordForms
												 select taggedWord.Text;

				var words = wordsQuery.ToArray();

				var sentenceBuilder = new StringBuilder(words.Sum(w => w.Length + 1));

				foreach (var word in words)
				{
					sentenceBuilder.Append(word);
					sentenceBuilder.Append(' ');
				}

				Interlocked.Add(ref totalWordsCount, words.Length);

				var inferredLemmata = InferLemmata(words);

				if (inferredLemmata != null)
				{
					bool isSentenceTaggingCorrect = true, isSentenceLemmatizationCorrect = true;

					for (int i = 0; i < taggedSentence.WordForms.Length; i++)
					{
						var wordForm = taggedSentence.WordForms[i];
						var inferredLemma = inferredLemmata[i];

						if (wordForm.Tag.Equals(inferredLemma.Tag))
							correctlyTaggedWordsCount++;
						else
							isSentenceTaggingCorrect = false;

						string normalizedCorrectLemma = languageProvider.NormalizeWord(wordForm.Lemma);

						if (normalizedCorrectLemma == inferredLemma.Lemma)
							correctlyLemmatizedWordsCount++;
						else
							isSentenceLemmatizationCorrect = false;
					}

					Trace.WriteLine(
						String.Format(
							"Sentence {6}: {5}\nCorrect tags: {0}, Correct lemmata: {1}, Total words: {2}, tagging rate: {3:F2}%, lemmatization rate: {4:F2}%",
							correctlyTaggedWordsCount,
							correctlyLemmatizedWordsCount,
							taggedSentence.WordForms.Length,
							100.0 * (double)correctlyTaggedWordsCount / (double)taggedSentence.WordForms.Length,
							100.0 * (double)correctlyLemmatizedWordsCount / (double)taggedSentence.WordForms.Length,
							sentenceBuilder,
							currentSentencesCount
						)
					);

					if (isSentenceTaggingCorrect)
					{
						Interlocked.Increment(ref correctlyTaggedSentencesCount);
						Trace.WriteLine("Correctly tagged.");
					}

					if (isSentenceLemmatizationCorrect)
					{
						Interlocked.Increment(ref correctlyLematizedSentencesCount);
						Trace.WriteLine("Correctly lemmatized.");
					}

				}
				else
				{
					Trace.WriteLine(String.Format("The sentence {0} is estimated as impossible.", sentenceBuilder));
				}

				Interlocked.Add(ref totalCorrectlyLemmatizedWordsCount, correctlyLemmatizedWordsCount);
				Interlocked.Add(ref totalCorrectlyTaggedWordsCount, correctlyTaggedWordsCount);
			});

			if (totalSentencesCount == 0) throw new InferenceException("The validation set does not contain any sentences.");
			if (totalWordsCount == 0) throw new InferenceException("The validation set does not contain any words in its sentences.");

			return new ValidationResult(
				totalSentencesCount,
				totalWordsCount,
				correctlyLematizedSentencesCount,
				totalCorrectlyLemmatizedWordsCount,
				correctlyTaggedSentencesCount,
				totalCorrectlyTaggedWordsCount);
		}

		#endregion

		#region Private methods

		private void InferLemmata(
			string[] words, 
			out CRF.LinearChainCRF<string[], Tag>.SequenceEvaluator sequenceEvaluator, 
			out LemmaInference[] lemmaInferences)
		{
			if (words == null) throw new ArgumentNullException("words");

			lemmaInferences = null;

			sequenceEvaluator = conditionalRandomField.GetSequenceEvaluator(words);

			var inferredTags = sequenceEvaluator.Y;

			if (inferredTags == null) return;

			var featureFunctionsProvider = (LanguageFeatureFunctionsProvider)sequenceEvaluator.FeatureFunctionsProvider;

			var featureFunctionsProviderFactory = (LanguageFeatureFunctionsProviderFactory)conditionalRandomField.FunctionsProviderFactory;

			int biasesOffset = featureFunctionsProviderFactory.BiasesOffset;

			var languageProvider = this.InferenceResource.LanguageProvider;

			lemmaInferences = new LemmaInference[inferredTags.Length];

			Vector w = conditionalRandomField.Weights;

			for (int k = 0; k < featureFunctionsProvider.ScoreBanks.Length; k++)
			{
				var inferredTag = inferredTags[k];

				var weighedClasses = from score in featureFunctionsProvider.GetScores(k, inferredTag)
														 let featureID = score.Feature.ID
														 select new
														 {
															 Class = score.Feature.Class,
															 WeighedScore = score.ScoreValue * w[featureID] + w[featureID + biasesOffset]
														 };

				var maxClass = weighedClasses.ArgMax(row => row.WeighedScore);

				string lemma;

				if (maxClass == null)
				{
					lemma = words[k];
				}
				else
				{
					var scoreBank = featureFunctionsProvider.ScoreBanks[k];

					var commandSequence = maxClass.Class.Sequence;

					var wordSyllables = scoreBank.Word;

					var lemmaSyllables = commandSequence.Execute(wordSyllables);

					lemma = languageProvider.Syllabizer.Reassemble(lemmaSyllables);
				}

				lemmaInferences[k] = new LemmaInference(words[k], inferredTag, lemma);
			}
		}

		#endregion
	}
}
