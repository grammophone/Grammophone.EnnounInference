using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gramma.Optimization;
using Gramma.Optimization.QuasiNewton;
using Gramma.CRF;
using Gramma.LanguageModel.Grammar;

namespace Gramma.Inference.Sentences
{
	/// <summary>
	/// Training options for a <see cref="SentenceClassifier"/>.
	/// </summary>
	[Serializable]
	public class SentenceClassifierTrainingOptions
	{
		#region Private fields

		private SentenceClassifierTrainingMethod trainingmethod;

		private Words.WordClassifierBank.AnalogiesScoreOptions analogiesScoreOptions;

		private LinearChainCRF<string[], Tag>.OfflineTrainingOptions offlineTrainingOptions;

		private LinearChainCRF<string[], Tag>.OnlineTrainingOptions onlineTrainingOptions;

		private bool condenseFeatures;

		private bool shuffleTrainingSamples;

		private WordScoringPolicy wordScoringPolicy;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public SentenceClassifierTrainingOptions()
		{
			trainingmethod = SentenceClassifierTrainingMethod.Offline;
			analogiesScoreOptions = null;

			var offlineOptimizer = new ConjugateGradientOptimizer();

			var offlineOptimizerOptions = new ConjugateGradient.LineSearchMinimizeOptions();

			offlineOptimizerOptions.StopCriterion = ConjugateGradient.LineSearchMinimizeOptions.GetGradientNormCriterion(1E-6);

			offlineTrainingOptions = new LinearChainCRF<string[], Tag>.OfflineTrainingOptions();

			offlineTrainingOptions.Optimizer = offlineOptimizer;

			onlineTrainingOptions = new LinearChainCRF<string[], Tag>.OnlineTrainingOptions();

			condenseFeatures = false;

			shuffleTrainingSamples = true;

			wordScoringPolicy = Sentences.WordScoringPolicy.Prioritized;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The training method to use.
		/// Default is <see cref="Sentences.SentenceClassifierTrainingMethod.Offline"/>.
		/// </summary>
		public SentenceClassifierTrainingMethod TrainingMethod
		{
			get
			{
				return trainingmethod;
			}
			set
			{
				trainingmethod = value;
			}
		}

		/// <summary>
		/// The conditional random field training options to use when <see cref="TrainingMethod"/> 
		/// is <see cref="Sentences.SentenceClassifierTrainingMethod.Offline"/>.
		/// </summary>
		public LinearChainCRF<string[], Tag>.OfflineTrainingOptions OfflineTrainingOptions
		{
			get
			{
				return offlineTrainingOptions;
			}
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				offlineTrainingOptions = value;
			}
		}

		/// <summary>
		/// The conditional random field training options to use when <see cref="TrainingMethod"/> 
		/// is <see cref="Sentences.SentenceClassifierTrainingMethod.Online"/>.
		/// </summary>
		public LinearChainCRF<string[], Tag>.OnlineTrainingOptions OnlineTrainingOptions
		{
			get
			{
				return onlineTrainingOptions;
			}
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				onlineTrainingOptions = value;
			}
		}

		/// <summary>
		/// The analogies scoring options to use. A null value is allowed, meaning no analogies scoring.
		/// Default is null.
		/// </summary>
		public Words.WordClassifierBank.AnalogiesScoreOptions AnalogiesScoreOptions
		{
			get
			{
				return analogiesScoreOptions;
			}
			set
			{
				analogiesScoreOptions = value;
			}
		}

		/// <summary>
		/// If false, all command sequences mapped in words dictionaries will have their unique feature id as 
		/// their conterparts in classifiers. If true, their feature ID will 
		/// be changed to match their commonly shared tag.
		/// Default is false.
		/// </summary>
		public bool CondenseFeatures
		{
			get { return condenseFeatures; }
			set { condenseFeatures = value; }
		}

		/// <summary>
		/// Only applicable to on-line training. 
		/// If true, the training samples stream consists of uniform random picks of samples.
		/// If false, the samples are cycled in order.
		/// Defauls is true.
		/// </summary>
		public bool ShuffleTrainingSamples 
		{
			get { return shuffleTrainingSamples; }
			set	{	shuffleTrainingSamples = value; }
		}

		/// <summary>
		/// How heach word is scored by its <see cref="Words.ScoreBank"/>.
		/// Default is <see cref="Sentences.WordScoringPolicy.Prioritized"/>.
		/// </summary>
		public WordScoringPolicy WordScoringPolicy
		{
			get { return wordScoringPolicy; }
			set { wordScoringPolicy = value; }
		}

		#endregion
	}

}
