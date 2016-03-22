using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.LanguageModel;
using Gramma.LanguageModel.Provision.EditCommands;
using Gramma.Kernels;
using Gramma.SVM;
using Gramma.GenericContentModel;
using Gramma.LanguageModel.Grammar;

namespace Gramma.Inference.Words
{
	/// <summary>
	/// A binary classifier for recognizing a <see cref="WordFeature"/> of a word.
	/// </summary>
	[Serializable]
	public class WordClassifier : IKeyedChild<WordClassifierBank, CommandSequenceClass>, IKeyedElement<TagType>
	{
		#region Private fields

		private BinaryClassifier<string[]> classifier;

		#endregion

		#region Construction

		internal WordClassifier(
			WordFeature feature,
			TaggedWordFormTrainingSample[] trainingSamples,
			WordClassifierTrainingOptions trainingOptions)
		{
			if (feature == null) throw new ArgumentNullException("feature");
			if (trainingSamples == null) throw new ArgumentNullException("trainingSamples");
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");

			this.Feature = feature;

			this.Train(trainingSamples, trainingOptions);
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The <see cref="WordClassifierBank"/> where this bank belongs.
		/// </summary>
		public WordClassifierBank ClassifierBank { get; private set; }

		/// <summary>
		/// The feature being recognized.
		/// </summary>
		public WordFeature Feature { get; private set; }

		#endregion

		#region Public methods

		/// <summary>
		/// Get the classification score value for a <paramref name="word"/>. Positive values mean that the classifier believes 
		/// that the <paramref name="word"/> belongs to the classifier's <see cref="WordFeature.Class"/>.
		/// </summary>
		/// <param name="word">The word to be tested.</param>
		/// <returns>
		/// A positive score value is an estimate of the belonging of a word to
		/// the <see cref="WordFeature.Class"/>.
		/// </returns>
		public double GetScoreValue(SyllabicWord word)
		{
			if (word == null) throw new ArgumentNullException("word");

			return GetScoreValue(word.ToArray());
		}

		/// <summary>
		/// Get the classification <see cref="Score"/> for a <paramref name="word"/>.
		/// A positive <see cref="Score.ScoreValue"/> means that the classifier believes 
		/// that the <paramref name="word"/> belongs to the classifier's <see cref="WordFeature.Class"/>.
		/// </summary>
		/// <param name="word">The word to be tested.</param>
		/// <returns>
		/// The <see cref="Score"/> for the <paramref name="word"/>.
		/// A positive <see cref="Score.ScoreValue"/> is an estimate of the belonging of a word to
		/// the <see cref="WordFeature.Class"/>.
		/// </returns>
		public Score GetScore(SyllabicWord word)
		{
			return GetScore(word.ToArray());
		}

		/// <summary>
		/// Validate a <see cref="WordClassifier"/> against samples.
		/// </summary>
		/// <param name="validationSamples">The samples to validate the classifier against.</param>
		/// <returns>Returns the BAC (BAlanced Accuracy) validation score.</returns>
		public double Validate(IEnumerable<TaggedWordFormTrainingSample> validationSamples)
		{
			int matchCοunt = 0;

			if (!validationSamples.Any()) return 0.0;

			int totalCount = 0;

			int totalPositiveCount = 0;
			int totalNegativeCount = 0;

			int positiveMatchCount = 0;
			int negativeMatchCount = 0;

			foreach (var validationSample in validationSamples)
			{
				totalCount++;

				var score = this.GetScoreValue(validationSample.WordSyllables);

				if (this.Feature.Class.Equals(validationSample.Class))
				{
					totalPositiveCount++;

					if (score > 0.0)
					{
						matchCοunt++;
						positiveMatchCount++;
					}
				}
				else
				{
					totalNegativeCount++;

					if (score < 0.0)
					{
						matchCοunt++;
						negativeMatchCount++;
					}
				}

			}

			if (totalPositiveCount > 0 && totalNegativeCount > 0)
				return ((double)positiveMatchCount / (double)totalPositiveCount + (double)negativeMatchCount / (double)totalNegativeCount) / 2.0;
			else
				return (double)matchCοunt / (double)totalCount;
		}

		#endregion

		#region Internal methods

		/// <summary>
		/// Get the classification score value for a <paramref name="word"/>. Positive values mean that the classifier believes 
		/// that the <paramref name="word"/> belongs to the classifier's <see cref="WordFeature.Class"/>.
		/// </summary>
		/// <param name="word">The word to be tested.</param>
		/// <returns>
		/// A positive score value is an estimate of the belonging of a word to
		/// the <see cref="WordFeature.Class"/>.
		/// </returns>
		internal double GetScoreValue(string[] word)
		{
			return this.classifier.Discriminate(word);
		}

		/// <summary>
		/// Get the classification <see cref="Score"/> for a <paramref name="word"/>.
		/// A positive <see cref="Score.ScoreValue"/> means that the classifier believes 
		/// that the <paramref name="word"/> belongs to the classifier's <see cref="WordFeature.Class"/>.
		/// </summary>
		/// <param name="word">The word to be tested.</param>
		/// <returns>
		/// The <see cref="Score"/> for the <paramref name="word"/>.
		/// A positive <see cref="Score.ScoreValue"/> is an estimate of the belonging of a word to
		/// the <see cref="WordFeature.Class"/>.
		/// </returns>
		internal Score GetScore(string[] word)
		{
			return new Score(this.Feature, GetScoreValue(word));
		}

		internal void Train(TaggedWordFormTrainingSample[] trainingSamples, WordClassifierTrainingOptions trainingOptions)
		{
			if (trainingSamples == null) throw new ArgumentNullException("trainingSamples");
			if (trainingOptions == null) throw new ArgumentNullException("trainingOptions");

			Kernel<string[]> kernel = new StringKernel<string>(trainingOptions.StringKernelExponent);

			if (trainingOptions.IsGaussified) kernel = new GaussianKernel<string[]>(trainingOptions.GaussianDeviation, kernel);

			var classifier = new Gramma.SVM.CoordinateDescent.SerialCoordinateDescentBinaryClassifier<string[]>(kernel);

			var clazz = this.Feature.Class;

			var trainingPairs = from sample in trainingSamples
													select new BinaryClassifier<string[]>.TrainingPair
													{
														Class = sample.Class.Equals(clazz) ? BinaryClass.Positive : BinaryClass.Negative,
														Item = sample.WordSyllables.ToArray()
													};

			classifier.Train(trainingPairs.ToArray(), trainingOptions.ClassificationMarginSlack);

			this.classifier = classifier;
		}

		#endregion

		#region IChild<WordClassifierBank> Members

		WordClassifierBank IChild<WordClassifierBank>.Parent
		{
			get
			{
				return this.ClassifierBank;
			}
			set
			{
				this.ClassifierBank = value;
			}
		}

		#endregion

		#region IKeyedElement<CommandSequenceClass> Members

		CommandSequenceClass IKeyedElement<CommandSequenceClass>.Key
		{
			get { return this.Feature.Class; }
		}

		#endregion

		#region IKeyedElement<TagType> Members

		TagType IKeyedElement<TagType>.Key
		{
			get { return this.Feature.Class.Tag.Type; }
		}

		#endregion
	}
}
